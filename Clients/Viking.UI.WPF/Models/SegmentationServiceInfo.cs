using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Globalization;

namespace Viking.UI.WPF.Models
{
    /// <summary>
    /// A segmentation service from Identity plus live GetServerStatus probe results used by the picker.
    /// </summary>
    /// <remarks>
    /// The picker shows three columns per server: its Identity <see cref="Name"/>, the model it actually serves
    /// (<see cref="ModelSummary"/>, from the server's own <c>ModelInfo</c>, so a stale Identity name cannot hide a
    /// model change), and how busy it is (<see cref="LoadSummary"/>).
    /// </remarks>
    public class SegmentationServiceInfo : INotifyPropertyChanged
    {
        private long _id;
        private string _name;
        private string _description;
        private string _endpoint;
        private bool _isProbing;
        private bool _hasProbeCompleted;
        private bool _isReachable;
        private bool _isPreferred;
        private int _preferenceRank;
        private double _probeLatencyMs;
        private uint _inFlightRequests;
        private double _recentLatencyMs;
        private uint _inferenceWorkers;
        private string _version;
        private string _serverMessage;
        private string _modelId;
        private string _modelVersion;
        private string _modelDisplayName;
        private string _trainingDataSet;
        private string _modelDescription;
        private uint _cachedImages;
        private ulong _cacheMemoryBytes;
        private string _statusSummary = "not checked";
        private string _loadSummary = "not checked";
        private string _loadToolTip = "not checked";
        private string _modelSummary = string.Empty;
        private string _modelToolTip;

        public long Id
        {
            get => _id;
            set => Set(ref _id, value, nameof(Id), refresh: false);
        }

        public string Name
        {
            get => _name;
            set => Set(ref _name, value, nameof(Name), refresh: false);
        }

        public string Description
        {
            get => _description;
            set => Set(ref _description, value, nameof(Description), refresh: false);
        }

        public string Endpoint
        {
            get => _endpoint;
            set => Set(ref _endpoint, value, nameof(Endpoint), refresh: false);
        }

        public bool IsProbing
        {
            get => _isProbing;
            set => Set(ref _isProbing, value, nameof(IsProbing));
        }

        public bool HasProbeCompleted
        {
            get => _hasProbeCompleted;
            set
            {
                if (_hasProbeCompleted != value)
                {
                    _hasProbeCompleted = value;
                    OnPropertyChanged(nameof(HasProbeCompleted));
                    OnPropertyChanged(nameof(IsUnreachable));
                    RefreshSummaries();
                }
            }
        }

        public bool IsReachable
        {
            get => _isReachable;
            set
            {
                if (_isReachable != value)
                {
                    _isReachable = value;
                    OnPropertyChanged(nameof(IsReachable));
                    OnPropertyChanged(nameof(IsUnreachable));
                    RefreshSummaries();
                }
            }
        }

        /// <summary>True after a completed probe that failed. Unreachable rows stay selectable but are grayed out.</summary>
        public bool IsUnreachable => HasProbeCompleted && !IsReachable;

        public bool IsPreferred
        {
            get => _isPreferred;
            set => Set(ref _isPreferred, value, nameof(IsPreferred));
        }

        public int PreferenceRank
        {
            get => _preferenceRank;
            set => Set(ref _preferenceRank, value, nameof(PreferenceRank));
        }

        public double ProbeLatencyMs
        {
            get => _probeLatencyMs;
            set
            {
                if (Math.Abs(_probeLatencyMs - value) > double.Epsilon)
                {
                    _probeLatencyMs = value;
                    OnPropertyChanged(nameof(ProbeLatencyMs));
                    RefreshSummaries();
                }
            }
        }

        public uint InFlightRequests
        {
            get => _inFlightRequests;
            set => Set(ref _inFlightRequests, value, nameof(InFlightRequests));
        }

        public double RecentLatencyMs
        {
            get => _recentLatencyMs;
            set
            {
                if (Math.Abs(_recentLatencyMs - value) > double.Epsilon)
                {
                    _recentLatencyMs = value;
                    OnPropertyChanged(nameof(RecentLatencyMs));
                    RefreshSummaries();
                }
            }
        }

        public uint InferenceWorkers
        {
            get => _inferenceWorkers;
            set => Set(ref _inferenceWorkers, value, nameof(InferenceWorkers));
        }

