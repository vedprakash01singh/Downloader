using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using FileDownloader.Data;
using FileDownloader.Models;

namespace FileDownloader.Services;

public class LibraryDownloadService
{
    private readonly ApplicationDbContext _context;
    private readonly IConfiguration _configuration;
    private Dictionary<string, string>? _cachedSettings;
    private readonly SemaphoreSlim _dbSemaphore = new SemaphoreSlim(1, 1); // Ensures only one DB operation at a time

    public LibraryDownloadService(ApplicationDbContext context, IConfiguration configuration)
    {
        _context = context;
        _configuration = configuration;
    }

    private async Task LoadSettingsAsync(bool forceReload = false)
    {
        if (_cachedSettings != null && !forceReload)
            return;

        _cachedSettings = new Dictionary<string, string>();

        try
        {
            var settings = await _context.Settings
                .ToListAsync();

            foreach (var setting in settings)
            {
                if (!string.IsNullOrEmpty(setting.Name) && !string.IsNullOrEmpty(setting.Value))
                {
                    _cachedSettings[setting.Name] = setting.Value;
                }
            }
        }
        catch (Exception)
        {
            // If loading from database fails, use empty dictionary
            _cachedSettings = new Dictionary<string, string>();
        }
    }

    private void ClearSettingsCache()
    {
        _cachedSettings = null;
    }

    private string? GetSettingValue(string settingName)
    {
        if (_cachedSettings != null && _cachedSettings.TryGetValue(settingName, out var value))
        {
            return value;
        }
        return null;
    }

    public async Task<LibraryInfo?> GetLibraryInfoAsync(int libraryId)
    {
        var library = await _context.Libraries
            .Where(l => l.ID == libraryId && !l.Deleted)
            .Select(l => new LibraryInfo
            {
                Id = l.ID,
                Name = l.LibraryName ?? "",
                Description = l.Description ?? "",
                DocumentCount = l.Folders
                    .SelectMany(f => f.Documents)
                    .Count(d => !d.Deleted)
            })
            .FirstOrDefaultAsync();

        return library;
    }

    public async Task<List<LibraryInfo>> GetAllLibrariesAsync()
    {
        var libraries = await _context.Libraries
            .Where(l => !l.Deleted)
            .OrderBy(l => l.LibraryName)
            .Select(l => new LibraryInfo
            {
                Id = l.ID,
                Name = l.LibraryName ?? "",
                Description = l.Description ?? "",
                DocumentCount = l.Folders
                    .SelectMany(f => f.Documents)
                    .Count(d => !d.Deleted)
            })
            .ToListAsync();

        return libraries;
    }

