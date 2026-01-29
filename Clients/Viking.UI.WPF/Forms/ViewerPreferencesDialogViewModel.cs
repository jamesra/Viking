using System;
using System.ComponentModel;
using System.Runtime.CompilerServices;
using System.Windows.Input;

namespace Viking.UI.WPF.Forms
{
    /// <summary>
    /// ViewModel for the Viewer Preferences Dialog
    /// </summary>
    public class ViewerPreferencesDialogViewModel : INotifyPropertyChanged
    {
        public event PropertyChangedEventHandler PropertyChanged;

        #region Constants
        /// <summary>
        /// Minimum allowed acceleration (in screen-height units)
        /// </summary>
        public const double MinAcceleration = 0.01;

        /// <summary>
        /// Maximum allowed acceleration (in screen-height units)
        /// </summary>
        public const double MaxAcceleration = 1.0;

        /// <summary>
        /// Default acceleration value
        /// </summary>
        public const double DefaultAcceleration = 0.1;

        /// <summary>
        /// Default opacity value
        /// </summary>
        public const double DefaultOpacity = 0.7;

        /// <summary>
        /// Default minimum opacity for non-center section numbers (0 to 0.8)
        /// </summary>
        public const double DefaultMinOpacityNonCenter = 0.5;
        public const double MaxMinOpacityNonCenter = 0.8;

        /// <summary>
        /// Default center magnification for section number overlay
        /// </summary>
        public const double DefaultCenterMagnification = 3.0;

        /// <summary>
        /// Min/max center magnification (1 to 4)
        /// </summary>
        public const double MinCenterMagnification = 1.0;
        public const double MaxCenterMagnification = 4.0;

        // PID Parameter Defaults
        public const double DefaultPidProportionalGain = 4.0;
        public const double DefaultPidDerivativeGain = 1.0;
        public const double DefaultPidIntegralGain = 0.01;
        public const double DefaultPidVelocityThreshold = 0.5;
        public const double DefaultPidPositionThreshold = 0.5;
        #endregion

        #region Original Values (for Cancel revert)
        private bool _originalSectionNumberOverlayEnabled;
        private int _originalSectionNumberOverlayCount;
        private double _originalSectionNumberOverlayAcceleration;
        private string _originalSectionNumberOverlayEdge;
        private double _originalSectionNumberOverlayOpacity;
        private double _originalSectionNumberOverlayMinOpacityNonCenter;
        private double _originalSectionNumberOverlayCenterMagnification;
        private double _originalPidProportionalGain;
        private double _originalPidDerivativeGain;
        private double _originalPidIntegralGain;
        private double _originalPidVelocityThreshold;
        private double _originalPidPositionThreshold;
        #endregion

        #region Section Number Overlay Properties

        private bool _sectionNumberOverlayEnabled;
        public bool SectionNumberOverlayEnabled
        {
            get => _sectionNumberOverlayEnabled;
            set
            {
                if (_sectionNumberOverlayEnabled != value)
                {
                    _sectionNumberOverlayEnabled = value;
                    OnPropertyChanged();
                    SectionNumberOverlaySettingsChanged?.Invoke();
                }
            }
        }

        private int _sectionNumberOverlayCount;
        public int SectionNumberOverlayCount
        {
            get => _sectionNumberOverlayCount;
            set
            {
                if (_sectionNumberOverlayCount != value)
                {
                    _sectionNumberOverlayCount = Clamp(value, 1, 20);
                    OnPropertyChanged();
                    SectionNumberOverlaySettingsChanged?.Invoke();
                }
            }
        }

        private double _sectionNumberOverlayAcceleration;
        public double SectionNumberOverlayAcceleration
        {
            get => _sectionNumberOverlayAcceleration;
            set
            {
                // Clamp to valid range
                double clampedValue = Clamp(value, MinAcceleration, MaxAcceleration);
                if (Math.Abs(_sectionNumberOverlayAcceleration - clampedValue) > 0.0001)
                {
                    _sectionNumberOverlayAcceleration = clampedValue;
                    OnPropertyChanged();
                    SectionNumberOverlaySettingsChanged?.Invoke();
                }
            }
        }

