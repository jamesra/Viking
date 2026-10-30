# Protected areas (proposal only)

Never edit these. When a candidate lands here, write a proposal (see [categories.md](categories.md), Proposals) and move on.

- **Auth and tokens:** `Clients/Viking.Tokens/**`, `Clients/Viking.UI.WPF/LoginWindow*`, `LoginViewModel.cs`, `Clients/Viking/WebAnnotation/VolumeAccessRoles.cs`, and any code that reads, stores, or sends access tokens or credentials.
- **On-disk formats:** anything that reads or writes files other programs or older Viking versions open: volume XML and mappings in `Clients/VolumeModel/**`, local bookmarks in `Clients/Viking/LocalBookmarks/**`, user settings persistence (`Properties/Settings*`, `webannotationusersettings.cs`), `BitmapFile.cs`, and any serializer attribute or element name. Changing a numeric field type in a serialized, persisted, or wire type counts as a format change.
- **Wire formats and contracts:** any `.proto` field number, name, type, or enum value in `gRPC_Protos`, and the shared data contracts in `AnnotationInterfaces`. Adding a new optional field or RPC is a proposal unless a parity row needs it and old clients keep working.
- **Server auth and identity:** `Servers/IdentityServer*`, token validation and `IdentityServer` settings in `GrpcAnnotationService`, certificates and `certbot`, and anything that reads or writes `.env`, `secrets.json`, or connection strings with credentials.
- **Real databases:** `Servers/AnnotationDatabase/Security/**` and `Server Objects/**` (logins, users, permissions), `Servers/SQL/**` (operational scripts), and any change that drops, truncates, or rewrites existing data or columns. Schema changes that add objects or fix a procedure are allowed on the SQL track; a breaking change to a table, column, or type used by deployed databases is a proposal with a migration plan.
- **Steering:** anything matched by an `avoid:` line in `STEERING.md`.

Callers of these areas may change only if the protected code and its observable inputs and outputs stay exactly the same.
