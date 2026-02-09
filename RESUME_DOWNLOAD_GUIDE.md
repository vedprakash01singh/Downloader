# Resume Download Functionality - User Guide

## Overview
The File Downloader application now supports **automatic resume functionality**. If your download is interrupted (due to network issues, application crash, power failure, etc.), you can resume from exactly where you left off without re-downloading already completed files.

## How It Works

### Automatic Progress Tracking
The application automatically tracks download progress by:
1. **Creating a progress file** (`_download_progress.json`) in the download folder
2. **Recording successful downloads** - tracks which documents have been successfully downloaded
3. **Recording failed downloads** - keeps track of which documents failed to download
4. **Updating after each batch** - saves progress after processing each batch of 100 documents

### Resume Process
When you start a download:
1. The application **automatically checks** for incomplete downloads for that library
2. If found, it **automatically resumes** from the same folder
3. **Skips already downloaded files** - doesn't re-download files that exist
4. **Retries failed files** - attempts to download previously failed documents again

## Using Resume Functionality

### Option 1: Automatic Resume (Recommended)
Simply start a new download for the same library:

```
MENU:
1. List all libraries
2. Download library by ID  <-- Select this
3. Resume interrupted download
4. Exit

Enter your choice (1-4): 2
Enter Library ID to download: 123
```

**The application will automatically detect and resume the incomplete download!**

### Option 2: Manual Resume
Use the dedicated resume menu option:

```
MENU:
1. List all libraries
2. Download library by ID
3. Resume interrupted download  <-- Select this
4. Exit

Enter your choice (1-4): 3
```

This will show you a list of all incomplete downloads:

```
Incomplete Downloads:
====================
#     Library ID   Library Name                   Progress             Last Update         
-------------------------------------------------------------------------------------------
1     123          Product Documentation          1234/5000 (24.7%)    2024-01-15 14:30
2     456          Technical Manuals              890/2000 (44.5%)     2024-01-15 12:15
-------------------------------------------------------------------------------------------
Total incomplete downloads: 2

Enter # to resume (or 0 to cancel): 1
```

## Progress Files

### Location
Progress files are stored in the download folder:
```
C:\Downloads\Library_123_20240115_143022\
??? _download_log.txt          <-- Error logs
??? _download_progress.json    <-- Progress tracking
??? [your downloaded files]
```

### Progress File Structure
The `_download_progress.json` file contains:
```json
{
  "LibraryId": 123,
  "LibraryName": "Product Documentation",
  "DownloadPath": "C:\\Downloads\\Library_123_20240115_143022",
  "StartTime": "2024-01-15T14:30:22",
  "LastUpdateTime": "2024-01-15T14:45:10",
  "TotalDocuments": 5000,
  "SuccessfulDocuments": [1, 2, 3, 4, 5, ...],
  "FailedDocuments": {
    "156": "corrupted_file.pdf",
    "789": "missing_chunks.docx"
  },
  "IsCompleted": false
}
```

### Download Log
The `_download_log.txt` file contains detailed error information:
```
Library Download Log - 2024-01-15 14:30:22
Library: Product Documentation (ID: 123)
Total Documents: 5000
Max Parallel Downloads: 4

ERROR: Document ID 156, fDocumentId (FileId): 789, Document Name: corrupted_file.pdf
Exception: Chunk 2 not found for fDocumentId (FileId): 789
Stack Trace: ...

FAILED: missing_chunks.docx (Document ID: 789, fDocumentId (FileId): 1234)
```

## Interrupting a Download

### Safe Interruption
Press **ESC** key during download to safely cancel:
```
====================
STARTING DOWNLOAD...
Max Parallel: 4
Press ESC to cancel      <-- Press ESC here
====================

Cancellation requested...
```

The application will:
1. ? Stop processing new files
2. ? Complete currently downloading files
3. ? Save progress to file
4. ? Allow you to resume later

### Unsafe Interruption
If the application crashes or is forcefully closed:
- ?? The last batch may not be fully saved
- ? Progress up to the last saved batch is preserved
- ? You can still resume from the last saved point
- ?? Some files in the interrupted batch may be re-downloaded

## Resume Features

### What Gets Resumed
? **Skips completed files** - Files that were successfully downloaded
? **Reuses same folder** - Downloads continue in the original folder
? **Retries failed files** - Previously failed files are attempted again
? **Maintains statistics** - Success/fail counts carry over

