using System;
using System.ComponentModel;
using System.Runtime.CompilerServices;
using System.Windows.Input;

namespace WebAnnotation.WPF.Forms
{
    public class AnnotationPreferencesDialogViewModel : INotifyPropertyChanged
    {
        public event PropertyChangedEventHandler PropertyChanged;

        // Helper methods for clamping values (Math.Clamp not available in .NET Framework 4.8)
        private static int Clamp(int value, int min, int max) => value < min ? min : (value > max ? max : value);
        private static double Clamp(double value, double min, double max) => value < min ? min : (value > max ? max : value);
        private static float Clamp(float value, double min, double max) => (float)(value < min ? min : (value > max ? max : value));

        #region Original Values (for Cancel revert)
        private int _originalNumSectionsInMemory;
        private int _originalNumSectionsLoading;
        private float _originalLocationTextScaleFactor;
        private float _originalReferenceLocationTextScaleFactor;
        private double _originalDefaultClosedLineWidth;
        private double _originalDefaultLocationJumpDownsample;
        private double _originalAdjacentLocationRadiusScalar;
        private uint _originalNumClosedCurveInterpolationPointsForDisplay;
        private int _originalPenSimplifyThreshold;
        private double _originalMinRadius;
        private double _originalPolygonOpacityParentless;
        private double _originalPolygonOpacityWithParent;
        private double _originalCircleOpacityParentless;
        private double _originalCircleOpacityWithParent;
        private double _originalSegmentationPointRadius;
        private double _originalPolygonPointRadius;
        private double _originalSmallestRenderedSize;
        #endregion

        #region Basic Settings Properties

        private int _numSectionsInMemory;
        public int NumSectionsInMemory
        {
            get => _numSectionsInMemory;
            set
            {
                if (_numSectionsInMemory != value)
                {
                    _numSectionsInMemory = Clamp(value, 1, 100);
                    OnPropertyChanged();
                }
            }
        }

        private int _numSectionsLoading;
        public int NumSectionsLoading
        {
            get => _numSectionsLoading;
            set
            {
                if (_numSectionsLoading != value)
                {
                    _numSectionsLoading = Clamp(value, 1, 50);
                    OnPropertyChanged();
                }
            }
        }

        private float _locationTextScaleFactor;
        public float LocationTextScaleFactor
        {
            get => _locationTextScaleFactor;
            set
            {
                if (Math.Abs(_locationTextScaleFactor - value) > 0.01f)
                {
                    _locationTextScaleFactor = Clamp(value, 0.1, 50.0);
                    OnPropertyChanged();
                }
            }
        }

        private float _referenceLocationTextScaleFactor;
        public float ReferenceLocationTextScaleFactor
        {
            get => _referenceLocationTextScaleFactor;
            set
            {
                if (Math.Abs(_referenceLocationTextScaleFactor - value) > 0.01f)
                {
                    _referenceLocationTextScaleFactor = Clamp(value, 0.1, 50.0);
                    OnPropertyChanged();
                }
            }
        }

        private double _defaultClosedLineWidth;
        public double DefaultClosedLineWidth
        {
            get => _defaultClosedLineWidth;
            set
            {
                if (Math.Abs(_defaultClosedLineWidth - value) > 0.01)
                {
                    _defaultClosedLineWidth = Clamp(value, 1.0, 100.0);
                    OnPropertyChanged();
                }
            }
        }

        #endregion

        #region Advanced Settings Properties

        private double _defaultLocationJumpDownsample;
        public double DefaultLocationJumpDownsample
        {
            get => _defaultLocationJumpDownsample;
            set
            {
                if (Math.Abs(_defaultLocationJumpDownsample - value) > 0.01)
                {
                    _defaultLocationJumpDownsample = Clamp(value, 1.0, 64.0);
                    OnPropertyChanged();
                }
            }
        }

        private double _adjacentLocationRadiusScalar;
        public double AdjacentLocationRadiusScalar
        {
            get => _adjacentLocationRadiusScalar;
            set
            {
                if (Math.Abs(_adjacentLocationRadiusScalar - value) > 0.01)
                {
                    _adjacentLocationRadiusScalar = Clamp(value, 0.1, 2.0);
                    OnPropertyChanged();
                }
            }
        }

