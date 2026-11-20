<#
.SYNOPSIS
    Drops and recreates AnnotationTest in the local annotation-sql container from the deploy script, with the seed
    rows the gRPC integration tests assume.

.DESCRIPTION
    Start-AnnotationTestStack.ps1 runs this before every test run, so each run starts from the same data and from the
    schema deployed databases have. Steps:
      1. Drop AnnotationTest.
      2. Build it with Servers/SQL/DatabaseCreateUpdate/CreateUpdateDatabase.sql, the script that creates and upgrades
         every real annotation database. The template database name and Windows data folder are replaced with
         AnnotationTest and the container's /var/opt/mssql/data/. The script's two CREATE LOGIN statements fail the
         container's password policy; those logins are not used here.
      3. Check that DBVersion reached the highest step in the script.
      4. Insert StructureType 1, Structure 1 and Location 1 (test-seed.sql).
    It works only through `docker exec` into the container, so it cannot reach any other SQL Server, and it drops only
    AnnotationTest.

.PARAMETER ContainerName
    The local SQL Server container (profile local-sql of Servers/GrpcAnnotationService/docker-compose.yml).

.PARAMETER EnvFile
    Env file with MSSQL_SA_PASSWORD.

.EXAMPLE
    .\Servers\AnnotationDatabase\scripts\Reset-AnnotationTestDatabase.ps1
#>
[CmdletBinding()]
param(
    [string] $ContainerName = "viking-annotation-sql",
    [string] $EnvFile = "D:\Docker\Builds\AnnotationSql\.env"
)

$ErrorActionPreference = "Stop"

$repoRoot = (Resolve-Path (Join-Path $PSScriptRoot "..\..\..")).Path
$createUpdate = Join-Path $repoRoot "Servers\SQL\DatabaseCreateUpdate\CreateUpdateDatabase.sql"
$seed = Join-Path $PSScriptRoot "test-seed.sql"

if (-not (Test-Path $EnvFile)) {
    throw "Missing env file: $EnvFile. Copy Servers/AnnotationDatabase/config-template/build/env-template.txt there first."
}
$saPassword = $null
Get-Content $EnvFile | ForEach-Object {
    if ($_ -match '^\s*MSSQL_SA_PASSWORD=(.+)\s*$') {
        $saPassword = $Matches[1].Trim().Trim('"').Trim("'")
    }
}
if ([string]::IsNullOrWhiteSpace($saPassword)) {
    throw "MSSQL_SA_PASSWORD not found in $EnvFile"
}

$sqlcmd = @("exec", "-i", $ContainerName, "/opt/mssql-tools18/bin/sqlcmd", "-S", "localhost", "-U", "sa", "-P", $saPassword, "-C")

# ROLLBACK IMMEDIATE ends connections a previous run left open, which would otherwise block the drop.
$drop = @"
IF DB_ID(N'AnnotationTest') IS NOT NULL
BEGIN
    ALTER DATABASE [AnnotationTest] SET SINGLE_USER WITH ROLLBACK IMMEDIATE;
    DROP DATABASE [AnnotationTest];
END
"@
Write-Host "Dropping AnnotationTest in '$ContainerName' ..."
docker @sqlcmd -b -Q $drop
if ($LASTEXITCODE -ne 0) {
    throw "Could not drop AnnotationTest in '$ContainerName' (sqlcmd exit $LASTEXITCODE). Is the container running and healthy?"
}

Write-Host "Building AnnotationTest from $createUpdate ..."
$script = (Get-Content -Raw $createUpdate).Replace("John", "AnnotationTest").Replace('C:\Databases\', "/var/opt/mssql/data/")
# No -b: the script runs on past the CREATE LOGIN failures; the DBVersion check below decides whether it worked.
$output = $script | docker @sqlcmd -I 2>&1
$lastStep = ([regex]::Matches((Get-Content -Raw $createUpdate), 'DBVersionID\s*=\s*(\d+)') | ForEach-Object { [int]$_.Groups[1].Value } | Measure-Object -Maximum).Maximum
$reached = (docker @sqlcmd -h -1 -W -Q "SET NOCOUNT ON; SELECT MAX(DBVersionID) FROM AnnotationTest.dbo.DBVersion" | Select-Object -First 1)
if ("$reached".Trim() -ne "$lastStep") {
    $output | Select-String -Pattern '^Msg \d+, Level 1[1-9]' -Context 0, 1 | Select-Object -First 20 | Out-Host
    throw "CreateUpdateDatabase.sql stopped at DBVersion '$reached', not $lastStep. The first errors are above."
}
Write-Host "AnnotationTest is at DBVersion $reached."

Write-Host "Seeding StructureType 1, Structure 1, Location 1 ..."
Get-Content -Raw $seed | docker @sqlcmd -I -b -d AnnotationTest
if ($LASTEXITCODE -ne 0) {
    throw "test-seed.sql failed (sqlcmd exit $LASTEXITCODE)."
}
Write-Host "AnnotationTest is reset."