        private string _sectionNumberOverlayEdge;
        public string SectionNumberOverlayEdge
        {
            get => _sectionNumberOverlayEdge;
            set
            {
                if (_sectionNumberOverlayEdge != value)
                {
                    _sectionNumberOverlayEdge = value;
                    OnPropertyChanged();
                    OnPropertyChanged(nameof(IsLeftEdge));
                    OnPropertyChanged(nameof(IsRightEdge));
                    SectionNumberOverlaySettingsChanged?.Invoke();
                }
            }
        }

        public bool IsLeftEdge
        {
            get => string.Equals(_sectionNumberOverlayEdge, "Left", StringComparison.OrdinalIgnoreCase);
            set
            {
                if (value)
                    SectionNumberOverlayEdge = "Left";
            }
        }

        public bool IsRightEdge
        {
            get => string.Equals(_sectionNumberOverlayEdge, "Right", StringComparison.OrdinalIgnoreCase);
            set
            {
                if (value)
                    SectionNumberOverlayEdge = "Right";
            }
        }

        private double _sectionNumberOverlayOpacity;
        public double SectionNumberOverlayOpacity
        {
            get => _sectionNumberOverlayOpacity;
            set
            {
                // Clamp to valid range (0.0 to 1.0)
                double clampedValue = Clamp(value, 0.0, 1.0);
                if (Math.Abs(_sectionNumberOverlayOpacity - clampedValue) > 0.001)
                {
                    _sectionNumberOverlayOpacity = clampedValue;
                    OnPropertyChanged();
                    SectionNumberOverlaySettingsChanged?.Invoke();
                }
            }
        }

        private double _sectionNumberOverlayMinOpacityNonCenter;
        public double SectionNumberOverlayMinOpacityNonCenter
        {
            get => _sectionNumberOverlayMinOpacityNonCenter;
            set
            {
                double clampedValue = Clamp(value, 0.0, MaxMinOpacityNonCenter);
                if (Math.Abs(_sectionNumberOverlayMinOpacityNonCenter - clampedValue) > 0.001)
                {
                    _sectionNumberOverlayMinOpacityNonCenter = clampedValue;
                    OnPropertyChanged();
                    SectionNumberOverlaySettingsChanged?.Invoke();
                }
            }
        }

        private double _sectionNumberOverlayCenterMagnification;
        public double SectionNumberOverlayCenterMagnification
        {
            get => _sectionNumberOverlayCenterMagnification;
            set
            {
                double clampedValue = Clamp(value, MinCenterMagnification, MaxCenterMagnification);
                if (Math.Abs(_sectionNumberOverlayCenterMagnification - clampedValue) > 0.001)
                {
                    _sectionNumberOverlayCenterMagnification = clampedValue;
                    OnPropertyChanged();
                    SectionNumberOverlaySettingsChanged?.Invoke();
                }
            }
        }

        #endregion

        #region PID Parameters

        private double _pidProportionalGain;
        public double PidProportionalGain
        {
            get => _pidProportionalGain;
            set
            {
                double clampedValue = Clamp(value, 0.1, 50.0);
                if (Math.Abs(_pidProportionalGain - clampedValue) > 0.001)
                {
                    _pidProportionalGain = clampedValue;
                    OnPropertyChanged();
                    SectionNumberOverlaySettingsChanged?.Invoke();
                }
            }
        }

        private double _pidDerivativeGain;
        public double PidDerivativeGain
        {
            get => _pidDerivativeGain;
            set
            {
                double clampedValue = Clamp(value, 0.0, 20.0);
                if (Math.Abs(_pidDerivativeGain - clampedValue) > 0.001)
                {
                    _pidDerivativeGain = clampedValue;
                    OnPropertyChanged();
                    SectionNumberOverlaySettingsChanged?.Invoke();
                }
            }
        }

        private double _pidIntegralGain;
        public double PidIntegralGain
        {
            get => _pidIntegralGain;
            set
            {
                double clampedValue = Clamp(value, 0.0, 1.0);
                if (Math.Abs(_pidIntegralGain - clampedValue) > 0.0001)
                {
                    _pidIntegralGain = clampedValue;
                    OnPropertyChanged();
                    SectionNumberOverlaySettingsChanged?.Invoke();
                }
            }
        }

        private double _pidVelocityThreshold;
        public double PidVelocityThreshold
        {
            get => _pidVelocityThreshold;
            set
            {
                double clampedValue = Clamp(value, 0.01, 0.05);
                if (Math.Abs(_pidVelocityThreshold - clampedValue) > 0.001)
                {
                    _pidVelocityThreshold = clampedValue;
                    OnPropertyChanged();
                    SectionNumberOverlaySettingsChanged?.Invoke();
                }
            }
        }

