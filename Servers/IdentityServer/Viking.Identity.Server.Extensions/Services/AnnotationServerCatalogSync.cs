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
        public long AnnotationContextId { get; set; }
        public string AnnotationContextName { get; set; }
        public CatalogSyncStatus Status { get; set; }
        public long? AnnotationServerId { get; set; }
        public string AnnotationServerName { get; set; }
        public bool CreatedAnnotationServer { get; set; }
        public bool CreatedVolume { get; set; }
        public string Message { get; set; }
    }

    public class MirrorCheckResult
    {
        public long MirrorId { get; set; }
        public Uri Url { get; set; }
        public bool MatchesVolume { get; set; }
        public string Status { get; set; }
    }

    public class CloneSuggestion
    {
        public string ContentHash { get; set; }
        public List<Volume> Volumes { get; set; } = new List<Volume>();
    }

    public class CatalogReport
    {
        /// <summary>Volumes not linked to an annotation server, with the last sync message.</summary>
        public List<AnnotationContext> UnlinkedAnnotationContexts { get; set; } = new List<AnnotationContext>();

        /// <summary>Volumes whose VikingXML hashes match; each group is probably one set with several mirrors.</summary>
        public List<CloneSuggestion> CloneSuggestions { get; set; } = new List<CloneSuggestion>();
    }

    /// <summary>
    /// Populates <see cref="AnnotationServer"/>, <see cref="Volume"/>, and <see cref="VolumeMirror"/> rows
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
            var volume = await _context.AnnotationContexts.FirstOrDefaultAsync(v => v.Id == volumeId, cancellationToken)
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
            var ids = await _context.AnnotationContexts.OrderBy(v => v.Id).Select(v => v.Id).ToListAsync(cancellationToken);
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
        public async Task<VolumeCatalogSyncResult> ApplyAsync(AnnotationContext volume, VikingXmlFetchResult fetched, CancellationToken cancellationToken = default)
        {
            ArgumentNullException.ThrowIfNull(volume);
            ArgumentNullException.ThrowIfNull(fetched);

            var now = DateTime.UtcNow;
            var result = new VolumeCatalogSyncResult
            {
                AnnotationContextId = volume.Id,
                AnnotationContextName = volume.Name,
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

            await EnsureConnectomeAsync(volume, cancellationToken);

            result.CreatedVolume = await EnsureVolumeAsync(volume, entry.ContentHash, now, notes, cancellationToken);

            if (volume.AnnotationServerPinned && volume.AnnotationServerId.HasValue)
            {
                await _context.Entry(volume).Reference(v => v.AnnotationServer).LoadAsync(cancellationToken);
                result.Status = CatalogSyncStatus.Linked;
                result.AnnotationServerName = volume.AnnotationServer?.Name;
                notes.Insert(0, $"Annotation server {volume.AnnotationServer?.Name} was set manually; the VikingXML VolumeToEndpoint is ignored.");
                result.Message = string.Join(" ", notes);
                await RecordAsync(volume, result, now, cancellationToken);
                return result;
            }

            if (entry.AnnotationEndpoint == null)
            {
                result.Status = entry.EndpointError != null ? CatalogSyncStatus.InvalidAnnotationEndpoint : CatalogSyncStatus.ImageOnly;
                notes.Insert(0, entry.EndpointError ?? "VikingXML has no VolumeToEndpoint; the volume shows images only.");
                result.Message = string.Join(" ", notes);
                await RecordAsync(volume, result, now, cancellationToken);
                return result;
            }

            var normalized = VikingXmlCatalog.NormalizeEndpoint(entry.AnnotationEndpoint);
            if (!VikingXmlCatalog.FitsEndpointColumn(normalized))
            {
                result.Status = CatalogSyncStatus.InvalidAnnotationEndpoint;
                notes.Insert(0, $"Annotation endpoint is longer than {AnnotationServer.MaxEndpointLength} characters.");
                result.Message = string.Join(" ", notes);
                await RecordAsync(volume, result, now, cancellationToken);
                return result;
            }

            var server = await _context.AnnotationServers
                .FirstOrDefaultAsync(s => s.AnnotationEndpoint == normalized, cancellationToken);
            if (server == null)
            {
                server = new AnnotationServer
                {
                    Name = UniqueAnnotationServerName(volume.Name),
                    Description = $"Annotation database for {volume.Name}, created from its VikingXML.",
                    ParentID = volume.ParentID,
                    ResourceTypeId = nameof(AnnotationServer),
                    AnnotationEndpoint = normalized,
                    ConnectomeId = volume.ConnectomeId
                };
                _context.AnnotationServers.Add(server);
                result.CreatedAnnotationServer = true;
            }
            else if (server.ConnectomeId == null && volume.ConnectomeId != null)
            {
                server.ConnectomeId = volume.ConnectomeId;
            }

            server.AnnotationDatabaseName ??= Truncate(entry.AnnotationDatabaseName, 128);
            server.ExportUrl ??= entry.ExportUrl;
            server.AuthenticationUrl ??= entry.AuthenticationUrl;

            if (volume.AnnotationServerId.HasValue && volume.AnnotationServerId != server.Id && server.Id != 0)
                notes.Add("Volume moved to a different annotation database.");

            volume.AnnotationServer = server;

            result.Status = CatalogSyncStatus.Linked;
            result.AnnotationServerName = server.Name;
            notes.Insert(0, result.CreatedAnnotationServer
                ? $"Created annotation server {server.Name}."
                : $"Linked to annotation server {server.Name}.");
            result.Message = string.Join(" ", notes);

            if (volume.Connectome != null
                && volume.Connectome.DefaultAnnotationContextId == null
                && volume.Id != 0)
            {
                volume.Connectome.DefaultAnnotationContextId = volume.Id;
            }

            await RecordAsync(volume, result, now, cancellationToken);
            result.AnnotationServerId = server.Id;
            return result;
        }

        /// <summary>
        /// Fetches every mirror of an image set and records whether it serves the same VikingXML.
        /// </summary>
        public async Task<List<MirrorCheckResult>> CheckMirrorsAsync(long imageSetId, CancellationToken cancellationToken = default)
        {
            var imageSet = await _context.Volumes
                .Include(i => i.Mirrors)
                .FirstOrDefaultAsync(i => i.Id == imageSetId, cancellationToken)
                ?? throw new InvalidOperationException($"Volume {imageSetId} was not found.");

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
                    check.MatchesVolume = imageSet.ContentHash == null
                        || string.Equals(imageSet.ContentHash, fetched.Entry.ContentHash, StringComparison.OrdinalIgnoreCase);
                    imageSet.ContentHash ??= fetched.Entry.ContentHash;
                    check.Status = check.MatchesVolume ? MirrorOk : MirrorContentDiffers;
                }

                mirror.LastCheckUtc = DateTime.UtcNow;
                mirror.LastStatus = Truncate(check.Status, MaxMirrorStatusLength);
                results.Add(check);
            }

            await _context.SaveChangesAsync(cancellationToken);
            return results;
        }

        /// <summary>
        /// Sets <see cref="Connectome.Endpoint"/> of every connectome using <paramref name="imageSet"/> to its
        /// preferred enabled mirror, so clients that only read Endpoint follow mirror changes. Does not save.
        /// </summary>
        public async Task ApplyPrimaryMirrorAsync(Volume imageSet, CancellationToken cancellationToken = default)
        {
            ArgumentNullException.ThrowIfNull(imageSet);

            var mirrors = _context.Entry(imageSet).Collection(i => i.Mirrors);
            if (!mirrors.IsLoaded && imageSet.Id != 0)
                await mirrors.LoadAsync(cancellationToken);

            var primary = PrimaryMirror(imageSet);
            if (primary == null)
                return;

            var volumes = await _context.AnnotationContexts.Where(v => v.VolumeId == imageSet.Id).ToListAsync(cancellationToken);
            foreach (var volume in volumes)
                volume.Endpoint = primary.VikingXmlUrl;
        }

        public static VolumeMirror PrimaryMirror(Volume imageSet) =>
            imageSet?.Mirrors
                .Where(m => m.Enabled)
                .OrderBy(m => m.Priority)
                .ThenBy(m => m.Id)
                .FirstOrDefault();

        public async Task<CatalogReport> BuildReportAsync(CancellationToken cancellationToken = default)
        {
            var report = new CatalogReport
            {
                UnlinkedAnnotationContexts = await _context.AnnotationContexts
                    .Include(v => v.Parent)
                    .Where(v => v.AnnotationServerId == null)
                    .OrderBy(v => v.Name)
                    .ToListAsync(cancellationToken)
            };

            var hashed = await _context.Volumes
                .Include(i => i.Mirrors)
                .Include(i => i.AnnotationContexts)
                .Where(i => i.ContentHash != null)
                .ToListAsync(cancellationToken);

            report.CloneSuggestions = hashed
                .GroupBy(i => i.ContentHash)
                .Where(g => g.Count() > 1)
                .Select(g => new CloneSuggestion { ContentHash = g.Key, Volumes = g.OrderBy(i => i.Id).ToList() })
                .ToList();

            return report;
        }

        private async Task EnsureConnectomeAsync(AnnotationContext context, CancellationToken cancellationToken)
        {
            if (context.ConnectomeId.HasValue)
            {
                if (context.Connectome == null)
                    await _context.Entry(context).Reference(c => c.Connectome).LoadAsync(cancellationToken);
                return;
            }

            var connectome = new Connectome
            {
                Name = context.Name,
                Description = context.Description,
                ParentID = context.ParentID,
                ResourceTypeId = nameof(Connectome),
                DefaultAnnotationContextId = context.Id != 0 ? context.Id : null
            };
            _context.Connectomes.Add(connectome);
            await _context.SaveChangesAsync(cancellationToken);
            context.ConnectomeId = connectome.Id;
            context.Connectome = connectome;
            if (connectome.DefaultAnnotationContextId == null && context.Id != 0)
            {
                connectome.DefaultAnnotationContextId = context.Id;
                await _context.SaveChangesAsync(cancellationToken);
            }
        }

        private async Task<bool> EnsureVolumeAsync(AnnotationContext volume, string contentHash, DateTime now, List<string> notes, CancellationToken cancellationToken)
        {
            if (volume.Endpoint == null)
                return false;

            if (volume.VolumeId.HasValue && volume.Volume == null)
                await _context.Entry(volume).Reference(v => v.Volume).LoadAsync(cancellationToken);

            var imageSet = volume.Volume;
            if (imageSet == null)
            {
                imageSet = new Volume
                {
                    Name = volume.Name,
                    Description = volume.Description,
                    ContentHash = contentHash,
                    CreatedUtc = now,
                    ConnectomeId = volume.ConnectomeId
                };
                imageSet.Mirrors.Add(new VolumeMirror
                {
                    VikingXmlUrl = volume.Endpoint,
                    Priority = 0,
                    Enabled = true,
                    LastCheckUtc = now,
                    LastStatus = MirrorOk
                });
                _context.Volumes.Add(imageSet);
                volume.Volume = imageSet;
                return true;
            }

            if (imageSet.ConnectomeId == null && volume.ConnectomeId != null)
                imageSet.ConnectomeId = volume.ConnectomeId;

            var mirrorsEntry = _context.Entry(imageSet).Collection(i => i.Mirrors);
            if (!mirrorsEntry.IsLoaded)
                await mirrorsEntry.LoadAsync(cancellationToken);

            var mirror = imageSet.Mirrors.FirstOrDefault(m => SameUrl(m.VikingXmlUrl, volume.Endpoint));
            if (mirror == null)
            {
                // The volume URL was edited by hand: treat the new URL as the preferred mirror.
                foreach (var other in imageSet.Mirrors)
                    other.Priority += 1;

                mirror = new VolumeMirror
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
                notes.Add("VikingXML content changed since the volume was registered.");

            imageSet.ContentHash = contentHash;
            mirror.LastCheckUtc = now;
            mirror.LastStatus = MirrorOk;
            return false;
        }

        private async Task RecordAsync(AnnotationContext volume, VolumeCatalogSyncResult result, DateTime now, CancellationToken cancellationToken)
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
