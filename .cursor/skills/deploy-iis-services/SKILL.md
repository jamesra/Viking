---
name: deploy-iis-services
description: >-
  Builds and deploys the IIS-hosted Viking services (ConnectomeODataV4 OData, DataExport, the WCF
  AnnotationService, and any IIS service added later) to the Debug and Release folders on
  \\OPR-MARC-WEBSV2\Services, which every volume (RC1, ...) runs from. Use when deploying or updating
  OData, Export or Annotation, when a server fix must reach RC1, when an IIS endpoint such as
  /RC1/OData/Scale() answers 500 after a fix was made, or when adding a new IIS service to the deploy.
---

# Deploy IIS-hosted services

The legacy services run on Windows IIS on **OPR-MARC-WEBSV2**. Every volume's IIS applications
(`/RC1/OData`, `/RC1/Annotation`, `/RC1/Export`, ...) point at one shared copy of the binaries, so a deploy
updates **every volume at once**. Docker services are not deployed this way (see `docker-config-and-builds-location`).

## 0. Commit and merge first

A fix that is only in a working tree, or only on a side branch, is not deployed and will regress: RC1 served
`Scale()` HTTP 500 for a day because its fix sat uncommitted. Before deploying:

- Commit the change on `dev` in `D:\src\git\Viking` (Annotation: on `Legacy` in `D:\src\git\VikingLegacy`). If the work was done on a worktree branch, merge `dev`
  into it, then fast-forward `dev` (`git merge --ff-only <branch>` in the main checkout). No feature branches.
- Build from the main checkout as it stands. Uncommitted edits from other sessions are included; that is expected.
- Commit locally only; push only when the user asks.

## 1. Where things are

Share `\\OPR-MARC-WEBSV2\Services` (= `C:\Services` on the host; `\\OpR-Marc-WebSv2\C$\Services` also works).
`OpR-Marc-WebSvc` does **not** resolve from the build machine; use `OPR-MARC-WEBSV2`.
Use PowerShell for the UNC paths (Git Bash `ls //host/share` fails).

```
Services\
  Release\  OData\  AnnotationService\  Export\      <- what the volume apps run (RC1 uses Release)
  Debug\    OData\  AnnotationService\  Export\      <- *Test apps (ODataTest, ...) where a volume has them
  Sites\RC1\                                         <- per-volume folders
  _<service>-<config>-backup-<yyyyMMdd-HHmmss>\      <- backups taken before each deploy
```

Always deploy **both** Debug and Release builds, each to its own folder.

| Service | Project | Build | Output to copy | Target | Keep on target (never overwrite) | Check |
|---|---|---|---|---|---|---|
| OData | `Servers/ConnectomeODataV4/ConnectomeODataV4.csproj` (SDK, net48) | `dotnet build -c <Config>` | `Servers/ConnectomeODataV4/bin/<Config>/net48/` | `<Config>\OData\bin` | `..\Web.config`, `..\Global.asax`, `bin\SqlServerTypes\` (only on the server) | `GET /RC1/OData/Scale()` → 200 with X/Y/Z |
| Annotation (WCF) | `Servers/AnnotationService/AnnotationService/AnnotationService.csproj` in **VikingLegacy** (`D:\src\git\VikingLegacy`, branch `Legacy`; SDK, net48) | `dotnet build -c <Config>` | the folder under `bin/<Config>/` that holds `AnnotationService.dll` (check; `DeployProduction.cmd`'s default `...\bin` source is stale) | `<Config>\AnnotationService\bin` | `web.config`, `webText.config`, `..\SqlServerTypes\`, `..\GraphClasses\`, `..\Service\`, `..\*.svc` | `GET /RC1/Annotation/Service.svc` → 200 |
| Export | `Servers/DataExport/DataExport.csproj` (net9.0) | `dotnet publish -c <Config> -o <scratch>\export-<Config>` | the publish folder | `<Config>\Export` | `web.config`, `appsettings*.json`, `logs\`, `Output\`, `Resources\` | `GET /RC1/Export/` → 200 |

`deploy/Deploy-VikingIis.ps1` wraps older scripts (`-Action Annotation` copies Release only; `Export`,
`ConfigureIIS` and `Certificate` must run on the IIS host as Administrator; `ConfigureIIS` recreates the odata and
dataexport pools and applications, and leaves the WCF annotation application alone). It has no OData action and no Debug deploy, so use the steps below for binaries and keep that script
for IIS setup and certificates.

A new IIS-hosted service gets a row here: its project, build command, output folder, target folder, the
server-owned files to keep, and an URL that proves it runs.

## 2. Build

From `D:\src\git\Viking` (Annotation from `D:\src\git\VikingLegacy`; VS MSBuild if `dotnet build` fails), for each config:

```bash
dotnet build Servers/ConnectomeODataV4/ConnectomeODataV4.csproj -c Release -nologo -v q
dotnet build Servers/ConnectomeODataV4/ConnectomeODataV4.csproj -c Debug -nologo -v q
```

In Git Bash write MSBuild switches as `-p:Name=Value`; `/p:` is rewritten as a path and the build fails
("Only one project can be specified"). Confirm the fix is in the output when it matters (for example list the
assembly's types with `System.Reflection.Metadata`) rather than trusting a timestamp.

## 3. Compare before copying

For each config, in PowerShell:

1. **Binding redirects** (net48 services): the server's `Web.config` is kept, so its `<runtime>` redirects must
   still match the build. Compare `dependentAssembly` name + `bindingRedirect newVersion` between the repo's
   `Web.config` and the deployed one. If they differ, update the deployed `Web.config`'s `<runtime>` section only
   (its connection strings and app settings are server-specific) and say so.
2. **File lists**: `Compare-Object` the build output and the target. Files only on the target (such as
   `SqlServerTypes\`) stay; files only in the build are new.

## 4. Back up, then copy

```powershell
$ts = Get-Date -Format 'yyyyMMdd-HHmmss'; $root = '\\OPR-MARC-WEBSV2\Services'
foreach ($c in 'Release','Debug') {
  robocopy "$root\$c\OData" "$root\_odata-$($c.ToLower())-backup-$ts" /E /R:1 /W:1 /NFL /NDL /NJH /NP
  if ($LASTEXITCODE -ge 8) { throw "backup $c failed" }
  robocopy "D:\src\git\Viking\Servers\ConnectomeODataV4\bin\$c\net48" "$root\$c\OData\bin" /E /R:2 /W:2 /NFL /NDL /NJH /NP
  if ($LASTEXITCODE -ge 8) { throw "deploy $c failed" }
}
$global:LASTEXITCODE = 0
```

- Never `/MIR` or `/PURGE`: that deletes server-only files. Exclude the "keep" files with `/XF` / `/XD` when
  the source contains them (Export's publish output has its own `web.config` and `appsettings.json`).
- robocopy exit codes 0-7 are success (1 copied, 2 extra files on target, 3 both); 8 or more is failure.
- Copying into `bin` recycles the application; the first requests after a deploy can fail while it starts.

## 5. Verify

- `curl` each service's check URL from the table (expect 200 and a sane body).
- Then run a real client against it (for OData, the MonogameTestbed launch config the user uses, or a small
  probe over `AnnotationVizLibODataClient`), because a hand-typed curl can pass where the client's query fails.
  Print the inner `DataServiceClientException` message: the outer one only says "An error occurred while
  processing this request".
- Report the commit deployed, both target folders, the backup folder names, and the verification results.

To roll back, robocopy the `_..._backup-<ts>` folder back over the target.