    /// <summary>
    /// Downloads all documents from a library
    /// </summary>
    /// <param name="libraryId">Library ID to download</param>
    /// <param name="logAction">Optional callback for logging</param>
    /// <param name="clearCacheBefore">If true, clears settings cache before loading</param>
    /// <param name="clearCacheAfter">If true, clears settings cache after download completes</param>
    /// <param name="forceReloadSettings">If true, reloads settings from database even if cached</param>
    /// <param name="progressAction">Optional callback for progress reporting (current, total)</param>
    /// <param name="maxParallelDownloads">Maximum number of parallel downloads (1-10)</param>
    /// <param name="cancellationToken">Cancellation token to stop download</param>
    public async Task<DownloadResult> DownloadLibraryAsync(
        int libraryId, 
        Action<string>? logAction = null,
        bool clearCacheBefore = false,
        bool clearCacheAfter = false,
        bool forceReloadSettings = false,
        Action<int, int>? progressAction = null,
        int maxParallelDownloads = 4,
        CancellationToken cancellationToken = default)
    {
        var result = new DownloadResult { LibraryId = libraryId };

        try
        {
            Log("=== STARTING LIBRARY DOWNLOAD ===", logAction);
            Log($"Library ID: {libraryId}", logAction);

            // Clear cache before loading if requested
            if (clearCacheBefore)
            {
                ClearSettingsCache();
                Log("Settings cache cleared before loading", logAction);
            }

            // Load all settings once at the start
            await LoadSettingsAsync(forceReloadSettings);
            Log($"Settings loaded from database{(forceReloadSettings ? " (forced reload)" : clearCacheBefore ? " (fresh)" : " (cached)")}", logAction);

            // Get all settings-dependent values once
            bool isFileStoredOnDB = IsFileStoredOnDB();
            string storagePath = GetStoragePath();
            Log($"File storage: {(isFileStoredOnDB ? "Database" : "File System")}", logAction);
            if (!isFileStoredOnDB)
            {
                Log($"Storage path: {storagePath}", logAction);
            }

            // Get library info
            var library = await GetLibraryInfoAsync(libraryId);
            if (library == null)
            {
                result.ErrorMessage = $"Library with ID {libraryId} not found or has been deleted";
                Log(result.ErrorMessage, logAction);
                return result;
            }

            result.LibraryName = library.Name;
            result.TotalDocuments = library.DocumentCount;
            Log($"Library: {library.Name} ({library.DocumentCount} documents)", logAction);

            if (library.DocumentCount == 0)
            {
                result.Message = "No documents found to download in this library.";
                result.IsSuccess = true;
                return result;
            }

            // Get download path from settings
            string downloadBasePath = GetSettingValue("DownloadPath") ?? _configuration["AppSettings:DownloadPath"] ?? "C:\\Downloads";
            string libraryPath = Path.Combine(downloadBasePath, $"Library_{libraryId}_{DateTime.Now:yyyyMMdd_HHmmss}");
            Directory.CreateDirectory(libraryPath);
            Log($"Download path: {libraryPath}", logAction);

            // Setup for parallel downloads and progress tracking
            int batchSize = 100; // Increased from 50 for better performance
            int totalCount = library.DocumentCount;
            int totalPages = (int)Math.Ceiling((double)totalCount / batchSize);
            int processedCount = 0;
            int successCount = 0;
            int failedCount = 0;
            
            // Create log file for errors
            string logFilePath = Path.Combine(libraryPath, "_download_log.txt");
            await System.IO.File.WriteAllTextAsync(logFilePath, $"Library Download Log - {DateTime.Now}\n");
            await System.IO.File.AppendAllTextAsync(logFilePath, $"Library: {library.Name} (ID: {libraryId})\n");
            await System.IO.File.AppendAllTextAsync(logFilePath, $"Total Documents: {totalCount}\n");
            await System.IO.File.AppendAllTextAsync(logFilePath, $"Max Parallel Downloads: {maxParallelDownloads}\n\n");
            
            Log($"Processing {totalPages} batches of {batchSize} documents each", logAction);
            Log($"Parallel downloads: {maxParallelDownloads}", logAction);

            // Semaphore to control parallel downloads
            using var semaphore = new SemaphoreSlim(maxParallelDownloads, maxParallelDownloads);
            // Semaphore to control log file access (prevents file access conflicts)
            using var logSemaphore = new SemaphoreSlim(1, 1);

            for (int pageNo = 0; pageNo < totalPages; pageNo++)
            {
                // Check cancellation
                if (cancellationToken.IsCancellationRequested)
                {
                    result.Message = "Download cancelled by user";
                    Log("Download cancelled", logAction);
                    await System.IO.File.AppendAllTextAsync(logFilePath, "\n*** Download cancelled by user ***\n");
                    break;
                }

                Log($"--- Processing batch {pageNo + 1} of {totalPages} ---", logAction);

                // Load ALL document data for this batch upfront (prevents DbContext threading issues)
                var documents = await _context.Documents
                    .Where(d => d.Folder!.LibraryId == libraryId && !d.Deleted)
                    .OrderBy(d => d.ID)
                    .Skip(pageNo * batchSize)
                    .Take(batchSize)
                    .Select(d => new
                    {
                        d.ID,
                        d.Name,
                        d.FileId,
                        d.IsArchived,
                        d.PhysicalPath
                    })
                    .ToListAsync(cancellationToken);

                Log($"Retrieved {documents.Count} documents from batch {pageNo + 1}", logAction);

                // Download in parallel with semaphore control - NO database access in parallel tasks
                var downloadTasks = documents.Select(async doc =>
                {
                    await semaphore.WaitAsync(cancellationToken);
                    try
                    {
                        if (cancellationToken.IsCancellationRequested)
                            return;

                        // Use pre-loaded document data (no database access needed)
                        bool success = await DownloadDocumentAsync(doc.ID, doc.Name, doc.FileId, doc.IsArchived, doc.PhysicalPath, 
                            libraryPath, isFileStoredOnDB, storagePath, logFilePath, logSemaphore);
                        
                        if (success)
                        {
                            Interlocked.Increment(ref successCount);
                        }
                        else
                        {
                            Interlocked.Increment(ref failedCount);
                        }
                        
                        // Update progress
                        int current = Interlocked.Increment(ref processedCount);
                        progressAction?.Invoke(current, totalCount);
                    }
                    catch (OperationCanceledException)
                    {
                        // Expected when cancelled
                    }
                    catch (Exception ex)
                    {
                        Interlocked.Increment(ref failedCount);
                        await WriteToLogAsync(logFilePath, $"ERROR: Document ID {doc.ID}: {ex.Message}\n", logSemaphore);
                    }
                    finally
                    {
                        semaphore.Release();
                    }
                });

                await Task.WhenAll(downloadTasks);

                // Periodic garbage collection for large downloads (every 10 batches)
                if ((pageNo + 1) % 10 == 0)
                {
                    GC.Collect();
                    GC.WaitForPendingFinalizers();
                    Log($"Memory cleanup performed after batch {pageNo + 1}", logAction);
                }
            }

            // Update result with final counts
            result.SuccessCount = successCount;
            result.FailedCount = failedCount;

            result.IsSuccess = true;
            result.Message = $"Download completed. Success: {result.SuccessCount}, Failed: {result.FailedCount}";
            Log("=== LIBRARY DOWNLOAD COMPLETED ===", logAction);
            Log(result.Message, logAction);
            
            // Clear settings cache only if requested
            if (clearCacheAfter)
            {
                ClearSettingsCache();
                Log("Settings cache cleared", logAction);
            }
        }
        catch (Exception ex)
        {
            result.IsSuccess = false;
            result.ErrorMessage = ex.Message;
            result.Message = $"Library download failed: {ex.Message}";
            Log($"=== LIBRARY DOWNLOAD FAILED: {ex.Message} ===", logAction);
            
            // Clear settings cache on error only if requested
            if (clearCacheAfter)
            {
                ClearSettingsCache();
                Log("Settings cache cleared", logAction);
            }
        }

        return result;
    }

