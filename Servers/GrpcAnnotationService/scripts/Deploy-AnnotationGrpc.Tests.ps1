<#
.SYNOPSIS
    Pester tests for the pure helpers of Deploy-AnnotationGrpc.ps1.

.DESCRIPTION
    Loads only the function definitions Get-ProjectName, Find-FreePortPair, Get-UnfilledKeys and
    Get-ComposeVariables from the script through the PowerShell AST, so the script's main body
    (parameter binding, docker, env files) never runs. No docker, no network, no sockets: the
    port helper is given its in-use list as an argument.

    The script-level settings the helpers read ($projectPrefix, $firstPort, $reservedPorts) are
    re-read from the script's own assignments, so a changed setting is tested, not a copy of it.

    Written for Pester 3.4 (the version shipped with Windows PowerShell); the same syntax runs on
    PowerShell 7 with that module.

.EXAMPLE
    Invoke-Pester .\Servers\GrpcAnnotationService\scripts\Deploy-AnnotationGrpc.Tests.ps1
#>

# $env:DEPLOY_SCRIPT_UNDER_TEST lets the mutation check point the tests at a scratch copy.
$scriptPath = if ($env:DEPLOY_SCRIPT_UNDER_TEST) { $env:DEPLOY_SCRIPT_UNDER_TEST }
              else { Join-Path $PSScriptRoot 'Deploy-AnnotationGrpc.ps1' }

$tokens = $null
$errors = $null
$ast = [System.Management.Automation.Language.Parser]::ParseFile($scriptPath, [ref]$tokens, [ref]$errors)
if ($errors.Count -gt 0) { throw "Cannot parse ${scriptPath}: $($errors[0].Message)" }

$helperNames = 'Get-ProjectName', 'Find-FreePortPair', 'Get-UnfilledKeys', 'Get-ComposeVariables'
$helperSource = $ast.FindAll({
        param($node)
        $node -is [System.Management.Automation.Language.FunctionDefinitionAst] -and $helperNames -contains $node.Name
    }, $true) | ForEach-Object { $_.Extent.Text }

# Top-level assignments of the settings the helpers read, evaluated without running anything else.
$settingNames = 'projectPrefix', 'firstPort', 'reservedPorts'
$settingSource = $ast.EndBlock.Statements |
    Where-Object {
        $_ -is [System.Management.Automation.Language.AssignmentStatementAst] -and
        $_.Left -is [System.Management.Automation.Language.VariableExpressionAst] -and
        $settingNames -contains $_.Left.VariablePath.UserPath
    } | ForEach-Object { $_.Extent.Text }

Describe 'Deploy-AnnotationGrpc helper loading' {
    It 'finds all four helpers and all three settings in the script' {
        @($helperSource).Count | Should Be 4
        @($settingSource).Count | Should Be 3
    }
}

