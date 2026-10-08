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

    public class VolumeImageSetInfo
    {
        public long Id { get; set; }
        public string Name { get; set; }
        public string VersionLabel { get; set; }
        public string PixelSpace { get; set; }
    }

    /// <summary>
    /// Metadata attached to each volume in the accessible-volume APIs. <c>Endpoint</c> keeps its
    /// meaning (a VikingXML URL, the preferred mirror) so existing clients are unaffected; the other
    /// keys are additive.
    /// </summary>
    public static class VolumeMetadata
    {
        public const string Description = "Description";
        public const string Endpoint = "Endpoint";
        public const string AnnotationServerName = "AnnotationServerName";
        public const string AnnotationEndpoint = "AnnotationEndpoint";
        public const string ImageSet = "ImageSet";
        public const string Mirrors = "Mirrors";
        public const string RegistrationName = "RegistrationName";

        /// <summary>Volumes with the navigation properties <see cref="Build"/> reads.</summary>
        public static IQueryable<Volume> WithCatalog(this ApplicationDbContext context) =>
            context.Volume
                .Include(v => v.AnnotationServer)
                .Include(v => v.ImageSet)
                    .ThenInclude(i => i.Mirrors);

        public static Dictionary<string, object> Build(Volume volume)
        {
            var metadata = new Dictionary<string, object>
            {
                [Description] = volume.Description,
                [Endpoint] = volume.Endpoint?.ToString(),
                [AnnotationServerName] = volume.AnnotationServer?.Name,
                [AnnotationEndpoint] = volume.AnnotationServer?.AnnotationEndpoint,
                [RegistrationName] = volume.RegistrationName,
                [Mirrors] = MirrorsOf(volume.ImageSet)
            };

            if (volume.ImageSet != null)
            {
                metadata[ImageSet] = new VolumeImageSetInfo
                {
                    Id = volume.ImageSet.Id,
                    Name = volume.ImageSet.Name,
                    VersionLabel = volume.ImageSet.VersionLabel,
                    PixelSpace = volume.ImageSet.PixelSpace.ToString()
                };
            }

            return metadata;
        }

        public static List<VolumeMirrorInfo> MirrorsOf(ImageSet imageSet)
        {
            if (imageSet == null)
                return new List<VolumeMirrorInfo>();

            return imageSet.Mirrors
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
    }
}
