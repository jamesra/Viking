# Viking – instructions for Claude Code

The rules and skills for this repo are authored for Cursor and live in `.cursor/`. They are the single source of
truth; this file links to them instead of copying them, so edits in `.cursor/` apply to both tools.
User-level rules are imported from `~/.claude/CLAUDE.md` (which links `~/.cursor/rules`).

## Always-apply rules (loaded every session)

@.cursor/rules/Solution-Overview.mdc
@.cursor/rules/repo-roles.mdc
@.cursor/rules/commenting-to-build.mdc
Slack: post only to `#ai` or `#ai-requests`, never `#general` or other human channels. Read the `slack-ai-channel` skill
(`.cursor/skills/slack-ai-channel/SKILL.md`) before posting; the full rule is `.cursor/rules/slack-ai-protocol.mdc`.

## Path-scoped rules (read the file before editing anything matching its globs)

The `.mdc` frontmatter `globs:` line says which paths each rule covers.

| Rule | Applies to |
|------|-----------|
| `.cursor/rules/Content-Pipeline.mdc` | Monogame content pipeline (`Content.mgcb`, `Content/**`) |
| `.cursor/rules/Database-persistence.mdc` | `DataContext*`/`DataModel*`, `Migrations/**`, `Servers/SQL/**`, `appsettings*.json` |
| `.cursor/rules/contours-to-mesh.mdc` | `MorphologyMesh/**`, Bajaj/Mesh tests in MonogameTestbed, `Geometry/**/Meshing/**` |
| `.cursor/rules/coordinate-precision.mdc` | `Geometry/Transforms`, `Primitives`, `CurveFitting`, `Meshing`, `Clients/VolumeModel` |
| `.cursor/rules/coordinate-spaces.mdc` | `Geometry`, `Annotation*`, `WebAnnotation`, `Morphology*`, views/viewmodels (mosaic vs volume vs screen) |
| `.cursor/rules/curve-fit-link-length.mdc` | `MorphologyAlgorithms.cs`, CurveFit tests, FsCheck tests |
| `.cursor/rules/difficult-cases.mdc` | `MorphologyMesh/**`, `MorphologyMeshTest/DifficultCases/**`, Bajaj testbed files |
| `.cursor/rules/docker-config-and-builds-location.mdc` | `docker-compose*.yml`, `Dockerfile*`, `config-template/**` (config/builds live in `D:\Docker`) |
| `.cursor/rules/geometry-placement.mdc` | `MorphologyMesh/**`, `Clients/MonogameTestbed/**`, `ColladaIO/**` |
| `.cursor/rules/graphics-library-choice.mdc` | MonogameTestbed, Clients/Viking, MonogameXNAGraphicsShared (XNA/Monogame) |
| `.cursor/rules/grpc-protos-folder.mdc` | `*.proto` (must live in gRPC_Protos) |
| `.cursor/rules/odata-conventions.mdc` | `*OData*`, `*OpenData*` projects |
| `.cursor/rules/sam2-pin.mdc` | `Servers/SegmentationServer/Dockerfile` |

## Skills (`.cursor/skills/<name>/SKILL.md`)

Same `SKILL.md` format as Claude skills. Read the SKILL.md when the task matches. If `.claude/skills` exists
(a local junction to `.cursor/skills`), they also appear in the Skill tool.

| Skill | Use for |
|-------|---------|
| `bajaj-stage-walkthrough` | Diagnosing a bad Bajaj slice mesh stage by stage (BajajTest/BajajMultiTest) |
| `bajajtest-cli` | Dumping/reviewing MonogameTestbed BAJAJTEST screenshots via CLI |
| `contours-to-mesh` | Contour-to-3D-mesh work with MorphologyMesh |
| `mesh-difficult-cases` | Maintaining and re-checking the DifficultCases regression list |
| `sam2-grounding` | Planning/editing SAM2 integration in SegmentationServer |
| `deploy-viking-test` | Publishing the VikingTest Velopack build and announcing in Slack #betatest |
| `docker-letsencrypt-sidecar` | Adding HTTPS/Let's Encrypt to a container via the shared certbot sidecar |
| `slack-ai-channel` | Posting/reading Slack #ai with the `[Viking-Server]` protocol |
| `viking-server-ai-watch` | Launching the self-pacing Slack #ai server watch |

Plans in `.cursor/plans/` are historical working notes (gitignored); read one only if asked.
