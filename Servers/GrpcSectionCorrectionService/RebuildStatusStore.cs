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
        string _currentVolume = "";
        string _currentStep = "";
        int _groupsDone;
        int _groupsTotal;

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
        public void Begin()
        {
            lock (_gate)
            {
                _inProgress = true;
                _lastStartedUtc = DateTime.UtcNow;
                _lastError = "";
                _currentVolume = "";
                _currentStep = "";
                _groupsDone = 0;
                _groupsTotal = 0;
            }
        }

        /// <summary>
        /// Records how many correction sets the running rebuild will build, once Identity has listed them.
        /// </summary>
        /// <param name="groupsTotal">Number of (database, StosGroup) groups in this run.</param>
        public void SetPlan(int groupsTotal)
        {
            lock (_gate)
                _groupsTotal = groupsTotal;
        }

        /// <summary>
        /// Notes the group now being built and clears the previous group's step message.
        /// </summary>
        /// <param name="volumeName">Annotation database name of the group.</param>
        public void BeginGroup(string volumeName)
        {
            lock (_gate)
            {
                _currentVolume = volumeName ?? "";
                _currentStep = "";
            }
        }

        /// <summary>
        /// Stores the latest builder message for the current group. Called from the publisher's log callback,
        /// possibly on a worker thread.
        /// </summary>
        /// <param name="step">Message text; null is treated as empty.</param>
        public void SetStep(string step)
        {
            lock (_gate)
                _currentStep = step ?? "";
        }

        /// <summary>
        /// Counts a group as processed, whether it succeeded, was skipped or failed.
        /// </summary>
        public void EndGroup()
        {
            lock (_gate)
                _groupsDone++;
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
                _currentVolume = "";
                _currentStep = "";
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
                    ServiceVersion = ServiceVersion,
                    CurrentVolume = _currentVolume ?? "",
                    CurrentStep = _currentStep ?? "",
                    GroupsDone = _groupsDone,
                    GroupsTotal = _groupsTotal
                };
                if (_lastStartedUtc.HasValue)
                    status.LastStartedUtc = Timestamp.FromDateTime(DateTime.SpecifyKind(_lastStartedUtc.Value, DateTimeKind.Utc));
                if (_lastCompletedUtc.HasValue)
                    status.LastCompletedUtc = Timestamp.FromDateTime(DateTime.SpecifyKind(_lastCompletedUtc.Value, DateTimeKind.Utc));
                return status;
            }
        }
    }
}
