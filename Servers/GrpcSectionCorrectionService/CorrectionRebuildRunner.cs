using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using Viking.SectionCorrection;
using Viking.SectionCorrectionBuilder;

namespace Viking.GrpcSectionCorrectionService
{
    /// <summary>
    /// Rebuilds the correction set of every (annotation database, StosGroup) pair the assigned contexts need, one pair
    /// at a time. Contexts that share a database and StosGroup are built once into
    /// <c>{Corrections:RootDirectory}/{database}/{stosGroup}</c>. Used by the hosted service and by unit tests with mocks.
    /// </summary>
    public sealed class CorrectionRebuildRunner
    {
        readonly IIdentityVolumeSource _volumes;
        readonly AnnotationConnectionResolver _connections;
        readonly IVolumeCorrectionPublisher _publisher;
        readonly VolumeCorrectionCatalog _catalog;
        readonly RebuildStatusStore _status;
        readonly IConfiguration _configuration;
        readonly ILogger<CorrectionRebuildRunner> _logger;
        readonly SemaphoreSlim _gate = new(1, 1);

        public CorrectionRebuildRunner(
            IIdentityVolumeSource volumes,
            AnnotationConnectionResolver connections,
            IVolumeCorrectionPublisher publisher,
            VolumeCorrectionCatalog catalog,
            RebuildStatusStore status,
            IConfiguration configuration,
            ILogger<CorrectionRebuildRunner> logger)
        {
            _volumes = volumes;
            _connections = connections;
            _publisher = publisher;
            _catalog = catalog;
            _status = status;
            _configuration = configuration;
            _logger = logger;
        }

        /// <summary>
        /// Lists the assigned contexts, groups them by (annotation database, StosGroup) and rebuilds each group.
        /// A group that fails is logged and reported in the rebuild status; the others still run. Only one run
        /// executes at a time; a second caller waits for the first.
        /// </summary>
        /// <param name="cancellationToken">Stops between groups and inside the publisher.</param>
        public async Task RunAsync(CancellationToken cancellationToken)
        {
            await _gate.WaitAsync(cancellationToken).ConfigureAwait(false);
            try
            {
                IReadOnlyList<IdentityVolumeRow> rows = await _volumes.ListAsync(cancellationToken).ConfigureAwait(false);
                List<IGrouping<(string Database, string StosGroup), IdentityVolumeRow>> groups = GroupByCorrectionKey(rows);
                _status.Begin([.. groups.Select(g => g.Key.Database).Distinct(StringComparer.OrdinalIgnoreCase)]);
                string error = "";
                try
                {
                    List<string> failures = [];
                    foreach (IGrouping<(string Database, string StosGroup), IdentityVolumeRow> group in groups)
                    {
                        try
                        {
                            await RebuildGroupAsync(group.Key.Database, group.Key.StosGroup, [.. group], cancellationToken)
                                .ConfigureAwait(false);
                        }
                        catch (Exception ex) when (ex is not OperationCanceledException)
                        {
                            _logger.LogError(ex, "Section-correction rebuild failed for {Database} / {StosGroup}.",
                                group.Key.Database, DisplayStos(group.Key.StosGroup));
                            failures.Add($"{group.Key.Database}/{DisplayStos(group.Key.StosGroup)}: {ex.Message}");
                        }
                    }

                    if (failures.Count > 0)
                        error = string.Join("; ", failures);
                }
                finally
                {
                    _status.Complete(error);
                    try
                    {
                        _catalog.Load();
                    }
                    catch (Exception ex)
                    {
                        _logger.LogWarning(ex, "Catalog reload after rebuild failed.");
                    }
                }
            }
            finally
            {
                _gate.Release();
            }
        }

        /// <summary>
        /// Groups rows that share an annotation database and StosGroup (both compared case-insensitively; a null
        /// StosGroup is the VikingXML default). Rows with no usable name are dropped.
        /// </summary>
        /// <param name="rows">Assigned contexts.</param>
        /// <returns>One group per correction set, in first-seen order.</returns>
        internal static List<IGrouping<(string Database, string StosGroup), IdentityVolumeRow>> GroupByCorrectionKey(
            IEnumerable<IdentityVolumeRow> rows) =>
            [.. rows
                .Where(r => !string.IsNullOrWhiteSpace(r.CorrectionDatabase))
                .GroupBy(r => (Database: r.CorrectionDatabase.Trim(), StosGroup: r.StosGroup?.Trim() ?? ""),
                    CorrectionKeyComparer.Instance)];

