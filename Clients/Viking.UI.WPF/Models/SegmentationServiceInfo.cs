using System;
using System.ComponentModel;
using System.Globalization;

namespace Viking.UI.WPF.Models
{
    /// <summary>
    /// A segmentation service from Identity plus live GetServerStatus probe results used by the picker.
    /// </summary>
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
        private string _statusSummary = "not checked";

        public long Id
        {
            get => _id;
            set
            {
                if (_id != value)
                {
                    _id = value;
                    OnPropertyChanged(nameof(Id));
                }
            }
        }

        public string Name
        {
            get => _name;
            set
            {
                if (_name != value)
                {
                    _name = value;
                    OnPropertyChanged(nameof(Name));
                }
            }
        }

        public string Description
        {
            get => _description;
            set
            {
                if (_description != value)
                {
                    _description = value;
                    OnPropertyChanged(nameof(Description));
                }
            }
        }

        public string Endpoint
        {
            get => _endpoint;
            set
            {
                if (_endpoint != value)
                {
                    _endpoint = value;
                    OnPropertyChanged(nameof(Endpoint));
                }
            }
        }

        public bool IsProbing
        {
            get => _isProbing;
            set
            {
                if (_isProbing != value)
                {
                    _isProbing = value;
                    OnPropertyChanged(nameof(IsProbing));
                    RefreshStatusSummary();
                }
            }
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
                    RefreshStatusSummary();
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
                    RefreshStatusSummary();
                }
            }
        }

        /// <summary>True after a completed probe that failed. Unreachable rows stay selectable but are grayed out.</summary>
        public bool IsUnreachable => HasProbeCompleted && !IsReachable;

        public bool IsPreferred
        {
            get => _isPreferred;
            set
            {
                if (_isPreferred != value)
                {
                    _isPreferred = value;
                    OnPropertyChanged(nameof(IsPreferred));
                    RefreshStatusSummary();
                }
            }
        }

        public int PreferenceRank
        {
            get => _preferenceRank;
            set
            {
                if (_preferenceRank != value)
                {
                    _preferenceRank = value;
                    OnPropertyChanged(nameof(PreferenceRank));
                    RefreshStatusSummary();
                }
            }
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
                    RefreshStatusSummary();
                }
            }
        }

        public uint InFlightRequests
        {
            get => _inFlightRequests;
            set
            {
                if (_inFlightRequests != value)
                {
                    _inFlightRequests = value;
                    OnPropertyChanged(nameof(InFlightRequests));
                    RefreshStatusSummary();
                }
            }
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
                    RefreshStatusSummary();
                }
            }
        }

        public uint InferenceWorkers
        {
            get => _inferenceWorkers;
            set
            {
                if (_inferenceWorkers != value)
                {
                    _inferenceWorkers = value;
                    OnPropertyChanged(nameof(InferenceWorkers));
                }
            }
        }

        public string Version
        {
            get => _version;
            set
            {
                if (_version != value)
                {
                    _version = value;
                    OnPropertyChanged(nameof(Version));
                }
            }
        }

        public string ServerMessage
        {
            get => _serverMessage;
            set
            {
                if (_serverMessage != value)
                {
                    _serverMessage = value;
                    OnPropertyChanged(nameof(ServerMessage));
                }
            }
        }

        public string StatusSummary
        {
            get => _statusSummary;
            private set
            {
                if (_statusSummary != value)
                {
                    _statusSummary = value;
                    OnPropertyChanged(nameof(StatusSummary));
                }
            }
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

        private void RefreshStatusSummary()
        {
            if (IsProbing)
            {
                StatusSummary = "checking…";
                return;
            }

            if (IsUnreachable)
            {
                StatusSummary = "unreachable";
                return;
            }

            if (!IsReachable)
            {
                StatusSummary = "not checked";
                return;
            }

            string rank = PreferenceRank > 0 ? $"#{PreferenceRank} · " : string.Empty;
            string preferred = IsPreferred ? "preferred · " : rank;
            StatusSummary = string.Format(
                CultureInfo.InvariantCulture,
                "{0}{1:0} ms RTT · {2} in flight · {3:0} ms infer",
                preferred,
                ProbeLatencyMs,
                InFlightRequests,
                RecentLatencyMs);
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
