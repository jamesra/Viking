using System.Reflection;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;

namespace Viking.Identity.Server
{
    /// <summary>
    /// Exposes the running service version so other agents and operators can read it without a debugger:
    /// an <c>X-Service-Version</c> header on every response and <c>GET /version</c>.
    /// </summary>
    public static class ServiceVersion
    {
        public const string HeaderName = "X-Service-Version";

        /// <summary>Informational version of the entry assembly, without the source revision suffix.</summary>
        public static string Current { get; } = Read();

        public static IApplicationBuilder UseServiceVersionHeader(this IApplicationBuilder app)
        {
            return app.Use(async (context, next) =>
            {
                context.Response.OnStarting(() =>
                {
                    context.Response.Headers[HeaderName] = Current;
                    return System.Threading.Tasks.Task.CompletedTask;
                });
                await next();
            });
        }

        public static IEndpointConventionBuilder MapServiceVersion(this IEndpointRouteBuilder endpoints, string serviceName)
        {
            return endpoints.MapGet("/version", () => Results.Json(new { service = serviceName, version = Current }));
        }

        private static string Read()
        {
            var assembly = Assembly.GetEntryAssembly() ?? typeof(ServiceVersion).Assembly;
            var informational = assembly.GetCustomAttribute<AssemblyInformationalVersionAttribute>()?.InformationalVersion;
            if (string.IsNullOrEmpty(informational))
                return assembly.GetName().Version?.ToString() ?? "unknown";

            var plus = informational.IndexOf('+');
            return plus > 0 ? informational.Substring(0, plus) : informational;
        }
    }
}
