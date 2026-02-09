# Fix: DbContext Threading Error

## Problem
When running parallel downloads, you encountered this error:
```
A second operation was started on this context instance before a previous operation completed. 
This is usually caused by different threads concurrently using the same instance of DbContext.
```

## Root Cause
**EF Core's DbContext is NOT thread-safe**. The parallel download tasks were all using the same `_context` instance simultaneously, which EF Core explicitly prohibits.

### Where It Happened
1. **Document queries** - Multiple tasks querying `_context.Documents` at the same time
2. **Chunk queries** - Multiple file downloads querying `_context.FileChunks` simultaneously  
3. **File queries** - Multiple tasks accessing `_context.Files` for hash values

## Solution Implemented

### 1. **Load Document Data Upfront (Batch-Level)**
Changed from loading document IDs and querying again in parallel tasks to loading ALL document data before parallel processing:

**Before:**
```csharp
// Load IDs only
var documentIds = await _context.Documents...Select(d => d.ID).ToListAsync();

// Then query again in parallel (CAUSES ERROR!)
await DownloadDocumentByIdAsync(docId, ...);
  -> var docData = await _context.Documents.Where(d => d.ID == documentId)...
```

**After:**
```csharp
// Load ALL document data upfront
var documents = await _context.Documents
    .Select(d => new { d.ID, d.Name, d.FileId, d.IsArchived, d.PhysicalPath })
    .ToListAsync();

// Use pre-loaded data in parallel (NO database access)
await DownloadDocumentAsync(doc.ID, doc.Name, doc.FileId, ...);
```

### 2. **Added Database Semaphore for File Data**
Added a class-level semaphore to ensure only ONE database operation at a time:

```csharp
private readonly SemaphoreSlim _dbSemaphore = new SemaphoreSlim(1, 1);
```

Protected all database access methods:
- `GetFileChunkCountAsync()` - Counts chunks
- `GetChunkDataAsync()` - Retrieves chunk data
- `GetFileHashValueAsync()` - Gets file hash

**Pattern:**
```csharp
private async Task<byte[]?> GetChunkDataAsync(int fileId, int chunkIndex, bool isArchived)
{
    await _dbSemaphore.WaitAsync();
    try
    {
        // Database query here
        return chunk?.ChunkData;
    }
    finally
    {
        _dbSemaphore.Release();
    }
}
```

## How It Works Now

### Document-Level (Batch)
1. Load 100 documents at once (single DB query)
2. Store all document data in memory
3. Process files in parallel using pre-loaded data
4. NO document queries during parallel processing

### File-Level (Per Download)
1. Chunk/hash queries happen during file download
2. Protected by `_dbSemaphore` - only ONE at a time
3. Still parallel file downloads (up to `maxParallelDownloads`)
4. Database access is serialized, but file I/O is parallel

## Performance Impact

### Positive
- ? **No more threading errors**
- ? **Fewer database queries** (batch loading)
- ? **Better memory efficiency** (only 100 documents loaded at once)

### Trade-offs
- ?? File chunk queries are serialized
- ?? If many files are chunked, this limits speed
- ? BUT most files use hash values (single query) or file system storage (no DB)

### Expected Performance
- **Small files (hash-based)**: Minimal impact - one quick DB query per file
- **Large chunked files**: Slightly slower - chunks retrieved one at a time
- **File system storage**: No impact - no database access for file data

## Files Modified
- `../FileDownloader/Services/LibraryDownloadService.cs`
  - Added `_dbSemaphore` class field
  - Changed document loading to load all data upfront
  - Updated `DownloadDocumentAsync` to accept pre-loaded data
  - Added semaphore protection to `GetFileChunkCountAsync`
  - Added semaphore protection to `GetChunkDataAsync`
  - Added semaphore protection to `GetFileHashValueAsync`

## Testing
1. **Stop the currently running application**
2. Restart the application
3. Try downloading a library with multiple files
4. Progress should now display correctly without DbContext errors

## Alternative Solutions Considered

### Option 1: Create DbContext Per Task ?
```csharp
// Create new context for each task
using var context = new ApplicationDbContext(options);
```
**Rejected:** Too expensive, connection pool exhaustion

### Option 2: Use DbContext Factory ?
```csharp
private readonly IDbContextFactory<ApplicationDbContext> _factory;
```
**Rejected:** Requires dependency injection changes, more complex

### Option 3: Serialize All DB Access ? (CHOSEN)
```csharp
private readonly SemaphoreSlim _dbSemaphore = new SemaphoreSlim(1, 1);
```
**Selected:** Simple, safe, minimal code changes

## Why This Solution Works
1. **Single DbContext instance** - No connection pool issues
2. **Thread-safe access** - Semaphore ensures no concurrent operations
3. **Parallel file I/O** - File writes/reads still happen in parallel
4. **Batch optimization** - Document data loaded once per 100 files
5. **Minimal changes** - Existing code structure preserved

## Next Steps
**Close and restart the application** to test the fix!