        private double _pidPositionThreshold;
        public double PidPositionThreshold
        {
            get => _pidPositionThreshold;
            set
            {
                double clampedValue = Clamp(value, 0.01, 1.0);
                if (Math.Abs(_pidPositionThreshold - clampedValue) > 0.001)
                {
                    _pidPositionThreshold = clampedValue;
                    OnPropertyChanged();
                    SectionNumberOverlaySettingsChanged?.Invoke();
                }
            }
        }

        #endregion

        #region Events

        /// <summary>
        /// Fired when any section number overlay setting changes (for real-time preview)
        /// </summary>
        public event Action SectionNumberOverlaySettingsChanged;

        #endregion

        #region Commands

        public ICommand ResetToDefaultsCommand { get; }

        #endregion

        public ViewerPreferencesDialogViewModel()
        {
            ResetToDefaultsCommand = new RelayCommand(ResetToDefaults);
        }

        /// <summary>
        /// Load current settings into the ViewModel
        /// </summary>
        public void LoadCurrentSettings(
            bool sectionNumberOverlayEnabled,
            int sectionNumberOverlayCount,
            double sectionNumberOverlayAcceleration,
            string sectionNumberOverlayEdge,
            double sectionNumberOverlayOpacity,
            double sectionNumberOverlayMinOpacityNonCenter,
            double sectionNumberOverlayCenterMagnification,
            double pidProportionalGain,
            double pidDerivativeGain,
            double pidIntegralGain,
            double pidVelocityThreshold,
            double pidPositionThreshold)
        {
            // Clamp values to valid ranges (in case saved value is from old settings)
            double clampedAcceleration = Clamp(sectionNumberOverlayAcceleration, MinAcceleration, MaxAcceleration);
            double clampedOpacity = Clamp(sectionNumberOverlayOpacity, 0.0, 1.0);
            double clampedMinOpacityNonCenter = Clamp(sectionNumberOverlayMinOpacityNonCenter, 0.0, MaxMinOpacityNonCenter);
            double clampedCenterMagnification = Clamp(sectionNumberOverlayCenterMagnification, MinCenterMagnification, MaxCenterMagnification);
            double clampedVelocityThreshold = Clamp(pidVelocityThreshold, 0.01, 0.05);
            double clampedPositionThreshold = Clamp(pidPositionThreshold, 0.01, 0.05);

            // Store original values for Cancel revert (use clamped values)
            _originalSectionNumberOverlayEnabled = sectionNumberOverlayEnabled;
            _originalSectionNumberOverlayCount = sectionNumberOverlayCount;
            _originalSectionNumberOverlayAcceleration = clampedAcceleration;
            _originalSectionNumberOverlayEdge = sectionNumberOverlayEdge;
            _originalSectionNumberOverlayOpacity = clampedOpacity;
            _originalSectionNumberOverlayMinOpacityNonCenter = clampedMinOpacityNonCenter;
            _originalSectionNumberOverlayCenterMagnification = clampedCenterMagnification;
            _originalPidProportionalGain = pidProportionalGain;
            _originalPidDerivativeGain = pidDerivativeGain;
            _originalPidIntegralGain = pidIntegralGain;
            _originalPidVelocityThreshold = clampedVelocityThreshold;
            _originalPidPositionThreshold = clampedPositionThreshold;

            // Set current values (without triggering change events during load)
            var tempHandler = SectionNumberOverlaySettingsChanged;
            SectionNumberOverlaySettingsChanged = null;

            _sectionNumberOverlayEnabled = sectionNumberOverlayEnabled;
            _sectionNumberOverlayCount = sectionNumberOverlayCount;
            _sectionNumberOverlayAcceleration = clampedAcceleration;
            _sectionNumberOverlayEdge = sectionNumberOverlayEdge;
            _sectionNumberOverlayOpacity = clampedOpacity;
            _sectionNumberOverlayMinOpacityNonCenter = clampedMinOpacityNonCenter;
            _sectionNumberOverlayCenterMagnification = clampedCenterMagnification;
            _pidProportionalGain = pidProportionalGain;
            _pidDerivativeGain = pidDerivativeGain;
            _pidIntegralGain = pidIntegralGain;
            _pidVelocityThreshold = clampedVelocityThreshold;
            _pidPositionThreshold = clampedPositionThreshold;

            SectionNumberOverlaySettingsChanged = tempHandler;

            OnPropertyChanged(string.Empty); // Notify all properties changed
        }

