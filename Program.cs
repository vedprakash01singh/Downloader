using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using FileDownloader.Data;
using FileDownloader.Services;

Console.WriteLine("=================================");
Console.WriteLine("File Downloader - .NET 8");
Console.WriteLine("Library Download Service");
Console.WriteLine("=================================");
Console.WriteLine();

try
{
    // Load configuration
    var configuration = new ConfigurationBuilder()
        .SetBasePath(Directory.GetCurrentDirectory())
        .AddJsonFile("appsettings.json", optional: false, reloadOnChange: true)
        .AddJsonFile("secrets.json", optional: true, reloadOnChange: true)
        .Build();

    // Get connection string
    string? connectionString = configuration.GetConnectionString("DefaultConnection");
    
    if (string.IsNullOrEmpty(connectionString))
    {
        Console.ForegroundColor = ConsoleColor.Red;
        Console.WriteLine("? ERROR: Connection string not configured!");
        Console.ResetColor();
        Console.WriteLine();
        Console.WriteLine("Please create a 'secrets.json' file in the project root with your connection string:");
        Console.WriteLine();
        Console.ForegroundColor = ConsoleColor.Yellow;
        Console.WriteLine("secrets.json content:");
        Console.WriteLine("{");
        Console.WriteLine("  \"ConnectionStrings\": {");
        Console.WriteLine("    \"DefaultConnection\": \"Server=YOUR_SERVER;Database=YOUR_DATABASE;User Id=YOUR_USER;Password=YOUR_PASSWORD;...\"");
        Console.WriteLine("  }");
        Console.WriteLine("}");
        Console.ResetColor();
        Console.WriteLine();
        Console.WriteLine("Note: secrets.json is already in .gitignore and won't be committed to Git.");
        Console.WriteLine();
        Console.WriteLine("Press any key to exit...");
        Console.ReadKey();
        return;
    }

    // Setup DbContext
    var optionsBuilder = new DbContextOptionsBuilder<ApplicationDbContext>();
    optionsBuilder.UseSqlServer(connectionString);

    using var context = new ApplicationDbContext(optionsBuilder.Options);

    // Test database connection
    Console.Write("Testing database connection... ");
    try
    {
        await context.Database.CanConnectAsync();
        Console.ForegroundColor = ConsoleColor.Green;
        Console.WriteLine("? Connected");
        Console.ResetColor();
    }
    catch (Exception ex)
    {
        Console.ForegroundColor = ConsoleColor.Red;
        Console.WriteLine($"? Failed");
        Console.ResetColor();
        Console.WriteLine($"Error: {ex.Message}");
        Console.WriteLine();
        Console.WriteLine("Press any key to exit...");
        Console.ReadKey();
        return;
    }

    Console.WriteLine();

    // Create service
    var downloadService = new LibraryDownloadService(context, configuration);

    // Main loop
    bool continueRunning = true;
    while (continueRunning)
    {
        Console.WriteLine("=====================================");
        Console.WriteLine("MENU:");
        Console.WriteLine("1. List all libraries");
        Console.WriteLine("2. Download library by ID");
        Console.WriteLine("3. Resume interrupted download");
        Console.WriteLine("4. Exit");
        Console.WriteLine("=====================================");
        Console.Write("Enter your choice (1-4): ");
        
        string? choice = Console.ReadLine();

        switch (choice)
        {
            case "1":
                await ListLibrariesAsync(downloadService);
                break;

            case "2":
                await DownloadLibraryAsync(downloadService);
                break;

            case "3":
                await ResumeDownloadAsync(downloadService);
                break;

            case "4":
                continueRunning = false;
                Console.WriteLine("Goodbye!");
                break;

            default:
                Console.ForegroundColor = ConsoleColor.Yellow;
                Console.WriteLine("Invalid choice. Please enter 1-4.");
                Console.ResetColor();
                break;
        }

        if (continueRunning)
        {
            Console.WriteLine();
            Console.WriteLine("Press any key to continue...");
            Console.ReadKey();
            Console.Clear();
        }
    }
}
catch (Exception ex)
{
    Console.ForegroundColor = ConsoleColor.Red;
    Console.WriteLine($"Fatal error: {ex.Message}");
    Console.ResetColor();
    Console.WriteLine();
    Console.WriteLine("Press any key to exit...");
    Console.ReadKey();
}