        private uint _numClosedCurveInterpolationPointsForDisplay;
        public uint NumClosedCurveInterpolationPointsForDisplay
        {
            get => _numClosedCurveInterpolationPointsForDisplay;
            set
            {
                if (_numClosedCurveInterpolationPointsForDisplay != value)
                {
                    _numClosedCurveInterpolationPointsForDisplay = (uint)Clamp((int)value, 2, 20);
                    OnPropertyChanged();
                }
            }
        }

        private int _penSimplifyThreshold;
        public int PenSimplifyThreshold
        {
            get => _penSimplifyThreshold;
            set
            {
                if (_penSimplifyThreshold != value)
                {
                    _penSimplifyThreshold = Clamp(value, 1, 100);
                    OnPropertyChanged();
                }
            }
        }

        private double _minRadius;
        public double MinRadius
        {
            get => _minRadius;
            set
            {
                if (Math.Abs(_minRadius - value) > 0.01)
                {
                    _minRadius = Clamp(value, 0.1, 10.0);
                    OnPropertyChanged();
                }
            }
        }

        private double _polygonOpacityParentless;
        public double PolygonOpacityParentless
        {
            get => _polygonOpacityParentless;
            set
            {
                double clampedValue = Clamp(value, 0.0, 1.0);
                if (_polygonOpacityParentless != clampedValue)
                {
                    _polygonOpacityParentless = clampedValue;
                    OnPropertyChanged();
                    OnPolygonOpacityChanged();
                }
            }
        }

        private double _polygonOpacityWithParent;
        public double PolygonOpacityWithParent
        {
            get => _polygonOpacityWithParent;
            set
            {
                double clampedValue = Clamp(value, 0.0, 1.0);
                if (_polygonOpacityWithParent != clampedValue)
                {
                    _polygonOpacityWithParent = clampedValue;
                    OnPropertyChanged();
                    OnPolygonOpacityChanged();
                }
            }
        }

        private double _circleOpacityParentless;
        public double CircleOpacityParentless
        {
            get => _circleOpacityParentless;
            set
            {
                double clampedValue = Clamp(value, 0.0, 1.0);
                if (_circleOpacityParentless != clampedValue)
                {
                    _circleOpacityParentless = clampedValue;
                    OnPropertyChanged();
                    OnCircleOpacityChanged();
                }
            }
        }

        private double _circleOpacityWithParent;
        public double CircleOpacityWithParent
        {
            get => _circleOpacityWithParent;
            set
            {
                double clampedValue = Clamp(value, 0.0, 1.0);
                if (_circleOpacityWithParent != clampedValue)
                {
                    _circleOpacityWithParent = clampedValue;
                    OnPropertyChanged();
                    OnCircleOpacityChanged();
                }
            }
        }

        private double _segmentationPointRadius;
        public double SegmentationPointRadius
        {
            get => _segmentationPointRadius;
            set
            {
                if (Math.Abs(_segmentationPointRadius - value) > 0.01)
                {
                    _segmentationPointRadius = Clamp(value, 1.0, 15.0);
                    OnPropertyChanged();
                }
            }
        }

        private double _polygonPointRadius;
        public double PolygonPointRadius
        {
            get => _polygonPointRadius;
            set
            {
                if (Math.Abs(_polygonPointRadius - value) > 0.01)
                {
                    _polygonPointRadius = Clamp(value, 1.0, 50.0);
                    OnPropertyChanged();
                }
            }
        }

        private double _smallestRenderedSize;
        public double SmallestRenderedSize
        {
            get => _smallestRenderedSize;
            set
            {
                if (Math.Abs(_smallestRenderedSize - value) > 0.01)
                {
                    _smallestRenderedSize = Clamp(value, 0.5, 10.0);
                    OnPropertyChanged();
                }
            }
        }

        #endregion

        #region Commands

        public ICommand ResetToDefaultsCommand { get; }
        public ICommand ApplyCommand { get; }

        #endregion

        public AnnotationPreferencesDialogViewModel()
        {
            ResetToDefaultsCommand = new RelayCommand(ResetToDefaults);
            ApplyCommand = new RelayCommand(Apply);
        }

        // Event to notify when polygon opacity changes for real-time preview
        public event Action<double, double> PolygonOpacityChanged;

