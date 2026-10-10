using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Viking.Identity.Data;
using Viking.Identity.Models;

namespace Viking.Identity.Server.Extensions.Services
{
    public enum CatalogSyncStatus
    {
        /// <summary>The volume points at an annotation server.</summary>
        Linked,

        /// <summary>The VikingXML has no VolumeToEndpoint, so the volume shows images only.</summary>
        ImageOnly,

        NoEndpoint,
        FetchFailed,
        InvalidXml,
        InvalidAnnotationEndpoint
    }

    /// <summary>
    /// Outcome of fetching and parsing one VikingXML. Kept separate from applying it so callers can
    /// do the network call before opening a database transaction.
    /// </summary>
    public class VikingXmlFetchResult
    {
        public Uri Url { get; set; }
        public VikingXmlCatalogEntry Entry { get; set; }
        public CatalogSyncStatus? Failure { get; set; }
        public string Error { get; set; }
    }

    public class VolumeCatalogSyncResult
    {
        public long VolumeId { get; set; }
        public string VolumeName { get; set; }
        public CatalogSyncStatus Status { get; set; }
        public long? AnnotationServerId { get; set; }
        public string AnnotationServerName { get; set; }
        public bool CreatedAnnotationServer { get; set; }
        public bool CreatedImageSet { get; set; }
        public string Message { get; set; }
    }

    public class MirrorCheckResult
    {
        public long MirrorId { get; set; }
        public Uri Url { get; set; }
        public bool MatchesImageSet { get; set; }
        public string Status { get; set; }
    }

    public class CloneSuggestion
    {
        public string ContentHash { get; set; }
        public List<ImageSet> ImageSets { get; set; } = new List<ImageSet>();
    }

    public class CatalogReport
    {
        /// <summary>Volumes not linked to an annotation server, with the last sync message.</summary>
        public List<Volume> UnlinkedVolumes { get; set; } = new List<Volume>();

        /// <summary>Image sets whose VikingXML hashes match; each group is probably one set with several mirrors.</summary>
        public List<CloneSuggestion> CloneSuggestions { get; set; } = new List<CloneSuggestion>();
    }

    /// <summary>
    /// Populates <see cref="AnnotationServer"/>, <see cref="ImageSet"/>, and <see cref="ImageSetMirror"/> rows
    /// from the VikingXML each volume references. Never merges image sets or volumes; it only links each
    /// volume to the annotation server named by its VolumeToEndpoint and reports likely clones.
    /// </summary>
    public class AnnotationServerCatalogSync
    {
        internal const string MirrorOk = "OK";
        internal const string MirrorContentDiffers = "Content differs from the image set";

        private const int MaxMessageLength = 1024;
        private const int MaxMirrorStatusLength = 256;

        private readonly ApplicationDbContext _context;
        private readonly IVikingXmlSource _xmlSource;
        private readonly ILogger<AnnotationServerCatalogSync> _logger;

        public AnnotationServerCatalogSync(
            ApplicationDbContext context,
            IVikingXmlSource xmlSource,
            ILogger<AnnotationServerCatalogSync> logger = null)
        {
            _context = context;
            _xmlSource = xmlSource;
            _logger = logger;
        }

        public async Task<VikingXmlFetchResult> FetchAsync(Uri vikingXmlUrl, CancellationToken cancellationToken = default)
        {
            var result = new VikingXmlFetchResult { Url = vikingXmlUrl };
            if (vikingXmlUrl == null)
            {
                result.Failure = CatalogSyncStatus.NoEndpoint;
                result.Error = "Volume has no VikingXML URL.";
                return result;
            }

            string xml;
            try
            {
                xml = await _xmlSource.FetchXmlAsync(vikingXmlUrl, cancellationToken);
            }
            catch (Exception ex) when (ex is not OperationCanceledException || !cancellationToken.IsCancellationRequested)
            {
                _logger?.LogWarning(ex, "Catalog sync could not fetch {Url}", vikingXmlUrl);
                result.Failure = CatalogSyncStatus.FetchFailed;
                result.Error = $"Could not fetch VikingXML: {ex.Message}";
                return result;
            }

            try
            {
                result.Entry = VikingXmlCatalog.Parse(xml);
            }
            catch (Exception ex)
            {
                _logger?.LogWarning(ex, "Catalog sync could not parse {Url}", vikingXmlUrl);
                result.Failure = CatalogSyncStatus.InvalidXml;
                result.Error = $"Could not parse VikingXML: {ex.Message}";
            }

            return result;
        }

