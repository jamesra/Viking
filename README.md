Viking
======

Annotation software for connectomes. This repository holds the Viking WCF client (a Windows desktop viewer and annotation tool for electron-microscopy volumes), the libraries it shares with other tools, and the annotation servers it talks to.

## Build

Requires Windows, Visual Studio 2022 or the .NET SDK 9 or later (`global.json`), and the .NET Framework 4.8 targeting pack. Run from the repository root:

```powershell
dotnet build Clients/Viking/Viking/Viking.csproj -c Release   # the Viking client (net48)
```

Close any running `Viking.exe` first; it locks the Debug output files. The whole client set (Viking, shared libraries, test projects) is in `Clients/Viking.sln`; every project is in `Everything.sln`.

## Test

```powershell
dotnet test Clients/CommonTests/CommonTests.csproj
dotnet test Clients/VolumeModelTests/VolumeModelTests.csproj
dotnet test Clients/Viking/LocalBookmarksTests/LocalBookmarksTests.csproj
dotnet test GeometryTests/GeometryTests.csproj
```

`GeometryTests` takes a minute or two per target framework. `Clients/MonogameTestbed` (a net9.0 graphics test harness) is mid-migration and does not currently build.

## Where things are

| Path | Contents |
|------|----------|
| `Clients/Viking/Viking` | The Viking application (entry point, settings, Velopack packaging scripts) |
| `Clients/Viking/NGVV` | `VikingCore`: the main viewer, tile and volume UI |
| `Clients/WebAnnotationModel`, `Clients/WebAnnotationViewModels` | Annotation model and view models |
| `Clients/VolumeModel`, `Clients/VolumeModelTests` | Volume definition and its tests |
| `Clients/MonogameTestbed` | Graphics and geometry test harness |
| `Geometry`, `Geometry.Core`, `MorphologyMesh` | Geometry primitives, curve fitting, and meshing shared by client and servers |
| `gRPC_Protos` | The only home of `.proto` files |
| `Servers` | Annotation, identity, segmentation, and OData services |
| `Documentation` | Sphinx source for the user and developer documentation |
| `Scripts` | Docker validation and DLL-sharing helper scripts |

## More documentation

- User-visible changes: [VikingChangelog.md](VikingChangelog.md)
- Using Viking: [Documentation/source/Client/useage.rst](Documentation/source/Client/useage.rst)
- Running the services in Docker: [Docker-Configuration-README.md](Docker-Configuration-README.md)
- Renewing the identity server certificate: [Documentation/IdentityServer-cert-renewal.md](Documentation/IdentityServer-cert-renewal.md)
