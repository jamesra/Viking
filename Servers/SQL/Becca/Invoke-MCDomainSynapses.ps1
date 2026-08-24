#Requires -Version 5.1
<#
.SYNOPSIS
    Run SelectMCDomainSynapses once per glial ID, writing each result set to
    {OutputDir}\{GlialID}_{ResultSet}.csv.

.EXAMPLE
    # Windows auth (default)
    .\Invoke-MCDomainSynapses.ps1 -GlialIDs 8887,9025

.EXAMPLE
    # SQL login, password prompt
    .\Invoke-MCDomainSynapses.ps1 -GlialIDs 8887,9025 -UserName sa

.EXAMPLE
    # SQL login from a credential object
    $cred = Get-Credential -Message "SQL login for RC1"
    .\Invoke-MCDomainSynapses.ps1 -GlialIDs 8887,9025 -Credential $cred

.EXAMPLE
    # First run, or after changing SelectMCDomainSynapses.sql
    .\Invoke-MCDomainSynapses.ps1 -GlialIDs 8887,9025 -Deploy

.EXAMPLE
    # Override the default database (RC1)
    .\Invoke-MCDomainSynapses.ps1 -GlialIDs 8887,9025 -Database Test
#>
[CmdletBinding(DefaultParameterSetName = "WindowsAuth")]
param(
    [Parameter(Mandatory = $true)]
    [long[]] $GlialIDs,

    [string] $Server = "192.168.0.110",
    [string] $Database = "RC1",
    [int] $ConnectTimeout = 3,
    [string] $OutputDir = "",
    [double] $SearchDistance = 500,
    [long[]] $SynapseTypeIDs = @(73, 34, 28),
    [string] $ProcSqlPath = "",
    [switch] $Deploy,

    [Parameter(ParameterSetName = "SqlAuthUser")]
    [string] $UserName,

    [Parameter(ParameterSetName = "SqlAuthCredential")]
    [System.Management.Automation.PSCredential] $Credential,

    [Parameter(ParameterSetName = "ConnectionString")]
    [string] $ConnectionString
)

$ErrorActionPreference = "Stop"

if (-not $OutputDir) {
    $OutputDir = Join-Path $PSScriptRoot "output"
}
if (-not $ProcSqlPath) {
    $ProcSqlPath = Join-Path $PSScriptRoot "SelectMCDomainSynapses.sql"
}

$ResultSetNames = @("Candidates", "SynapseCount", "ClosestPairs", "ShapeMap")

function Get-SqlConnection {
    if ($ConnectionString) {
        $conn = New-Object System.Data.SqlClient.SqlConnection $ConnectionString
        $conn.Open()
        return $conn
    }

    $sqlCredential = $null
    if ($PSCmdlet.ParameterSetName -eq "SqlAuthCredential") {
        $sqlCredential = $Credential
    }
    elseif ($PSCmdlet.ParameterSetName -eq "SqlAuthUser") {
        $sqlCredential = Get-Credential -UserName $UserName -Message "SQL password for $Server.$Database"
    }

    if ($sqlCredential) {
        $cs = "Server=$Server;Database=$Database;Integrated Security=False;TrustServerCertificate=True;Connection Timeout=$ConnectTimeout"
        $conn = New-Object System.Data.SqlClient.SqlConnection $cs
        $password = $sqlCredential.Password.Copy()
        $password.MakeReadOnly()
        $conn.Credential = New-Object System.Data.SqlClient.SqlCredential(
            $sqlCredential.UserName,
            $password
        )
        Write-Host "Connecting to $Server.$Database (timeout ${ConnectTimeout}s) ..."
        $conn.Open()
        Write-Host "Connected."
        return $conn
    }

    $cs = "Server=$Server;Database=$Database;Integrated Security=True;TrustServerCertificate=True;Connection Timeout=$ConnectTimeout"
    $conn = New-Object System.Data.SqlClient.SqlConnection $cs
    Write-Host "Connecting to $Server.$Database (timeout ${ConnectTimeout}s) ..."
    $conn.Open()
    Write-Host "Connected."
    return $conn
}

function Test-ProcedureExists {
    param([System.Data.SqlClient.SqlConnection] $Connection)

    $cmd = $Connection.CreateCommand()
    $cmd.CommandText = "SELECT OBJECT_ID(N'dbo.SelectMCDomainSynapses', N'P')"
    $id = $cmd.ExecuteScalar()
    return -not ($null -eq $id -or $id -is [DBNull])
}

