# Protected areas (proposal only)

Never edit these. When a candidate lands here, write a proposal (see [categories.md](categories.md), Proposals) and move on.

- **Deep links and single-instance activation:** `Clients/Viking/NGVV/VikingDeepLinkParser.cs`, `Clients/Viking/NGVV/VikingLaunchExchangeParser.cs`, `Clients/Viking/Viking/VikingDeepLinkActivation.cs`, `VikingSingleInstance.cs`, `VikingProtocolRegistration.cs`, `VikingChannelIdentity.cs`, and deep-link handling in `Program.cs`.
- **Auth and tokens:** `Clients/WCFTokenInjector/**`, `Clients/Viking.UI.WPF/LoginWindow*`, `LoginViewModel.cs`, `Clients/Viking/WebAnnotation/VolumeAccessRoles.cs`, and any code that reads, stores, or sends access tokens or credentials.
- **On-disk formats:** anything that reads or writes files other programs or older Viking versions open: volume XML and mappings in `Clients/VolumeModel/**`, local bookmarks in `Clients/Viking/LocalBookmarks/**`, user settings persistence (`Properties/Settings*`, `webannotationusersettings.cs`), `BitmapFile.cs`, and any serializer attribute or element name. Changing a numeric field type in a serialized, persisted, or wire type counts as a format change.
- **Steering:** anything matched by an `avoid:` line in `STEERING.md`.

Callers of these areas may change only if the protected code and its observable inputs and outputs stay exactly the same.