        /// <summary>
        /// Called when Apply is clicked - updates original values to current
        /// </summary>
        public void Apply()
        {
            _originalSectionNumberOverlayEnabled = _sectionNumberOverlayEnabled;
            _originalSectionNumberOverlayCount = _sectionNumberOverlayCount;
            _originalSectionNumberOverlayAcceleration = _sectionNumberOverlayAcceleration;
            _originalSectionNumberOverlayEdge = _sectionNumberOverlayEdge;
            _originalSectionNumberOverlayOpacity = _sectionNumberOverlayOpacity;
            _originalSectionNumberOverlayMinOpacityNonCenter = _sectionNumberOverlayMinOpacityNonCenter;
            _originalSectionNumberOverlayCenterMagnification = _sectionNumberOverlayCenterMagnification;
            _originalPidProportionalGain = _pidProportionalGain;
            _originalPidDerivativeGain = _pidDerivativeGain;
            _originalPidIntegralGain = _pidIntegralGain;
            _originalPidVelocityThreshold = _pidVelocityThreshold;
            _originalPidPositionThreshold = _pidPositionThreshold;
        }

        /// <summary>
        /// Revert to original values (for Cancel)
        /// </summary>
        public void RevertToOriginal()
        {
            _sectionNumberOverlayEnabled = _originalSectionNumberOverlayEnabled;
            _sectionNumberOverlayCount = _originalSectionNumberOverlayCount;
            _sectionNumberOverlayAcceleration = _originalSectionNumberOverlayAcceleration;
            _sectionNumberOverlayEdge = _originalSectionNumberOverlayEdge;
            _sectionNumberOverlayOpacity = _originalSectionNumberOverlayOpacity;
            _pidProportionalGain = _originalPidProportionalGain;
            _pidDerivativeGain = _originalPidDerivativeGain;
            _pidIntegralGain = _originalPidIntegralGain;
            _pidVelocityThreshold = _originalPidVelocityThreshold;
            _pidPositionThreshold = _originalPidPositionThreshold;

            OnPropertyChanged(string.Empty);
        }

        /// <summary>
        /// Reset all settings to their defaults
        /// </summary>
        public void ResetToDefaults()
        {
            SectionNumberOverlayEnabled = true;
            SectionNumberOverlayCount = 4;
            SectionNumberOverlayAcceleration = DefaultAcceleration;
            SectionNumberOverlayEdge = "Left";
            SectionNumberOverlayOpacity = DefaultOpacity;
            SectionNumberOverlayMinOpacityNonCenter = DefaultMinOpacityNonCenter;
            SectionNumberOverlayCenterMagnification = DefaultCenterMagnification;
            PidProportionalGain = DefaultPidProportionalGain;
            PidDerivativeGain = DefaultPidDerivativeGain;
            PidIntegralGain = DefaultPidIntegralGain;
            PidVelocityThreshold = DefaultPidVelocityThreshold;
            PidPositionThreshold = DefaultPidPositionThreshold;
        }

        protected virtual void OnPropertyChanged([CallerMemberName] string propertyName = null)
        {
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
        }

        private static int Clamp(int value, int min, int max)
        {
            if (value < min) return min;
            if (value > max) return max;
            return value;
        }

        private static double Clamp(double value, double min, double max)
        {
            if (value < min) return min;
            if (value > max) return max;
            return value;
        }
    }

    /// <summary>
    /// Simple RelayCommand implementation for ICommand
    /// </summary>
    public class RelayCommand : ICommand
    {
        private readonly Action _execute;
        private readonly Func<bool> _canExecute;

        public RelayCommand(Action execute, Func<bool> canExecute = null)
        {
            _execute = execute ?? throw new ArgumentNullException(nameof(execute));
            _canExecute = canExecute;
        }

        public event EventHandler CanExecuteChanged
        {
            add { }
            remove { }
        }

        public bool CanExecute(object parameter) => _canExecute is null || _canExecute();

        public void Execute(object parameter) => _execute();
    }
}
