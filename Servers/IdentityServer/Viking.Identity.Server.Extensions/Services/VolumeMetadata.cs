using System.Collections.Generic;
using System.Linq;
using Microsoft.EntityFrameworkCore;
using Viking.Identity.Data;
using Viking.Identity.Models;

namespace Viking.Identity.Server.Extensions.Services
{
    /// <summary>One enabled mirror as clients see it, preferred first.</summary>
    public class VolumeMirrorInfo
    {
        public string Url { get; set; }
        public string Region { get; set; }
        public int Priority { get; set; }
    }

    public class VolumeInfo
    {
        public long Id { get; set; }
        public string Name { get; set; }
        public string VersionLabel { get; set; }
        public string PixelSpace { get; set; }
    }

    /// <summary>
    /// One annotation server linked to an annotation context, as clients see it in AccessibleVolumes.
    /// </summary>
    public class VolumeAnnotationServerInfo
    {
        public string Name { get; set; }
        public string BaseUrl { get; set; }
        public string AnnotationEndpoint { get; set; }
        public string ODataEndpoint { get; set; }
        public string ExportEndpoint { get; set; }
        public bool IsDefault { get; set; }
    }

    /// <summary>
    /// Metadata attached to each annotation context in the accessible-volume APIs. <c>Endpoint</c> keeps its
    /// meaning (a VikingXML URL, the preferred mirror) so existing clients are unaffected; the other
    /// keys are additive. <c>AnnotationEndpoint</c> is the resolved WCF URL from the default server.
    /// </summary>
    public static class VolumeMetadata
    {
        public const string Description = "Description";
        public const string Endpoint = "Endpoint";
        public const string AnnotationServerName = "AnnotationServerName";
        public const string AnnotationEndpoint = "AnnotationEndpoint";
        public const string BaseUrl = "BaseUrl";
        public const string ODataEndpoint = "ODataEndpoint";
        public const string ExportEndpoint = "ExportEndpoint";
        public const string AnnotationServers = "AnnotationServers";
        public const string Volume = "Volume";
        public const string Mirrors = "Mirrors";
        public const string RegistrationName = "RegistrationName";

        /// <summary>Annotation contexts with the navigation properties <see cref="Build"/> reads.</summary>
        public static IQueryable<AnnotationContext> WithCatalog(this ApplicationDbContext context) =>
            context.AnnotationContexts
                .Include(c => c.AnnotationServer)
                .Include(c => c.AnnotationServerLinks)
                    .ThenInclude(l => l.AnnotationServer)
                .Include(c => c.Volume)
                    .ThenInclude(v => v.Mirrors);

        public static Dictionary<string, object> Build(AnnotationContext annotationContext)
        {
            var defaultServer = DefaultServer(annotationContext);
            var servers = AnnotationServersOf(annotationContext);

            var metadata = new Dictionary<string, object>
            {
                [Description] = annotationContext.Description,
                [Endpoint] = annotationContext.Endpoint?.ToString(),
                [AnnotationServerName] = defaultServer?.Name,
                [AnnotationEndpoint] = AnnotationServiceUrls.AnnotationUrl(defaultServer),
                [BaseUrl] = defaultServer?.BaseUrl,
                [ODataEndpoint] = AnnotationServiceUrls.ODataUrl(defaultServer),
                [ExportEndpoint] = AnnotationServiceUrls.ExportUrl(defaultServer),
                [AnnotationServers] = servers,
                [RegistrationName] = annotationContext.RegistrationName,
                [Mirrors] = MirrorsOf(annotationContext.Volume)
            };

            if (annotationContext.Volume != null)
            {
                metadata[Volume] = new VolumeInfo
                {
                    Id = annotationContext.Volume.Id,
                    Name = annotationContext.Volume.Name,
                    VersionLabel = annotationContext.Volume.VersionLabel,
                    PixelSpace = annotationContext.Volume.PixelSpace.ToString()
                };
            }

            return metadata;
        }

        public static List<VolumeMirrorInfo> MirrorsOf(Volume volume)
        {
            if (volume == null)
                return new List<VolumeMirrorInfo>();

            return volume.Mirrors
                .Where(m => m.Enabled && m.VikingXmlUrl != null)
                .OrderBy(m => m.Priority)
                .ThenBy(m => m.Id)
                .Select(m => new VolumeMirrorInfo
                {
                    Url = m.VikingXmlUrl.ToString(),
                    Region = m.RegionLabel,
                    Priority = m.Priority
                })
                .ToList();
        }

        private static AnnotationServer DefaultServer(AnnotationContext annotationContext)
        {
            if (annotationContext == null)
                return null;

            var defaultLink = annotationContext.AnnotationServerLinks?
                .FirstOrDefault(l => l.IsDefault && l.AnnotationServer != null);
            if (defaultLink != null)
                return defaultLink.AnnotationServer;

            return annotationContext.AnnotationServer;
        }

        private static List<VolumeAnnotationServerInfo> AnnotationServersOf(AnnotationContext annotationContext)
        {
            if (annotationContext?.AnnotationServerLinks == null || annotationContext.AnnotationServerLinks.Count == 0)
            {
                if (annotationContext?.AnnotationServer == null)
                    return new List<VolumeAnnotationServerInfo>();

                return new List<VolumeAnnotationServerInfo>
                {
                    ToInfo(annotationContext.AnnotationServer, isDefault: true)
                };
            }

            return annotationContext.AnnotationServerLinks
                .Where(l => l.AnnotationServer != null)
                .OrderByDescending(l => l.IsDefault)
                .ThenBy(l => l.AnnotationServer.Name)
                .Select(l => ToInfo(l.AnnotationServer, l.IsDefault))
                .ToList();
        }

        private static VolumeAnnotationServerInfo ToInfo(AnnotationServer server, bool isDefault) =>
            new VolumeAnnotationServerInfo
            {
                Name = server.Name,
                BaseUrl = server.BaseUrl,
                AnnotationEndpoint = AnnotationServiceUrls.AnnotationUrl(server),
                ODataEndpoint = AnnotationServiceUrls.ODataUrl(server),
                ExportEndpoint = AnnotationServiceUrls.ExportUrl(server),
                IsDefault = isDefault
            };
    }
}