        public async Task<VolumeCatalogSyncResult> SyncVolumeAsync(long volumeId, CancellationToken cancellationToken = default)
        {
            var volume = await _context.Volume.FirstOrDefaultAsync(v => v.Id == volumeId, cancellationToken)
                ?? throw new InvalidOperationException($"Volume {volumeId} was not found.");

            var fetched = await FetchAsync(volume.Endpoint, cancellationToken);
            return await ApplyAsync(volume, fetched, cancellationToken);
        }

        /// <summary>
        /// Syncs every volume, lowest Id first, so an annotation server created here takes the name of the
        /// oldest volume that points at it.
        /// </summary>
        public async Task<List<VolumeCatalogSyncResult>> SyncAllVolumesAsync(CancellationToken cancellationToken = default)
        {
            var ids = await _context.Volume.OrderBy(v => v.Id).Select(v => v.Id).ToListAsync(cancellationToken);
            var results = new List<VolumeCatalogSyncResult>(ids.Count);
            foreach (var id in ids)
            {
                cancellationToken.ThrowIfCancellationRequested();
                results.Add(await SyncVolumeAsync(id, cancellationToken));
            }

            return results;
        }

        /// <summary>
        /// Applies a fetched VikingXML to <paramref name="volume"/> and saves. A failed fetch keeps any
        /// existing links and records the error on the volume.
        /// </summary>
        public async Task<VolumeCatalogSyncResult> ApplyAsync(Volume volume, VikingXmlFetchResult fetched, CancellationToken cancellationToken = default)
        {
            ArgumentNullException.ThrowIfNull(volume);
            ArgumentNullException.ThrowIfNull(fetched);

            var now = DateTime.UtcNow;
            var result = new VolumeCatalogSyncResult
            {
                VolumeId = volume.Id,
                VolumeName = volume.Name,
                AnnotationServerId = volume.AnnotationServerId
            };

            if (fetched.Failure.HasValue)
            {
                result.Status = fetched.Failure.Value;
                result.Message = fetched.Error;
                await RecordAsync(volume, result, now, cancellationToken);
                return result;
            }

            var entry = fetched.Entry;
            var notes = new List<string>();

            result.CreatedImageSet = await EnsureImageSetAsync(volume, entry.ContentHash, now, notes, cancellationToken);

            if (entry.AnnotationEndpoint == null)
            {
                // Image-only: keep any manually assigned annotation-server links.
                result.Status = entry.EndpointError != null ? CatalogSyncStatus.InvalidAnnotationEndpoint : CatalogSyncStatus.ImageOnly;
                notes.Insert(0, entry.EndpointError ?? "VikingXML has no VolumeToEndpoint; the volume shows images only.");
                result.Message = string.Join(" ", notes);
                result.AnnotationServerId = volume.AnnotationServerId;
                await RecordAsync(volume, result, now, cancellationToken);
                return result;
            }

            var baseUrl = AnnotationServiceUrls.ToBaseUrl(entry.AnnotationEndpoint);
            if (!AnnotationServiceUrls.FitsBaseUrlColumn(baseUrl))
            {
                result.Status = CatalogSyncStatus.InvalidAnnotationEndpoint;
                notes.Insert(0, $"Annotation base URL is longer than {AnnotationServer.MaxEndpointLength} characters.");
                result.Message = string.Join(" ", notes);
                await RecordAsync(volume, result, now, cancellationToken);
                return result;
            }

            var server = await _context.AnnotationServers
                .FirstOrDefaultAsync(s => s.BaseUrl == baseUrl, cancellationToken);
            if (server == null)
            {
                server = new AnnotationServer
                {
                    Name = UniqueAnnotationServerName(volume.Name),
                    Description = $"Annotation database for {volume.Name}, created from its VikingXML.",
                    ParentID = volume.ParentID,
                    ResourceTypeId = nameof(AnnotationServer),
                    BaseUrl = baseUrl
                };
                _context.AnnotationServers.Add(server);
                result.CreatedAnnotationServer = true;
            }

            server.AnnotationDatabaseName ??= Truncate(entry.AnnotationDatabaseName, 128);
            if (entry.ExportUrl != null && !AnnotationServiceUrls.IsDerivedExportUrl(baseUrl, entry.ExportUrl))
                server.ExportUrl ??= entry.ExportUrl;
            server.AuthenticationUrl ??= entry.AuthenticationUrl;

            bool hadDefault = volume.AnnotationServerId.HasValue;
            await VolumeAnnotationServerLinks.EnsureLinkAsync(
                _context,
                volume,
                server,
                forceDefault: false,
                cancellationToken);

            if (hadDefault && volume.AnnotationServerId.HasValue
                && volume.AnnotationServerId != server.Id && server.Id != 0
                && !volume.AnnotationServerLinks.Any(l => l.IsDefault && (l.AnnotationServerId == server.Id || ReferenceEquals(l.AnnotationServer, server))))
            {
                notes.Add($"Also linked VikingXML server {server.Name}; default left unchanged.");
            }

            result.Status = CatalogSyncStatus.Linked;
            result.AnnotationServerName = volume.AnnotationServer?.Name ?? server.Name;
            notes.Insert(0, result.CreatedAnnotationServer
                ? $"Created annotation server {server.Name}."
                : $"Linked to annotation server {server.Name}.");
            result.Message = string.Join(" ", notes);

            await RecordAsync(volume, result, now, cancellationToken);
            result.AnnotationServerId = volume.AnnotationServerId ?? server.Id;
            return result;
        }

