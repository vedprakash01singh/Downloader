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
        .Build();

    // Get connection string
    string? connectionString = configuration.GetConnectionString("DefaultConnection");
    
    if (string.IsNullOrEmpty(connectionString) || connectionString.Contains("YOUR_"))
    {
        Console.ForegroundColor = ConsoleColor.Red;
        Console.WriteLine("? ERROR: Connection string not configured!");
        Console.ResetColor();
        Console.WriteLine();
        Console.WriteLine("Please update the connection string in appsettings.json");
        Console.WriteLine("File location: appsettings.json");
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
        Console.WriteLine("3. Exit");
        Console.WriteLine("=====================================");
        Console.Write("Enter your choice (1-3): ");
        
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
                continueRunning = false;
                Console.WriteLine("Goodbye!");
                break;

            default:
                Console.ForegroundColor = ConsoleColor.Yellow;
                Console.WriteLine("Invalid choice. Please enter 1, 2, or 3.");
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

