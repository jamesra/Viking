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
        /// executes at a time; a second caller waits for the first. The status shows the run as started even when
        /// listing the contexts fails; a listing failure or cancellation is recorded as the status error and rethrown.
        /// </summary>
        /// <param name="cancellationToken">Stops between groups and inside the publisher.</param>
        public Task RunAsync(CancellationToken cancellationToken) => RunAsync(null, false, cancellationToken);

        /// <summary>
        /// Same as <see cref="RunAsync(CancellationToken)"/>, optionally limited to one volume and optionally forced.
        /// </summary>
        /// <param name="volumeName">Annotation database to rebuild (every StosGroup of it), compared case-insensitively;
        /// null or empty rebuilds all. A name no assigned context uses is recorded as the status error.</param>
        /// <param name="force">Rebuild even when the published set is current, in addition to Rebuild:Force.</param>
        /// <param name="cancellationToken">Stops between groups and inside the publisher.</param>
        public async Task RunAsync(string volumeName, bool force, CancellationToken cancellationToken)
        {
            await _gate.WaitAsync(cancellationToken).ConfigureAwait(false);
            try
            {
                await RunLockedAsync(volumeName, force, cancellationToken).ConfigureAwait(false);
            }
            finally
            {
                _gate.Release();
            }
        }

        /// <summary>
        /// Starts a rebuild on a background task and returns at once, for the manual trigger. Unlike
        /// <see cref="RunAsync(string, bool, CancellationToken)"/> it never queues behind a running rebuild.
        /// Failures are logged and recorded in the status, never thrown to the caller.
        /// </summary>
        /// <param name="volumeName">See <see cref="RunAsync(string, bool, CancellationToken)"/>.</param>
        /// <param name="force">See <see cref="RunAsync(string, bool, CancellationToken)"/>.</param>
        /// <param name="stoppingToken">Host shutdown; not the gRPC call's token, which ends when the call returns.</param>
        /// <returns>False when a rebuild already holds the run slot, so nothing was started.</returns>
        public bool TryStartInBackground(string volumeName, bool force, CancellationToken stoppingToken)
        {
            if (!_gate.Wait(0, CancellationToken.None))
                return false;

            _ = Task.Run(async () =>
            {
                try
                {
                    await RunLockedAsync(volumeName, force, stoppingToken).ConfigureAwait(false);
                }
                catch (OperationCanceledException)
                {
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "Manual section-correction rebuild failed.");
                }
                finally
                {
                    _gate.Release();
                }
            }, CancellationToken.None);
            return true;
        }

        /// <summary>Runs one rebuild; the caller must already hold <c>_gate</c>.</summary>
        async Task RunLockedAsync(string volumeName, bool force, CancellationToken cancellationToken)
        {
            // Begin first so an Identity outage still shows a start time and an error in GetRebuildStatus.
            _status.Begin([]);
            string error = "";
            List<string> failures = [];
            try
            {
                IReadOnlyList<IdentityVolumeRow> rows = await _volumes.ListAsync(cancellationToken).ConfigureAwait(false);
                List<IGrouping<(string Database, string StosGroup), IdentityVolumeRow>> groups = GroupByCorrectionKey(rows);
                if (!string.IsNullOrWhiteSpace(volumeName))
                {
                    groups = [.. groups.Where(g => string.Equals(g.Key.Database, volumeName.Trim(), StringComparison.OrdinalIgnoreCase))];
                    if (groups.Count == 0)
                        failures.Add($"No assigned volume named '{volumeName.Trim()}'.");
                }

                _status.SetVolumes([.. groups.Select(g => g.Key.Database).Distinct(StringComparer.OrdinalIgnoreCase)]);
                _status.SetPlan(groups.Count);
                foreach (IGrouping<(string Database, string StosGroup), IdentityVolumeRow> group in groups)
                {
                    _status.BeginGroup(group.Key.Database);
                    try
                    {
                        await RebuildGroupAsync(group.Key.Database, group.Key.StosGroup, [.. group], force, cancellationToken)
                            .ConfigureAwait(false);
                    }
                    catch (Exception ex) when (ex is not OperationCanceledException)
                    {
                        _logger.LogError(ex, "Section-correction rebuild failed for {Database} / {StosGroup}.",
                            group.Key.Database, DisplayStos(group.Key.StosGroup));
                        failures.Add($"{group.Key.Database}/{DisplayStos(group.Key.StosGroup)}: {ex.Message}");
                    }
                    finally
                    {
                        _status.EndGroup();
                    }
                }

                if (failures.Count > 0)
                    error = string.Join("; ", failures);
            }
            catch (OperationCanceledException)
            {
                failures.Add("Rebuild cancelled before it finished.");
                error = string.Join("; ", failures);
                throw;
            }
            catch (Exception ex)
            {
                // Identity listing failed (group failures are handled above); the hosted service logs the rethrow.
                error = $"Rebuild could not start: {ex.Message}";
                throw;
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
            bool force,
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
                Force = force || rebuild.GetValue("Force", false),
                SkipQuiver = rebuild.GetValue("SkipQuiver", false),
                MinLocations = rebuild.GetValue("MinLocations", 3),
                // Edits newer than this wait for a later check, so an editing session is not rebuilt halfway through.
                QuietPeriod = TimeSpan.FromMinutes(Math.Max(0, rebuild.GetValue("QuietMinutes", 60.0))),
                // An unset group follows the global setting, then the VikingXML default.
                StosGroups = string.IsNullOrEmpty(stosGroup) ? rebuild["StosGroups"] : stosGroup,
                PublicVolumeUrl = source.VikingXmlEndpoint,
                Log = message =>
                {
                    _status.SetStep(message);
                    _logger.LogInformation("{Message}", message);
                }
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
