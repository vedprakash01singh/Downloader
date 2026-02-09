# File Downloader - .NET 8 Console Application

## ?? Project Information
- **Project Name**: FileDownloader
- **Type**: .NET 8 Console Application
- **Target Framework**: .NET 8.0
- **C# Language Version**: 12.0
- **Location**: C:\GithubRepo\FileDownloader
- **Project Format**: SDK-style (modern)

## ?? Features
- Modern .NET 8 SDK-style project
- Top-level statements support
- Implicit usings enabled
- Nullable reference types enabled
- C# 12.0 language features

## ?? Project Structure
```
C:\GithubRepo\FileDownloader\
??? FileDownloader.csproj    # SDK-style project file
??? Program.cs               # Main entry point (top-level statements)
??? bin\                     # Build output (after build)
??? obj\                     # Intermediate build files (after build)
```

## ?? Building the Project

### Using .NET CLI (Recommended)
```bash
cd C:\GithubRepo\FileDownloader
dotnet build
```

### Using MSBuild
```bash
msbuild C:\GithubRepo\FileDownloader\FileDownloader.csproj /t:Build /p:Configuration=Debug
```

### Using Visual Studio
1. Open `FileDownloader.csproj` in Visual Studio 2022
2. Press **Ctrl+Shift+B** to build
3. Press **F5** to run with debugging or **Ctrl+F5** without debugging

## ?? Running the Application

### Using .NET CLI
```bash
cd C:\GithubRepo\FileDownloader
dotnet run
```

### From Build Output
```bash
C:\GithubRepo\FileDownloader\bin\Debug\net8.0\FileDownloader.exe
```

## ?? Adding NuGet Packages

### Using .NET CLI
```bash
cd C:\GithubRepo\FileDownloader
dotnet add package <PackageName>
```

Example:
```bash
dotnet add package Newtonsoft.Json
dotnet add package Microsoft.EntityFrameworkCore
dotnet add package Microsoft.Extensions.Configuration.Json
```

### Using Package Manager Console (Visual Studio)
```powershell
Install-Package <PackageName>
```

## ?? Adding Project References

If you want to reference the .NET Framework Download project, you'll need to:

1. **Option 1: Create a .NET Standard wrapper library**
   - Create a .NET Standard 2.0 class library
   - Port the needed classes from Download project
   - Reference from both .NET Framework and .NET 8 projects

2. **Option 2: Use .NET Framework 4.x target**
   - Change `TargetFramework` to `net48` in the .csproj
   - Add reference to Download.dll

## ?? Example Code

### Basic Console App with Arguments
```csharp
// Program.cs
if (args.Length == 0)
{
    Console.WriteLine("No arguments provided");
    Console.WriteLine("Usage: FileDownloader <libraryId> <username>");
    return;
}

Console.WriteLine($"Library ID: {args[0]}");
Console.WriteLine($"Username: {args[1]}");
```

### With Configuration File
Add package:
```bash
dotnet add package Microsoft.Extensions.Configuration.Json
```

Create `appsettings.json`:
```json
{
  "DatabaseConnection": "your-connection-string",
  "DownloadPath": "C:\\Downloads"
}
```

Update .csproj to copy appsettings.json:
```xml
<ItemGroup>
  <None Update="appsettings.json">
    <CopyToOutputDirectory>PreserveNewest</CopyToOutputDirectory>
  </None>
</ItemGroup>
```

Use in Program.cs:
```csharp
using Microsoft.Extensions.Configuration;

var configuration = new ConfigurationBuilder()
    .SetBasePath(Directory.GetCurrentDirectory())
    .AddJsonFile("appsettings.json", optional: false)
    .Build();

string connectionString = configuration["DatabaseConnection"] ?? "";
Console.WriteLine($"Connection: {connectionString}");
```

## ?? Key Differences from .NET Framework

| Feature | .NET Framework 4.5 | .NET 8 |
|---------|-------------------|--------|
| Project Format | Old-style XML | SDK-style |
| Implicit Usings | No | Yes (enabled) |
| Nullable Reference Types | No | Yes (enabled) |
| Top-level Statements | No | Yes |
| C# Version | 7.3 | 12.0 |
| Global Usings | No | Yes |
| File-scoped Namespaces | No | Yes |

## ?? Prerequisites

- .NET 8 SDK installed ([Download](https://dotnet.microsoft.com/download/dotnet/8.0))
- Visual Studio 2022 (17.8 or later) or VS Code with C# extension

## ? Verify Installation

Check if .NET 8 SDK is installed:
```bash
dotnet --version
```

Should show version 8.0.x or higher.

List all installed SDKs:
```bash
dotnet --list-sdks
```

## ?? Next Steps

1. **Build the project**
   ```bash
   cd C:\GithubRepo\FileDownloader
   dotnet build
   ```

2. **Run the application**
   ```bash
   dotnet run
   ```

3. **Add your file download logic**
   - Create services/classes as needed
   - Add NuGet packages
   - Implement download functionality

4. **Publish for deployment**
   ```bash
   dotnet publish -c Release -r win-x64 --self-contained
   ```

## ?? Advantages of .NET 8

- ? Cross-platform (Windows, Linux, macOS)
- ? Better performance than .NET Framework
- ? Modern C# language features
- ? Smaller deployment size
- ? Active development and support
- ? Cloud-native features
- ? Minimal APIs for web services

## ?? Resources

- [.NET 8 Documentation](https://learn.microsoft.com/en-us/dotnet/core/whats-new/dotnet-8)
- [C# 12 What's New](https://learn.microsoft.com/en-us/dotnet/csharp/whats-new/csharp-12)
- [.NET CLI Reference](https://learn.microsoft.com/en-us/dotnet/core/tools/)

## ?? Project Status

? **Created and ready to use!**

---

*Created: 2024*  
*Target: .NET 8.0*  
*Language: C# 12.0*
