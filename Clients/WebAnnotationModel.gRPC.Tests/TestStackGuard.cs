using System;
using System.Collections.Concurrent;
using System.Linq;
using System.Net.Http;
using System.Text.Json;
using System.Threading.Tasks;
using Microsoft.Extensions.Configuration;
using NUnit.Framework;

namespace WebAnnotationModel.gRPC.Tests
{
    /// <summary>
    /// Stops the integration tests before their first call unless the gRPC server is the DevTest test stack.
    /// </summary>
    /// <remarks>
    /// The tests create, edit and delete structures, so they must never reach a production volume (RC1 listens on 5011,
    /// RC2 on 5013). The port alone proves nothing: the server must answer GET /test-stack, which only the test stack
    /// maps (TestStack__Enabled in docker-compose.test-identity.yml), and name one of the databases in
    /// GrpcServer:TestDatabases. The answer is checked once per endpoint and run.
    /// </remarks>
    internal static class TestStackGuard
    {
        private static readonly string[] DefaultTestDatabases = ["AnnotationTest"];

        private static readonly ConcurrentDictionary<string, string> Failures = new(StringComparer.OrdinalIgnoreCase);

        /// <summary>Fails the calling test with the reason when <paramref name="grpcEndpoint"/> is not the DevTest stack.</summary>
        public static void EnsureDevTestStack(IConfiguration config, string grpcEndpoint)
        {
            string failure = Failures.GetOrAdd(grpcEndpoint, endpoint => Check(config, endpoint));
            if (failure is not null)
                Assert.Fail(failure);
        }

        /// <summary>Null when the server is the test stack, otherwise why the run must stop.</summary>
        private static string Check(IConfiguration config, string grpcEndpoint)
        {
            string[] allowed = config.GetSection("GrpcServer:TestDatabases").Get<string[]>() is { Length: > 0 } configured
                ? configured
                : DefaultTestDatabases;
            string refuse = $"Refusing to run the gRPC integration tests against {grpcEndpoint}: ";
            const string fix = " Start the DevTest stack with Servers/GrpcAnnotationService/scripts/Start-AnnotationTestStack.ps1 " +
                               "(https://localhost:5019) and point GrpcServer:Endpoint at it.";
            try
            {
                using var http = new HttpClient(TestServerTls.CreateHandler(config)) { Timeout = TimeSpan.FromSeconds(15) };
                using HttpResponseMessage response = http.GetAsync(new Uri(new Uri(grpcEndpoint), "/test-stack")).GetAwaiter().GetResult();
                if (!response.IsSuccessStatusCode)
                    return refuse + $"GET /test-stack answered {(int)response.StatusCode}, so it is not the test stack." + fix;

                using JsonDocument marker = JsonDocument.Parse(response.Content.ReadAsStringAsync().GetAwaiter().GetResult());
                JsonElement root = marker.RootElement;
                bool isTestStack = root.TryGetProperty("testStack", out JsonElement flag) && flag.ValueKind == JsonValueKind.True;
                string database = root.TryGetProperty("database", out JsonElement name) ? name.GetString() : null;
                if (!isTestStack)
                    return refuse + "its /test-stack answer does not say testStack: true." + fix;
                if (!allowed.Contains(database, StringComparer.OrdinalIgnoreCase))
                    return refuse + $"it uses database '{database}', not one of GrpcServer:TestDatabases ({string.Join(", ", allowed)})." + fix;

                TestContext.Progress.WriteLine($"gRPC test stack confirmed at {grpcEndpoint} (database {database}).");
                return null;
            }
            catch (Exception e) when (e is HttpRequestException or TaskCanceledException or JsonException or UriFormatException)
            {
                return refuse + $"GET /test-stack failed ({e.GetType().Name}: {e.Message})." + fix;
            }
        }
    }
}