        private void OnPolygonOpacityChanged() => PolygonOpacityChanged?.Invoke(_polygonOpacityParentless, _polygonOpacityWithParent);

        // Event to notify when circle opacity changes for real-time preview
        public event Action<double, double> CircleOpacityChanged;

        private void OnCircleOpacityChanged() => CircleOpacityChanged?.Invoke(_circleOpacityParentless, _circleOpacityWithParent);

        public void LoadCurrentSettings(
            int numSectionsInMemory,
            int numSectionsLoading,
            float locationTextScaleFactor,
            float referenceLocationTextScaleFactor,
            double defaultClosedLineWidth,
            double defaultLocationJumpDownsample,
            double adjacentLocationRadiusScalar,
            uint numClosedCurveInterpolationPointsForDisplay,
            int penSimplifyThreshold,
            double minRadius,
            double polygonOpacityParentless,
            double polygonOpacityWithParent,
            double circleOpacityParentless,
            double circleOpacityWithParent,
            double segmentationPointRadius,
            double polygonPointRadius,
            double smallestRenderedSize)
        {
            // Store current values
            _numSectionsInMemory = numSectionsInMemory;
            _numSectionsLoading = numSectionsLoading;
            _locationTextScaleFactor = locationTextScaleFactor;
            _referenceLocationTextScaleFactor = referenceLocationTextScaleFactor;
            _defaultClosedLineWidth = defaultClosedLineWidth;
            _defaultLocationJumpDownsample = defaultLocationJumpDownsample;
            _adjacentLocationRadiusScalar = adjacentLocationRadiusScalar;
            _numClosedCurveInterpolationPointsForDisplay = numClosedCurveInterpolationPointsForDisplay;
            _penSimplifyThreshold = penSimplifyThreshold;
            _minRadius = minRadius;
            _segmentationPointRadius = segmentationPointRadius;
            _polygonPointRadius = polygonPointRadius;
            _smallestRenderedSize = smallestRenderedSize;

            // Store original values for Cancel revert BEFORE setting properties
            _originalNumSectionsInMemory = numSectionsInMemory;
            _originalNumSectionsLoading = numSectionsLoading;
            _originalLocationTextScaleFactor = locationTextScaleFactor;
            _originalReferenceLocationTextScaleFactor = referenceLocationTextScaleFactor;
            _originalDefaultClosedLineWidth = defaultClosedLineWidth;
            _originalDefaultLocationJumpDownsample = defaultLocationJumpDownsample;
            _originalAdjacentLocationRadiusScalar = adjacentLocationRadiusScalar;
            _originalNumClosedCurveInterpolationPointsForDisplay = numClosedCurveInterpolationPointsForDisplay;
            _originalPenSimplifyThreshold = penSimplifyThreshold;
            _originalMinRadius = minRadius;
            _originalPolygonOpacityParentless = polygonOpacityParentless;
            _originalPolygonOpacityWithParent = polygonOpacityWithParent;
            _originalCircleOpacityParentless = circleOpacityParentless;
            _originalCircleOpacityWithParent = circleOpacityWithParent;
            _originalSegmentationPointRadius = segmentationPointRadius;
            _originalPolygonPointRadius = polygonPointRadius;
            _originalSmallestRenderedSize = smallestRenderedSize;

            // Use property setters to ensure bindings are established correctly
            // Temporarily disable preview updates during initial load
            var tempPolygonHandler = PolygonOpacityChanged;
            PolygonOpacityChanged = null;
            PolygonOpacityParentless = polygonOpacityParentless;
            PolygonOpacityWithParent = polygonOpacityWithParent;
            PolygonOpacityChanged = tempPolygonHandler;

            var tempCircleHandler = CircleOpacityChanged;
            CircleOpacityChanged = null;
            CircleOpacityParentless = circleOpacityParentless;
            CircleOpacityWithParent = circleOpacityWithParent;
            CircleOpacityChanged = tempCircleHandler;

            SegmentationPointRadius = segmentationPointRadius;
            PolygonPointRadius = polygonPointRadius;
            SmallestRenderedSize = smallestRenderedSize;

            OnPropertyChanged(string.Empty); // Notify all properties changed
        }

