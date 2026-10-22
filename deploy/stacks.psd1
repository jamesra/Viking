# Viking stack registry, read by nornir-docker's stack.ps1 (Import-PowerShellDataFile).
#
# Each key is a stack name. stack.ps1 runs a compose stack as
#   docker compose -p <stack> --env-file <root>/Run/_host/host.env --env-file <root>/Run/<stack>/<stack>.run.env
#                  -f <ComposeFiles...> [--profile <each of Profiles>] <action>
# from the repo root named by RepoRootVar, with NORNIR_DOCKER_USER_ROOT, STACK_NAME, STACK_RUN_DIR,
# STACK_SECRETS_DIR, STACK_DATA_DIR, STACK_LOGS_DIR, DOCKER_CERTS_DIR and VIKING_REPO_ROOT_HOST exported.
#
# Fields
#   Engine        compose | iis
#   ComposeFiles  relative to the repo root, forward slashes (compose resolves paths inside a file
#                 relative to that file's folder)
#   Profiles      compose profiles stack.ps1 turns on by default
#   Required      env keys (host.env + run env) that must be non-empty
#   Example       run env template to copy to Run/<stack>/<stack>.run.env
#   Secrets       files expected in STACK_SECRETS_DIR
#   HealthUrl     checked after `up`; {VAR} is replaced from the env. Empty = no HTTP check.
#   Deploy        (iis) pwsh script, relative to the repo root
#   Ports         host ports the stack publishes (firewall / conflict checks)
#   Os            host OSes the stack runs on
#
# Shared keys every stack reads from host.env (deploy/examples/example.viking-host.env):
# VIKING_DOMAIN, VIKING_HOSTNAME_*, TLS_CERT_NAME. Docs: Documentation/source/deployment/.
@{
    Stacks = @{
        'viking-proxy' = @{
            Engine       = 'compose'
            RepoRootVar  = 'VIKING_REPO_ROOT_HOST'
            ComposeFiles = @('Servers/proxy/docker-compose.yml')
            Profiles     = @()
            Required     = @('VIKING_DOMAIN', 'VIKING_COMPUTE_HOST', 'VIKING_IIS_HOST')
            Example      = 'deploy/examples/example.viking-proxy.run.env'
            Secrets      = @()
            HealthUrl    = 'http://127.0.0.1/healthz'
            Ports        = @(80, 443)
            Os           = @('linux', 'windows')
            Description  = 'nginx: SNI passthrough on 443 to every Viking service, ACME HTTP-01 webroot and https redirect on 80'
        }

        'viking-certs' = @{
            Engine       = 'compose'
            RepoRootVar  = 'VIKING_REPO_ROOT_HOST'
            ComposeFiles = @('Servers/certbot/docker-compose.yml')
            Profiles     = @()
            Required     = @('VIKING_DOMAIN', 'TLS_MODE', 'TLS_CERT_NAME')
            Example      = 'deploy/examples/example.viking-certs.run.env'
            Secrets      = @()
            HealthUrl    = ''
            Ports        = @()
            Os           = @('linux', 'windows')
            Description  = 'Single certificate renewer (letsencrypt | acme-eab | manual): publishes DOCKER_CERTS_DIR/<TLS_CERT_NAME>/ and restarts viking.tls-reload containers'
        }

        'viking-identity' = @{
            Engine       = 'compose'
            RepoRootVar  = 'VIKING_REPO_ROOT_HOST'
            ComposeFiles = @('Servers/IdentityServer/docker-compose-all.yml')
            Profiles     = @()
            Required     = @(
                'VIKING_DOMAIN', 'VIKING_HOSTNAME_IDENTITY', 'TLS_CERT_NAME',
                'SQL_SERVER_HOST', 'SQL_SERVER_USER', 'SQL_SERVER_PASSWORD',
                'SQL_SERVER_IDENTITY_DB', 'SQL_SERVER_CONFIG_DB', 'SQL_SERVER_GRANTS_DB',
                'IDENTITY_SERVER_SECRET', 'IDENTITY_SERVER_API_SECRET', 'IDENTITY_SERVER_MVC_SECRET',
                'IDENTITY_SERVER_RO_VIKING_SECRET'
            )
            Example      = 'deploy/examples/example.viking-identity.run.env'
            Secrets      = @('secrets.json', 'Duende_License.key')
            HealthUrl    = 'https://{VIKING_HOSTNAME_IDENTITY}/.well-known/openid-configuration'
            Ports        = @(5001, 6001, 4001)
            Os           = @('linux', 'windows')
            Description  = 'Duende IdentityServer (OIDC issuer, WebApi, Management)'
        }

        'viking-annotation' = @{
            Engine       = 'compose'
            RepoRootVar  = 'VIKING_REPO_ROOT_HOST'
            ComposeFiles = @('Servers/GrpcAnnotationService/docker-compose.yml')
            # 'local-sql' adds a throwaway SQL Server container; production uses external SQL.
            Profiles     = @()
            Required     = @(
                'VIKING_HOSTNAME_ANNOTATION', 'TLS_CERT_NAME',
                'ConnectionStrings__AnnotationConnection',
                'IdentityServer__ClientId', 'IdentityServer__ClientSecret'
            )
            Example      = 'deploy/examples/example.viking-annotation.run.env'
            Secrets      = @()
            HealthUrl    = 'https://{VIKING_HOSTNAME_ANNOTATION}/'
            Ports        = @(5010, 5011)
            Os           = @('linux', 'windows')
            Description  = 'gRPC annotation service (Viking annotation API) with optional local SQL (profile local-sql)'
        }

        'viking-corrections' = @{
            Engine       = 'compose'
            RepoRootVar  = 'VIKING_REPO_ROOT_HOST'
            ComposeFiles = @('Servers/GrpcSectionCorrectionService/docker-compose.yml')
            Profiles     = @()
            Required     = @(
                'VIKING_HOSTNAME_CORRECTIONS', 'TLS_CERT_NAME',
                'IdentityServer__ClientSecret', 'CorrectionService__Name'
            )
            Example      = 'deploy/examples/example.viking-corrections.run.env'
            Secrets      = @()
            HealthUrl    = 'https://{VIKING_HOSTNAME_CORRECTIONS}/'
            Ports        = @(41080, 41443)
            Os           = @('linux', 'windows')
            Description  = 'gRPC section-correction service (builds and serves residual correction maps)'
        }

        'viking-segmentation' = @{
            Engine       = 'compose'
            RepoRootVar  = 'VIKING_REPO_ROOT_HOST'
            ComposeFiles = @('Servers/SegmentationServer/docker-compose.yml')
            # 'models' = segmentation-v3, -v4 and -sem-v1 (also seg-v3 / seg-v4 / seg-sem-v1 one at a time).
            Profiles     = @('models')
            Required     = @('VIKING_HOSTNAME_SEGMENTATION', 'TLS_CERT_NAME')
            Example      = 'deploy/examples/example.viking-segmentation.run.env'
            Secrets      = @()
            # gRPC only (and the optional demo page); no plain HTTP endpoint to probe.
            HealthUrl    = ''
            Ports        = @(40443, 40444, 40445, 40446, 40447)
            Os           = @('linux', 'windows')
            Description  = 'SAM2 segmentation gRPC servers (GPU): default model plus v3 / v4 / sem-v1 profiles'
        }

        'viking-iis' = @{
            Engine       = 'iis'
            RepoRootVar  = 'VIKING_REPO_ROOT_HOST'
            ComposeFiles = @()
            Profiles     = @()
            Required     = @('VIKING_HOSTNAME_WEBSVC', 'VIKING_IIS_DEPLOY_ROOT')
            Example      = 'deploy/examples/example.viking-iis.run.env'
            Secrets      = @()
            Deploy       = 'deploy/Deploy-VikingIis.ps1'
            HealthUrl    = 'https://{VIKING_HOSTNAME_WEBSVC}/'
            Ports        = @(80, 443)
            Os           = @('windows')
            Description  = 'Legacy IIS: WCF annotation, OData, DataExport and tiles (websvc.<domain> through the proxy)'
        }
    }
}
