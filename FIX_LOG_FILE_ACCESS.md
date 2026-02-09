# Fix: Log File Access Conflict

## Problem
When running parallel downloads, you encountered this error:
```
The process cannot access the file 'C:\Downloads\Library_9_20260209_171729\_download_log.txt' 
because it is being used by another process.
```

## Root Cause
Multiple parallel download tasks were trying to write to the same log file simultaneously without synchronization, causing file access conflicts.

## Solution Implemented
Added thread-safe logging mechanism using a `SemaphoreSlim`:

### 1. **Added Log Semaphore**
```csharp
// Semaphore to control log file access (prevents file access conflicts)
using var logSemaphore = new SemaphoreSlim(1, 1);
```

### 2. **Created Thread-Safe Helper Method**
```csharp
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
```

### 3. **Updated All Log Writing Calls**
Replaced all direct `File.AppendAllTextAsync` calls with the thread-safe `WriteToLogAsync` method in:
- `DownloadDocumentByIdAsync` method
- Parallel download task error handling

## How It Works
- The `SemaphoreSlim(1, 1)` ensures only **one task at a time** can write to the log file
- Other tasks wait in queue until the file is available
- This eliminates file access conflicts while maintaining parallel download performance
- The download semaphore (`maxParallelDownloads`) controls concurrent downloads
- The log semaphore (always 1) serializes log file writes

## To Test the Fix
1. **Close the currently running application** (important!)
2. Build the project:
   ```bash
   dotnet build "../FileDownloader/FileDownloader.csproj"
   ```
3. Run the application:
   ```bash
   dotnet run --project "../FileDownloader/FileDownloader.csproj"
   ```
4. Try downloading a library with multiple files

## Performance Impact
- **Minimal** - Log file writing is very fast compared to file downloads
- Downloads continue in parallel at full speed
- Only log writes are serialized (which is necessary for file system safety)

## Files Modified
- `../FileDownloader/Services/LibraryDownloadService.cs`
  - Added `WriteToLogAsync` helper method
  - Added `logSemaphore` parameter to `DownloadDocumentByIdAsync`
  - Updated all log file writing to use thread-safe method
  - Added `logSemaphore` in `DownloadLibraryAsync` method

## Next Steps
Close the running application and restart it to test the fix!