        public string Version
        {
            get => _version;
            set => Set(ref _version, value, nameof(Version));
        }

        public string ServerMessage
        {
            get => _serverMessage;
            set => Set(ref _serverMessage, value, nameof(ServerMessage), refresh: false);
        }

        /// <summary>The served model's id from <c>ModelInfo.model_id</c>, e.g. "v5".</summary>
        public string ModelId
        {
            get => _modelId;
            set => Set(ref _modelId, value, nameof(ModelId));
        }

        /// <summary>The served model and epoch from <c>ModelInfo.model_version</c>, e.g. "v5.3".</summary>
        public string ModelVersion
        {
            get => _modelVersion;
            set => Set(ref _modelVersion, value, nameof(ModelVersion));
        }

        public string ModelDisplayName
        {
            get => _modelDisplayName;
            set => Set(ref _modelDisplayName, value, nameof(ModelDisplayName));
        }

        /// <summary>The training set the model learned from (<c>ModelInfo.training_data_set</c>), e.g. "v3".</summary>
        public string TrainingDataSet
        {
            get => _trainingDataSet;
            set => Set(ref _trainingDataSet, value, nameof(TrainingDataSet));
        }

        public string ModelDescription
        {
            get => _modelDescription;
            set => Set(ref _modelDescription, value, nameof(ModelDescription));
        }

        public uint CachedImages
        {
            get => _cachedImages;
            set => Set(ref _cachedImages, value, nameof(CachedImages));
        }

        public ulong CacheMemoryBytes
        {
            get => _cacheMemoryBytes;
            set => Set(ref _cacheMemoryBytes, value, nameof(CacheMemoryBytes));
        }

        /// <summary>Probe state, rank and timings in one line (the Load column's tooltip).</summary>
        public string StatusSummary
        {
            get => _statusSummary;
            private set => SetDerived(ref _statusSummary, value, nameof(StatusSummary));
        }

        /// <summary>The Load column: "idle", "busy · 1 running", "busy · 1 running, 2 queued", or "offline".</summary>
        public string LoadSummary
        {
            get => _loadSummary;
            private set => SetDerived(ref _loadSummary, value, nameof(LoadSummary));
        }

        public string LoadToolTip
        {
            get => _loadToolTip;
            private set => SetDerived(ref _loadToolTip, value, nameof(LoadToolTip));
        }

        /// <summary>The Model column: the model and epoch the server reports, e.g. "v5.3 · set v3".</summary>
        public string ModelSummary
        {
            get => _modelSummary;
            private set => SetDerived(ref _modelSummary, value, nameof(ModelSummary));
        }

        public string ModelToolTip
        {
            get => _modelToolTip;
            private set => SetDerived(ref _modelToolTip, value, nameof(ModelToolTip));
        }

        /// <summary>Lower is better. Unreachable servers sort last.</summary>
        public double PreferenceScore
        {
            get
            {
                if (!IsReachable)
                {
                    return double.MaxValue;
                }

                return InFlightRequests * 1_000_000d + RecentLatencyMs * 100d + ProbeLatencyMs;
            }
        }

        public event PropertyChangedEventHandler PropertyChanged;

