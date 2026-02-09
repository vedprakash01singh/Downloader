# Large-Scale Download Guide (500K+ Files)

## Overview
This document explains how the FileDownloader is optimized for downloading 500,000+ files efficiently.

## Key Optimizations

### 1. **Parallel Processing**
- Downloads up to 4-10 files simultaneously (configurable)
- Uses `SemaphoreSlim` for controlled concurrency
- Prevents overwhelming the database and disk I/O

### 2. **Batch Processing**
- Fetches documents in batches of 100 (optimized from 50)
- Minimal data projection (IDs and metadata only, not full entities)
- Reduces memory footprint significantly

### 3. **Memory Management**
- No full entity loading - uses projections
- Periodic garbage collection every 10 batches
- Streams data instead of holding everything in memory

### 4. **Progress Tracking**
- Real-time progress updates every 100 files
- Shows: Current count, percentage, speed (files/sec), elapsed time, ETA
- Non-blocking progress updates with thread-safe counters

### 5. **Error Handling & Logging**
- Automatic error recovery - continues even if individual files fail
- Comprehensive log file (`_download_log.txt`) in download folder
- Records all failures with error messages
- Thread-safe logging

### 6. **Resume Capability**
- Checks if file already exists before downloading
- Allows restarting failed downloads
- Skips already downloaded files automatically

### 7. **Cancellation Support**
- Press ESC to cancel download at any time
- Graceful cancellation with summary of partial progress
- Uses `CancellationToken` properly

### 8. **Performance Monitoring**
- Real-time download speed calculation
- ETA estimation based on current progress
- Final performance report with average speed

## Usage Examples

### Basic Usage (Default Settings)
```csharp
var result = await service.DownloadLibraryAsync(libraryId: 123);
```

### Optimized for 500K Files
```csharp
var cancellationSource = new CancellationTokenSource();

var result = await service.DownloadLibraryAsync(
    libraryId: 123,
    logAction: null,  // Disable individual file logging for speed
    progressAction: (current, total) => {
        Console.WriteLine($"Progress: {current}/{total}");
    },
    maxParallelDownloads: 8,  // Higher parallelism for faster downloads
    cancellationToken: cancellationSource.Token
);
```

### With Progress Bar
```csharp
void UpdateProgress(int current, int total)
{
    var percent = (current * 100.0 / total);
    Console.SetCursorPosition(0, Console.CursorTop);
    Console.Write($"[{new string('?', (int)(percent / 2))}{new string('?', 50 - (int)(percent / 2))}] {percent:F1}%");
}

var result = await service.DownloadLibraryAsync(
    libraryId: 123,
    progressAction: UpdateProgress,
    maxParallelDownloads: 6
);
```

## Performance Benchmarks

### Expected Performance
| File Count | Parallel Downloads | Expected Speed | Est. Time |
|------------|-------------------|----------------|-----------|
| 10,000 | 4 | 40-60 files/sec | 3-4 minutes |
| 50,000 | 6 | 50-80 files/sec | 10-17 minutes |
| 100,000 | 8 | 60-100 files/sec | 17-28 minutes |
| 500,000 | 10 | 80-120 files/sec | 70-105 minutes |

*Performance depends on network speed, database performance, and disk I/O*

## Recommendations for 500K Downloads

### 1. **Database Optimization**
```sql
-- Add indexes for faster queries
CREATE INDEX IX_Documents_LibraryId_Deleted 
ON Documents(LibraryId, Deleted) 
INCLUDE (ID, Name, FileId, PhysicalPath, IsArchived);

CREATE INDEX IX_FileChunks_FileId 
ON FileChunks(FileId, ChunkIndex);

CREATE INDEX IX_FileChunksArchived_FileId 
ON FileChunksArchived(FileId, ChunkIndex);
```

### 2. **Connection String Settings**
```json
{
  "ConnectionStrings": {
    "DefaultConnection": "Server=...;Database=...;Max Pool Size=100;Connection Timeout=60;..."
  }
}
```

