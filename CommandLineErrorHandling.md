# Command Line Error Handling

When a program's command line cannot be parsed, it prints the parse errors followed by its usage text and exits with code 1. This page lists the programs, how to see that output, and what it looks like.

## Programs

All use CommandLineParser 2.9.1. Each handler is in the program's `Program.cs`.

| Program | Source | Target | Required options |
|---------|--------|--------|------------------|
| Viking | `Clients/Viking/Viking/Program.cs` | net48 | `-v, --Volume` |
| VikingAU | `Clients/VikingAU/Program.cs` | net48, net9.0 | `-v, --VolumeURL` |
| MonogameTestbed | `Clients/MonogameTestbed/Program.cs` | net9.0-windows | none |
| MeasureDistance | `Clients/MeasureDistance/Program.cs` | net9.0 | `-v, --VolumeURL` (its only option) |
| Neo4JGenerator | `Servers/Neo4JGenerator/Program.cs` | net9.0 | `-s, --Neo4JDatabase` |

## See the error output

From the repository root, after building the programs in Debug:

```powershell
.\test_command_line_errors.ps1
```

or `test_command_line_errors.bat`. Each script runs Neo4JGenerator, VikingAU and MonogameTestbed (net9.0 Debug builds) with bad arguments, prints the exit code (expected 1), and skips a program that is not built.

Or run one program by hand:

```powershell
# Missing required option (-s, --Neo4JDatabase)
Servers\Neo4JGenerator\bin\Debug\net9.0\Neo4JGenerator.exe

# Unknown option
Clients\VikingAU\bin\Debug\net9.0\VikingAU.exe --invalid-option
```

Example output (VikingAU, trimmed):

```
ERROR: Unable to parse command line arguments.
The following errors occurred:
  CommandLine.UnknownOptionError
  CommandLine.MissingRequiredOptionError

  -v, --VolumeURL    Required. URL of VolumeXML file
  ...
```

The library also prints its own `ERROR(S):` block with the readable messages (`Option 'invalid-option' is unknown.`, `Required option 'v, VolumeURL' is missing.`) before the usage text, so the errors appear twice. A value that has the wrong type is reported as `Option 't, threads' is defined with a bad format.` (try `VikingAU.exe -v x --threads abc`). A string option such as `--Neo4JDatabase=not-a-url` is not a parse error; it parses and fails later, when the program uses it.

## Known limits

| Program | State |
|---------|-------|
| MonogameTestbed | Does not build at the moment (`MorphologyMesh` cannot find `GridVector2`, `GridPolygon`, `GridLineSegment`), so its output was not checked. Its source prints `Aborting: Unable to parse command line arguments` and the arguments it received, then exits with 1. |
| MeasureDistance | Does not build at the moment (`ODataClient.ConnectomeODataV4` is missing), so its output was not checked. Its source follows the same pattern as VikingAU. |
| Viking | A GUI program; its handler was not run. |
