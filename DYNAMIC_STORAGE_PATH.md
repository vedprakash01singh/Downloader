# Dynamic Storage Path from Database

## Changes Made

### 1. Created Setting Entity Model
**File:** `Models\Setting.cs`

Added a new entity model to represent the `tblSetting` table in the database:

```csharp
[Table("tblSetting")]
public class Setting
{
    [Key]
    public int ID { get; set; }
    public string? Name { get; set; }
    public string? Value { get; set; }
    public string? Description { get; set; }
    public bool? Deleted { get; set; }
}
```

### 2. Updated ApplicationDbContext
**File:** `Data\ApplicationDbContext.cs`

Added `DbSet<Setting> Settings` to enable querying the settings table.

### 3. Updated LibraryDownloadService
**File:** `Services\LibraryDownloadService.cs`

#### Added GetStoragePathAsync Method
This method retrieves the storage path dynamically from the database:

```csharp
private async Task<string> GetStoragePathAsync()
{
    try
    {
        var setting = await _context.Settings
            .Where(s => s.Name == "NAS_Storage" && s.Deleted != true)
            .FirstOrDefaultAsync();

        if (setting != null && !string.IsNullOrEmpty(setting.Value))
        {
            return setting.Value;
        }

        // Fallback to configuration
        return _configuration["AppSettings:StoragePath"] ?? "C:\\Storage";
    }
    catch (Exception)
    {
        // Fallback to configuration on error
        return _configuration["AppSettings:StoragePath"] ?? "C:\\Storage";
    }
}
```

#### Updated ReconstructFileAsync Method
Changed the file system storage logic to use the database storage path:

**Before:**
```csharp
string storagePath = _configuration["AppSettings:StoragePath"] ?? "C:\\Storage";
```

**After:**
```csharp
string storagePath = await GetStoragePathAsync();
```

## How It Works

1. **Database Lookup:** When a file is stored in the file system (not in the database), the service queries the `tblSetting` table for a setting with `Name = "NAS_Storage"`.

2. **Fallback Mechanism:** If the database lookup fails or returns null/empty:
   - First tries the configuration value from `appsettings.json` (`AppSettings:StoragePath`)
   - Finally falls back to the default path `"C:\\Storage"`

3. **File Reconstruction:** The storage path is then combined with the file ID to locate the file chunks on the file system:
   ```csharp
   string fileContentPath = Path.Combine(storagePath, fileId.ToString());
   ```

## Database Query

The service executes this query to get the storage path:

```sql
SELECT TOP 1 * 
FROM tblSetting 
WHERE Name = 'NAS_Storage' 
  AND (Deleted IS NULL OR Deleted = 0)
```

## Benefits

- ? **Dynamic Configuration:** Storage path can be changed in the database without redeploying the application
- ? **Centralized Management:** All applications using the same database will use the same storage path
- ? **Resilient:** Multiple fallback levels ensure the application continues to work even if the database setting is missing
- ? **Matches Original Logic:** Replicates the exact behavior from `FileDownloadHandler.ashx.cs` (line 102)

## Testing

To verify the dynamic storage path is working:

1. Check your `tblSetting` table:
   ```sql
   SELECT * FROM tblSetting WHERE Name = 'NAS_Storage'
   ```

2. The `Value` column should contain your actual storage path (e.g., `\\server\share\storage` or `C:\FileStorage`)

3. Run the application and download a library with files stored in the file system

4. Check the logs to see which storage path was used

## Example Database Record

```sql
INSERT INTO tblSetting (Name, Value, Description, Deleted)
VALUES ('NAS_Storage', 'C:\NAS_Storage', 'Network Attached Storage path for file system storage', 0)
```
