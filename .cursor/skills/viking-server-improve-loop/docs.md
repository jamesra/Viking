# Documentation: consistent, solid, short

Read by the implementer on every wake that edits code, scripts, or docs, and by the scout on Docs wakes.

## Code documentation

- C#: load the `csharp-xml-docs` skill. Python: load the `python-comments` skill. PowerShell: comment-based help (`.SYNOPSIS`, `.DESCRIPTION`, `.PARAMETER`, `.EXAMPLE`) on every script and exported function. SQL: a header comment on each stored procedure, function, and view stating purpose, parameters, and callers.
- A changed signature, parameter, return value, or behavior updates its doc comment in the same commit. A stale comment is a bug.
- Keep comments that state a contract, threading rule, or edge case. Do not add comments that restate the next line.

## User documentation

Readers are lab members who run Viking and operators who deploy a database. Keep it task-first:

- **Where:** `README.md` per repo (what it is, how to build, where things are); `Servers/AnnotationDatabase/config-template/README.md` (database setup); `Servers/GrpcAnnotationService/config-template/README.md` (service setup and deploy); `Documentation/` for guides. Add a file only when none of these fits.
- **Shape:** one purpose per page. A one-line summary, numbered steps with copy-paste commands, then a short troubleshooting table of real errors. No history, no design essays, nothing the code or `--help` already says.
- **Length:** a how-to fits one or two screens. If a page grows, split by task or cut.
- **One source of truth.** Link instead of copying. A command, port, path, or setting appears in one place only.
- **Verified.** Run every documented command or script against the test stack before committing. A step that fails is fixed in the same commit.
- **Consistent terms.** "WCF client", "gRPC client", "annotation database", and "gRPC annotation service" mean one thing each everywhere.
- **Current.** A change to a script, compose file, proto, setting, or schema that users see updates the doc that names it in the same commit. Remove docs for removed features.

## Consistency checks (Docs track)

The scout looks for:

1. Docs whose commands, paths, ports, env names, or script parameters no longer match the files they describe.
2. The same instructions in two places (merge them; link the second).
3. Stale summary files at a repo root (`*-SUMMARY.md`, `*.plan.md`, `UpgradeLog*.htm`): propose archiving or deleting them as a decision; do not delete them directly.
4. Public types, members, scripts, and SQL objects in changed areas that lack the docs above.
5. README claims about WCF or gRPC status that disagree with `PARITY.md`.

A docs change is structural when it removes a mismatch, removes duplication, or fills a missing required doc. Rewording alone does not count.
