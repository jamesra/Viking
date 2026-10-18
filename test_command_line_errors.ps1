<#
.SYNOPSIS
Runs each command line program with bad arguments to show its parse-error output.

.DESCRIPTION
Runs the Debug builds of Neo4JGenerator, VikingAU and MonogameTestbed (all net9.0) from the
repository root. A program whose exe is missing is reported and skipped. MeasureDistance is not
run: it does not currently build (see CommandLineErrorHandling.md). Each program should print the
parse errors, then the usage text, and exit with code 1.

.EXAMPLE
.\test_command_line_errors.ps1
#>
$root = $PSScriptRoot

$tests = @(
    @{ Name = 'Neo4JGenerator (missing required --Neo4JDatabase)'; Exe = 'Servers\Neo4JGenerator\bin\Debug\net9.0\Neo4JGenerator.exe'; Args = @() },
    @{ Name = 'Neo4JGenerator (unknown option)';                    Exe = 'Servers\Neo4JGenerator\bin\Debug\net9.0\Neo4JGenerator.exe'; Args = @('--invalid-option') },
    @{ Name = 'VikingAU (unknown option)';                          Exe = 'Clients\VikingAU\bin\Debug\net9.0\VikingAU.exe';             Args = @('--invalid-option') },
    @{ Name = 'MonogameTestbed (unknown option)';                   Exe = 'Clients\MonogameTestbed\bin\Debug\net9.0-windows\MonogameTestbed.exe'; Args = @('--invalid-option') }
)

Write-Host "Testing Command Line Error Handling" -ForegroundColor Green
foreach ($t in $tests) {
    Write-Host ""
    Write-Host "Testing $($t.Name):" -ForegroundColor Yellow
    $exe = Join-Path $root $t.Exe
    if (-not (Test-Path $exe)) {
        Write-Host "Not built, skipping: $exe" -ForegroundColor Red
        continue
    }
    # Start-Process -Wait because MonogameTestbed is a WinExe that allocates its own console.
    $spArgs = @{ FilePath = $exe; Wait = $true; PassThru = $true; NoNewWindow = $true }
    if ($t.Args.Count -gt 0) { $spArgs.ArgumentList = $t.Args }
    $p = Start-Process @spArgs
    Write-Host "Exit code: $($p.ExitCode) (expected 1)"
}

Write-Host ""
Write-Host "All tests completed." -ForegroundColor Green
