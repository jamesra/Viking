# Scope: tracks, repos, and builds

Read by the scout and the implementer. Protected areas are in [protected.md](protected.md).

## Tracks

Each wake works one track. The scout rotates tracks as well as categories (see [categories.md](categories.md)) and prefers `STEERING.md` `focus:`.

| Track | Repo / branch | Paths | Build and test |
|-------|---------------|-------|----------------|
| **Client** (WCF) | `d:\src\git\VikingLegacy` / `Legacy` | `Clients/Viking`, `Clients/MonogameTestbed`, shared client libraries those compile, `Clients/VikingBenchmarks` | Viking (net48), MonogameTestbed (net9.0), covering test projects |
| **Docs** | `d:\src\git\VikingLegacy` / `Legacy` | see [docs.md](docs.md) | documented commands actually run |

## Rules

- Confirm the branch before the first edit. Edit only `Legacy`, never `main` or `master`.
- Do not edit generated code, comment code out to compile, or push. Do not edit `d:\src\git\Viking`; the server loop owns it.
- **Mirroring.** A change to a shared client library that also exists under `d:\src\git\Viking\Clients` gets its commit sha appended to `mirrorPending`; the loop does not mirror it.
- Stage by path only.
- **`.proto` files** live only in `gRPC_Protos`. A field number, name, type, or enum value change is a wire-format change: proposal only.
- **Config and secrets.** Connection strings and credentials come from configuration (`appsettings`, `.env` under `D:\Docker\Builds\`). Never write secrets, filled-in env files, or `secrets.json` into a commit, the ledger, or a report.
- **No production data.** Tests never write to a real annotation server or database (`OpR-Marc-DB2` or any live volume service).
