using System;
using System.Threading;
using System.Threading.Tasks;
using Cronos;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Viking.SectionCorrection;

namespace Viking.GrpcSectionCorrectionService
{
    /// <summary>
    /// Serves the catalog already loaded at startup, then checks for updates in the background.
    /// After that it checks every Rebuild:CheckInterval (default 24 h) and rebuilds each group whose annotations or stos
    /// changed, once the newest edit is Rebuild:QuietMinutes old. Rebuild:Cron (default Sunday 02:00 UTC) forces a
    /// rebuild of everything as a safety net.
    /// One volume at a time via CorrectionRebuildRunner. gRPC keeps reading the previous publish
    /// until a new directory is in place.
    /// </summary>
    public sealed class CorrectionRebuildHostedService : BackgroundService
    {
        readonly CorrectionRebuildRunner _runner;
        readonly VolumeCorrectionCatalog _catalog;
        readonly IHostApplicationLifetime _lifetime;
        readonly IConfiguration _configuration;
        readonly ILogger<CorrectionRebuildHostedService> _logger;

        public CorrectionRebuildHostedService(
            CorrectionRebuildRunner runner,
            VolumeCorrectionCatalog catalog,
            IHostApplicationLifetime lifetime,
            IConfiguration configuration,
            ILogger<CorrectionRebuildHostedService> logger)
        {
            _runner = runner;
            _catalog = catalog;
            _lifetime = lifetime;
            _configuration = configuration;
            _logger = logger;
        }

        protected override async Task ExecuteAsync(CancellationToken stoppingToken)
        {
            try
            {
                await WaitUntilServingAsync(stoppingToken).ConfigureAwait(false);
            }
            catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
            {
                return;
            }

            _logger.LogInformation(
                "Serving {Count} cached correction sets. Update checks run in the background.",
                _catalog.Sets.Count);

            IConfigurationSection rebuild = _configuration.GetSection("Rebuild");
            if (rebuild.GetValue("RunOnStartup", true))
            {
                try
                {
                    await _runner.RunAsync(stoppingToken).ConfigureAwait(false);
                }
                catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
                {
                    return;
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "Startup section-correction rebuild failed; waiting for the next cron.");
                }
            }

            string cronText = rebuild["Cron"];
            if (string.IsNullOrWhiteSpace(cronText))
                cronText = "0 2 * * 0";

            // Zero disables the periodic check, leaving only the forced full rebuild on the cron.
            TimeSpan interval = rebuild.GetValue("CheckInterval", TimeSpan.FromHours(24));
            CronExpression cron = CronExpression.Parse(cronText);
            DateTimeOffset nextCheck = interval > TimeSpan.Zero ? DateTimeOffset.UtcNow + interval : DateTimeOffset.MaxValue;
            while (!stoppingToken.IsCancellationRequested)
            {
                DateTimeOffset now = DateTimeOffset.UtcNow;
                DateTimeOffset? nextFull = cron.GetNextOccurrence(now, TimeZoneInfo.Utc);
                if (nextFull is null && nextCheck == DateTimeOffset.MaxValue)
                {
                    _logger.LogWarning("Rebuild cron {Cron} has no next occurrence and no CheckInterval is set.", cronText);
                    return;
                }

                // The cron run is a forced full rebuild, the safety net for changes the section fingerprints cannot see.
                bool full = nextFull is not null && nextFull.Value <= nextCheck;
                DateTimeOffset next = full ? nextFull.Value : nextCheck;
                TimeSpan delay = next - now;
                if (delay < TimeSpan.Zero)
                    delay = TimeSpan.Zero;
                _logger.LogInformation(
                    "Next section-correction {Kind} at {When:u}", full ? "full rebuild" : "update check", next.UtcDateTime);
                try
                {
                    await Task.Delay(delay, stoppingToken).ConfigureAwait(false);
                    if (!full)
                        nextCheck = DateTimeOffset.UtcNow + interval;
                    await _runner.RunAsync(null, full, stoppingToken).ConfigureAwait(false);
                    if (full && interval > TimeSpan.Zero)
                        nextCheck = DateTimeOffset.UtcNow + interval;
                }
                catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
                {
                    return;
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "Scheduled section-correction rebuild failed.");
                }
            }
        }

        /// <summary>
        /// Returns once Kestrel is accepting requests so a startup rebuild cannot delay the first
        /// read of the catalog loaded in Startup.
        /// </summary>
        async Task WaitUntilServingAsync(CancellationToken stoppingToken)
        {
            if (_lifetime.ApplicationStarted.IsCancellationRequested)
                return;

            TaskCompletionSource started = new(TaskCreationOptions.RunContinuationsAsynchronously);
            using CancellationTokenRegistration appReg =
                _lifetime.ApplicationStarted.Register(() => started.TrySetResult());
            using CancellationTokenRegistration stopReg =
                stoppingToken.Register(() => started.TrySetCanceled(stoppingToken));
            await started.Task.ConfigureAwait(false);
        }
    }
}
