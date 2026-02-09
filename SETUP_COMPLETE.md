# File Downloader - Complete Setup Guide

## ? **Project Successfully Set Up with Entity Framework!**

### ?? Location
```
C:\GithubRepo\FileDownloader\
```

---

## ?? **What Was Added**

### Entity Framework Core Setup
? **NuGet Packages Installed:**
- `Microsoft.EntityFrameworkCore.SqlServer` (v8.0.11)
- `Microsoft.EntityFrameworkCore.Design` (v8.0.11)
- `Microsoft.Extensions.Configuration` (v10.0.2)
- `Microsoft.Extensions.Configuration.Json` (v10.0.2)

### Project Structure
```
C:\GithubRepo\FileDownloader\
??? Models/                          # Entity classes
?   ??? Library.cs                   # tblLibrary entity
?   ??? Folder.cs                    # tblFolder entity
?   ??? Document.cs                  # tblDocument entity
?   ??? File.cs                      # tblFile entity
?   ??? FileChunk.cs                 # tblFileChunk entity
?   ??? FileChunksArchived.cs        # tblFileChunksArchived entity
?
??? Data/                            # Data context
?   ??? ApplicationDbContext.cs      # EF Core DbContext
?
??? Services/                        # Business logic
?   ??? LibraryDownloadService.cs    # Download service
?
??? Program.cs                       # Main entry point (Interactive Console)
??? appsettings.json                 # Configuration (Connection strings)
??? FileDownloader.csproj            # Project file

```

---

## ?? **Configuration Required**

### 1. Update Connection String

Edit `appsettings.json` and update the connection string:

```json
{
  "ConnectionStrings": {
    "DefaultConnection": "Server=YOUR_SERVER_NAME;Database=YOUR_DATABASE_NAME;User Id=YOUR_USERNAME;Password=YOUR_PASSWORD;TrustServerCertificate=True;"
  }
}
```

**Example configurations:**

#### Windows Authentication:
```json
"DefaultConnection": "Server=localhost;Database=RicohDocs;Integrated Security=True;TrustServerCertificate=True;"
```

#### SQL Server Authentication:
```json
"DefaultConnection": "Server=192.168.1.100;Database=RicohDocs;User Id=sa;Password=YourPassword;TrustServerCertificate=True;"
```

### 2. Configure Download Paths

Update paths in `appsettings.json`:

```json
{
  "AppSettings": {
    "DownloadPath": "C:\\Downloads",           # Where files will be downloaded
    "FileStorageLocation": "DATABASE",         # or "FILESYSTEM"
    "NAS_StoragePath": "C:\\Storage\\NAS",     # If using file system storage
    "StoragePath": "C:\\Storage\\Temp"         # Temporary storage path
  }
}
```

---

## ?? **How to Run**

### 1. Build the Project
```bash
cd C:\GithubRepo\FileDownloader
dotnet build
```

### 2. Run the Application
```bash
dotnet run
```

### 3. Or Run Executable Directly
```bash
C:\GithubRepo\FileDownloader\bin\Debug\net8.0\FileDownloader.exe
```

---

## ?? **Application Features**

### Interactive Console Menu

When you run the application, you'll see:

```
=================================
File Downloader - .NET 8
Library Download Service
=================================

Testing database connection... ? Connected

=====================================
MENU:
1. List all libraries
2. Download library by ID
3. Exit
=====================================
Enter your choice (1-3):
```

### Option 1: List All Libraries
Displays all available libraries with document counts:
```
Available Libraries:
====================
ID         Name                                     Documents      
-----------------------------------------------------------------
1          Legal Documents                          245            
2          HR Files                                 1,532          
3          Financial Reports                        89             
-----------------------------------------------------------------
Total libraries: 3
```

### Option 2: Download Library by ID
1. Enter library ID
2. View library information
3. Confirm download
4. See real-time progress
5. View download summary

**Example Output:**
```
Enter Library ID to download: 1

Library Information:
====================
ID:          1
Name:        Legal Documents
Description: Company legal documents archive
Documents:   245

Do you want to proceed with download? (Y/N): Y

====================
STARTING DOWNLOAD...
====================

[2024-02-09 15:30:15] === STARTING LIBRARY DOWNLOAD ===
[2024-02-09 15:30:15] Library ID: 1
[2024-02-09 15:30:15] Library: Legal Documents (245 documents)
[2024-02-09 15:30:15] Download path: C:\Downloads\Library_1_20240209_153015
[2024-02-09 15:30:15] Processing 5 pages of 50 documents each
[2024-02-09 15:30:15] --- Processing page 1 of 5 ---
[2024-02-09 15:30:16] Downloading: Contract_2023.pdf (ID: 101)
[2024-02-09 15:30:16] File is chunked (5 chunks)
[2024-02-09 15:30:17] ? Success (1/245)
...

====================
DOWNLOAD SUMMARY
====================
Status:          ? SUCCESS
Library:         Legal Documents
Total Documents: 245
Downloaded:      245
Failed:          0
Duration:        02:45
Message:         Download completed. Success: 245, Failed: 0
====================
```

---

## ?? **How It Works**

### Database Connection
1. Application reads `appsettings.json`
2. Tests database connectivity
3. Creates `ApplicationDbContext` with EF Core
4. Queries library and document data