    /// <summary>
    /// Thread-safe log file writing
    /// </summary>
    private async Task WriteToLogAsync(string logFilePath, string message, SemaphoreSlim logSemaphore)
    {
        await logSemaphore.WaitAsync();
        try
        {
            await System.IO.File.AppendAllTextAsync(logFilePath, message);
        }
        finally
        {
            logSemaphore.Release();
        }
    }

    /// <summary>
    /// Downloads a document using pre-loaded data (thread-safe, no database access)
    /// </summary>
    private async Task<bool> DownloadDocumentAsync(int documentId, string documentName, int fileId, byte? isArchived, string? physicalPath,
        string libraryPath, bool isFileStoredOnDB, string storagePath, string logFilePath, SemaphoreSlim logSemaphore)
    {
        try
        {
            if (string.IsNullOrEmpty(documentName))
            {
                await WriteToLogAsync(logFilePath, $"SKIP: Document ID {documentId} has no name\n", logSemaphore);
                return false;
            }

            // Create folder structure
            string relativePath = physicalPath?.Replace("/", "\\") ?? "";
            string documentDir = Path.Combine(libraryPath, relativePath.TrimStart('\\'));
            Directory.CreateDirectory(documentDir);

            string outputFilePath = Path.Combine(documentDir, documentName);

            // Check if already exists (resume capability)
            if (System.IO.File.Exists(outputFilePath))
            {
                return true; // Skip already downloaded files
            }

            // Download file
            bool archived = isArchived.HasValue && isArchived.Value == (byte)1;
            bool success = await ReconstructFileAsync(fileId, documentName, documentDir, archived, isFileStoredOnDB, storagePath, null);

            if (!success)
            {
                await WriteToLogAsync(logFilePath, $"FAILED: {documentName} (ID: {documentId})\n", logSemaphore);
            }

            return success;
        }
        catch (Exception ex)
        {
            await WriteToLogAsync(logFilePath, $"ERROR: Document ID {documentId}: {ex.Message}\n", logSemaphore);
            return false;
        }
    }