        protected virtual void OnPropertyChanged(string propertyName) => PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));

        private void Set<T>(ref T field, T value, string propertyName, bool refresh = true)
        {
            if (EqualityComparer<T>.Default.Equals(field, value))
            {
                return;
            }

            field = value;
            OnPropertyChanged(propertyName);
            if (refresh)
            {
                RefreshSummaries();
            }
        }

        private void SetDerived(ref string field, string value, string propertyName)
        {
            if (!string.Equals(field, value, StringComparison.Ordinal))
            {
                field = value;
                OnPropertyChanged(propertyName);
            }
        }

        private void RefreshSummaries()
        {
            StatusSummary = BuildStatusSummary();
            LoadSummary = BuildLoadSummary();
            LoadToolTip = BuildLoadToolTip();
            ModelSummary = BuildModelSummary();
            ModelToolTip = BuildModelToolTip();
        }

        private string BuildStatusSummary()
        {
            if (IsProbing)
            {
                return "checking…";
            }

            if (IsUnreachable)
            {
                return "unreachable";
            }

            if (!IsReachable)
            {
                return "not checked";
            }

            string rank = PreferenceRank > 0 ? $"#{PreferenceRank} · " : string.Empty;
            string preferred = IsPreferred ? "preferred · " : rank;
            return string.Format(
                CultureInfo.InvariantCulture,
                "{0}{1:0} ms RTT · {2} in flight · {3:0} ms infer",
                preferred,
                ProbeLatencyMs,
                InFlightRequests,
                RecentLatencyMs);
        }

        private string BuildLoadSummary()
        {
            if (IsProbing)
            {
                return "checking…";
            }

            if (IsUnreachable)
            {
                return "offline";
            }

            if (!IsReachable)
            {
                return "not checked";
            }

            // In-flight requests beyond the server's inference workers are waiting their turn.
            uint workers = Math.Max(InferenceWorkers, 1u);
            string busy;
            if (InFlightRequests == 0)
            {
                busy = "idle";
            }
            else if (InFlightRequests <= workers)
            {
                busy = string.Format(CultureInfo.InvariantCulture, "busy · {0} running", InFlightRequests);
            }
            else
            {
                busy = string.Format(
                    CultureInfo.InvariantCulture,
                    "busy · {0} running, {1} queued",
                    workers,
                    InFlightRequests - workers);
            }

            return RecentLatencyMs > 0
                ? string.Format(CultureInfo.InvariantCulture, "{0} · {1:0} ms/request", busy, RecentLatencyMs)
                : busy;
        }

        private string BuildLoadToolTip()
        {
            if (IsUnreachable)
            {
                return "Offline: the server did not answer. It is checked again every few seconds and can be selected once it is back.";
            }

            if (!IsReachable)
            {
                return StatusSummary;
            }

            var lines = new List<string> { StatusSummary };
            if (!string.IsNullOrWhiteSpace(Version))
            {
                lines.Add("Server " + Version);
            }

            lines.Add(string.Format(
                CultureInfo.InvariantCulture,
                "Cache: {0} tiles, {1:0} MiB; {2} inference worker(s)",
                CachedImages,
                CacheMemoryBytes / (1024d * 1024d),
                Math.Max(InferenceWorkers, 1u)));
            return string.Join(Environment.NewLine, lines);
        }

        private string BuildModelSummary()
        {
            if (IsProbing)
            {
                return "…";
            }

            if (IsUnreachable)
            {
                return "—";
            }

            if (!IsReachable)
            {
                return string.Empty;
            }

            string model = FirstNonEmpty(ModelVersion, ModelDisplayName, ModelId);
            if (model == null)
            {
                return "unknown model";
            }

            return string.IsNullOrWhiteSpace(TrainingDataSet) ? model : $"{model} · set {TrainingDataSet}";
        }

        private string BuildModelToolTip()
        {
            if (!IsReachable)
            {
                return null;
            }

            if (FirstNonEmpty(ModelVersion, ModelDisplayName, ModelId) == null)
            {
                return "The server reports no model information (an older server build).";
            }

            var lines = new List<string>();
            if (!string.IsNullOrWhiteSpace(ModelVersion))
            {
                lines.Add("Model " + ModelVersion + (string.IsNullOrWhiteSpace(ModelId) ? string.Empty : $" (id {ModelId})"));
            }

            if (!string.IsNullOrWhiteSpace(ModelDisplayName)
                && !string.Equals(ModelDisplayName, ModelVersion, StringComparison.Ordinal)
                && !string.Equals(ModelDisplayName, ModelId, StringComparison.Ordinal))
            {
                lines.Add(ModelDisplayName);
            }

            if (!string.IsNullOrWhiteSpace(TrainingDataSet))
            {
                lines.Add("Training set " + TrainingDataSet);
            }

            if (!string.IsNullOrWhiteSpace(ModelDescription))
            {
                lines.Add(ModelDescription);
            }

            return string.Join(Environment.NewLine, lines);
        }

        private static string FirstNonEmpty(params string[] values)
        {
            foreach (string value in values)
            {
                if (!string.IsNullOrWhiteSpace(value))
                {
                    return value.Trim();
                }
            }

            return null;
        }

        public override string ToString()
        {
            if (!string.IsNullOrWhiteSpace(Name))
            {
                return Name;
            }

            return !string.IsNullOrWhiteSpace(Endpoint)
                ? Endpoint
                : $"Segmentation Service {Id}";
        }
    }
}