### What Doesn't Get Resumed
? **Deleted progress files** - If you delete `_download_progress.json`, it can't resume
? **Completed downloads** - Downloads marked as completed won't show in resume list
? **Moved folders** - If you move the download folder, the path won't match

## Best Practices

### For Large Downloads (10,000+ documents)
1. **Use parallel downloads** - Set max parallel to 6-8 for faster downloads
2. **Monitor disk space** - Ensure sufficient space before starting
3. **Check progress file** - Verify progress is being saved (check file timestamp)
4. **Don't delete folders** - Keep the download folder until completely finished

### For Unstable Networks
1. **Use lower parallelism** - Set max parallel to 2-3 to reduce network load
2. **Regular checkpoints** - Progress is saved every 100 documents
3. **Check error logs** - Review `_download_log.txt` for network issues
4. **Resume multiple times** - You can resume as many times as needed

### For Server Maintenance
1. **Pause before maintenance** - Press ESC to stop cleanly
2. **Note the folder name** - Write down the download folder path
3. **Resume after maintenance** - Use menu option 3 to resume
4. **Verify completion** - Check that all documents downloaded successfully

## Progress Statistics

During resume, you'll see:
```
Download completed. Success: 4850, Failed: 15, Skipped: 135 (Resumed from previous download)

Status:          ? SUCCESS
Library:         Product Documentation
Total Documents: 5,000
Downloaded:      4,850
Failed:          15
Skipped:         135 (already downloaded)
Resume Mode:     Yes
Duration:        00:15:30
Avg Speed:       5.21 files/sec
```

Where:
- **Downloaded** = Files successfully downloaded (in this session + previous sessions)
- **Failed** = Files that failed to download
- **Skipped** = Files that were already downloaded in previous session
- **Resume Mode: Yes** = This was a resumed download

## Troubleshooting

### "No incomplete downloads found"
**Cause**: No progress files exist or all downloads are complete.
**Solution**: 
- Check if the download folder exists in `C:\Downloads`
- Verify `_download_progress.json` exists in the folder
- Check if `IsCompleted: true` in the progress file

### Download starts from beginning instead of resuming
**Cause**: Progress file not found or corrupted.
**Solution**:
- Check the download folder for `_download_progress.json`
- Ensure the file is valid JSON (open with text editor)
- Check file permissions

### Files being re-downloaded
**Cause**: Files were deleted or moved from download folder.
**Solution**:
- The application checks if files exist before skipping
- If files are missing, they will be re-downloaded
- Keep the folder intact until download completes

### Progress not updating
**Cause**: Progress file may be locked or permissions issue.
**Solution**:
- Check if `_download_progress.json` timestamp is updating
- Ensure you have write permissions to the download folder
- Close any applications that might have the file open

## Security Considerations

### Progress File Storage
- ? Progress files contain only document IDs and names
- ? No sensitive data or credentials stored
- ? Safe to commit `.gitignore` includes `_download_progress.json` pattern

### Connection String Protection
- ? Connection strings stored in `secrets.json` (not committed to Git)
- ? `.gitignore` prevents accidental commits
- ? Separate configuration from code

## Advanced Usage

### Finding Download Folders
All downloads are stored in: `C:\Downloads` (or configured path)
Folder naming pattern: `Library_{LibraryId}_{Timestamp}`

Example:
```
C:\Downloads\Library_123_20240115_143022\
C:\Downloads\Library_123_20240115_160530\  (new attempt/resume)
```

### Cleaning Up Completed Downloads
Once a download is complete and verified:
1. Check `IsCompleted: true` in progress file
2. Verify `Failed: 0` in summary
3. Safe to delete `_download_progress.json`
4. Safe to move download folder elsewhere

### Manual Progress File Editing
?? **Not recommended** but possible for advanced users:

To manually mark documents as completed:
1. Open `_download_progress.json`
2. Add document IDs to `SuccessfulDocuments` array
3. Remove from `FailedDocuments` if present
4. Save and resume download

## Summary

The resume functionality makes the File Downloader **robust and reliable** for:
- ? Large library downloads (10,000+ documents)
- ? Unstable network conditions
- ? Long-running downloads
- ? Server maintenance windows
- ? Interrupted operations

**Key Benefits:**
- ?? No need to re-download completed files
- ?? Automatic progress tracking
- ?? Resume as many times as needed
- ?? Detailed progress statistics
- ??? Safe interruption with ESC key

**Remember**: Always let the application complete or press ESC to stop cleanly. This ensures progress is properly saved!
