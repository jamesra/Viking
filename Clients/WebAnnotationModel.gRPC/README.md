# WebAnnotationModel.gRPC

The gRPC client for the annotation database: the stores and gRPC clients that Viking and VikingAU use to talk to the gRPC annotation service. Feature and speed parity with the WCF client is tracked in [PARITY.md](PARITY.md).

## Point the client at a server

Viking reads the endpoint from the `Endpoint` attribute of the `VolumeToEndpoint` element in the VikingXML volume file. `AnnotationBootstrap` passes it to `ConfigureAnnotationModel` as `GrpcRepositorySettings.Endpoint`.

- Viking is .NET Framework 4.8 and uses `WinHttpHandler`, which only speaks gRPC over TLS. Use the HTTPS endpoint (default `https://localhost:5011`).
- If the XML still says `http://`, `GrpcChannelManager` rewrites it to `https://`, and a loopback `:5010` becomes `:5011`. The rewrite is traced as `[gRPC] WinHttpHandler requires TLS`. Fix the XML anyway.
- A plain `http://` endpoint with a non-WinHttp handler (.NET 5+, the tests) is sent as HTTP/2 cleartext.
- The bearer token comes from the registered `IAnnotationAccessTokenProvider`. Ports, compose variables and the test stack are described once, in the [gRPC annotation service README](../../Servers/GrpcAnnotationService/config-template/README.md).

## Run the tests

Tests without a server (stores, converters, `GrpcChannelManager`, 60 tests):

```powershell
dotnet test Clients\WebAnnotationModel.gRPC.Tests\WebAnnotationModel.gRPC.Tests.csproj --filter "FullyQualifiedName!~Tests.Tests.&FullyQualifiedName!~AnnotationStoresSmokeTests"
```

The remaining tests (`Test.cs`, `AnnotationStoresSmokeTests`) call a live service and sign in to identity. They read `appsettings.json` in the test project: gRPC at `http://localhost:5010`, identity at `http://host.docker.internal:5020`. Start the test stack first, from the repo root, as described in the service README:

```powershell
.\Servers\GrpcAnnotationService\scripts\Start-AnnotationTestStack.ps1 -ApplySchema -Build
```

Then run `dotnet test` on the project without the filter. These tests run only against the throwaway `AnnotationTest` database, never a real annotation database.

## Troubleshooting

| Symptom | Cause and fix |
|---|---|
| `Error connecting to http://host.docker.internal:5020/.well-known/openid-configuration` | The identity DevTest container is not running. Start the test stack (above). |
| gRPC calls return HTTP 401 | The token's issuer does not match the service's identity server, for example a token from identity.codepharm.net sent to the DevTest stack. See the identity overlay note in the service README. |
| Viking cannot connect to `http://localhost:5010` | `WinHttpHandler` needs TLS. Set the VikingXML `Endpoint` to `https://localhost:5011`. |
| `[gRPC] No access token; request will be sent without Authorization.` in the trace | No token provider is registered, or it returned an empty token. Sign in again. |
