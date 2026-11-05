# Viking.SegmentationPrompts

The one place a segmentation request for annotation locations is built. The Viking clients (this tree and VikingLegacy)
and the annotation gallery (Sam2Trainer) all ask the segmentation server through it, so the same location gives the
same request bytes from every client: they share the server's result cache, and every request matches how the served
model was trained.

| Project | What it is |
|---------|------------|
| `Viking.SegmentationPrompts` | netstandard2.0 library; depends on Google.Protobuf 3.17.2 only. |
| `Viking.SegmentationPrompts.Native` | The same as a NativeAOT shared library with C entry points, for the gallery (ctypes). |
| `Viking.SegmentationPrompts.Tests` | MSTest: curve fit against the training exporter, prompt rules, a golden request digest. |

## API

Bytes in, bytes out: callers exchange serialized messages, so the library's own generated protocol types (compiled
`internal`) never clash with a client's.

- `PromptBuilder.BuildStart(PromptInput)` returns the `SegmentTilesRequest` to send as `start`.
- `PromptBuilder.Reach(PromptInput)` returns a `PromptReach`: the rectangle within which the caller must pass every
  annotation of the section as a candidate (the targets' cells grown by one cell each way).
- `PromptBuilder.BackgroundFor(PromptInput, TilesNeeded, sentIds)` returns a `PromptBackground` answering one
  `TilesNeeded`: the shapes for its `background_cells` not sent yet, and their ids.

`PromptInput`, `PromptAnnotation`, `PromptStructure`, `PromptReach` and `PromptBackground` are in
`gRPC_Protos/Segmentation/SAM2/prompt_input.proto`. Errors: `ArgumentException`/`FormatException` for a bad input,
`PromptTooLargeException` past 64 start tiles or 512 shapes.

Native entry points (`check_native.py` shows the calls): `sp_build_start(in, len, &out, &out_len)`,
`sp_reach(...)`, `sp_background_for(in, len, needed, needed_len, sent_ids, sent_count, &out, &out_len)` and
`sp_free(out)`. Status 0 is success; 1 invalid argument, 2 too large, 3 failure, each with a UTF-8 message in `out`.

## Rules

- **Tiles:** MOSAIC space at downsample 1; `channel` and `transform` are both the VikingXML tileset name, which the
  server reads tiles by when the request names an annotation context. Start tiles are the cells each target's bounds
  reach, row by row.
- **Circle (TypeCode 1):** `Shape.circle` at Location X, Y and the TRUE mosaic radius (the server applies the model's
  `circle_diameter_scale`), plus one click at X, Y: training's circle prompt is the disc and its centre.
- **Polygon (4), curve polygon (6), closed curve (7):** an outline to correct (`correction_feature`), one polygon per
  WKT part with its holes. Curve rings are fitted as Viking draws them and the training exporter fitted them
  (`Geometry/ClosedCurveFit.cs`, a port of nornir `segmentationtraining/curves.py`), and sent fitted. No click: the
  server gives each island its deepest pixel, training's click for a mask prompt, from the raster it encodes.
- **One prompt is circles or areas**, not both (seeds and an outline to correct cannot be mixed).
- **Background:** a candidate is a neighbour when it is a circle, polygon or curve polygon of a structure whose type is
  Cell or a child type of Cell (what the model saw as negatives), is not a target, is not the targets' structure or one
  of its descendants (Viking's `IsOtherStructure`), and its bounds meet the reach. `neighbors` lists every neighbour, so
  the server's cache key does not depend on how far the growth goes; `background_feature` carries those reaching the
  start tiles and `BackgroundFor` the rest on demand.
- **Order:** targets and neighbours by location id, tiles by row then column, so the bytes depend on the input's
  content, not its order. A neighbour whose WKT cannot be read is left out.
- **Arithmetic:** square roots, not `Math.Pow`, so Windows and Linux builds give the same bits. The golden digest in
  `PromptBuilderTests` pins the request for a fixed input; change it only when the rules change on purpose, since it
  changes every client's cache keys.

## Builds

- From Viking: `dotnet test SegmentationPrompts/Viking.SegmentationPrompts.Tests`.
- Native, for Linux (the gallery's Docker build stage, `mcr.microsoft.com/dotnet/sdk:10.0` plus `clang zlib1g-dev`):
  `dotnet publish SegmentationPrompts/Viking.SegmentationPrompts.Native -c Release -r linux-x64 -o out`, then
  `python check_native.py out/Viking.SegmentationPrompts.Native.so ../Viking.SegmentationPrompts.Tests`.
- The protos are read from `../../gRPC_Protos` relative to the library project. VikingLegacy reaches this folder
  through a directory link, so there that is VikingLegacy's own `gRPC_Protos` checkout, which must be at the same
  commit as Viking's.
- Fixtures: `Fixtures/make_curve_fixtures.py <nornir-buildmanager>` rewrites the curve fixtures from the exporter;
  `SP_WRITE_GOLDEN=1 dotnet test` rewrites `golden_prompt_input.hex` after the golden input changes.
