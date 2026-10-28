<#
.SYNOPSIS
    Pester tests for Deploy-AnnotationGrpc.ps1: its helpers, and the start step with docker faked.

.DESCRIPTION
    Loads the helper function definitions from the script through the PowerShell AST, so the
    helper tests never run the script's main body. The port helper is given its in-use list as an
    argument; the TLS probe is tested only against loopback listeners these tests open.

    The script-level settings the helpers read ($projectPrefix, $firstPort, $reservedPorts) are
    re-read from the script's own assignments, so a changed setting is tested, not a copy of it.

    The whole-script tests replace docker with a function named docker that records its
    arguments, so no real docker command runs: -WhatIf must skip `up`, a real run must call
    `up -d --build grpc-annotation-service`, and a running project must keep its port.

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

$helperNames = 'Get-ProjectName', 'Find-FreePortPair', 'Get-UnfilledKeys', 'Get-ComposeVariables',
               'Invoke-ComposeService', 'Get-PublishedHttpPort', 'Test-TlsHandshake', 'Wait-ServiceReady',
               'Start-ComposeProject'
$helperSource = $ast.FindAll({
        param($node)
        $node -is [System.Management.Automation.Language.FunctionDefinitionAst] -and $helperNames -contains $node.Name
    }, $true) | ForEach-Object { $_.Extent.Text }

# Top-level assignments of the settings the helpers read, evaluated without running anything else.
$settingNames = 'projectPrefix', 'firstPort', 'reservedPorts', 'serviceName'
$settingSource = $ast.EndBlock.Statements |
    Where-Object {
        $_ -is [System.Management.Automation.Language.AssignmentStatementAst] -and
        $_.Left -is [System.Management.Automation.Language.VariableExpressionAst] -and
        $settingNames -contains $_.Left.VariablePath.UserPath
    } | ForEach-Object { $_.Extent.Text }

