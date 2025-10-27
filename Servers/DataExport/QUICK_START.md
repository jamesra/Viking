# DataExport Quick Start Guide

## Overview
DataExport is a modernized ASP.NET Core 9.0 web service for exporting neural network and morphology data in various formats (DOT, TLP, GraphML, JSON).

## Prerequisites
- .NET 9.0 SDK or later
- Visual Studio 2022 / VS Code / Rider

## Quick Start

### Running the Service
```bash
cd Servers/DataExport
dotnet run
```

The service will start on `http://localhost:62418` (configurable in `Properties/launchSettings.json`).

### Running Tests
```bash
cd Servers/DataExport.Tests
dotnet test
```

## API Endpoints

### Network Export
Export neural network connectivity graphs:

- `GET /Network/GetDot?id=180;476&hops=2` - Export as DOT format
- `GET /Network/GetTLP?id=180;476&hops=2` - Export as Tulip (TLP) format
- `GET /Network/GetGML?id=180;476&hops=2` - Export as GraphML format
- `GET /Network/GetJSON?id=180;476&hops=2` - Export as JSON format

**Parameters:**
- `id` or `ids` - Semicolon-separated list of structure IDs
- `hops` - Number of network hops to traverse (default: 1)

### Morphology Export
Export morphological structure data:

- `GET /Morphology/GetTLP?id=180` - Export as Tulip (TLP) format with spatial data
- `GET /Morphology/GetJSON?id=180` - Export as JSON format
- `POST /Morphology/PostTLP` - Export with query parameters in POST body
- `POST /Morphology/PostJSON` - Export with query parameters in POST body

**Parameters:**
- `id` or `ids` - Semicolon-separated list of structure IDs
- `stick` or `Stick` - Set to 1 to generate stick figure representation

### Motif Export
Export motif pattern graphs:

- `GET /Motif/GetDot` - Export motif graph as DOT format
- `GET /Motif/GetTLP` - Export motif graph as Tulip format
- `GET /Motif/GetJSON` - Export motif graph as JSON format

## Configuration

### appsettings.json
Main configuration file with default settings:

```json
{
  "AppSettings": {
    "VolumeURL": "https://connectomes.utah.edu/RC1",
    "ODataURL": "https://connectomes.utah.edu/RC1/OData"
  }
}
```

**Note:** Scale values (X/Y/Z) are automatically retrieved from the OData service and do not need to be configured.

### appsettings.Development.json
Development-specific overrides:

```json
{
  "AppSettings": {
    "VolumeURL": "https://webdev.connectomes.utah.edu/RC1Test",
    "ODataURL": "https://webdev.connectomes.utah.edu/RC1Test/OData"
  }
}
```

### Environment Variables
You can override any setting using environment variables:
```bash
export AppSettings__VolumeURL="https://custom.server.com"
export AppSettings__ODataURL="https://custom.server.com/OData"
```

## Output Files
Generated files are saved to the `Output/` directory in the application root.

### File Naming Conventions
- **Network exports**: `nw-{IDs}_hops_{N} {timestamp}.{ext}`
- **Morphology exports**: `morph-{IDs} {timestamp}.{ext}`
- **Motif exports**: `motifs{N}{timestamp}.{ext}`

## Development

### Project Structure
```
DataExport/
├── Controllers/          # API controllers
│   ├── NetworkController.cs
│   ├── MorphologyController.cs
│   └── MotifController.cs
├── Utils/               # Utility classes
│   └── RequestVariables.cs
├── Resources/           # Color maps and resources
├── Scripts/             # Client-side JavaScript
├── Content/             # Static content
├── Output/              # Generated export files
├── GlobalUsings.cs      # Global using directives
├── Program.cs           # Application entry point
└── appsettings.json     # Configuration
```

### Adding New Export Formats

1. Add a new action method to the appropriate controller
2. Implement the export logic using existing patterns
3. Follow the naming conventions for output files
4. Add XML documentation to the method

Example:
```csharp
/// <summary>
/// Exports network data in custom format.
/// </summary>
/// <returns>The generated file for download.</returns>
[HttpGet]
public async Task<IActionResult> GetCustomFormat()
{
    ICollection<long> requestIDs = RequestVariables.GetIDsFromQueryData(Request.Query);
    string outputFilename = GetOutputFilename(requestIDs, "custom");
    string outputFileFullPath = Path.Combine(GetAndCreateOutputDirectory(), outputFilename);

    NeuronGraph neuronGraph = await GetGraphAsync(requestIDs);
    // Implement your custom export logic here
    
    return PhysicalFile(outputFileFullPath, "text/plain", outputFilename);
}
```

## Troubleshooting

### Common Issues

**Issue**: Service fails to start
- **Solution**: Check that port 62418 is not in use, or modify `Properties/launchSettings.json`

**Issue**: Output files not being created
- **Solution**: Ensure the application has write permissions to the `Output/` directory

**Issue**: OData queries not working
- **Solution**: Verify the ODataURL in configuration points to a valid OData service

### Logging
Logs are output to the console. Adjust log levels in `appsettings.json`:

```json
{
  "Logging": {
    "LogLevel": {
      "Default": "Debug",
      "Microsoft.AspNetCore": "Information"
    }
  }
}
```

## Testing

### Unit Tests
```bash
cd Servers/DataExport.Tests
dotnet test
```

### Code Coverage
```bash
dotnet test --collect:"XPlat Code Coverage"
```

Coverage reports are generated in the `TestResults/` directory.

### Manual Testing
Use tools like:
- **Postman** - For API testing
- **curl** - For command-line testing
- **Browser** - For GET requests

Example curl command:
```bash
curl "http://localhost:62418/Network/GetJSON?id=180;476&hops=2" -o network.json
```

## Modern C# Features Used

This project leverages modern C# features:
- **File-scoped namespaces** - Reduces indentation
- **Global usings** - Eliminates repetitive using statements
- **Nullable reference types** - Prevents null reference exceptions
- **Pattern matching** - Simplifies conditional logic
- **Target-typed new** - Cleaner object instantiation
- **String interpolation** - More readable string formatting

## Additional Resources

- [ASP.NET Core Documentation](https://docs.microsoft.com/aspnet/core)
- [.NET 9.0 What's New](https://docs.microsoft.com/dotnet/core/whats-new/dotnet-9)
- [C# 12 Features](https://docs.microsoft.com/dotnet/csharp/whats-new/csharp-12)
- [Tulip Graph Format](https://tulip.labri.fr/)
- [DOT Graph Format](https://graphviz.org/doc/info/lang.html)

## Support

For issues or questions, refer to:
- Project documentation in `MODERNIZATION_SUMMARY.md`
- Code comments and XML documentation
- Team knowledge base