        /// <summary>
        /// Fetches every mirror of an image set and records whether it serves the same VikingXML.
        /// </summary>
        public async Task<List<MirrorCheckResult>> CheckMirrorsAsync(long imageSetId, CancellationToken cancellationToken = default)
        {
            var imageSet = await _context.ImageSets
                .Include(i => i.Mirrors)
                .FirstOrDefaultAsync(i => i.Id == imageSetId, cancellationToken)
                ?? throw new InvalidOperationException($"Image set {imageSetId} was not found.");

            var results = new List<MirrorCheckResult>();
            foreach (var mirror in imageSet.Mirrors.OrderBy(m => m.Priority).ThenBy(m => m.Id))
            {
                var fetched = await FetchAsync(mirror.VikingXmlUrl, cancellationToken);
                var check = new MirrorCheckResult { MirrorId = mirror.Id, Url = mirror.VikingXmlUrl };
                if (fetched.Failure.HasValue)
                {
                    check.Status = fetched.Error;
                }
                else
                {
                    check.MatchesImageSet = imageSet.ContentHash == null
                        || string.Equals(imageSet.ContentHash, fetched.Entry.ContentHash, StringComparison.OrdinalIgnoreCase);
                    imageSet.ContentHash ??= fetched.Entry.ContentHash;
                    check.Status = check.MatchesImageSet ? MirrorOk : MirrorContentDiffers;
                }

                mirror.LastCheckUtc = DateTime.UtcNow;
                mirror.LastStatus = Truncate(check.Status, MaxMirrorStatusLength);
                results.Add(check);
            }

            await _context.SaveChangesAsync(cancellationToken);
            return results;
        }

        /// <summary>
        /// Sets <see cref="Volume.Endpoint"/> of every volume using <paramref name="imageSet"/> to its
        /// preferred enabled mirror, so clients that only read Endpoint follow mirror changes. Does not save.
        /// </summary>
        public async Task ApplyPrimaryMirrorAsync(ImageSet imageSet, CancellationToken cancellationToken = default)
        {
            ArgumentNullException.ThrowIfNull(imageSet);

            var mirrors = _context.Entry(imageSet).Collection(i => i.Mirrors);
            if (!mirrors.IsLoaded && imageSet.Id != 0)
                await mirrors.LoadAsync(cancellationToken);

            var primary = PrimaryMirror(imageSet);
            if (primary == null)
                return;

            var volumes = await _context.Volume.Where(v => v.ImageSetId == imageSet.Id).ToListAsync(cancellationToken);
            foreach (var volume in volumes)
                volume.Endpoint = primary.VikingXmlUrl;
        }

        public static ImageSetMirror PrimaryMirror(ImageSet imageSet) =>
            imageSet?.Mirrors
                .Where(m => m.Enabled)
                .OrderBy(m => m.Priority)
                .ThenBy(m => m.Id)
                .FirstOrDefault();