function Convert-SqlCell {
    param($Value)
    if ($null -eq $Value -or $Value -is [DBNull]) { return "" }
    if ($Value -is [byte[]]) { return "" }
    $text = $Value.ToString()
    if ($text.StartsWith("Microsoft.SqlServer.Types")) { return "" }
    return $text
}

function Write-ResultCsv {
    param(
        [System.Data.SqlClient.SqlDataReader] $Reader,
        [string] $Path
    )

    $spatialTypes = @("geometry", "geography", "hierarchyid")
    $ordinals = @()
    $names = @()
    for ($i = 0; $i -lt $Reader.FieldCount; $i++) {
        $typeName = $Reader.GetDataTypeName($i)
        if ($spatialTypes -contains $typeName) { continue }
        $ordinals += $i
        $names += $Reader.GetName($i)
    }
    $builder = New-Object System.Text.StringBuilder
    [void]$builder.AppendLine(($names | ForEach-Object { '"' + ($_ -replace '"', '""') + '"' }) -join ",")

    while ($Reader.Read()) {
        $cells = foreach ($i in $ordinals) {
            try {
                $text = Convert-SqlCell $Reader.GetValue($i)
            }
            catch {
                $text = ""
            }
            '"' + ($text -replace '"', '""') + '"'
        }
        [void]$builder.AppendLine(($cells -join ","))
    }

    $dir = Split-Path -Parent $Path
    if (-not (Test-Path $dir)) {
        New-Item -ItemType Directory -Path $dir | Out-Null
    }
    [System.IO.File]::WriteAllText($Path, $builder.ToString(), [System.Text.UTF8Encoding]::new($false))
}

function Invoke-SqlBatches {
    param(
        [System.Data.SqlClient.SqlConnection] $Connection,
        [string] $Sql
    )

    $batches = [regex]::Split($Sql, '(?im)^\s*GO\s*$')
    foreach ($batch in $batches) {
        if ([string]::IsNullOrWhiteSpace($batch)) { continue }
        $cmd = $Connection.CreateCommand()
        $cmd.CommandText = $batch
        $cmd.CommandTimeout = 0
        [void]$cmd.ExecuteNonQuery()
    }
}

New-Item -ItemType Directory -Path $OutputDir -Force | Out-Null

$conn = Get-SqlConnection
try {
    $procExists = Test-ProcedureExists -Connection $conn

    if ($Deploy) {
        if (-not (Test-Path $ProcSqlPath)) {
            throw "Procedure script not found: $ProcSqlPath"
        }
        Write-Host "Deploying $ProcSqlPath to $Server.$Database ..."
        Invoke-SqlBatches -Connection $conn -Sql (Get-Content -Raw $ProcSqlPath)
    }
    elseif (-not $procExists) {
        throw "dbo.SelectMCDomainSynapses is not on $Server.$Database. Re-run with -Deploy to create it from SelectMCDomainSynapses.sql."
    }

    $typeTable = New-Object System.Data.DataTable
    [void]$typeTable.Columns.Add("ID", [long])
    foreach ($typeId in $SynapseTypeIDs) {
        [void]$typeTable.Rows.Add($typeId)
    }

    foreach ($glialId in $GlialIDs) {
        Write-Host "Running SelectMCDomainSynapses for glial $glialId ..."
        $cmd = $conn.CreateCommand()
        $cmd.CommandText = "dbo.SelectMCDomainSynapses"
        $cmd.CommandType = [System.Data.CommandType]::StoredProcedure
        $cmd.CommandTimeout = 0
        $cmd.Parameters.Add("@GlialID", [System.Data.SqlDbType]::Int).Value = [int]$glialId
        $cmd.Parameters.Add("@SearchDistance", [System.Data.SqlDbType]::Float).Value = $SearchDistance
        $tvp = $cmd.Parameters.Add("@SynapseTypeIDs", [System.Data.SqlDbType]::Structured)
        $tvp.TypeName = "dbo.integer_list"
        $tvp.Value = $typeTable

        $reader = $cmd.ExecuteReader()
        try {
            for ($set = 0; $set -lt $ResultSetNames.Count; $set++) {
                $outFile = Join-Path $OutputDir ("{0}_{1}.csv" -f $glialId, $ResultSetNames[$set])
                Write-ResultCsv -Reader $reader -Path $outFile
                Write-Host "  wrote $outFile"
                if ($set -lt $ResultSetNames.Count - 1 -and -not $reader.NextResult()) {
                    throw "Procedure returned fewer than 4 result sets for glial $glialId"
                }
            }
        }
        finally {
            $reader.Close()
        }
    }
}
finally {
    $conn.Close()
}

Write-Host "Done. Files are in $OutputDir"
