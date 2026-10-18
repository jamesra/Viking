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
    /// the catalog root: <see cref="CorrectionCatalog.ResolveInsideRoot"/> rejects route values that escape it,
    /// and <see cref="SendFile"/> answers 404 for those and for missing files.
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

            endpoints.MapGet("/corrections/{group}/volume.npz", async context =>
            {
                await SendFile(context, groupFile(context, PublishedCorrectionSet.VolumeFileName), "application/octet-stream");
            }).RequireAuthorization(policy);

            endpoints.MapGet("/corrections/{group}/{file}", async context =>
            {
                string file = context.Request.RouteValues["file"]?.ToString();
                if (string.IsNullOrEmpty(file) || file.IndexOfAny(Path.GetInvalidFileNameChars()) >= 0)
                {
                    context.Response.StatusCode = StatusCodes.Status400BadRequest;
                    return;
                }

                await SendFile(context, groupFile(context, file), "application/octet-stream");
            }).RequireAuthorization(policy);

            endpoints.MapGet("/corrections/{group}/preview/{file}", async context =>
            {
                string file = context.Request.RouteValues["file"]?.ToString();
                if (string.IsNullOrEmpty(file) || file.IndexOfAny(Path.GetInvalidFileNameChars()) >= 0)
                {
                    context.Response.StatusCode = StatusCodes.Status400BadRequest;
                    return;
                }

                await SendFile(context, groupFile(context, PublishedCorrectionSet.PreviewFolderName, file), PreviewContentType(file));
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

        /// <summary>
        /// Resolves a file inside the requested group's folder under the catalog root, or null when the
        /// catalog is not registered, has no root, or the route values resolve outside the root.
        /// </summary>
        static string groupFile(HttpContext context, params string[] pathInGroup)
        {
            CorrectionCatalog catalog = context.RequestServices.GetService(typeof(CorrectionCatalog)) as CorrectionCatalog;
            string group = context.Request.RouteValues["group"]?.ToString() ?? "";
            return CorrectionCatalog.ResolveInsideRoot(catalog?.RootDirectory, [group, .. pathInGroup]);
        }

        /// <summary>
        /// Streams the file at <paramref name="fullPath"/> (from <see cref="groupFile"/>), or sets 404 when it is
        /// null, meaning it resolved outside the catalog root, or the file does not exist.
        /// </summary>
        static async System.Threading.Tasks.Task SendFile(HttpContext context, string fullPath, string contentType)
        {
            if (string.IsNullOrEmpty(fullPath) || !File.Exists(fullPath))
            {
                context.Response.StatusCode = StatusCodes.Status404NotFound;
                return;
            }

            context.Response.ContentType = contentType;
            await context.Response.SendFileAsync(fullPath);
        }
    }
}
