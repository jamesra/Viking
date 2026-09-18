using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;
using System;
using System.IO;
using Viking.SectionCorrection;

namespace gRPCAnnotationService
{
    static class CorrectionsFileEndpoints
    {
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

        static string PreviewContentType(string file)
        {
            if (file.EndsWith(".svg", StringComparison.OrdinalIgnoreCase))
                return "image/svg+xml";
            if (file.EndsWith(".png", StringComparison.OrdinalIgnoreCase))
                return "image/png";
            return "application/octet-stream";
        }

        static string groupFile(HttpContext context, string fileName)
        {
            CorrectionCatalog catalog = context.RequestServices.GetService(typeof(CorrectionCatalog)) as CorrectionCatalog;
            string group = context.Request.RouteValues["group"]?.ToString() ?? "";
            return Path.Combine(catalog?.RootDirectory ?? "", group, fileName);
        }

        static async System.Threading.Tasks.Task SendFile(HttpContext context, string path, string contentType)
        {
            if (string.IsNullOrEmpty(path) || !File.Exists(path))
            {
                context.Response.StatusCode = StatusCodes.Status404NotFound;
                return;
            }

            string fullRoot = Path.GetFullPath(context.RequestServices.GetService(typeof(CorrectionCatalog)) is CorrectionCatalog c
                ? c.RootDirectory ?? ""
                : "");
            string fullPath = Path.GetFullPath(path);
            if (!string.IsNullOrEmpty(fullRoot) &&
                !fullPath.StartsWith(fullRoot, StringComparison.OrdinalIgnoreCase))
            {
                context.Response.StatusCode = StatusCodes.Status404NotFound;
                return;
            }

            context.Response.ContentType = contentType;
            await context.Response.SendFileAsync(fullPath);
        }
    }
}