    /// <summary>
    /// Legacy method - kept for backward compatibility
    /// </summary>
    private async Task<bool> DownloadDocumentByIdAsync(int documentId, string documentName, int fileId, byte? isArchived, string? physicalPath,
        string libraryPath, bool isFileStoredOnDB, string storagePath, string logFilePath, SemaphoreSlim logSemaphore)
    {
        return await DownloadDocumentAsync(documentId, documentName, fileId, isArchived, physicalPath, 
            libraryPath, isFileStoredOnDB, storagePath, logFilePath, logSemaphore);
    }

    private async Task<bool> DownloadDocumentAsync(Document document, string libraryPath, bool isFileStoredOnDB, string storagePath, Action<string>? logAction)
    {
        try
        {
            if (string.IsNullOrEmpty(document.Name))
                return false;

            // Create folder structure
            string relativePath = document.PhysicalPath?.Replace("/", "\\") ?? "";
            string documentDir = Path.Combine(libraryPath, relativePath.TrimStart('\\'));
            Directory.CreateDirectory(documentDir);

            string outputFilePath = Path.Combine(documentDir, document.Name);

            // Check if already exists
            if (System.IO.File.Exists(outputFilePath))
            {
                Log($"File already exists: {document.Name}", logAction);
                return true;
            }

            // Download file using FileDownloadHandler logic
            bool isArchived = document.IsArchived.HasValue && document.IsArchived.Value == (byte)1;
            bool success = await ReconstructFileAsync(document.FileId, document.Name, documentDir, isArchived, isFileStoredOnDB, storagePath, logAction);

            return success;
        }
        catch (Exception ex)
        {
            Log($"Error in DownloadDocumentAsync: {ex.Message}", logAction);
            return false;
        }
    }

    private async Task<bool> ReconstructFileAsync(int fileId, string fileName, string outputDir, bool isArchived, bool isFileStoredOnDB, string storagePath, Action<string>? logAction)
    {
        try
        {
            string outputFilePath = Path.Combine(outputDir, fileName);

            // Use passed-in parameter instead of calling method
            if (isFileStoredOnDB)
            {
                // Get chunk count
                int chunkCount = await GetFileChunkCountAsync(fileId, isArchived);

                if (chunkCount > 0)
                {
                    Log($"File is chunked ({chunkCount} chunks)", logAction);

                    // Process chunks
                    for (int i = 1; i <= chunkCount; i++)
                    {
                        // Get chunk (last chunk has index -1)
                        int chunkIndex = (i == chunkCount) ? -1 : i;
                        var chunkData = await GetChunkDataAsync(fileId, chunkIndex, isArchived);

                        if (chunkData == null)
                        {
                            Log($"Chunk {chunkIndex} not found", logAction);
                            return false;
                        }

                        // Write chunk
                        FileMode mode = (i == 1) ? FileMode.Create : FileMode.Append;
                        using (var fs = new FileStream(outputFilePath, mode))
                        {
                            await fs.WriteAsync(chunkData, 0, chunkData.Length);
                        }
                    }

                    return true;
                }
                else
                {
                    // Non-chunked file - get from HashValue
                    var fileData = await GetFileHashValueAsync(fileId);
                    if (fileData == null || fileData.Length == 0)
                    {
                        Log("File HashValue not found or empty", logAction);
                        return false;
                    }

                    using (var fs = new FileStream(outputFilePath, FileMode.Create))
                    {
                        await fs.WriteAsync(fileData, 0, fileData.Length);
                    }

                    return true;
                }
            }
            else
            {
                // File system storage - Use passed-in storage path
                string fileContentPath = Path.Combine(storagePath, fileId.ToString());

                if (!Directory.Exists(fileContentPath))
                {
                    // Fallback to database
                    var fileData = await GetFileHashValueAsync(fileId);
                    if (fileData == null || fileData.Length == 0)
                        return false;

                    using (var fs = new FileStream(outputFilePath, FileMode.Create))
                    {
                        await fs.WriteAsync(fileData, 0, fileData.Length);
                    }

                    return true;
                }
                else
                {
                    // Read from file system
                    var chunkFiles = Directory.GetFiles(fileContentPath)
                        .OrderBy(f => 
                        {
                            // Sort by chunk number: 1.tmp, 2.tmp, ..., -1.tmp (last)
                            var fileName = Path.GetFileNameWithoutExtension(f);
                            return fileName == "-1" ? int.MaxValue : int.Parse(fileName);
                        })
                        .ToList();

                    for (int i = 0; i < chunkFiles.Count; i++)
                    {
                        byte[] data = await System.IO.File.ReadAllBytesAsync(chunkFiles[i]);
                        FileMode mode = (i == 0) ? FileMode.Create : FileMode.Append;

                        using (var fs = new FileStream(outputFilePath, mode))
                        {
                            await fs.WriteAsync(data, 0, data.Length);
                        }
                    }

                    return true;
                }
            }
        }
        catch (Exception ex)
        {
            Log($"Error reconstructing file: {ex.Message}", logAction);
            return false;
        }
    }

