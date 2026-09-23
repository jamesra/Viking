using System;
using System.Collections.Generic;
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
                    LastError = _lastError ?? ""
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
