using System;
using System.Collections.Generic;
using System.Reflection;
using Google.Protobuf.WellKnownTypes;
using Viking.SectionCorrectionServiceTypes.gRPC.V1.Protos;

namespace Viking.GrpcSectionCorrectionService
{
    /// <summary>
    /// Process-wide last rebuild snapshot for GetRebuildStatus.
    /// </summary>
    /// <remarks>
    /// Registered as a singleton. <see cref="CorrectionRebuildRunner"/> writes it with <see cref="Begin"/> and
    /// <see cref="Complete"/>; <see cref="SectionCorrectionsService"/> reads it with <see cref="Snapshot"/>.
    /// All instance members are thread safe (one internal lock); nothing is persisted, so a restart clears it.
    /// </remarks>
    public sealed class RebuildStatusStore
    {
        readonly object _gate = new();
        bool _inProgress;
        DateTime? _lastStartedUtc;
        DateTime? _lastCompletedUtc;
        string _lastError = "";
        List<string> _lastVolumes = [];

        /// <summary>
        /// Assembly InformationalVersion for this host. Clients key CorrectStructures caches
        /// on field built_utc plus this value so algorithm-only deploys invalidate without
        /// republishing residual fields.
        /// </summary>
        public static string ServiceVersion { get; } = ResolveServiceVersion();

        static string ResolveServiceVersion()
        {
            Assembly asm = typeof(RebuildStatusStore).Assembly;
            string informational = asm.GetCustomAttribute<AssemblyInformationalVersionAttribute>()?.InformationalVersion;
            if (!string.IsNullOrWhiteSpace(informational))
                return informational.Split('+')[0];
            Version version = asm.GetName().Version;
            return version is null ? "0.0.0" : $"{version.Major}.{version.Minor}.{version.Build}";
        }

        /// <summary>
        /// Marks a rebuild as running, stamps the start time, and clears the previous error.
        /// </summary>
        /// <param name="volumeNames">Annotation database names this rebuild covers; copied, must not be null.</param>
        public void Begin(IReadOnlyList<string> volumeNames)
        {
            lock (_gate)
            {
                _inProgress = true;
                _lastStartedUtc = DateTime.UtcNow;
                _lastError = "";
                _lastVolumes = [.. volumeNames];
            }
        }

        /// <summary>
        /// Replaces the database names reported for the running rebuild, once Identity has listed them.
        /// </summary>
        /// <param name="volumeNames">Annotation database names; copied, must not be null.</param>
        public void SetVolumes(IReadOnlyList<string> volumeNames)
        {
            lock (_gate)
                _lastVolumes = [.. volumeNames];
        }

        /// <summary>
        /// Marks the running rebuild as finished and stamps the completion time. Call from a <c>finally</c> so a
        /// failed rebuild does not leave <c>InProgress</c> set.
        /// </summary>
        /// <param name="error">Failure text to report from GetRebuildStatus; null or empty means success.</param>
        public void Complete(string error)
        {
            lock (_gate)
            {
                _inProgress = false;
                _lastCompletedUtc = DateTime.UtcNow;
                _lastError = error ?? "";
            }
        }

        /// <summary>
        /// Copies the current state into a new protobuf message, so callers can return it without further locking.
        /// </summary>
        /// <returns>A status whose timestamps are unset until a rebuild has started or completed.</returns>
        public RebuildStatus Snapshot()
        {
            lock (_gate)
            {
                RebuildStatus status = new()
                {
                    InProgress = _inProgress,
                    LastError = _lastError ?? "",
                    ServiceVersion = ServiceVersion
                };
                if (_lastStartedUtc.HasValue)
                    status.LastStartedUtc = Timestamp.FromDateTime(DateTime.SpecifyKind(_lastStartedUtc.Value, DateTimeKind.Utc));
                if (_lastCompletedUtc.HasValue)
                    status.LastCompletedUtc = Timestamp.FromDateTime(DateTime.SpecifyKind(_lastCompletedUtc.Value, DateTimeKind.Utc));
                status.LastVolumes.AddRange(_lastVolumes);
                return status;
            }
        }
    }
}