    private bool IsFileStoredOnDB()
    {
        var setting = GetSettingValue("StorageLocation");
        
        if (string.IsNullOrEmpty(setting))
        {
            // Fallback to configuration
            setting = _configuration["AppSettings:StorageLocation"];
        }

        return string.IsNullOrEmpty(setting) || setting.ToUpper() == "DATABASE" || setting.ToUpper() == "DB";
    }

    private string GetStoragePath()
    {
        var storagePath = GetSettingValue("NAS_Storage");
        
        if (!string.IsNullOrEmpty(storagePath))
        {
            return storagePath;
        }

        // Fallback to configuration
        return _configuration["AppSettings:NAS_Storage"] ?? "C:\\Storage";
    }

    private async Task<int> GetFileChunkCountAsync(int fileId, bool isArchived)
    {
        await _dbSemaphore.WaitAsync();
        try
        {
            if (isArchived)
                return await _context.FileChunksArchiveds.CountAsync(c => c.FileId == fileId);
            else
                return await _context.FileChunks.CountAsync(c => c.FileId == fileId);
        }
        finally
        {
            _dbSemaphore.Release();
        }
    }

    private async Task<byte[]?> GetChunkDataAsync(int fileId, int chunkIndex, bool isArchived)
    {
        await _dbSemaphore.WaitAsync();
        try
        {
            if (isArchived)
            {
                var chunk = await _context.FileChunksArchiveds
                    .Where(c => c.FileId == fileId && c.ChunkIndex == chunkIndex)
                    .FirstOrDefaultAsync();
                return chunk?.ChunkData;
            }
            else
            {
                var chunk = await _context.FileChunks
                    .Where(c => c.FileId == fileId && c.ChunkIndex == chunkIndex)
                    .FirstOrDefaultAsync();
                return chunk?.ChunkData;
            }
        }
        finally
        {
            _dbSemaphore.Release();
        }
    }

    private async Task<byte[]?> GetFileHashValueAsync(int fileId)
    {
        await _dbSemaphore.WaitAsync();
        try
        {
            var hashValue = await _context.Files
                .Where(f => f.Id == fileId)
                .Select(f => f.HashValue)
                .FirstOrDefaultAsync();
            return hashValue;
        }
        finally
        {
            _dbSemaphore.Release();
        }
    }

    private void Log(string message, Action<string>? logAction)
    {
        string logMessage = $"[{DateTime.Now:yyyy-MM-dd HH:mm:ss}] {message}";
        Console.WriteLine(logMessage);
        logAction?.Invoke(logMessage);
    }
}

public class LibraryInfo
{
    public int Id { get; set; }
    public string Name { get; set; } = "";
    public string Description { get; set; } = "";
    public int DocumentCount { get; set; }
}

public class DownloadResult
{
    public bool IsSuccess { get; set; }
    public int LibraryId { get; set; }
    public string LibraryName { get; set; } = "";
    public int TotalDocuments { get; set; }
    public int SuccessCount { get; set; }
    public int FailedCount { get; set; }
    public string Message { get; set; } = "";
    public string ErrorMessage { get; set; } = "";
}

public class ConnectionTestResult
{
    public bool IsSuccess { get; set; }
    public bool CanConnect { get; set; }
    public int SettingsCount { get; set; }
    public string Message { get; set; } = "";
    public string ErrorMessage { get; set; } = "";
    public bool CacheCleared { get; set; }
}

public class SettingsCacheInfo
{
    public bool IsCached { get; set; }
    public int SettingsCount { get; set; }
    public List<string> SettingNames { get; set; } = new List<string>();
}
