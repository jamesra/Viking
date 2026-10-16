using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;
using System;
using System.IO;
using Viking.SectionCorrection;

namespace gRPCAnnotationService
{
    /// <summary>
    /// HTTP endpoints that stream the published correction files (manifest, volume archive, other group
    /// files, and preview images) from <see cref="CorrectionCatalog.RootDirectory"/>.
    /// </summary>
    /// <remarks>
    /// Every route requires the caller-supplied authorization policy. Files are only served from beneath
    /// the catalog root: <see cref="SendFile"/> answers 404 for anything missing or resolving outside it.
    /// </remarks>
    static class CorrectionsFileEndpoints
    {
        /// <summary>Maps the <c>/corrections/{group}/...</c> routes onto <paramref name="endpoints"/>.</summary>
        /// <param name="endpoints">Route builder to extend.</param>
        /// <param name="policy">Authorization policy name applied to every route.</param>
        public static void Map(IEndpointRouteBuilder endpoints, string policy)
        {
            endpoints.MapGet("/corrections/{group}/manifest.json", async context =>
            {
                await SendFile(context, groupFile(context, PublishedCorrectionSet.ManifestFileName), "application/json");
            }).RequireAuthorization(policy);

            // Not stored at publish: built from the section files on first request and cached until the next publish.
            endpoints.MapGet("/corrections/{group}/volume.npz", async context =>
            {
                string manifest = groupFile(context, PublishedCorrectionSet.ManifestFileName);
                if (!IsUnderRoot(context, manifest))
                {
                    context.Response.StatusCode = StatusCodes.Status404NotFound;
                    return;
                }

                string archive = await System.Threading.Tasks.Task.Run(
                    () => VolumeArchiveCache.EnsureArchive(Path.GetDirectoryName(manifest)),
                    context.RequestAborted);
                await SendFile(context, archive, "application/octet-stream");
            }).RequireAuthorization(policy);

            endpoints.MapGet("/corrections/{group}/{file}", async context =>
            {
                string file = context.Request.RouteValues["file"]?.ToString();
                if (string.IsNullOrEmpty(file) || file.IndexOfAny(Path.GetInvalidFileNameChars()) >= 0)
                {
                    context.Response.StatusCode = StatusCodes.Status400BadRequest;
                    return;
                }

                string contentType = file.EndsWith(".npz", StringComparison.OrdinalIgnoreCase)
                    ? "application/octet-stream"
                    : "application/octet-stream";
                await SendFile(context, groupFile(context, file), contentType);
            }).RequireAuthorization(policy);

            endpoints.MapGet("/corrections/{group}/preview/{file}", async context =>
            {
                string file = context.Request.RouteValues["file"]?.ToString();
                if (string.IsNullOrEmpty(file) || file.IndexOfAny(Path.GetInvalidFileNameChars()) >= 0)
                {
                    context.Response.StatusCode = StatusCodes.Status400BadRequest;
                    return;
                }

                CorrectionCatalog catalog = context.RequestServices.GetService(typeof(CorrectionCatalog)) as CorrectionCatalog;
                string path = Path.Combine(catalog?.RootDirectory ?? "",
                    context.Request.RouteValues["group"]?.ToString() ?? "",
                    PublishedCorrectionSet.PreviewFolderName,
                    file);
                await SendFile(context, path, PreviewContentType(file));
            }).RequireAuthorization(policy);
        }

        /// <summary>Maps a preview file extension to its content type, defaulting to a binary stream.</summary>
        static string PreviewContentType(string file)
        {
            if (file.EndsWith(".svg", StringComparison.OrdinalIgnoreCase))
                return "image/svg+xml";
            if (file.EndsWith(".png", StringComparison.OrdinalIgnoreCase))
                return "image/png";
            return "application/octet-stream";
        }

        /// <summary>Builds the path of a file inside the requested group's folder under the catalog root.</summary>
        static string groupFile(HttpContext context, string fileName)
        {
            CorrectionCatalog catalog = context.RequestServices.GetService(typeof(CorrectionCatalog)) as CorrectionCatalog;
            string group = context.Request.RouteValues["group"]?.ToString() ?? "";
            return Path.Combine(catalog?.RootDirectory ?? "", group, fileName);
        }

        /// <summary>
        /// Streams the file at <paramref name="path"/>, or sets 404 when it is missing or resolves outside the catalog root.
        /// </summary>
        static async System.Threading.Tasks.Task SendFile(HttpContext context, string path, string contentType)
        {
            if (string.IsNullOrEmpty(path) || !File.Exists(path))
            {
                context.Response.StatusCode = StatusCodes.Status404NotFound;
                return;
            }

            if (!IsUnderRoot(context, path))
            {
                context.Response.StatusCode = StatusCodes.Status404NotFound;
                return;
            }

            context.Response.ContentType = contentType;
            await context.Response.SendFileAsync(Path.GetFullPath(path));
        }

        /// <summary>True when <paramref name="path"/> resolves beneath the catalog root (or no root is configured).</summary>
        static bool IsUnderRoot(HttpContext context, string path)
        {
            string fullRoot = Path.GetFullPath(context.RequestServices.GetService(typeof(CorrectionCatalog)) is CorrectionCatalog c
                ? c.RootDirectory ?? ""
                : "");
            return string.IsNullOrEmpty(fullRoot) ||
                Path.GetFullPath(path).StartsWith(fullRoot, StringComparison.OrdinalIgnoreCase);
        }
    }
}
