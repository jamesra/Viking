using System;
using System.IO;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Configuration;

namespace Viking.Identity.Server.WebManagement.Extensions
{
    /// <summary>
    /// Serves Let's Encrypt HTTP-01 ACME challenge files from a configurable directory
    /// at <c>/.well-known/acme-challenge/&lt;token&gt;</c>. No auth; path traversal blocked.
    /// </summary>
    public static class AcmeChallengeExtensions
    {
        internal const string AcmeChallengePathPrefix = "/.well-known/acme-challenge/";

        public static IApplicationBuilder UseAcmeChallenge(this IApplicationBuilder app, IConfiguration configuration)
        {
            var challengePath = configuration["ACME_CHALLENGE_PATH"]
                ?? configuration.GetValue<string>("Acme:ChallengePath", "/app/acme-challenge");
            return app.UseMiddleware<AcmeChallengeMiddleware>(challengePath);
        }
    }

    internal class AcmeChallengeMiddleware
    {
        private readonly RequestDelegate _next;
        private readonly string _challengeDir;

        public AcmeChallengeMiddleware(RequestDelegate next, string challengeDir)
        {
            _next = next;
            _challengeDir = challengeDir?.Trim() ?? string.Empty;
        }

        public async Task InvokeAsync(HttpContext context)
        {
            if (!string.Equals(context.Request.Method, "GET", StringComparison.OrdinalIgnoreCase))
            {
                await _next(context).ConfigureAwait(false);
                return;
            }

            var path = context.Request.Path.Value;
            if (path == null || !path.StartsWith(AcmeChallengeExtensions.AcmeChallengePathPrefix, StringComparison.OrdinalIgnoreCase))
            {
                await _next(context).ConfigureAwait(false);
                return;
            }

            var token = path.Substring(AcmeChallengeExtensions.AcmeChallengePathPrefix.Length).TrimStart('/');
            if (string.IsNullOrEmpty(token) || token.IndexOfAny(new[] { '/', '\\' }) >= 0 || token.Contains(".."))
            {
                context.Response.StatusCode = StatusCodes.Status404NotFound;
                return;
            }

            if (string.IsNullOrEmpty(_challengeDir))
            {
                context.Response.StatusCode = StatusCodes.Status404NotFound;
                return;
            }

            var fullPath = Path.GetFullPath(Path.Combine(_challengeDir, token));
            var challengeDirFull = Path.GetFullPath(_challengeDir);
            if (!fullPath.StartsWith(challengeDirFull, StringComparison.Ordinal))
            {
                context.Response.StatusCode = StatusCodes.Status404NotFound;
                return;
            }

            if (!File.Exists(fullPath))
            {
                context.Response.StatusCode = StatusCodes.Status404NotFound;
                return;
            }

            context.Response.ContentType = "application/octet-stream";
            context.Response.StatusCode = StatusCodes.Status200OK;
            await context.Response.SendFileAsync(fullPath).ConfigureAwait(false);
        }
    }
}