async Task ListLibrariesAsync(LibraryDownloadService service)
{
    Console.WriteLine();
    Console.WriteLine("Loading libraries...");
    
    var libraries = await service.GetAllLibrariesAsync();
    
    if (libraries.Count == 0)
    {
        Console.ForegroundColor = ConsoleColor.Yellow;
        Console.WriteLine("No libraries found in the database.");
        Console.ResetColor();
        return;
    }

    Console.WriteLine();
    Console.WriteLine("Available Libraries:");
    Console.WriteLine("====================");
    Console.WriteLine($"{"ID",-10} {"Name",-40} {"Documents",-15}");
    Console.WriteLine(new string('-', 65));
    
    foreach (var lib in libraries)
    {
        Console.WriteLine($"{lib.Id,-10} {lib.Name,-40} {lib.DocumentCount,-15}");
    }
    
    Console.WriteLine(new string('-', 65));
    Console.WriteLine($"Total libraries: {libraries.Count}");
}

async Task DownloadLibraryAsync(LibraryDownloadService service)
{
    Console.WriteLine();
    Console.Write("Enter Library ID to download: ");
    string? input = Console.ReadLine();

    if (!int.TryParse(input, out int libraryId))
    {
        Console.ForegroundColor = ConsoleColor.Red;
        Console.WriteLine("Invalid Library ID. Please enter a number.");
        Console.ResetColor();
        return;
    }

    Console.WriteLine();
    Console.WriteLine($"Checking Library ID: {libraryId}...");
    
    // Get library info first
    var libraryInfo = await service.GetLibraryInfoAsync(libraryId);
    
    if (libraryInfo == null)
    {
        Console.ForegroundColor = ConsoleColor.Red;
        Console.WriteLine($"Library with ID {libraryId} not found or has been deleted.");
        Console.ResetColor();
        return;
    }

    Console.WriteLine();
    Console.WriteLine("Library Information:");
    Console.WriteLine("====================");
    Console.WriteLine($"ID:          {libraryInfo.Id}");
    Console.WriteLine($"Name:        {libraryInfo.Name}");
    Console.WriteLine($"Description: {libraryInfo.Description}");
    Console.WriteLine($"Documents:   {libraryInfo.DocumentCount:N0}");
    Console.WriteLine();

    if (libraryInfo.DocumentCount == 0)
    {
        Console.ForegroundColor = ConsoleColor.Yellow;
        Console.WriteLine("This library has no documents to download.");
        Console.ResetColor();
        return;
    }

    // Show estimated time for large downloads
    if (libraryInfo.DocumentCount > 10000)
    {
        Console.ForegroundColor = ConsoleColor.Yellow;
        Console.WriteLine($"? LARGE DOWNLOAD: {libraryInfo.DocumentCount:N0} documents");
        Console.WriteLine($"Estimated time (at 50 files/sec): ~{TimeSpan.FromSeconds(libraryInfo.DocumentCount / 50.0):hh\\:mm\\:ss}");
        Console.ResetColor();
        Console.WriteLine();
    }

    // Ask for parallel download settings for large downloads
    int maxParallel = 4;
    if (libraryInfo.DocumentCount > 1000)
    {
        Console.Write("Max parallel downloads (1-10, default 4): ");
        string? parallelInput = Console.ReadLine();
        if (int.TryParse(parallelInput, out int p) && p >= 1 && p <= 10)
        {
            maxParallel = p;
        }
        Console.WriteLine();
    }

    Console.Write("Do you want to proceed with download? (Y/N): ");
    string? confirm = Console.ReadLine();

    if (confirm?.ToUpper() != "Y")
    {
        Console.WriteLine("Download cancelled.");
        return;
    }

    Console.WriteLine();
    Console.WriteLine("====================");
    Console.WriteLine("STARTING DOWNLOAD...");
    Console.WriteLine($"Max Parallel: {maxParallel}");
    Console.WriteLine("Press ESC to cancel");
    Console.WriteLine("====================");
    Console.WriteLine();

    var startTime = DateTime.Now;
    var cancellationSource = new CancellationTokenSource();
    var lastProgressUpdate = DateTime.MinValue;
    
    // Start cancellation monitoring task
    var cancelTask = Task.Run(() =>
    {
        while (!cancellationSource.Token.IsCancellationRequested)
        {
            if (Console.KeyAvailable && Console.ReadKey(true).Key == ConsoleKey.Escape)
            {
                Console.WriteLine("\n\nCancellation requested...");
                cancellationSource.Cancel();
                break;
            }
            Thread.Sleep(100);
        }
    });

    // Progress callback - simplified for better visibility
    void UpdateProgress(int current, int total)
    {
        // Throttle updates to once per second to avoid console flickering
        var now = DateTime.Now;
        if ((now - lastProgressUpdate).TotalMilliseconds < 1000 && current < total)
            return;
        
        lastProgressUpdate = now;
        
        var elapsed = DateTime.Now - startTime;
        var rate = elapsed.TotalSeconds > 0 ? current / elapsed.TotalSeconds : 0;
        var eta = rate > 0 && current > 0 ? TimeSpan.FromSeconds((total - current) / rate) : TimeSpan.Zero;
        var percent = (current * 100.0 / total);
        
        // Simple progress line without cursor positioning
        Console.WriteLine($"[{DateTime.Now:HH:mm:ss}] Progress: {current:N0}/{total:N0} ({percent:F1}%) | Speed: {rate:F1} files/sec | Elapsed: {elapsed:hh\\:mm\\:ss} | ETA: {eta:hh\\:mm\\:ss}");
    }

    var result = await service.DownloadLibraryAsync(
        libraryId, 
        logAction: null, // Don't log individual files to console for large downloads
        progressAction: UpdateProgress,
        maxParallelDownloads: maxParallel,
        cancellationToken: cancellationSource.Token);

    cancellationSource.Cancel(); // Stop the cancel monitoring task
    await cancelTask;

    var duration = DateTime.Now - startTime;

    Console.WriteLine("\n\n");
    Console.WriteLine("====================");
    Console.WriteLine("DOWNLOAD SUMMARY");
    Console.WriteLine("====================");
    Console.ForegroundColor = result.IsSuccess ? ConsoleColor.Green : ConsoleColor.Red;
    Console.WriteLine($"Status:          {(result.IsSuccess ? "? SUCCESS" : "? FAILED")}");
    Console.ResetColor();
    Console.WriteLine($"Library:         {result.LibraryName}");
    Console.WriteLine($"Total Documents: {result.TotalDocuments:N0}");
    Console.ForegroundColor = ConsoleColor.Green;
    Console.WriteLine($"Downloaded:      {result.SuccessCount:N0}");
    Console.ResetColor();
    if (result.FailedCount > 0)
    {
        Console.ForegroundColor = ConsoleColor.Red;
        Console.WriteLine($"Failed:          {result.FailedCount:N0}");
        Console.ResetColor();
    }
    if (result.SkippedCount > 0)
    {
        Console.ForegroundColor = ConsoleColor.Cyan;
        Console.WriteLine($"Skipped:         {result.SkippedCount:N0} (already downloaded)");
        Console.ResetColor();
    }
    if (result.IsResumed)
    {
        Console.ForegroundColor = ConsoleColor.Cyan;
        Console.WriteLine($"Resume Mode:     Yes");
        Console.ResetColor();
    }
    Console.WriteLine($"Duration:        {duration:hh\\:mm\\:ss}");
    Console.WriteLine($"Avg Speed:       {(result.SuccessCount / duration.TotalSeconds):F2} files/sec");
    Console.WriteLine($"Message:         {result.Message}");
    
    if (!string.IsNullOrEmpty(result.ErrorMessage))
    {
        Console.ForegroundColor = ConsoleColor.Red;
        Console.WriteLine($"Error:           {result.ErrorMessage}");
        Console.ResetColor();
    }
    
    Console.WriteLine("====================");
    Console.WriteLine("\nCheck _download_log.txt in the output folder for details.");
}

