# VikingLegacy

Rules and skills are authored for Cursor in `.cursor/` and imported here by reference, so there is one source of truth. Edit the `.cursor` files, not copies.

## Always-apply rules
@.cursor/rules/repo-roles.mdc
@.cursor/rules/commenting-code.mdc
@.cursor/rules/commenting-to-build.mdc
@.cursor/rules/grpc-protos-folder.mdc

## Conditional rules (read the file when the situation matches)
- `.cursor/rules/async-returns-tasks.mdc` — before writing or editing C# async methods.
- `.cursor/rules/coordinate-precision.mdc` — before touching `Geometry/Transforms`, `Geometry/Primitives`, `Geometry.Core/Primitives`, `Geometry/CurveFitting`, `Geometry/Meshing`, `Clients/VolumeModel`.
- `.cursor/rules/docker-config-and-builds-location.mdc` — before touching docker-compose files, Dockerfiles, or `config-template/`.
- `.cursor/rules/graphics-library-choice.mdc` — before working on XNA/MonoGame projects.
- `.cursor/rules/templates.mdc` — before editing anything that looks generated (edit the template or generator input instead).
- `.cursor/rules/slack-ai-protocol.mdc` — only when asked to post to or watch Slack `#ai`, `#ai-requests`, `#ai-offtopic`. Do not adopt the `[Viking-Client]` Slack identity otherwise.

## Skills
`.claude/skills/` links to `.cursor/skills/` (`deploy-viking-test`, `viking-client-ai-loop`).

## Notes
- `.cursor/plans/` and `.cursor/Built Plans/` hold past design plans; consult them for background on segmentation and pen-mode work.
- Cursor-specific wording ("workspace", "Task" tool) maps to this session's equivalents.
