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

        public void Complete(string error)
        {
            lock (_gate)
            {
                _inProgress = false;
                _lastCompletedUtc = DateTime.UtcNow;
                _lastError = error ?? "";
            }
        }

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
