# ODataClient T4 Template Regeneration

This document explains how to regenerate the ODataClient.cs file from the T4 template.

## Overview

The ODataClient project uses a T4 template to generate client code from an OData service metadata. The generated code is compatible with both .NET Framework 4.8 and .NET 9.0, using different OData client APIs for each target framework.

## Automatic Regeneration

The T4 template is automatically regenerated during the build process for each target framework. This ensures that the generated code is always up-to-date with the latest template changes.

## Manual Regeneration Options

### Option 1: PowerShell Script (Recommended)

Use the PowerShell script for the most control and detailed output:

```powershell
# Regenerate for all target frameworks
.\RegenerateT4.ps1

# Regenerate for specific framework
.\RegenerateT4.ps1 -TargetFramework net9.0
.\RegenerateT4.ps1 -TargetFramework net48
```

### Option 2: Batch File

Use the batch file for quick execution:

```cmd
# Regenerate for all target frameworks
RegenerateT4.bat

# Regenerate for specific framework
RegenerateT4.bat net9.0
RegenerateT4.bat net48
```

### Option 3: MSBuild Targets

Use MSBuild targets for integration with build processes:

```cmd
# Regenerate T4 template
dotnet msbuild ODataClient.csproj -t:RegenerateT4

# Clean and regenerate
dotnet msbuild ODataClient.csproj -t:CleanAndRegenerateT4

# Regenerate for specific framework
dotnet msbuild ODataClient.csproj -t:RegenerateT4ForFramework -p:TargetFramework=net9.0
```

### Option 4: Visual Studio

In Visual Studio, you can:
1. Right-click on `ODataClient.tt` in Solution Explorer
2. Select "Run Custom Tool"

## Prerequisites

- Visual Studio 2019/2022 or Build Tools
- TextTransform.exe (included with Visual Studio/Build Tools)

## Troubleshooting

### TextTransform.exe not found
If the PowerShell script can't find TextTransform.exe, ensure you have:
- Visual Studio 2019/2022 installed, or
- Build Tools installed

The script searches common installation paths automatically.

### Template not regenerating
If the template isn't regenerating automatically:
1. Clean the project: `dotnet clean`
2. Manually regenerate using one of the options above
3. Rebuild the project

### Build errors after regeneration
If you get build errors after regeneration:
1. Check that the generated code uses the correct conditional compilation
2. Ensure the target framework is properly detected
3. Clean and rebuild the project

## Generated Code Structure

The generated `ODataClient.cs` file includes conditional compilation directives:

```csharp
#if NET9_0_OR_GREATER || NET8_0_OR_GREATER || NET7_0_OR_GREATER || NET6_0_OR_GREATER || NETSTANDARD2_0_OR_GREATER
    return global::Microsoft.OData.Edm.Csdl.CsdlReader.Parse(reader);
#else
    return global::Microsoft.OData.Edm.Csdl.EdmxReader.Parse(reader);
#endif
```

This ensures compatibility with both .NET Framework 4.8 and modern .NET versions.

## Configuration

The T4 template configuration is in `ODataClient.tt`:

- **MetadataDocumentUri**: The OData service metadata endpoint
- **UseDataServiceCollection**: Enable entity tracking
- **NamespacePrefix**: The namespace for generated types
- **TargetLanguage**: C# or VB
- **EnableNamingAlias**: Enable naming conventions
- **IgnoreUnexpectedElementsAndAttributes**: Ignore unknown metadata elements

## Notes

- The generated file is committed to source control for convenience
- Manual regeneration is typically only needed when:
  - The OData service metadata changes
  - The T4 template is modified
  - Switching between target frameworks
- The automatic regeneration during build ensures the code is always current 