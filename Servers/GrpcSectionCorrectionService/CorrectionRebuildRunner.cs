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
    /// One-volume-at-a-time rebuild of every Identity volume that has a VikingXML Endpoint and
    /// an annotation SQL mapping. Used by the hosted service and by unit tests with mocks.
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

        public async Task RunAsync(CancellationToken cancellationToken)
        {
            await _gate.WaitAsync(cancellationToken).ConfigureAwait(false);
            try
            {
                IReadOnlyList<IdentityVolumeRow> volumes = await _volumes.ListAsync(cancellationToken).ConfigureAwait(false);
                _status.Begin([.. volumes.Select(v => v.Name)]);
                string error = "";
                try
                {
                    List<string> failures = [];
                    foreach (IdentityVolumeRow volume in volumes)
                    {
                        try
                        {
                            await RebuildVolumeAsync(volume, cancellationToken).ConfigureAwait(false);
                        }
                        catch (Exception ex) when (ex is not OperationCanceledException)
                        {
                            _logger.LogError(ex, "Section-correction rebuild failed for volume {Volume}.", volume.Name);
                            failures.Add($"{volume.Name}: {ex.Message}");
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

        async Task RebuildVolumeAsync(IdentityVolumeRow volume, CancellationToken cancellationToken)
        {
            if (string.IsNullOrWhiteSpace(volume.Name))
            {
                _logger.LogWarning("Skipping Identity volume with empty name.");
                return;
            }

            if (string.IsNullOrWhiteSpace(volume.VikingXmlEndpoint))
            {
                _logger.LogWarning("Skipping volume {Volume}: missing VikingXML Endpoint.", volume.Name);
                return;
            }

            string connection = _connections.Resolve(volume.Name);
            if (string.IsNullOrWhiteSpace(connection))
            {
                _logger.LogWarning("Skipping volume {Volume}: no annotation SQL mapping.", volume.Name);
                return;
            }

            string folder = VolumeCorrectionCatalog.SanitizeVolumeName(volume.Name);
            string root = _configuration["Corrections:RootDirectory"];
            if (string.IsNullOrWhiteSpace(root))
                throw new InvalidOperationException("Corrections:RootDirectory is not configured.");

            string output = Path.Combine(root, folder);
            Directory.CreateDirectory(output);

            IConfigurationSection rebuild = _configuration.GetSection("Rebuild");
            var options = new CorrectionPublishOptions
            {
                OutputDirectory = output,
                CachePath = _configuration["Corrections:CachePath"],
                Force = rebuild.GetValue("Force", false),
                SkipQuiver = rebuild.GetValue("SkipQuiver", false),
                MinLocations = rebuild.GetValue("MinLocations", 3),
                StosGroups = rebuild["StosGroups"],
                PublicVolumeUrl = volume.VikingXmlEndpoint,
                Log = message => _logger.LogInformation("{Message}", message)
            };

            _logger.LogInformation("Rebuilding corrections for {Volume} into {Output}", volume.Name, output);
            int code = await _publisher.PublishVolumeAsync(
                connection, volume.VikingXmlEndpoint, options, cancellationToken).ConfigureAwait(false);
            if (code != 0)
                throw new InvalidOperationException($"Publisher exited {code} for volume {volume.Name}.");
        }
    }
}