Describe 'Deploy-AnnotationGrpc helper loading' {
    It 'finds all nine helpers and all four settings in the script' {
        @($helperSource).Count | Should Be 9
        @($settingSource).Count | Should Be 4
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

    Context 'Get-PublishedHttpPort' {
        It 'returns the host port mapped to 80/tcp' {
            Get-PublishedHttpPort '0.0.0.0:5011->443/tcp, [::]:5011->443/tcp, 0.0.0.0:5010->80/tcp, [::]:5010->80/tcp' | Should Be 5010
        }
        It 'returns $null when 80/tcp is not published' {
            Get-PublishedHttpPort '0.0.0.0:5011->443/tcp' | Should Be $null
            Get-PublishedHttpPort '' | Should Be $null
        }
        It 'does not take a container port that only ends in 80' {
            Get-PublishedHttpPort '0.0.0.0:6080->8080/tcp, 0.0.0.0:6180->180/tcp' | Should Be $null
        }
    }

    Context 'Wait-ServiceReady' {
        Mock Start-Sleep {}

        It 'returns $true without sleeping when the first probe answers' {
            Mock Test-TlsHandshake { $true }
            Wait-ServiceReady 5011 10 2 | Should Be $true
            Assert-MockCalled Test-TlsHandshake -Exactly 1 -Scope It -ParameterFilter { $Port -eq 5011 -and $TimeoutMilliseconds -eq 3000 }
            Assert-MockCalled Start-Sleep -Exactly 0 -Scope It
        }
        It 'keeps probing until the port answers' {
            $state = @{ n = 0 }
            Mock Test-TlsHandshake { $state.n++; $state.n -ge 3 }
            Wait-ServiceReady 5011 10 2 | Should Be $true
            Assert-MockCalled Test-TlsHandshake -Exactly 3 -Scope It
            Assert-MockCalled Start-Sleep -Exactly 2 -Scope It -ParameterFilter { $Seconds -eq 2 }
        }
        It 'gives up after ceiling(timeout / interval) probes' {
            Mock Test-TlsHandshake { $false }
            Wait-ServiceReady 5011 10 3 | Should Be $false
            Assert-MockCalled Test-TlsHandshake -Exactly 4 -Scope It
            Assert-MockCalled Start-Sleep -Exactly 3 -Scope It
        }
        It 'probes at least once when the timeout is shorter than the interval' {
            Mock Test-TlsHandshake { $false }
            Wait-ServiceReady 5011 0 2 | Should Be $false
            Assert-MockCalled Test-TlsHandshake -Exactly 1 -Scope It
        }
    }

    # Pester 3.4 keeps a Mock made inside an It for the rest of its Context, so these tests, which
    # sleep for real, have their own Context and run in this order.
    Context 'Wait-ServiceReady deadline (real clock)' {
        Mock Start-Sleep {}

        It 'ends at the deadline when every probe runs to its own timeout' {
            # A probe against a port that accepts TCP but never handshakes takes its whole timeout
            # (3000 ms when none is passed). Counting attempts alone, this wait took 2 x 3 s = 6 s.
            Mock Test-TlsHandshake {
                $ms = if ($TimeoutMilliseconds -gt 0) { $TimeoutMilliseconds } else { 3000 }
                [System.Threading.Thread]::Sleep($ms)
                $false
            }
            $clock = [System.Diagnostics.Stopwatch]::StartNew()
            Wait-ServiceReady 5011 2 1 | Should Be $false
            $clock.Elapsed.TotalSeconds | Should BeLessThan 2.5
            Assert-MockCalled Test-TlsHandshake -Exactly 1 -Scope It -ParameterFilter { $TimeoutMilliseconds -le 2000 }
            Assert-MockCalled Start-Sleep -Exactly 0 -Scope It
        }
        It 'does not sleep when the next probe would start after the deadline' {
            Mock Test-TlsHandshake { [System.Threading.Thread]::Sleep(1500); $false }
            Wait-ServiceReady 5011 2 1 | Should Be $false
            Assert-MockCalled Test-TlsHandshake -Exactly 1 -Scope It
            Assert-MockCalled Start-Sleep -Exactly 0 -Scope It
        }
        It 'gives the last probe only the time left before the deadline' {
            Mock Start-Sleep { [System.Threading.Thread]::Sleep(1200) }
            Mock Test-TlsHandshake { $false }
            Wait-ServiceReady 5011 2 1 | Should Be $false
            Assert-MockCalled Test-TlsHandshake -Exactly 2 -Scope It
            Assert-MockCalled Test-TlsHandshake -Exactly 1 -Scope It -ParameterFilter { $TimeoutMilliseconds -ge 1 -and $TimeoutMilliseconds -le 800 }
        }
        It 'never passes a timeout below 1 ms when a sleep runs past the deadline' {
            # A negative timeout would make the connect wait forever (-1) or throw.
            Mock Start-Sleep { [System.Threading.Thread]::Sleep(2500) }
            Mock Test-TlsHandshake { $false }
            Wait-ServiceReady 5011 2 1 | Should Be $false
            Assert-MockCalled Test-TlsHandshake -Exactly 1 -Scope It -ParameterFilter { $TimeoutMilliseconds -eq 1 }
        }
    }

    Context 'Test-TlsHandshake' {
        It 'returns $false for a port that accepts TCP but never answers TLS (as Docker does before the service listens)' {
            $listener = [System.Net.Sockets.TcpListener]::new([System.Net.IPAddress]::Loopback, 0)
            $listener.Start()
            try {
                Test-TlsHandshake $listener.LocalEndpoint.Port 500 | Should Be $false
                # The probe connected to this port (the connection waits in the backlog).
                $listener.Pending() | Should Be $true
            }
            finally { $listener.Stop() }
        }
        It 'returns $false for a closed port' {
            $listener = [System.Net.Sockets.TcpListener]::new([System.Net.IPAddress]::Loopback, 0)
            $listener.Start()
            $port = $listener.LocalEndpoint.Port
            $listener.Stop()
            Test-TlsHandshake $port 500 | Should Be $false
        }
        It 'returns $true once the port completes a TLS handshake, whatever name the certificate carries' {
            # A loopback TLS server with a throwaway self-signed certificate whose name is not
            # "localhost" (the probe must accept any certificate). It runs on its own runspace so the
            # server and client sides of the handshake run at the same time. The certificate is
            # re-imported from PFX because Schannel on .NET Framework cannot use an ephemeral key.
            $rsa = [System.Security.Cryptography.RSA]::Create(2048)
            $request = [System.Security.Cryptography.X509Certificates.CertificateRequest]::new('CN=pester', $rsa,
                [System.Security.Cryptography.HashAlgorithmName]::SHA256, [System.Security.Cryptography.RSASignaturePadding]::Pkcs1)
            $ephemeral = $request.CreateSelfSigned([DateTimeOffset]::UtcNow.AddDays(-1), [DateTimeOffset]::UtcNow.AddDays(1))
            $cert = [System.Security.Cryptography.X509Certificates.X509Certificate2]::new(
                $ephemeral.Export([System.Security.Cryptography.X509Certificates.X509ContentType]::Pfx, 'pester'), 'pester',
                [System.Security.Cryptography.X509Certificates.X509KeyStorageFlags]::Exportable)
            $listener = [System.Net.Sockets.TcpListener]::new([System.Net.IPAddress]::Loopback, 0)
            $listener.Start()
            $server = [powershell]::Create().AddScript({
                param($listener, $cert)
                $client = $listener.AcceptTcpClient()
                try {
                    $ssl = [System.Net.Security.SslStream]::new($client.GetStream(), $false)
                    $ssl.AuthenticateAsServer($cert)
                    $ssl.Dispose()
                }
                finally { $client.Dispose() }
            }).AddArgument($listener).AddArgument($cert)
            $handle = $server.BeginInvoke()
            try {
                Test-TlsHandshake $listener.LocalEndpoint.Port 5000 | Should Be $true
                # Rethrows a server-side handshake failure.
                $server.EndInvoke($handle)
            }
            finally {
                # Stopping the listener first unblocks AcceptTcpClient if the probe never connected.
                $listener.Stop(); $server.Dispose(); $cert.Dispose(); $ephemeral.Dispose(); $rsa.Dispose()
            }
        }
    }

    Context 'Invoke-ComposeService' {
        # Join-Path needs an existing drive, not an existing folder.
        $repoRoot = 'C:\repo'
        $calls = New-Object System.Collections.ArrayList
        $fake = @{ exit = 0 }
        function docker { [void]$calls.Add(@{ args = @($args); port = $env:GRPC_ANNOTATION_HTTP_PORT }); $global:LASTEXITCODE = $fake.exit }

        It 'runs docker compose for the project and stack file with the arguments in order' {
            $calls.Clear(); $fake.exit = 0
            Invoke-ComposeService 'viking-annotation-rc3' @{} @('up', '-d', '--build', 'grpc-annotation-service')
            $calls.Count | Should Be 1
            $calls[0].args -join ' ' | Should BeExactly 'compose --project-name viking-annotation-rc3 -f C:\repo\Servers\GrpcAnnotationService\docker-compose.yml up -d --build grpc-annotation-service'
        }
        It 'sets the variables only for the call and restores the previous values' {
            $calls.Clear(); $fake.exit = 0
            $old = $env:GRPC_ANNOTATION_HTTP_PORT
            $env:GRPC_ANNOTATION_HTTP_PORT = 'before'
            try {
                Invoke-ComposeService 'p' @{ GRPC_ANNOTATION_HTTP_PORT = '6010' } @('config', '--quiet')
                $calls[0].port | Should BeExactly '6010'
                $env:GRPC_ANNOTATION_HTTP_PORT | Should BeExactly 'before'
            }
            finally { $env:GRPC_ANNOTATION_HTTP_PORT = $old }
        }
        It 'throws naming the subcommand on a non-zero exit, and still restores the variables' {
            $calls.Clear(); $fake.exit = 3
            $old = $env:GRPC_ANNOTATION_HTTP_PORT
            $env:GRPC_ANNOTATION_HTTP_PORT = 'before'
            try {
                { Invoke-ComposeService 'p' @{ GRPC_ANNOTATION_HTTP_PORT = '6010' } @('up', '-d') } |
                    Should Throw 'docker compose up failed for p (exit 3).'
                $env:GRPC_ANNOTATION_HTTP_PORT | Should BeExactly 'before'
            }
            finally { $env:GRPC_ANNOTATION_HTTP_PORT = $old; $global:LASTEXITCODE = 0 }
        }
    }

    Context 'Start-ComposeProject' {
        $order = New-Object System.Collections.ArrayList
        Mock Invoke-ComposeService { [void]$order.Add('compose ' + ($Arguments -join ' ')) }

        It 'runs up -d --build, then waits on the HTTPS port (HTTP port + 1) and prints that endpoint' {
            $order.Clear()
            Mock Wait-ServiceReady { [void]$order.Add('wait'); $true }
            $out = Start-ComposeProject 'viking-annotation-rc3' @{ A = '1' } 5010 30 6>&1 | Out-String
            @($order) -join ';' | Should BeExactly 'compose up -d --build grpc-annotation-service;wait'
            Assert-MockCalled Invoke-ComposeService -Exactly 1 -Scope It -ParameterFilter { $ProjectName -eq 'viking-annotation-rc3' -and $Variables['A'] -eq '1' }
            Assert-MockCalled Wait-ServiceReady -Exactly 1 -Scope It -ParameterFilter { $Port -eq 5011 -and $TimeoutSeconds -eq 30 }
            $out | Should Match ([regex]::Escape('Viking endpoint: https://localhost:5011/'))
        }
        It 'throws with the endpoint and a logs hint when the port does not answer in time' {
            Mock Wait-ServiceReady { $false }
            { Start-ComposeProject 'viking-annotation-rc3' @{} 5010 30 6>$null } |
                Should Throw 'viking-annotation-rc3 did not answer on https://localhost:5011/ within 30 s. Check: docker compose --project-name viking-annotation-rc3 logs grpc-annotation-service'
        }
        It 'does not wait when up fails' {
            Mock Invoke-ComposeService { throw 'docker compose up failed' }
            Mock Wait-ServiceReady { $true }
            { Start-ComposeProject 'p' @{} 5010 30 6>$null } | Should Throw 'docker compose up failed'
            Assert-MockCalled Wait-ServiceReady -Exactly 0 -Scope It
        }
    }
}

Describe 'Deploy-AnnotationGrpc start step (docker faked)' {
    $calls = New-Object System.Collections.ArrayList
    $fake = @{ ps = '' }
    # Shadows docker.exe for the script: records every call and never runs a real docker command.
    # `up` always fails, so a run that reaches it stops before waiting on a port.
    function docker {
        $a = @($args)
        [void]$calls.Add(@{ args = $a; port = $env:GRPC_ANNOTATION_HTTP_PORT })
        $global:LASTEXITCODE = 0
        if ($a[0] -eq 'ps') { return $fake.ps }
        if ($a -contains 'up') { $global:LASTEXITCODE = 1 }
    }
    $envPath = Join-Path $TestDrive 'Pester.env'
    Set-Content -LiteralPath $envPath -Value 'ConnectionStrings__AnnotationConnection=Server=s;Database=Pester;Password=x'
    $composeCalls = { @($calls | Where-Object { $_.args[0] -eq 'compose' } | ForEach-Object { $_.args[5..($_.args.Count - 1)] -join ' ' }) }

    It '-WhatIf checks the configuration and skips up' {
        $calls.Clear(); $fake.ps = ''
        & $scriptPath -Database Pester -EnvFile $envPath -WhatIf 6>$null
        $c = @(& $composeCalls)
        $c.Count | Should Be 1
        $c[0] | Should BeExactly 'config --quiet grpc-annotation-service'
    }
    It 'a real run starts grpc-annotation-service with up -d --build, after the config check' {
        $calls.Clear(); $fake.ps = ''
        # Guard: never reach a real docker up.
        (Get-Command docker).CommandType | Should Be 'Function'
        { & $scriptPath -Database Pester -EnvFile $envPath 6>$null } | Should Throw 'docker compose up failed for viking-annotation-pester (exit 1).'
        $c = @(& $composeCalls)
        $c.Count | Should Be 2
        $c[0] | Should BeExactly 'config --quiet grpc-annotation-service'
        $c[1] | Should BeExactly 'up -d --build grpc-annotation-service'
        $global:LASTEXITCODE = 0
    }
    It 'a running project keeps the port pair it publishes' {
        $calls.Clear(); $fake.ps = '0.0.0.0:6010->80/tcp, 0.0.0.0:6011->443/tcp'
        & $scriptPath -Database Pester -EnvFile $envPath -WhatIf 6>$null
        $ps = @($calls | Where-Object { $_.args[0] -eq 'ps' })
        $ps.Count | Should Be 1
        ($ps[0].args -join ' ') | Should Match 'label=com\.docker\.compose\.project=viking-annotation-pester'
        @($calls | Where-Object { $_.args[0] -eq 'compose' })[0].port | Should BeExactly '6010'
    }
    It '-Port may name the pair the running project already listens on' {
        # Two loopback listeners on a consecutive pair stand in for the running project's ports.
        $listeners = $null
        for ($p = 42010; $p -lt 42100 -and -not $listeners; $p += 2) {
            try {
                $a = [System.Net.Sockets.TcpListener]::new([System.Net.IPAddress]::Loopback, $p); $a.Start()
                try { $b = [System.Net.Sockets.TcpListener]::new([System.Net.IPAddress]::Loopback, $p + 1); $b.Start(); $listeners = @($a, $b) }
                catch { $a.Stop() }
            }
            catch { }
        }
        $port = $listeners[0].LocalEndpoint.Port
        try {
            $calls.Clear(); $fake.ps = "0.0.0.0:$port->80/tcp"
            & $scriptPath -Database Pester -EnvFile $envPath -Port $port -WhatIf 6>$null
            @($calls | Where-Object { $_.args[0] -eq 'compose' })[0].port | Should BeExactly ([string]$port)
            $fake.ps = ''
            { & $scriptPath -Database Pester -EnvFile $envPath -Port $port -WhatIf 6>$null } | Should Throw 'already in use'
        }
        finally { $listeners | ForEach-Object { $_.Stop() } }
    }
}
