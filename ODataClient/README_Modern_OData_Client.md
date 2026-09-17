# Modern OData Client Generation

This document explains how to generate OData clients using the modern Microsoft.OData.CLI approach.

## Overview

The ODataClient project has been migrated from T4 templates to the modern Microsoft.OData.CLI tool. This provides:

- **Better performance** - Optimized for modern .NET
- **Enhanced features** - Better LINQ support, async operations
- **Cross-platform** - Works on Windows, macOS, Linux
- **Modern .NET support** - Targets .NET 6+, .NET Standard 2.0+
- **Easier maintenance** - No complex T4 templates to maintain

## Prerequisites

Install the Microsoft.OData.CLI tool:

```bash
dotnet tool install --global microsoft.odata.cli
```

## Generation Options

### Option 1: PowerShell Script (Recommended)

```powershell
# Generate with default settings
.\GenerateODataClient.ps1

# Generate with custom metadata URI
.\GenerateODataClient.ps1 -MetadataUri "http://localhost:8080/odata/$metadata"

# Generate with all custom settings
.\GenerateODataClient.ps1 -MetadataUri "http://localhost:8080/odata/$metadata" -OutputDirectory "CustomOutput" -Namespace "MyNamespace"
```

### Option 2: Batch File

```cmd
# Generate with default settings
GenerateODataClient.bat

# Generate with custom settings
GenerateODataClient.bat "http://localhost:8080/odata/$metadata" "CustomOutput" "MyNamespace"
```

### Option 3: MSBuild Target

```cmd
# Generate OData client
dotnet msbuild ODataClient.csproj -t:GenerateODataClientManual

# Build with automatic generation
dotnet build ODataClient.csproj
```

### Option 4: Direct CLI

```cmd
# Generate OData client directly
odata-cli generate -m "http://webdev.connectomes.utah.edu/RC1Test/OData/$metadata" -ns "ODataClient" -et -o "Generated" -fn "ODataClient.cs"
```

## Configuration

### Project File Settings

The project file includes:

- **Multi-target support**: `net48` and `net9.0`
- **Conditional package references**: Different OData.Client versions for each target
- **Automatic generation**: Runs during build (with error handling)
- **Manual generation**: Separate target for explicit generation

### Generated Files

Generated files are placed in the `Generated/` directory:

- `ODataClient.cs` - Main generated client code
- Additional files if using `-multiple-files` option

## CLI Options

The Microsoft.OData.CLI tool supports many options:

```bash
odata-cli generate --help
```

Key options:
- `-m, --metadata-uri` - OData metadata URI
- `-ns, --namespace-prefix` - Generated code namespace
- `-et, --enable-tracking` - Enable entity tracking
- `-o, --outputdir` - Output directory
- `-fn, --file-name` - Generated file name
- `-multiple-files` - Split into multiple files
- `-i, --enable-internal` - Use internal visibility

## Async Support

### .NET 9.0 and Later
- The generated client and Microsoft.OData.Client 8.x+ provide true async APIs.
- Use the built-in async methods where available.

### .NET Framework 4.8
- The generated client (with Microsoft.OData.Client 7.x) does **not** provide true async APIs.
- **Async wrappers are provided via extension methods** in `ODataClientAsyncExtensions.cs`:
  - `await query.ToListAsync()`
  - `await query.FirstOrDefaultAsync()`
  - `await querySingle.GetValueAsync()`
- These wrap the public DataServiceQuery APIs in `Task.Run` for compatibility.
- This is not true async, but allows for async/await code style in consumers.

## Migration to Modern OData Client

### What Changed

1. **Modern tooling** - Uses Microsoft.OData.CLI instead of legacy approaches
2. **Better error handling** - Graceful handling of network issues
3. **Simplified maintenance** - No complex template logic
4. **Cross-platform** - Works on any OS with .NET
5. **Future-proof** - Supported by Microsoft going forward

### Benefits

- **Faster generation** - Modern tooling is more efficient
- **Better error messages** - Clearer feedback on issues
- **Cross-platform** - Works on any OS with .NET
- **Future-proof** - Supported by Microsoft going forward

## Troubleshooting

### Metadata URI Not Accessible

If the metadata URI is not accessible:

1. **Check network connectivity**
2. **Verify the URI is correct**
3. **Try with a local metadata file**:
   ```bash
   odata-cli generate -m "file:///path/to/metadata.xml" -ns "ODataClient" -et -o "Generated"
   ```

### Generation Fails

If generation fails:

1. **Check odata-cli is installed**: `odata-cli --version`
2. **Verify metadata format**: Ensure it's valid OData metadata
3. **Check output directory permissions**
4. **Review error messages** for specific issues

### Build Errors

If you get build errors:

1. **Clean and rebuild**: `dotnet clean && dotnet build`
2. **Check generated files**: Ensure `Generated/ODataClient.cs` exists
3. **Verify package references**: Check OData.Client versions match targets

## Advanced Usage

### Custom Headers

```bash
odata-cli generate -m "http://service/odata/$metadata" -h "Authorization:Bearer token" -ns "ODataClient" -et -o "Generated"
```

### Proxy Settings

```bash
odata-cli generate -m "http://service/odata/$metadata" -p "domain\user:password@proxy:port" -ns "ODataClient" -et -o "Generated"
```

### Exclude Operations

```bash
odata-cli generate -m "http://service/odata/$metadata" -eoi "Operation1,Operation2" -ns "ODataClient" -et -o "Generated"
```

## Integration with CI/CD

Add to your build pipeline:

```yaml
- task: DotNetCoreCLI@2
  inputs:
    command: 'tool'
    arguments: 'install --global microsoft.odata.cli'

- task: DotNetCoreCLI@2
  inputs:
    command: 'msbuild'
    arguments: 'ODataClient.csproj -t:GenerateODataClientManual'
```

## Notes

- Generated files are committed to source control for convenience
- The build process attempts generation but continues on failure
- Manual generation is available for explicit control
- The modern approach is more reliable and maintainable than T4 templates 