### Download Process
1. **List Libraries**: Queries `tblLibrary` with document counts
2. **Get Library Info**: Retrieves library details and document count
3. **Download Files**: 
   - Reads documents from `tblDocument`
   - Gets file chunks from `tblFileChunk` or `tblFileChunksArchived`
   - Reconstructs files following FileDownloadHandler.ashx logic
   - Saves to output directory maintaining folder structure

### File Reconstruction Logic
Follows the **exact same logic** as FileDownloadHandler.ashx:
- Checks if file is stored in DB or file system
- For chunked files: processes chunks sequentially
  - Last chunk uses index `-1`
  - First chunk creates file
  - Other chunks append
- For non-chunked files: uses `HashValue` from `tblFile`
- Falls back to file system if database storage fails

---

## ?? **Database Schema**

The application expects these tables:

### Tables Used
1. **tblLibrary** - Libraries
2. **tblFolder** - Folders in libraries
3. **tblDocument** - Documents in folders
4. **tblFile** - File metadata
5. **tblFileChunk** - File chunks (active)
6. **tblFileChunksArchived** - File chunks (archived)

### Key Relationships
```
Library (1) ?? (N) Folder
Folder (1) ?? (N) Document
Document (1) ?? (1) File
File (1) ?? (N) FileChunk
File (1) ?? (N) FileChunksArchived
```

---

## ??? **Troubleshooting**

### Issue: Connection Failed
**Solution:**
1. Check connection string in `appsettings.json`
2. Verify SQL Server is running
3. Check firewall settings
4. Verify database exists
5. Test with SQL Server Management Studio

### Issue: No Libraries Found
**Solution:**
1. Check if `tblLibrary` table exists
2. Verify data exists: `SELECT * FROM tblLibrary WHERE Deleted = 0`
3. Check database permissions

### Issue: Download Fails
**Solution:**
1. Check `AppSettings:DownloadPath` in appsettings.json
2. Verify folder write permissions
3. Ensure sufficient disk space
4. Check file chunk data exists in database

### Issue: Cannot Find Type 'File'
**Solution:**
- The project uses `FileDownloader.Models.File` to avoid conflict with `System.IO.File`
- Entity is properly qualified in DbContext

---

## ?? **Key Features**

? **Interactive Console Interface** - Easy to use menu system  
? **Entity Framework Core 8** - Modern ORM with async/await  
? **Connection String Configuration** - Flexible database connection  
? **Real-time Progress** - See download status as it happens  
? **Error Handling** - Graceful failure handling  
? **Exact Logic Match** - Follows FileDownloadHandler.ashx exactly  
? **Batch Processing** - Processes documents in pages (50 per page)  
? **Folder Structure** - Maintains original folder hierarchy  
? **Chunked Files** - Handles large files split into chunks  
? **Archived Files** - Supports archived chunk tables  

---

## ?? **Example Usage Scenarios**

### Scenario 1: Download Single Library
```
1. Run application
2. Choose option "2" (Download library by ID)
3. Enter library ID (e.g., 1)
4. Confirm download
5. Wait for completion
6. Files saved to: C:\Downloads\Library_1_YYYYMMDD_HHMMSS\
```

### Scenario 2: Browse Available Libraries
```
1. Run application
2. Choose option "1" (List all libraries)
3. Review list of libraries
4. Choose option "2" to download specific one
```

### Scenario 3: Batch Download Multiple Libraries
```
1. Run application
2. List libraries (option 1)
3. Download first library (option 2)
4. After completion, download next library
5. Repeat as needed
```

---

## ?? **Security Notes**

1. **Connection Strings**: Never commit `appsettings.json` with real credentials
2. **Use User Secrets** in production: `dotnet user-secrets`
3. **File Permissions**: Ensure download directory has proper access control
4. **SQL Injection**: Entity Framework Core protects against SQL injection
5. **Trust Server Certificate**: Use `TrustServerCertificate=True` only in development

---

## ?? **Advanced Configuration**

### Using User Secrets (Production)
```bash
cd C:\GithubRepo\FileDownloader
dotnet user-secrets init
dotnet user-secrets set "ConnectionStrings:DefaultConnection" "YourConnectionString"
```

### Environment Variables
You can also use environment variables:
```bash
set ConnectionStrings__DefaultConnection=Server=...;Database=...
```

---

## ?? **Next Steps**

1. **Update Connection String** in appsettings.json
2. **Run Application**: `dotnet run`
3. **Test Connection**: Verify database connectivity
4. **List Libraries**: See available libraries
5. **Download Test Library**: Start with a small library
6. **Review Output**: Check downloaded files

---

## ?? **Build Status**

? **Build**: Success  
? **Entity Framework**: Configured  
? **Database Models**: Created  
? **Download Service**: Implemented  
? **Interactive Console**: Ready  
? **Configuration**: Template provided  

---

## ?? **Additional Resources**

- [Entity Framework Core Documentation](https://docs.microsoft.com/en-us/ef/core/)
- [.NET 8 Documentation](https://docs.microsoft.com/en-us/dotnet/core/whats-new/dotnet-8)
- [SQL Server Connection Strings](https://www.connectionstrings.com/sql-server/)

---

*Created: February 9, 2024*  
*Framework: .NET 8.0*  
*EF Core: 8.0.11*  
*Status: ? Ready for Use*

**Happy Downloading!** ??