async Task ResumeDownloadAsync(LibraryDownloadService service)
{
    Console.WriteLine();
    Console.WriteLine("Loading incomplete downloads...");
    
    var incompleteDownloads = await service.GetIncompleteDownloadsAsync();
    
    if (incompleteDownloads.Count == 0)
    {
        Console.ForegroundColor = ConsoleColor.Yellow;
        Console.WriteLine("No incomplete downloads found.");
        Console.ResetColor();
        return;
    }

    Console.WriteLine();
    Console.WriteLine("Incomplete Downloads:");
    Console.WriteLine("====================");
    Console.WriteLine($"{"#",-5} {"Library ID",-12} {"Library Name",-30} {"Progress",-20} {"Last Update",-20}");
    Console.WriteLine(new string('-', 95));
    
    for (int i = 0; i < incompleteDownloads.Count; i++)
    {
        var download = incompleteDownloads[i];
        int completed = download.SuccessfulDocuments.Count;
        int total = download.TotalDocuments;
        double percent = total > 0 ? (completed * 100.0 / total) : 0;
        string progress = $"{completed}/{total} ({percent:F1}%)";
        
        Console.WriteLine($"{i + 1,-5} {download.LibraryId,-12} {download.LibraryName,-30} {progress,-20} {download.LastUpdateTime:yyyy-MM-dd HH:mm}");
    }
    
    Console.WriteLine(new string('-', 95));
    Console.WriteLine($"Total incomplete downloads: {incompleteDownloads.Count}");
    Console.WriteLine();
    
    Console.Write("Enter # to resume (or 0 to cancel): ");
    string? input = Console.ReadLine();

    if (!int.TryParse(input, out int selection) || selection < 1 || selection > incompleteDownloads.Count)
    {
        Console.WriteLine("Cancelled.");
        return;
    }

    var selectedDownload = incompleteDownloads[selection - 1];
    
    Console.WriteLine();
    Console.WriteLine("Resume Information:");
    Console.WriteLine("====================");
    Console.WriteLine($"Library ID:      {selectedDownload.LibraryId}");
    Console.WriteLine($"Library Name:    {selectedDownload.LibraryName}");
    Console.WriteLine($"Total Documents: {selectedDownload.TotalDocuments:N0}");
    Console.WriteLine($"Completed:       {selectedDownload.SuccessfulDocuments.Count:N0}");
    Console.WriteLine($"Failed:          {selectedDownload.FailedDocuments.Count:N0}");
    Console.WriteLine($"Remaining:       {selectedDownload.TotalDocuments - selectedDownload.SuccessfulDocuments.Count:N0}");
    Console.WriteLine($"Started:         {selectedDownload.StartTime:yyyy-MM-dd HH:mm:ss}");
    Console.WriteLine($"Last Update:     {selectedDownload.LastUpdateTime:yyyy-MM-dd HH:mm:ss}");
    Console.WriteLine($"Download Path:   {selectedDownload.DownloadPath}");
    Console.WriteLine();

    // Ask for parallel download settings
    Console.Write("Max parallel downloads (1-10, default 4): ");
    string? parallelInput = Console.ReadLine();
    int maxParallel = 4;
    if (int.TryParse(parallelInput, out int p) && p >= 1 && p <= 10)
    {
        maxParallel = p;
    }
    Console.WriteLine();

    Console.Write("Resume download? (Y/N): ");
    string? confirm = Console.ReadLine();

    if (confirm?.ToUpper() != "Y")
    {
        Console.WriteLine("Resume cancelled.");
        return;
    }

    Console.WriteLine();
    Console.WriteLine("====================");
    Console.WriteLine("RESUMING DOWNLOAD...");
    Console.WriteLine($"Max Parallel: {maxParallel}");
    Console.WriteLine("Press ESC to cancel");
    Console.WriteLine("====================");
    Console.WriteLine();

    var startTime = DateTime.Now;
    var cancellationSource = new CancellationTokenSource();
    var lastProgressUpdate = DateTime.MinValue;
    
    // Start cancellation monitoring task
    var cancelTask = Task.Run(() =>
    {
        while (!cancellationSource.Token.IsCancellationRequested)
        {
            if (Console.KeyAvailable && Console.ReadKey(true).Key == ConsoleKey.Escape)
            {
                Console.WriteLine("\n\nCancellation requested...");
                cancellationSource.Cancel();
                break;
            }
            Thread.Sleep(100);
        }
    });

    // Progress callback
    void UpdateProgress(int current, int total)
    {
        var now = DateTime.Now;
        if ((now - lastProgressUpdate).TotalMilliseconds < 1000 && current < total)
            return;
        
        lastProgressUpdate = now;
        
        var elapsed = DateTime.Now - startTime;
        var rate = elapsed.TotalSeconds > 0 ? current / elapsed.TotalSeconds : 0;
        var eta = rate > 0 && current > 0 ? TimeSpan.FromSeconds((total - current) / rate) : TimeSpan.Zero;
        var percent = (current * 100.0 / total);
        
        Console.WriteLine($"[{DateTime.Now:HH:mm:ss}] Progress: {current:N0}/{total:N0} ({percent:F1}%) | Speed: {rate:F1} files/sec | Elapsed: {elapsed:hh\\:mm\\:ss} | ETA: {eta:hh\\:mm\\:ss}");
    }

    var result = await service.DownloadLibraryAsync(
        selectedDownload.LibraryId, 
        logAction: null,
        progressAction: UpdateProgress,
        maxParallelDownloads: maxParallel,
        cancellationToken: cancellationSource.Token);

    cancellationSource.Cancel();
    await cancelTask;

    var duration = DateTime.Now - startTime;

    Console.WriteLine("\n\n");
    Console.WriteLine("====================");
    Console.WriteLine("RESUME SUMMARY");
    Console.WriteLine("====================");
    Console.ForegroundColor = result.IsSuccess ? ConsoleColor.Green : ConsoleColor.Red;
    Console.WriteLine($"Status:          {(result.IsSuccess ? "? SUCCESS" : "? FAILED")}");
    Console.ResetColor();
    Console.WriteLine($"Library:         {result.LibraryName}");
    Console.WriteLine($"Total Documents: {result.TotalDocuments:N0}");
    Console.ForegroundColor = ConsoleColor.Green;
    Console.WriteLine($"Downloaded:      {result.SuccessCount:N0}");
    Console.ResetColor();
    if (result.FailedCount > 0)
    {
        Console.ForegroundColor = ConsoleColor.Red;
        Console.WriteLine($"Failed:          {result.FailedCount:N0}");
        Console.ResetColor();
    }
    if (result.SkippedCount > 0)
    {
        Console.ForegroundColor = ConsoleColor.Cyan;
        Console.WriteLine($"Skipped:         {result.SkippedCount:N0} (already downloaded)");
        Console.ResetColor();
    }
    Console.WriteLine($"Resume Duration: {duration:hh\\:mm\\:ss}");
    Console.WriteLine($"Total Duration:  {DateTime.Now - selectedDownload.StartTime:hh\\:mm\\:ss}");
    Console.WriteLine($"Message:         {result.Message}");
    
    if (!string.IsNullOrEmpty(result.ErrorMessage))
    {
        Console.ForegroundColor = ConsoleColor.Red;
        Console.WriteLine($"Error:           {result.ErrorMessage}");
        Console.ResetColor();
    }
    
    Console.WriteLine("====================");
    Console.WriteLine("\nCheck _download_log.txt in the output folder for details.");
}