        public void Apply()
        {
            // Apply is handled by the caller who has access to Global.AnnotationSettings
            // Update original values to current values after apply
            _originalNumSectionsInMemory = _numSectionsInMemory;
            _originalNumSectionsLoading = _numSectionsLoading;
            _originalLocationTextScaleFactor = _locationTextScaleFactor;
            _originalReferenceLocationTextScaleFactor = _referenceLocationTextScaleFactor;
            _originalDefaultClosedLineWidth = _defaultClosedLineWidth;
            _originalDefaultLocationJumpDownsample = _defaultLocationJumpDownsample;
            _originalAdjacentLocationRadiusScalar = _adjacentLocationRadiusScalar;
            _originalNumClosedCurveInterpolationPointsForDisplay = _numClosedCurveInterpolationPointsForDisplay;
            _originalPenSimplifyThreshold = _penSimplifyThreshold;
            _originalMinRadius = _minRadius;
            _originalPolygonOpacityParentless = _polygonOpacityParentless;
            _originalPolygonOpacityWithParent = _polygonOpacityWithParent;
            _originalCircleOpacityParentless = _circleOpacityParentless;
            _originalCircleOpacityWithParent = _circleOpacityWithParent;
            _originalSegmentationPointRadius = _segmentationPointRadius;
            _originalPolygonPointRadius = _polygonPointRadius;
            _originalSmallestRenderedSize = _smallestRenderedSize;
        }

        public void RevertToOriginal()
        {
            _numSectionsInMemory = _originalNumSectionsInMemory;
            _numSectionsLoading = _originalNumSectionsLoading;
            _locationTextScaleFactor = _originalLocationTextScaleFactor;
            _referenceLocationTextScaleFactor = _originalReferenceLocationTextScaleFactor;
            _defaultClosedLineWidth = _originalDefaultClosedLineWidth;
            _defaultLocationJumpDownsample = _originalDefaultLocationJumpDownsample;
            _adjacentLocationRadiusScalar = _originalAdjacentLocationRadiusScalar;
            _numClosedCurveInterpolationPointsForDisplay = _originalNumClosedCurveInterpolationPointsForDisplay;
            _penSimplifyThreshold = _originalPenSimplifyThreshold;
            _minRadius = _originalMinRadius;
            _polygonOpacityParentless = _originalPolygonOpacityParentless;
            _polygonOpacityWithParent = _originalPolygonOpacityWithParent;
            _circleOpacityParentless = _originalCircleOpacityParentless;
            _circleOpacityWithParent = _originalCircleOpacityWithParent;
            _segmentationPointRadius = _originalSegmentationPointRadius;
            _polygonPointRadius = _originalPolygonPointRadius;
            _smallestRenderedSize = _originalSmallestRenderedSize;

            OnPropertyChanged(string.Empty); // Notify all properties changed
        }

        public void ResetToDefaults()
        {
            _numSectionsInMemory = 10;
            _numSectionsLoading = 5;
            _locationTextScaleFactor = 5;
            _referenceLocationTextScaleFactor = 2.5f;
            _defaultClosedLineWidth = 24.0;
            _defaultLocationJumpDownsample = 4.0;
            _adjacentLocationRadiusScalar = 0.5;
            _numClosedCurveInterpolationPointsForDisplay = 4;
            _penSimplifyThreshold = 12;
            _minRadius = 0.5;
            _polygonOpacityParentless = 0.5;
            _polygonOpacityWithParent = 0.33;
            _circleOpacityParentless = 0.5;
            _circleOpacityWithParent = 1.0;
            _segmentationPointRadius = 5.0;
            _polygonPointRadius = 6.0;
            _smallestRenderedSize = 0.5;

            OnPropertyChanged(string.Empty); // Notify all properties changed
        }

        protected virtual void OnPropertyChanged([CallerMemberName] string propertyName = null) => PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
    }

    /// <summary>
    /// Simple RelayCommand implementation for ICommand
    /// </summary>
    public class RelayCommand(Action execute, Func<bool> canExecute = null) : ICommand
    {
        private readonly Action _execute = execute ?? throw new ArgumentNullException(nameof(execute));
        private readonly Func<bool> _canExecute = canExecute;

        public event EventHandler CanExecuteChanged
        {
            add { }
            remove { }
        }

        public bool CanExecute(object parameter) => _canExecute is null || _canExecute();

        public void Execute(object parameter) => _execute();
    }
}

