# Connection String Configuration - Quick Reference

## ? Current Setup (Secure)

Your application is now configured to use `secrets.json` for the connection string, which is **NOT committed to Git**.

### File Structure:
```
FileDownloader/
??? appsettings.json          (No connection string - safe for Git)
??? secrets.json              (Has connection string - blocked by .gitignore)
??? Program.cs                (Loads both files)
??? .gitignore                (Blocks secrets.json)
```

## ?? How It Works:

### 1. **appsettings.json** (Committed to Git)
```json
{
  "Logging": { ... },
  "AppSettings": { ... }
}
```
- ? NO connection string here
- ? Safe to commit to Git
- ? Contains non-sensitive settings only

### 2. **secrets.json** (NOT Committed to Git)
```json
{
  "ConnectionStrings": {
    "DefaultConnection": "Server=YOUR_SERVER;Database=YOUR_DB;..."
  }
}
```
- ? Contains actual connection string
- ? Blocked by `.gitignore`
- ? Never pushed to Git repository

### 3. **Program.cs** (Configuration Loading)
```csharp
var configuration = new ConfigurationBuilder()
    .SetBasePath(Directory.GetCurrentDirectory())
    .AddJsonFile("appsettings.json", optional: false, reloadOnChange: true)
    .AddJsonFile("secrets.json", optional: true, reloadOnChange: true)  // Overrides appsettings
    .Build();
```
- Loads `appsettings.json` first
- Then loads `secrets.json` (overrides any matching keys)
- Connection string comes from `secrets.json`

## ?? Troubleshooting

### Error: "Connection string not configured!"

**Cause**: `secrets.json` file is missing or doesn't have the connection string.

**Solution**:

1. **Check if `secrets.json` exists** in the project root:
   ```
   FileDownloader/secrets.json  ? Should be here
   ```

2. **Verify `secrets.json` content**:
   ```json
   {
     "ConnectionStrings": {
       "DefaultConnection": "Server=DESKTOP-ERA0D07\\SQLEXPRESS;Database=RicohDisneyUATNew;User Id=sa;Password=p@ssw0rd1111;TrustServerCertificate=True;MultipleActiveResultSets=True;Encrypt=False;"
     }
   }
   ```

3. **Check the file location**:
   - Must be in the **same directory as Program.cs**
   - NOT in bin/Debug or any subdirectory
   - File name must be exactly `secrets.json` (case-sensitive on Linux)

4. **Verify file properties**:
   - Not marked as "Read-only"
   - Not hidden (on Windows)
   - Proper permissions to read

### Error: "Database connection failed"

**Possible Causes**:

1. **SQL Server not running**
   - Check if SQL Server service is running
   - Verify server name: `DESKTOP-ERA0D07\SQLEXPRESS`

2. **Wrong credentials**
   - User: `sa`
   - Password: `p@ssw0rd1111`
   - Database: `RicohDisneyUATNew`

3. **Firewall blocking connection**
   - Allow SQL Server through Windows Firewall
   - Enable TCP/IP protocol in SQL Server Configuration Manager

## ?? Setting Up on a New Machine

When you clone the repository on a new machine:

### Step 1: Clone the repository
```bash
git clone https://github.com/vedprakash01singh/Downloader
cd FileDownloader
```

### Step 2: Create `secrets.json`
```bash
# Create the file
notepad secrets.json
```

Add your connection string:
```json
{
  "ConnectionStrings": {
    "DefaultConnection": "Server=YOUR_SERVER\\SQLEXPRESS;Database=YOUR_DATABASE;User Id=YOUR_USER;Password=YOUR_PASSWORD;TrustServerCertificate=True;MultipleActiveResultSets=True;Encrypt=False;"
  }
}
```

### Step 3: Verify `.gitignore` includes secrets.json
```
# Should already be there:
secrets.json
```

### Step 4: Run the application
```bash
dotnet run
```

## ?? Security Verification

### ? Verify secrets.json is NOT in Git:
```bash
# Check Git status (should NOT show secrets.json)
git status

# Verify .gitignore is working
git check-ignore secrets.json
# Output: secrets.json  (means it's ignored)
```

### ? What SHOULD be in Git:
- ? `appsettings.json` (no connection string)
- ? `.gitignore` (includes secrets.json)
- ? `Program.cs`
- ? All source code files

### ? What should NOT be in Git:
- ? `secrets.json`
- ? `bin/` directory
- ? `obj/` directory
- ? Any file with passwords or connection strings

## ?? Quick Commands

### Check configuration is correct:
```bash
# Build project
dotnet build

# Run application (will show error if secrets.json is missing)
dotnet run
```

### Verify file locations:
```bash
# Windows PowerShell
Get-ChildItem -Filter secrets.json
Get-ChildItem -Filter appsettings.json

# Linux/Mac
ls -la secrets.json appsettings.json
```

## ?? Common Questions

**Q: Why not just put the connection string in appsettings.json?**
A: Because appsettings.json is committed to Git. Anyone with access to the repository would see your credentials!

**Q: What if I accidentally committed secrets.json?**
A: 
1. Remove it from Git history:
   ```bash
   git rm --cached secrets.json
   git commit -m "Remove secrets.json from tracking"
   ```
2. Change your database password immediately!
3. Update secrets.json with the new password

**Q: Do I need secrets.json for production?**
A: No! In production, use:
- Environment variables
- Azure Key Vault
- AWS Secrets Manager
- Kubernetes Secrets
- Or other secure credential management systems

**Q: Can I use User Secrets instead?**
A: Yes! User Secrets is another secure option for development:
```bash
dotnet user-secrets init
dotnet user-secrets set "ConnectionStrings:DefaultConnection" "Your-Connection-String"
```

## ? Summary

Your setup is **secure and correct**:
- ? Connection string in `secrets.json` (not in Git)
- ? `appsettings.json` has no sensitive data
- ? `.gitignore` blocks `secrets.json`
- ? Application loads both files automatically
- ? Ready for team collaboration without exposing credentials

**Remember**: Never commit passwords or connection strings to Git!