        static string DisplayStos(string stosGroup) => string.IsNullOrEmpty(stosGroup) ? "(default)" : stosGroup;

        async Task RebuildGroupAsync(
            string database,
            string stosGroup,
            IReadOnlyList<IdentityVolumeRow> contexts,
            CancellationToken cancellationToken)
        {
            // Contexts in a group share the database and StosGroup, so any one VikingXML supplies the transforms.
            IdentityVolumeRow source = contexts.FirstOrDefault(c => !string.IsNullOrWhiteSpace(c.VikingXmlEndpoint));
            if (source is null)
            {
                _logger.LogWarning("Skipping {Database}: no assigned context has a VikingXML Endpoint.", database);
                return;
            }

            string connection = _connections.Resolve(database, contexts.Select(c => c.Name));
            Uri odata = null;
            if (string.IsNullOrWhiteSpace(connection))
            {
                // No SQL mapping: read the annotations through OData when Identity knows the endpoint.
                string odataText = contexts.Select(c => c.ODataEndpoint).FirstOrDefault(e => !string.IsNullOrWhiteSpace(e));
                if (odataText is null || !Uri.TryCreate(odataText, UriKind.Absolute, out odata))
                {
                    _logger.LogWarning("Skipping {Database}: no annotation SQL mapping and no OData endpoint.", database);
                    return;
                }
            }

            string root = _configuration["Corrections:RootDirectory"];
            if (string.IsNullOrWhiteSpace(root))
                throw new InvalidOperationException("Corrections:RootDirectory is not configured.");

            string output = Path.Combine(root, VolumeCorrectionCatalog.SanitizeVolumeName(database));
            Directory.CreateDirectory(output);

            IConfigurationSection rebuild = _configuration.GetSection("Rebuild");
            var options = new CorrectionPublishOptions
            {
                OutputDirectory = output,
                CachePath = _configuration["Corrections:CachePath"],
                Force = rebuild.GetValue("Force", false),
                SkipQuiver = rebuild.GetValue("SkipQuiver", false),
                MinLocations = rebuild.GetValue("MinLocations", 3),
                // An unset group follows the global setting, then the VikingXML default.
                StosGroups = string.IsNullOrEmpty(stosGroup) ? rebuild["StosGroups"] : stosGroup,
                PublicVolumeUrl = source.VikingXmlEndpoint,
                Log = message => _logger.LogInformation("{Message}", message)
            };

            _logger.LogInformation("Rebuilding corrections for {Database} / {StosGroup} ({Count} context(s)) into {Output}",
                database, DisplayStos(stosGroup), contexts.Count, output);
            int code = odata is null
                ? await _publisher.PublishVolumeAsync(connection, source.VikingXmlEndpoint, options, cancellationToken).ConfigureAwait(false)
                : await _publisher.PublishVolumeFromODataAsync(odata, source.VikingXmlEndpoint, options, cancellationToken).ConfigureAwait(false);
            if (code != 0)
                throw new InvalidOperationException($"Publisher exited {code} for {database} / {DisplayStos(stosGroup)}.");
        }

        sealed class CorrectionKeyComparer : IEqualityComparer<(string Database, string StosGroup)>
        {
            public static readonly CorrectionKeyComparer Instance = new();

            public bool Equals((string Database, string StosGroup) x, (string Database, string StosGroup) y) =>
                string.Equals(x.Database, y.Database, StringComparison.OrdinalIgnoreCase)
                && string.Equals(x.StosGroup, y.StosGroup, StringComparison.OrdinalIgnoreCase);

            public int GetHashCode((string Database, string StosGroup) key) =>
                HashCode.Combine(
                    StringComparer.OrdinalIgnoreCase.GetHashCode(key.Database),
                    StringComparer.OrdinalIgnoreCase.GetHashCode(key.StosGroup));
        }
    }
}
