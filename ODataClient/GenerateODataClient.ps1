#!/usr/bin/env pwsh
<#
.SYNOPSIS
    Generates modern OData client using Microsoft.OData.CLI.

.DESCRIPTION
    This script generates a modern OData client using the Microsoft.OData.CLI tool.
    It supports both .NET Framework 4.8 and .NET 9.0 targets.

.PARAMETER MetadataUri
    The URI of the OData metadata document.

.PARAMETER OutputDirectory
    The output directory for generated files.

.PARAMETER Namespace
    The namespace for the generated client.

.EXAMPLE
    .\GenerateODataClient.ps1
    Generates the OData client with default settings.

.EXAMPLE
    .\GenerateODataClient.ps1 -MetadataUri "http://localhost:8080/odata/$metadata"
    Generates the OData client with a custom metadata URI.
#>

param(
    [Parameter(Mandatory=$false)]
    [string]$MetadataUri = "http://websvc1.connectomes.utah.edu/RC1/OData/$metadata",
    
    [Parameter(Mandatory=$false)]
    [string]$OutputDirectory = "Generated",
    
    [Parameter(Mandatory=$false)]
    [string]$Namespace = "ODataClient"
)

$ErrorActionPreference = "Stop"

Write-Host "Generating modern OData client..." -ForegroundColor Green
Write-Host "Metadata URI: $MetadataUri" -ForegroundColor Yellow
Write-Host "Output Directory: $OutputDirectory" -ForegroundColor Yellow
Write-Host "Namespace: $Namespace" -ForegroundColor Yellow

# Check if odata-cli is installed
try {
    $null = Get-Command odata-cli -ErrorAction Stop
    Write-Host "Found odata-cli tool" -ForegroundColor Green
} catch {
    Write-Error "odata-cli tool not found. Please install it with: dotnet tool install --global microsoft.odata.cli"
    exit 1
}

# Create output directory
$fullOutputPath = Join-Path $PSScriptRoot $OutputDirectory
if (-not (Test-Path $fullOutputPath)) {
    New-Item -ItemType Directory -Path $fullOutputPath -Force | Out-Null
    Write-Host "Created output directory: $fullOutputPath" -ForegroundColor Green
}

# Test metadata URI accessibility
Write-Host "Testing metadata URI accessibility..." -ForegroundColor Yellow
try {
    $response = Invoke-WebRequest -Uri $MetadataUri -Method Head -TimeoutSec 10 -ErrorAction Stop
    Write-Host "Metadata URI is accessible (Status: $($response.StatusCode))" -ForegroundColor Green
} catch {
    Write-Warning "Metadata URI is not accessible: $($_.Exception.Message)"
    Write-Host "Continuing with generation attempt..." -ForegroundColor Yellow
}

# Generate OData client
Write-Host "Generating OData client..." -ForegroundColor Yellow

$arguments = @(
    "generate",
    "-m", $MetadataUri,
    "-ns", $Namespace,
    "-et",
    "-o", $fullOutputPath,
    "-fn", "ODataClient.cs"
)

try {
    $process = Start-Process -FilePath "odata-cli" -ArgumentList $arguments -Wait -PassThru -NoNewWindow
    
    if ($process.ExitCode -eq 0) {
        Write-Host "OData client generated successfully!" -ForegroundColor Green
        
        $generatedFile = Join-Path $fullOutputPath "ODataClient.cs"
        if (Test-Path $generatedFile) {
            $fileInfo = Get-Item $generatedFile
            Write-Host "Generated file: $generatedFile" -ForegroundColor Green
            Write-Host "File size: $($fileInfo.Length) bytes" -ForegroundColor Green
            Write-Host "Last modified: $($fileInfo.LastWriteTime)" -ForegroundColor Green
        } else {
            Write-Warning "Generated file not found at expected location: $generatedFile"
        }
    } else {
        Write-Error "OData client generation failed with exit code: $($process.ExitCode)"
        exit 1
    }
} catch {
    Write-Error "Error generating OData client: $_"
    exit 1
}

Write-Host "OData client generation completed!" -ForegroundColor Green 