        public async Task<CatalogReport> BuildReportAsync(CancellationToken cancellationToken = default)
        {
            var report = new CatalogReport
            {
                UnlinkedVolumes = await _context.Volume
                    .Include(v => v.Parent)
                    .Where(v => v.AnnotationServerId == null)
                    .OrderBy(v => v.Name)
                    .ToListAsync(cancellationToken)
            };

            var hashed = await _context.ImageSets
                .Include(i => i.Mirrors)
                .Include(i => i.Volumes)
                .Where(i => i.ContentHash != null)
                .ToListAsync(cancellationToken);

            report.CloneSuggestions = hashed
                .GroupBy(i => i.ContentHash)
                .Where(g => g.Count() > 1)
                .Select(g => new CloneSuggestion { ContentHash = g.Key, ImageSets = g.OrderBy(i => i.Id).ToList() })
                .ToList();

            return report;
        }

        private async Task<bool> EnsureImageSetAsync(Volume volume, string contentHash, DateTime now, List<string> notes, CancellationToken cancellationToken)
        {
            if (volume.Endpoint == null)
                return false;

            if (volume.ImageSetId.HasValue && volume.ImageSet == null)
                await _context.Entry(volume).Reference(v => v.ImageSet).LoadAsync(cancellationToken);

            var imageSet = volume.ImageSet;
            if (imageSet == null)
            {
                imageSet = new ImageSet
                {
                    Name = volume.Name,
                    Description = volume.Description,
                    ContentHash = contentHash,
                    CreatedUtc = now
                };
                imageSet.Mirrors.Add(new ImageSetMirror
                {
                    VikingXmlUrl = volume.Endpoint,
                    Priority = 0,
                    Enabled = true,
                    LastCheckUtc = now,
                    LastStatus = MirrorOk
                });
                _context.ImageSets.Add(imageSet);
                volume.ImageSet = imageSet;
                return true;
            }

            var mirrorsEntry = _context.Entry(imageSet).Collection(i => i.Mirrors);
            if (!mirrorsEntry.IsLoaded)
                await mirrorsEntry.LoadAsync(cancellationToken);

            var mirror = imageSet.Mirrors.FirstOrDefault(m => SameUrl(m.VikingXmlUrl, volume.Endpoint));
            if (mirror == null)
            {
                // The volume URL was edited by hand: treat the new URL as the preferred mirror.
                foreach (var other in imageSet.Mirrors)
                    other.Priority += 1;

                mirror = new ImageSetMirror
                {
                    VikingXmlUrl = volume.Endpoint,
                    Priority = 0,
                    Enabled = true
                };
                imageSet.Mirrors.Add(mirror);
                notes.Add("Added the volume URL as the preferred mirror.");
                await ApplyPrimaryMirrorAsync(imageSet, cancellationToken);
            }

            if (imageSet.ContentHash != null && !string.Equals(imageSet.ContentHash, contentHash, StringComparison.OrdinalIgnoreCase))
                notes.Add("VikingXML content changed since the image set was registered.");

            imageSet.ContentHash = contentHash;
            mirror.LastCheckUtc = now;
            mirror.LastStatus = MirrorOk;
            return false;
        }

        private async Task RecordAsync(Volume volume, VolumeCatalogSyncResult result, DateTime now, CancellationToken cancellationToken)
        {
            volume.CatalogSyncedUtc = now;
            volume.CatalogSyncMessage = Truncate(result.Message, MaxMessageLength);
            await _context.SaveChangesAsync(cancellationToken);

            _logger?.LogInformation("Catalog sync {Volume}: {Status} {Message}", volume.Name, result.Status, result.Message);
        }

        private string UniqueAnnotationServerName(string baseName)
        {
            var name = Truncate(string.IsNullOrWhiteSpace(baseName) ? "Annotations" : baseName.Trim(), 120);
            var candidate = name;
            for (var n = 2; IsAnnotationServerNameTaken(candidate); n++)
                candidate = $"{name}-{n}";
            return candidate;
        }

        private bool IsAnnotationServerNameTaken(string name) =>
            _context.IsResourceNameTaken(name, nameof(AnnotationServer))
            || _context.ChangeTracker.Entries<AnnotationServer>()
                .Any(e => e.State == EntityState.Added && string.Equals(e.Entity.Name, name, StringComparison.OrdinalIgnoreCase));

        public static bool SameUrl(Uri a, Uri b) =>
            a != null && b != null && string.Equals(a.AbsoluteUri, b.AbsoluteUri, StringComparison.OrdinalIgnoreCase);

        private static string Truncate(string value, int max) =>
            value == null || value.Length <= max ? value : value.Substring(0, max);
    }
}