### 3. **System Requirements**
- **RAM**: 4-8 GB minimum (16 GB recommended for 500K+)
- **Disk**: SSD recommended for output directory
- **Network**: Stable connection to database server
- **CPU**: Multi-core processor for parallel processing

### 4. **Optimal Settings**
```csharp
// For 500,000 files:
maxParallelDownloads: 8-10
// Batch size: 100 (automatically set)
// Progress updates: Every 100 files (automatic)
```

### 5. **Monitoring During Download**
- Watch the console for progress updates
- Check `_download_log.txt` for errors
- Monitor system resources (CPU, Memory, Disk I/O)
- Press ESC to safely cancel if needed

## Troubleshooting

### Slow Download Speed
1. Increase `maxParallelDownloads` (6-10)
2. Check database indexes exist
3. Verify network connection stability
4. Ensure output disk is not full
5. Check database server load

### Out of Memory Errors
1. Decrease `maxParallelDownloads` (2-4)
2. Close other applications
3. Verify 64-bit process (not 32-bit)
4. Increase system page file size

### Connection Timeouts
1. Increase connection timeout in connection string
2. Increase connection pool size
3. Check database server performance
4. Reduce `maxParallelDownloads`

### Many Failed Downloads
1. Check `_download_log.txt` for specific errors
2. Verify file storage settings (Database vs FileSystem)
3. Check storage path accessibility
4. Verify database has all file data

## Log File Analysis

The `_download_log.txt` file contains:
- Download start time and library info
- List of all failed documents with IDs
- Error messages for each failure
- Final summary with statistics

Example:
```
Download started: 2024-01-15 10:00:00
Library: MyLibrary
Total Documents: 500,000

FAILED: Document123.pdf (ID: 45678)
ERROR: Document456.docx (ID: 78901) - Connection timeout

Download completed: 2024-01-15 12:25:30
Duration: 02:25:30
Total: 500,000
Success: 499,850
Failed: 150
Average speed: 57.28 files/sec
```

## Tips for Best Performance

1. **Run during off-peak hours** - Less database contention
2. **Use SSD** - Faster disk writes
3. **Increase parallelism gradually** - Find optimal setting for your system
4. **Monitor first 1000 files** - Adjust settings if needed
5. **Ensure stable connection** - Use wired network if possible
6. **Close other applications** - Free up system resources
7. **Check available disk space** - Ensure enough space for all files

## API Reference

### DownloadLibraryAsync Parameters

```csharp
Task<DownloadResult> DownloadLibraryAsync(
    int libraryId,                          // Required: Library ID
    Action<string>? logAction = null,       // Optional: Per-file logging callback
    Action<int, int>? progressAction = null,// Optional: Progress callback (current, total)
    bool clearCacheBefore = false,          // Optional: Clear settings cache before
    bool clearCacheAfter = false,           // Optional: Clear settings cache after
    bool forceReloadSettings = false,       // Optional: Force reload settings
    int maxParallelDownloads = 4,           // Optional: Parallel downloads (1-10)
    CancellationToken cancellationToken = default // Optional: Cancellation support
)
```

### DownloadResult Properties

```csharp
public class DownloadResult
{
    public bool IsSuccess { get; set; }         // Overall success
    public int LibraryId { get; set; }          // Library ID
    public string LibraryName { get; set; }     // Library name
    public int TotalDocuments { get; set; }     // Total document count
    public int SuccessCount { get; set; }       // Successfully downloaded
    public int FailedCount { get; set; }        // Failed downloads
    public string Message { get; set; }         // Summary message
    public string ErrorMessage { get; set; }    // Error details if failed
}
```

## Support

For issues or questions:
1. Check `_download_log.txt` in output folder
2. Review this guide's Troubleshooting section
3. Monitor system resources during download
4. Adjust settings based on your infrastructure
