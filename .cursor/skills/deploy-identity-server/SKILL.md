---
name: deploy-identity-server
description: Rebuild and restart the Identity server container (IdentityServer, WebApi, management site at identity.codepharm.net:4001) on this workstation from the live Viking checkout, and check it came back. Use when an Identity or management-site change must go live.
---

# Deploy the Identity server

Ask James before deploying unless he asked for it. The build uses the live `D:\src\git\Viking\Servers` tree, so other
sessions' uncommitted edits there go live too: check `git status -- Servers` first.

## Steps

`D:\Docker\Builds\IdentityServer\restart_omni.cmd` runs `up --build` attached (it blocks the shell), so run its
compose steps yourself, detached, from PowerShell:

```powershell
$d = 'D:\Docker\Builds\IdentityServer'
$env:DUENDE_KEY_PATH = 'D:\Docker\mounted-configs\IdentityServer\Duende_License.key'
$env:IDENTITY_BUILD_CONTEXT = 'D:/src/git/Viking/Servers'
Set-Location D:\src\git\Viking\Servers
$a = '-f', "$d\docker-compose.legacy.yml", '--env-file', "$d\.env.All", '--env-file', "$d\.env.All.Production", '--env-file', "$d\.env.All.Docker"
$log = "$d\_deploy-$(Get-Date -Format yyyyMMdd)-<topic>.log"
docker-compose @a build *> $log    # stop here if it fails; the running container is untouched
docker-compose @a up -d *>> $log   # recreates identityserver-identity-all-services-1 only
```

## Check

- For the first ~30 s HTTPS fails ("SSL connection could not be established") and health shows `starting`; that is
  normal. Check again (don't sleep-loop in the shell; use Monitor or a second call).
- `Invoke-WebRequest https://identity.codepharm.net:4001/ -UseBasicParsing` answers 200, and
  `docker logs --tail 15 identityserver-identity-all-services-1` shows `[MGMT]` requests and no exceptions.
- Leave the `identity-certbot-renewer` container alone.