Describe 'Deploy-AnnotationGrpc helpers' {
    . ([scriptblock]::Create(($settingSource -join "`n")))
    . ([scriptblock]::Create(($helperSource -join "`n")))

    Context 'Get-ProjectName' {
        It 'prefixes and lowercases the database name' {
            Get-ProjectName 'RC3' | Should BeExactly 'viking-annotation-rc3'
        }
        It 'keeps underscores and hyphens' {
            Get-ProjectName 'My_Db-2' | Should BeExactly 'viking-annotation-my_db-2'
        }
        It 'lowercases invariantly (Turkish dotted I)' {
            $old = [System.Threading.Thread]::CurrentThread.CurrentCulture
            try {
                [System.Threading.Thread]::CurrentThread.CurrentCulture = [System.Globalization.CultureInfo]::GetCultureInfo('tr-TR')
                Get-ProjectName 'DBI' | Should BeExactly 'viking-annotation-dbi'
            }
            finally { [System.Threading.Thread]::CurrentThread.CurrentCulture = $old }
        }
    }

    Context 'Find-FreePortPair' {
        It 'starts at the first port when nothing is in use' {
            Find-FreePortPair @() | Should Be 5010
        }
        It 'skips a pair whose HTTP port is in use' {
            Find-FreePortPair @(5010) | Should Be 5012
        }
        It 'skips a pair whose HTTPS port (port + 1) is in use' {
            Find-FreePortPair @(5011) | Should Be 5012
        }
        It 'moves past consecutive busy pairs' {
            Find-FreePortPair @(5010, 5013, 5014) | Should Be 5016
        }
        It 'skips the reserved Identity DevTest port 5020 as the HTTP port' {
            Find-FreePortPair @(5010..5019) | Should Be 5022
        }
        It 'always returns a pair that is free and not reserved' {
            foreach ($busy in @(@(), @(5010), @(5011, 5013), @(5010..5030), @(5019, 5020, 5021))) {
                $p = Find-FreePortPair $busy
                ($busy -contains $p) | Should Be $false
                ($busy -contains ($p + 1)) | Should Be $false
                ($reservedPorts -contains $p) | Should Be $false
                ($reservedPorts -contains ($p + 1)) | Should Be $false
                ($p -ge $firstPort) | Should Be $true
            }
        }
        It 'throws when every pair is taken' {
            { Find-FreePortPair @($firstPort..65535) } | Should Throw 'No free port pair found.'
        }
    }

    Context 'Get-UnfilledKeys' {
        It 'returns the key names that still hold a REPLACE_WITH_ placeholder' {
            $lines = @('A=REPLACE_WITH_PASSWORD', 'B=real', 'C = REPLACE_WITH_X')
            $keys = @(Get-UnfilledKeys $lines)
            $keys.Count | Should Be 2
            $keys[0] | Should BeExactly 'A'
            $keys[1] | Should BeExactly 'C'
        }
        It 'ignores commented lines' {
            @(Get-UnfilledKeys @('# A=REPLACE_WITH_X', '   #B=REPLACE_WITH_Y')).Count | Should Be 0
        }
        It 'finds a placeholder embedded inside a longer value' {
            $keys = @(Get-UnfilledKeys @('ConnectionStrings__X=Server=s;Password=REPLACE_WITH_PW;'))
            $keys.Count | Should Be 1
            $keys[0] | Should BeExactly 'ConnectionStrings__X'
        }
        It 'splits on the first = only and never returns values' {
            $keys = @(Get-UnfilledKeys @('K=a=b=REPLACE_WITH_Z'))
            $keys[0] | Should BeExactly 'K'
        }
        It 'returns an empty array when nothing is unfilled' {
            @(Get-UnfilledKeys @('A=1', '', 'B=2')).Count | Should Be 0
        }
        It 'does not flag other REPLACE_ text, only the REPLACE_WITH_ marker' {
            @(Get-UnfilledKeys @('A=REPLACE_ME')).Count | Should Be 0
        }
        It 'trims leading whitespace from key names' {
            @(Get-UnfilledKeys @('   A=REPLACE_WITH_X'))[0] | Should BeExactly 'A'
        }
    }

    Context 'Get-ComposeVariables' {
        It 'maps the port pair and env file to the GRPC_ANNOTATION_* variables' {
            $v = Get-ComposeVariables 5010 'D:\Docker\Builds\GrpcAnnotationService\RC3.env'
            $v['GRPC_ANNOTATION_HTTP_PORT'] | Should BeExactly '5010'
            $v['GRPC_ANNOTATION_HTTPS_PORT'] | Should BeExactly '5011'
            $v['GRPC_ANNOTATION_ENV_FILE'] | Should BeExactly 'D:/Docker/Builds/GrpcAnnotationService/RC3.env'
        }
        It 'sets exactly those three variables, in order, as strings' {
            $v = Get-ComposeVariables 5100 'C:\x.env'
            @($v.Keys) -join ',' | Should BeExactly 'GRPC_ANNOTATION_HTTP_PORT,GRPC_ANNOTATION_HTTPS_PORT,GRPC_ANNOTATION_ENV_FILE'
            foreach ($value in $v.Values) { $value.GetType().Name | Should Be 'String' }
        }
        It 'leaves GRPC_ANNOTATION_CORRECTIONS_PATH and ANNOTATION_SQL_* unset' {
            $v = Get-ComposeVariables 5010 'C:\x.env'
            $v.Contains('GRPC_ANNOTATION_CORRECTIONS_PATH') | Should Be $false
            @($v.Keys | Where-Object { $_ -like 'ANNOTATION_SQL_*' }).Count | Should Be 0
        }
    }
}
