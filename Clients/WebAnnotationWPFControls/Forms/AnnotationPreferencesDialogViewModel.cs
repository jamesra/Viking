using System;
using System.ComponentModel;
using System.Runtime.CompilerServices;
using System.Windows.Input;
using Viking.Common;

namespace WebAnnotation.WPF.Forms
{
    public class AnnotationPreferencesDialogViewModel : INotifyPropertyChanged
    {
        public event PropertyChangedEventHandler PropertyChanged;

        // Use shared MathUtils.Clamp methods (Math.Clamp not available in .NET Framework 4.8)

        #region Original Values (for Cancel revert)
        private int _originalNumSectionsInMemory;
        private int _originalNumSectionsLoading;
        private float _originalLocationTextScaleFactor;
        private float _originalReferenceLocationTextScaleFactor;
        private double _originalDefaultClosedLineWidth;
        private double _originalDefaultLocationJumpDownsample;
        private double _originalAdjacentLocationRadiusScalar;
        private int _originalLocationLinkWaistPixels;
        private bool _originalLocationLinkDrawUnderMarkers;
        private int _originalLocationLinkVisibilityPercent = DefaultLocationLinkVisibilityPercent;
        private bool _originalLocationLinkLabels = true;
        private uint _originalNumClosedCurveInterpolationPointsForDisplay;
        private int _originalPenSimplifyThreshold;
        private double _originalMinRadius;
        private double _originalPolygonOpacityParentless;
        private double _originalPolygonOpacityWithParent;
        private double _originalCircleOpacityParentless;
        private double _originalCircleOpacityWithParent;
        private double _originalSegmentationPointRadius;
        private double _originalSegmentationHoleDropFraction;
        private int _originalSegmentationEdgeCleanupRadius;
        private double _originalSegmentationMaskThreshold;
        private bool _originalSegmentationUseMaskInput;
        private bool _originalSegmentationEdgeSnapEnabled;
        private double _originalSegmentationSmoothingSigma;
        private double _originalSegmentationMinFeatureNanometers;
        private bool _originalAutoPolygonizeCircles;
        private double _originalAutoPolygonizeMinRadiusNanometers;
        private bool _originalAutoPolygonizeOverlayMasks;
        private bool _originalAutoPolygonizeOverlayPrompts;
        private bool _originalAutoPolygonizeHideSegmentationRings;
        private bool _originalAutoPolygonizeShimmerEnabled;
        private bool _originalSegmentLinkedAnnotations;
        private double _originalAutoPolygonizeShimmerBrightness;
        private int _originalAutoPolygonizeShimmerDurationMs;
        private bool _originalCorrectionShimEnabled;
        private bool _originalCorrectionShimShowOffset;
        private double _originalCorrectionShimFadeStartDownsample;
        private double _originalCorrectionShimFadeEndDownsample;
        private double _originalAutoPolygonizeMaxDownsample;
        private double _originalPolygonPointDiameter;
        private double _originalSmallestRenderedSize;
        private double _originalPolygonVertexPointsVisibleAtWidthFraction;
        private double _originalPolygonVertexPointsHiddenAtWidthFraction;
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
                    _numSectionsInMemory = MathUtils.Clamp(value, 1, 100);
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
                    _numSectionsLoading = MathUtils.Clamp(value, 1, 50);
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
                    _locationTextScaleFactor = MathUtils.Clamp(value, 0.1, 50.0);
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
                    _referenceLocationTextScaleFactor = MathUtils.Clamp(value, 0.1, 50.0);
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
                    _defaultClosedLineWidth = MathUtils.Clamp(value, 1.0, 100.0);
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
                    _defaultLocationJumpDownsample = MathUtils.Clamp(value, 1.0, 64.0);
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
                    _adjacentLocationRadiusScalar = MathUtils.Clamp(value, 0.1, 2.0);
                    OnPropertyChanged();
                }
            }
        }

        /// <summary>Starting <see cref="LocationLinkWaistPixels"/>; matches WebAnnotation's stored default.</summary>
        public const int DefaultLocationLinkWaistPixels = 8;

        /// <summary>Narrowest allowed <see cref="LocationLinkWaistPixels"/>.</summary>
        public const int MinLocationLinkWaistPixels = 2;

        /// <summary>Widest allowed <see cref="LocationLinkWaistPixels"/>.</summary>
        public const int MaxLocationLinkWaistPixels = 32;

        private int _locationLinkWaistPixels = DefaultLocationLinkWaistPixels;

        /// <summary>Width in screen pixels of the narrowest part of a link line between locations on different sections.</summary>
        public int LocationLinkWaistPixels
        {
            get => _locationLinkWaistPixels;
            set
            {
                int clampedValue = MathUtils.Clamp(value, MinLocationLinkWaistPixels, MaxLocationLinkWaistPixels);
                if (_locationLinkWaistPixels != clampedValue)
                {
                    _locationLinkWaistPixels = clampedValue;
                    OnPropertyChanged();
                }
            }
        }

        private bool _locationLinkDrawUnderMarkers;

        /// <summary>Also draws each location link under its two location markers, as a round end the size of each marker.</summary>
        public bool LocationLinkDrawUnderMarkers
        {
            get => _locationLinkDrawUnderMarkers;
            set
            {
                if (_locationLinkDrawUnderMarkers != value)
                {
                    _locationLinkDrawUnderMarkers = value;
                    OnPropertyChanged();
                }
            }
        }

        /// <summary>Starting <see cref="LocationLinkVisibilityPercent"/>; matches WebAnnotation's stored default.</summary>
        public const int DefaultLocationLinkVisibilityPercent = 60;

        /// <summary>Faintest allowed <see cref="LocationLinkVisibilityPercent"/>.</summary>
        public const int MinLocationLinkVisibilityPercent = 10;

        /// <summary>Strongest allowed <see cref="LocationLinkVisibilityPercent"/>.</summary>
        public const int MaxLocationLinkVisibilityPercent = 100;

        private int _locationLinkVisibilityPercent = DefaultLocationLinkVisibilityPercent;

        /// <summary>
        /// How strongly location links are drawn, as a percent: the link's opacity next to its markers and the strength of
        /// its contrasting shadow. The middle of a link is always fainter than its ends.
        /// </summary>
        public int LocationLinkVisibilityPercent
        {
            get => _locationLinkVisibilityPercent;
            set
            {
                int clampedValue = MathUtils.Clamp(value, MinLocationLinkVisibilityPercent, MaxLocationLinkVisibilityPercent);
                if (_locationLinkVisibilityPercent != clampedValue)
                {
                    _locationLinkVisibilityPercent = clampedValue;
                    OnPropertyChanged();
                }
            }
        }

        private bool _locationLinkLabels = true;

        /// <summary>Labels each end of a location link with the ID of the location at its other end.</summary>
        public bool LocationLinkLabels
        {
            get => _locationLinkLabels;
            set
            {
                if (_locationLinkLabels != value)
                {
                    _locationLinkLabels = value;
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
                    _numClosedCurveInterpolationPointsForDisplay = (uint)MathUtils.Clamp((int)value, 2, 20);
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
                    _penSimplifyThreshold = MathUtils.Clamp(value, 1, 100);
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
                    _minRadius = MathUtils.Clamp(value, 0.1, 10.0);
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
                double clampedValue = MathUtils.Clamp(value, 0.0, 1.0);
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
                double clampedValue = MathUtils.Clamp(value, 0.0, 1.0);
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
                double clampedValue = MathUtils.Clamp(value, 0.0, 1.0);
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
                double clampedValue = MathUtils.Clamp(value, 0.0, 1.0);
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
                    _segmentationPointRadius = Math.Max(1.0, value);
                    OnPropertyChanged();
                }
            }
        }

        /// <summary>Factory default for <see cref="SegmentationHoleDropFraction"/>. Keep in step with WebAnnotation.Properties.Settings.</summary>
        public const double DefaultSegmentationHoleDropFraction = 0.03;

        private double _segmentationHoleDropFraction;
        public double SegmentationHoleDropFraction
        {
            get => _segmentationHoleDropFraction;
            set
            {
                if (Math.Abs(_segmentationHoleDropFraction - value) > 0.0001)
                {
                    _segmentationHoleDropFraction = MathUtils.Clamp(value, 0.0, 1.0);
                    OnPropertyChanged();
                }
            }
        }

        /// <summary>Factory default for <see cref="SegmentationEdgeCleanupRadius"/>. Keep in step with WebAnnotation.Properties.Settings.</summary>
        public const int DefaultSegmentationEdgeCleanupRadius = 8;

        private int _segmentationEdgeCleanupRadius;
        public int SegmentationEdgeCleanupRadius
        {
            get => _segmentationEdgeCleanupRadius;
            set
            {
                int clampedValue = MathUtils.Clamp(value, 0, 32);
                if (_segmentationEdgeCleanupRadius != clampedValue)
                {
                    _segmentationEdgeCleanupRadius = clampedValue;
                    OnPropertyChanged();
                }
            }
        }

        /// <summary>Starting <see cref="SegmentationMaskThreshold"/>; matches WebAnnotation's stored default.</summary>
        public const double DefaultSegmentationMaskThreshold = 0.5;

        /// <summary>Smallest allowed <see cref="SegmentationMaskThreshold"/>, in SAM2 logit units.</summary>
        public const double MinSegmentationMaskThreshold = -2.5;

        /// <summary>Largest allowed <see cref="SegmentationMaskThreshold"/>, in SAM2 logit units.</summary>
        public const double MaxSegmentationMaskThreshold = 5.0;

        private double _segmentationMaskThreshold;

        /// <summary>
        /// Logit a pixel must exceed for SAM2 to call it object. 0 is SAM2's own default; higher
        /// values shrink every returned mask and lower values grow it. Changing it re-segments.
        /// Also on the viewer toolbar.
        /// </summary>
        public double SegmentationMaskThreshold
        {
            get => _segmentationMaskThreshold;
            set
            {
                double clampedValue = MathUtils.Clamp(value, MinSegmentationMaskThreshold, MaxSegmentationMaskThreshold);
                if (Math.Abs(_segmentationMaskThreshold - clampedValue) > 0.0001)
                {
                    _segmentationMaskThreshold = clampedValue;
                    OnPropertyChanged();
                }
            }
        }

        private bool _segmentationUseMaskInput;

        /// <summary>
        /// When true the server predicts twice per cell, feeding the first pass's low-resolution
        /// logits back to SAM2 as mask_input. Off by default. Changing it re-segments.
        /// </summary>
        public bool SegmentationUseMaskInput
        {
            get => _segmentationUseMaskInput;
            set
            {
                if (_segmentationUseMaskInput != value)
                {
                    _segmentationUseMaskInput = value;
                    OnPropertyChanged();
                }
            }
        }

        /// <summary>Starting <see cref="SegmentationMinFeatureNanometers"/>; matches WebAnnotation's stored default.</summary>
        public const double DefaultSegmentationMinFeatureNanometers = 10;

        /// <summary>Largest allowed <see cref="SegmentationMinFeatureNanometers"/>.</summary>
        public const double MaxSegmentationMinFeatureNanometers = 100;

        private double _segmentationMinFeatureNanometers = DefaultSegmentationMinFeatureNanometers;

        /// <summary>
        /// Narrowest part of a SAM2 mask that is kept, in nanometers. Thinner bridges and specks are cut before the outline
        /// is traced without moving the edge of anything wider. A membrane is at least 7 to 10 nm thick, so no
        /// cytoplasm connection is narrower than that. 0 keeps everything. Changing it retraces what is on screen.
        /// </summary>
        public double SegmentationMinFeatureNanometers
        {
            get => _segmentationMinFeatureNanometers;
            set
            {
                double clampedValue = MathUtils.Clamp(value, 0, MaxSegmentationMinFeatureNanometers);
                if (Math.Abs(_segmentationMinFeatureNanometers - clampedValue) > 0.0001)
                {
                    _segmentationMinFeatureNanometers = clampedValue;
                    OnPropertyChanged();
                }
            }
        }

        /// <summary>Starting <see cref="SegmentationSmoothingSigma"/>; matches WebAnnotation's stored default.</summary>
        public const double DefaultSegmentationSmoothingSigma = 0.5;

        /// <summary>Largest allowed <see cref="SegmentationSmoothingSigma"/>, in logit samples.</summary>
        public const double MaxSegmentationSmoothingSigma = 2.0;

        private double _segmentationSmoothingSigma = DefaultSegmentationSmoothingSigma;

        /// <summary>
        /// Smoothing, in logit samples (four pixels each), applied to SAM2's logits before the outline is traced. 0 traces
        /// them as sent; higher values remove one-sample holes and zigzags but start to merge thin necks and push the line
        /// outward. Applies to the next segmentation, not to proposals already on screen.
        /// </summary>
        public double SegmentationSmoothingSigma
        {
            get => _segmentationSmoothingSigma;
            set
            {
                double clampedValue = MathUtils.Clamp(value, 0, MaxSegmentationSmoothingSigma);
                if (Math.Abs(_segmentationSmoothingSigma - clampedValue) > 0.0001)
                {
                    _segmentationSmoothingSigma = clampedValue;
                    OnPropertyChanged();
                }
            }
        }

        private bool _segmentationEdgeSnapEnabled = true;

        /// <summary>
        /// When true each traced contour is moved onto the nearest strong bright-to-dark edge of the image, so the line
        /// sits on the membrane instead of where the SAM2 logits cross the threshold. On by default. Changing it
        /// retraces the proposals on screen and does not re-segment.
        /// </summary>
        public bool SegmentationEdgeSnapEnabled
        {
            get => _segmentationEdgeSnapEnabled;
            set
            {
                if (_segmentationEdgeSnapEnabled != value)
                {
                    _segmentationEdgeSnapEnabled = value;
                    OnPropertyChanged();
                }
            }
        }

        private bool _autoPolygonizeCircles;
        public bool AutoPolygonizeCircles
        {
            get => _autoPolygonizeCircles;
            set
            {
                if (_autoPolygonizeCircles != value)
                {
                    _autoPolygonizeCircles = value;
                    OnPropertyChanged();
                }
            }
        }

        /// <summary>
        /// Debug overlay of returned SAM2 masks on auto-polygonize proposals.
        /// Debug builds reset this to on; release builds reset it to off.
        /// </summary>
        private bool _autoPolygonizeOverlayMasks;
        public bool AutoPolygonizeOverlayMasks
        {
            get => _autoPolygonizeOverlayMasks;
            set
            {
                if (_autoPolygonizeOverlayMasks != value)
                {
                    _autoPolygonizeOverlayMasks = value;
                    OnPropertyChanged();
                }
            }
        }

        /// <summary>Starting <see cref="AutoPolygonizeShimmerBrightness"/>; matches WebAnnotation's stored default.</summary>
        public const double DefaultAutoPolygonizeShimmerBrightness = 0.6;

        /// <summary>Dimmest allowed <see cref="AutoPolygonizeShimmerBrightness"/>.</summary>
        public const double MinAutoPolygonizeShimmerBrightness = 0.1;

        /// <summary>Brightest allowed <see cref="AutoPolygonizeShimmerBrightness"/>.</summary>
        public const double MaxAutoPolygonizeShimmerBrightness = 1.0;

        /// <summary>Starting <see cref="AutoPolygonizeShimmerDurationMs"/>; matches WebAnnotation's stored default.</summary>
        public const int DefaultAutoPolygonizeShimmerDurationMs = 750;

        /// <summary>Shortest allowed <see cref="AutoPolygonizeShimmerDurationMs"/>.</summary>
        public const int MinAutoPolygonizeShimmerDurationMs = 250;

        /// <summary>Longest allowed <see cref="AutoPolygonizeShimmerDurationMs"/>.</summary>
        public const int MaxAutoPolygonizeShimmerDurationMs = 1000;

        private bool _segmentLinkedAnnotations = true;

        /// <summary>Runs the segmentation step (accept, cancel, skip) after a linked annotation is dragged onto the next section.</summary>
        public bool SegmentLinkedAnnotations
        {
            get => _segmentLinkedAnnotations;
            set
            {
                if (_segmentLinkedAnnotations != value)
                {
                    _segmentLinkedAnnotations = value;
                    OnPropertyChanged();
                }
            }
        }

        private bool _autoPolygonizeShimmerEnabled = true;

        /// <summary>Plays a one-shot shimmer over each newly shown auto-polygonize result.</summary>
        public bool AutoPolygonizeShimmerEnabled
        {
            get => _autoPolygonizeShimmerEnabled;
            set
            {
                if (_autoPolygonizeShimmerEnabled != value)
                {
                    _autoPolygonizeShimmerEnabled = value;
                    OnPropertyChanged();
                }
            }
        }

        private double _autoPolygonizeShimmerBrightness = DefaultAutoPolygonizeShimmerBrightness;

        /// <summary>Peak alpha of the shimmer lines.</summary>
        public double AutoPolygonizeShimmerBrightness
        {
            get => _autoPolygonizeShimmerBrightness;
            set
            {
                double clampedValue = MathUtils.Clamp(value, MinAutoPolygonizeShimmerBrightness, MaxAutoPolygonizeShimmerBrightness);
                if (Math.Abs(_autoPolygonizeShimmerBrightness - clampedValue) > 0.0001)
                {
                    _autoPolygonizeShimmerBrightness = clampedValue;
                    OnPropertyChanged();
                }
            }
        }

        private int _autoPolygonizeShimmerDurationMs = DefaultAutoPolygonizeShimmerDurationMs;

        /// <summary>Milliseconds the shimmer takes to cross a result.</summary>
        public int AutoPolygonizeShimmerDurationMs
        {
            get => _autoPolygonizeShimmerDurationMs;
            set
            {
                int clampedValue = MathUtils.Clamp(value, MinAutoPolygonizeShimmerDurationMs, MaxAutoPolygonizeShimmerDurationMs);
                if (_autoPolygonizeShimmerDurationMs != clampedValue)
                {
                    _autoPolygonizeShimmerDurationMs = clampedValue;
                    OnPropertyChanged();
                }
            }
        }

        /// <summary>
        /// Green foreground and red background clicks on auto-polygonize proposals.
        /// Debug builds reset this to on; release builds reset it to off. Independent of the mask overlay.
        /// </summary>
        private bool _autoPolygonizeOverlayPrompts;
        public bool AutoPolygonizeOverlayPrompts
        {
            get => _autoPolygonizeOverlayPrompts;
            set
            {
                if (_autoPolygonizeOverlayPrompts != value)
                {
                    _autoPolygonizeOverlayPrompts = value;
                    OnPropertyChanged();
                }
            }
        }

        /// <summary>
        /// Hides auto-polygonize outlines while masks are shown. The checkbox is enabled only when masks are on.
        /// </summary>
        private bool _autoPolygonizeHideSegmentationRings;
        public bool AutoPolygonizeHideSegmentationRings
        {
            get => _autoPolygonizeHideSegmentationRings;
            set
            {
                if (_autoPolygonizeHideSegmentationRings != value)
                {
                    _autoPolygonizeHideSegmentationRings = value;
                    OnPropertyChanged();
                }
            }
        }

        /// <summary>Lowest and highest downsample the correction shim range accepts.</summary>
        public const double MinCorrectionShimDownsample = 0.25;
        public const double MaxCorrectionShimDownsample = 256;

        private bool _correctionShimEnabled = true;

        /// <summary>
        /// Whether the section-correction shim is on. Has an effect only for annotation contexts that have a correction service.
        /// </summary>
        public bool CorrectionShimEnabled
        {
            get => _correctionShimEnabled;
            set
            {
                if (_correctionShimEnabled != value)
                {
                    _correctionShimEnabled = value;
                    OnPropertyChanged();
                }
            }
        }

        private bool _correctionShimShowOffset;

        /// <summary>Whether the status bar shows the applied correction offset (nm) after "Corrections: On".</summary>
        public bool CorrectionShimShowOffset
        {
            get => _correctionShimShowOffset;
            set
            {
                if (_correctionShimShowOffset != value)
                {
                    _correctionShimShowOffset = value;
                    OnPropertyChanged();
                }
            }
        }

        private double _correctionShimFadeStartDownsample = 8;

        /// <summary>Downsample at and above which the correction shim has no effect.</summary>
        public double CorrectionShimFadeStartDownsample
        {
            get => _correctionShimFadeStartDownsample;
            set
            {
                double clamped = MathUtils.Clamp(value, MinCorrectionShimDownsample, MaxCorrectionShimDownsample);
                if (Math.Abs(_correctionShimFadeStartDownsample - clamped) > 0.001)
                {
                    _correctionShimFadeStartDownsample = clamped;
                    OnPropertyChanged();
                }
            }
        }

        private double _correctionShimFadeEndDownsample = 4;

        /// <summary>Downsample at and below which the correction shim is fully applied. Should be below the start.</summary>
        public double CorrectionShimFadeEndDownsample
        {
            get => _correctionShimFadeEndDownsample;
            set
            {
                double clamped = MathUtils.Clamp(value, MinCorrectionShimDownsample, MaxCorrectionShimDownsample);
                if (Math.Abs(_correctionShimFadeEndDownsample - clamped) > 0.001)
                {
                    _correctionShimFadeEndDownsample = clamped;
                    OnPropertyChanged();
                }
            }
        }

        /// <summary>
        /// Factory default for <see cref="AutoPolygonizeMinRadiusNanometers"/>.
        /// Keep in step with WebAnnotation.Properties.Settings.
        /// </summary>
        public const double DefaultAutoPolygonizeMinRadiusNanometers = 40.0;

        /// <summary>
        /// Auto-polygonize cutoff as circle radius in nanometers.
        /// Default <see cref="DefaultAutoPolygonizeMinRadiusNanometers"/>.
        /// The preview circle is that radius at the current <see cref="NanometersPerPixel"/>.
        /// </summary>
        private double _autoPolygonizeMinRadiusNanometers;
        public double AutoPolygonizeMinRadiusNanometers
        {
            get => _autoPolygonizeMinRadiusNanometers;
            set
            {
                if (Math.Abs(_autoPolygonizeMinRadiusNanometers - value) > 0.001)
                {
                    _autoPolygonizeMinRadiusNanometers = MathUtils.Clamp(value, 0.0, 10000.0);
                    OnPropertyChanged();
                }
            }
        }

        /// <summary>
        /// Factory default for <see cref="AutoPolygonizeMaxDownsample"/>.
        /// Keep in step with WebAnnotation.Properties.Settings.
        /// </summary>
        public const double DefaultAutoPolygonizeMaxDownsample = 2.0;

        /// <summary>
        /// Coarsest camera downsample at which auto-segment still runs. Default <see cref="DefaultAutoPolygonizeMaxDownsample"/>.
        /// The preference allows 1 to 4. The value itself still runs; anything coarser is not sent
        /// to the segmentation service.
        /// </summary>
        private double _autoPolygonizeMaxDownsample;
        public double AutoPolygonizeMaxDownsample
        {
            get => _autoPolygonizeMaxDownsample;
            set
            {
                // Preference slider steps by 0.25 over [1, 4]; snap so keyboard/bindings match ticks.
                double clamped = MathUtils.Clamp(Math.Round(value * 4.0) / 4.0, 1.0, 4.0);
                if (Math.Abs(_autoPolygonizeMaxDownsample - clamped) > 0.001)
                {
                    _autoPolygonizeMaxDownsample = clamped;
                    OnPropertyChanged();
                }
            }
        }

        /// <summary>
        /// Nanometers per Viking screen pixel at the current zoom. Sizes the min-size
        /// preview for <see cref="AutoPolygonizeMinRadiusNanometers"/>.
        /// </summary>
        private double _nanometersPerPixel;
        public double NanometersPerPixel
        {
            get => _nanometersPerPixel;
            set
            {
                if (Math.Abs(_nanometersPerPixel - value) > 0.0001)
                {
                    _nanometersPerPixel = value;
                    OnPropertyChanged();
                }
            }
        }

        private double _polygonPointDiameter;
        public double PolygonPointDiameter
        {
            get => _polygonPointDiameter;
            set
            {
                if (Math.Abs(_polygonPointDiameter - value) > 0.01)
                {
                    _polygonPointDiameter = MathUtils.Clamp(value, 2.0, 100.0);
                    OnPropertyChanged();
                    OnPropertyChanged(nameof(PolygonVertexDisplayDiameterPx));
                }
            }
        }

        private double _sceneDownsample = 1.0;
        public double SceneDownsample
        {
            get => _sceneDownsample;
            set
            {
                if (Math.Abs(_sceneDownsample - value) > 0.0001)
                {
                    _sceneDownsample = value;
                    OnPropertyChanged();
                    OnPropertyChanged(nameof(PolygonVertexDisplayDiameterPx));
                }
            }
        }

        public int PolygonVertexDisplayDiameterPx =>
            _sceneDownsample > 0 ? (int)(_polygonPointDiameter / _sceneDownsample) : 0;

        private double _polygonPointOpacity = 0.75;
        private double _originalPolygonPointOpacity = 0.75;

        /// <summary>Opacity (0..1) of polygon control points, independent of polygon fill opacity.</summary>
        public double PolygonPointOpacity
        {
            get => _polygonPointOpacity;
            set
            {
                double clamped = Math.Max(0.0, Math.Min(1.0, value));
                if (Math.Abs(_polygonPointOpacity - clamped) > 0.0001)
                {
                    _polygonPointOpacity = clamped;
                    OnPropertyChanged();
                }
            }
        }

        private bool _hidePolygonPointsInPenMode = true;
        private bool _originalHidePolygonPointsInPenMode = true;

        /// <summary>When true, polygon control points are not drawn in pen mode.</summary>
        public bool HidePolygonPointsInPenMode
        {
            get => _hidePolygonPointsInPenMode;
            set
            {
                if (_hidePolygonPointsInPenMode != value)
                {
                    _hidePolygonPointsInPenMode = value;
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
                    _smallestRenderedSize = Math.Max(0.5, value);
                    OnPropertyChanged();
                }
            }
        }

        private int _sceneWidthPixels = 1920;
        public int SceneWidthPixels
        {
            get => _sceneWidthPixels;
            set
            {
                if (_sceneWidthPixels != value)
                {
                    _sceneWidthPixels = value;
                    OnPropertyChanged(nameof(VisibleAtPixelDisplay));
                    OnPropertyChanged(nameof(HiddenAtPixelDisplay));
                }
            }
        }

        private double _polygonVertexPointsVisibleAtWidthFraction;
        public double PolygonVertexPointsVisibleAtWidthFraction
        {
            get => _polygonVertexPointsVisibleAtWidthFraction;
            set
            {
                double clamped = MathUtils.Clamp(value, 0.0001, 0.02);
                if (clamped <= _polygonVertexPointsHiddenAtWidthFraction)
                    clamped = Math.Min(0.02, _polygonVertexPointsHiddenAtWidthFraction + 0.0001);
                if (Math.Abs(_polygonVertexPointsVisibleAtWidthFraction - clamped) > 0.00001)
                {
                    _polygonVertexPointsVisibleAtWidthFraction = clamped;
                    OnPropertyChanged();
                    OnPropertyChanged(nameof(VisibleAtPixelDisplay));
                    OnPropertyChanged(nameof(VertexVisibilityRangeDisplay));
                }
            }
        }

        private double _polygonVertexPointsHiddenAtWidthFraction;
        public double PolygonVertexPointsHiddenAtWidthFraction
        {
            get => _polygonVertexPointsHiddenAtWidthFraction;
            set
            {
                double clamped = MathUtils.Clamp(value, 0.00005, 0.015);
                if (clamped >= _polygonVertexPointsVisibleAtWidthFraction)
                    clamped = Math.Max(0.00005, _polygonVertexPointsVisibleAtWidthFraction - 0.0001);
                if (Math.Abs(_polygonVertexPointsHiddenAtWidthFraction - clamped) > 0.00001)
                {
                    _polygonVertexPointsHiddenAtWidthFraction = clamped;
                    OnPropertyChanged();
                    OnPropertyChanged(nameof(HiddenAtPixelDisplay));
                    OnPropertyChanged(nameof(VertexVisibilityRangeDisplay));
                }
            }
        }

        public string VertexVisibilityRangeDisplay =>
            $"Hidden: {_polygonVertexPointsHiddenAtWidthFraction * 100:F2}% ({(int)(_sceneWidthPixels * _polygonVertexPointsHiddenAtWidthFraction)} px){Environment.NewLine}Visible: {_polygonVertexPointsVisibleAtWidthFraction * 100:F2}% ({(int)(_sceneWidthPixels * _polygonVertexPointsVisibleAtWidthFraction)} px)";

        public string VisibleAtPixelDisplay =>
            $"{_polygonVertexPointsVisibleAtWidthFraction * 100:F2}% ({(int)(_sceneWidthPixels * _polygonVertexPointsVisibleAtWidthFraction)} px)";

        public string HiddenAtPixelDisplay =>
            $"{_polygonVertexPointsHiddenAtWidthFraction * 100:F2}% ({(int)(_sceneWidthPixels * _polygonVertexPointsHiddenAtWidthFraction)} px)";


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
            double polygonPointDiameter,
            double smallestRenderedSize,
            double polygonVertexPointsVisibleAtWidthFraction,
            double polygonVertexPointsHiddenAtWidthFraction,
            int? sceneWidthPixels = null,
            double? sceneDownsample = null,
            bool correctionShimEnabled = true,
            double correctionShimFadeStartDownsample = 8,
            double correctionShimFadeEndDownsample = 4,
            bool correctionShimShowOffset = false,
            double segmentationHoleDropFraction = DefaultSegmentationHoleDropFraction,
            int segmentationEdgeCleanupRadius = DefaultSegmentationEdgeCleanupRadius,
            bool autoPolygonizeCircles = false,
            double autoPolygonizeMinRadiusNanometers = DefaultAutoPolygonizeMinRadiusNanometers,
            double autoPolygonizeMaxDownsample = DefaultAutoPolygonizeMaxDownsample,
            bool autoPolygonizeOverlayMasks = false,
            bool autoPolygonizeOverlayPrompts = false,
            bool autoPolygonizeHideSegmentationRings = false,
            double segmentationMaskThreshold = DefaultSegmentationMaskThreshold,
            bool segmentationUseMaskInput = false,
            double polygonPointOpacity = 0.75,
            bool hidePolygonPointsInPenMode = true,
            bool segmentationEdgeSnapEnabled = true,
            double segmentationSmoothingSigma = DefaultSegmentationSmoothingSigma,
            double segmentationMinFeatureNanometers = DefaultSegmentationMinFeatureNanometers,
            bool autoPolygonizeShimmerEnabled = true,
            double autoPolygonizeShimmerBrightness = DefaultAutoPolygonizeShimmerBrightness,
            int autoPolygonizeShimmerDurationMs = DefaultAutoPolygonizeShimmerDurationMs,
            bool segmentLinkedAnnotations = true,
            int locationLinkWaistPixels = DefaultLocationLinkWaistPixels,
            bool locationLinkDrawUnderMarkers = false,
            int locationLinkVisibilityPercent = DefaultLocationLinkVisibilityPercent,
            bool locationLinkLabels = true)
        {
            _polygonPointOpacity = _originalPolygonPointOpacity = polygonPointOpacity;
            _hidePolygonPointsInPenMode = _originalHidePolygonPointsInPenMode = hidePolygonPointsInPenMode;

            // Store current values
            _numSectionsInMemory = numSectionsInMemory;
            _numSectionsLoading = numSectionsLoading;
            _locationTextScaleFactor = locationTextScaleFactor;
            _referenceLocationTextScaleFactor = referenceLocationTextScaleFactor;
            _defaultClosedLineWidth = defaultClosedLineWidth;
            _defaultLocationJumpDownsample = defaultLocationJumpDownsample;
            _adjacentLocationRadiusScalar = adjacentLocationRadiusScalar;
            _locationLinkWaistPixels = locationLinkWaistPixels;
            _locationLinkDrawUnderMarkers = locationLinkDrawUnderMarkers;
            _numClosedCurveInterpolationPointsForDisplay = numClosedCurveInterpolationPointsForDisplay;
            _penSimplifyThreshold = penSimplifyThreshold;
            _minRadius = minRadius;
            _segmentationPointRadius = segmentationPointRadius;
            _segmentationHoleDropFraction = segmentationHoleDropFraction;
            _segmentationEdgeCleanupRadius = segmentationEdgeCleanupRadius;
            _segmentationMaskThreshold = segmentationMaskThreshold;
            _segmentationUseMaskInput = segmentationUseMaskInput;
            _segmentationEdgeSnapEnabled = segmentationEdgeSnapEnabled;
            _polygonPointDiameter = polygonPointDiameter;
            _smallestRenderedSize = smallestRenderedSize;
            _sceneDownsample = sceneDownsample ?? 1.0;
            _polygonVertexPointsVisibleAtWidthFraction = polygonVertexPointsVisibleAtWidthFraction;
            _polygonVertexPointsHiddenAtWidthFraction = polygonVertexPointsHiddenAtWidthFraction;
            _sceneWidthPixels = sceneWidthPixels ?? 1920;
            _autoPolygonizeCircles = autoPolygonizeCircles;
            _autoPolygonizeMinRadiusNanometers = autoPolygonizeMinRadiusNanometers;
            _autoPolygonizeMaxDownsample = autoPolygonizeMaxDownsample;
            _autoPolygonizeOverlayMasks = autoPolygonizeOverlayMasks;
            _autoPolygonizeOverlayPrompts = autoPolygonizeOverlayPrompts;
            _autoPolygonizeHideSegmentationRings = autoPolygonizeHideSegmentationRings;
            _autoPolygonizeShimmerEnabled = autoPolygonizeShimmerEnabled;
            _segmentLinkedAnnotations = segmentLinkedAnnotations;
            _autoPolygonizeShimmerBrightness = autoPolygonizeShimmerBrightness;
            _autoPolygonizeShimmerDurationMs = autoPolygonizeShimmerDurationMs;

            // Store original values for Cancel revert BEFORE setting properties
            _originalNumSectionsInMemory = numSectionsInMemory;
            _originalNumSectionsLoading = numSectionsLoading;
            _originalLocationTextScaleFactor = locationTextScaleFactor;
            _originalReferenceLocationTextScaleFactor = referenceLocationTextScaleFactor;
            _originalDefaultClosedLineWidth = defaultClosedLineWidth;
            _originalDefaultLocationJumpDownsample = defaultLocationJumpDownsample;
            _originalAdjacentLocationRadiusScalar = adjacentLocationRadiusScalar;
            _originalLocationLinkWaistPixels = locationLinkWaistPixels;
            _originalLocationLinkDrawUnderMarkers = locationLinkDrawUnderMarkers;
            _originalLocationLinkVisibilityPercent = locationLinkVisibilityPercent;
            _originalLocationLinkLabels = locationLinkLabels;
            _originalNumClosedCurveInterpolationPointsForDisplay = numClosedCurveInterpolationPointsForDisplay;
            _originalPenSimplifyThreshold = penSimplifyThreshold;
            _originalMinRadius = minRadius;
            _originalPolygonOpacityParentless = polygonOpacityParentless;
            _originalPolygonOpacityWithParent = polygonOpacityWithParent;
            _originalCircleOpacityParentless = circleOpacityParentless;
            _originalCircleOpacityWithParent = circleOpacityWithParent;
            _originalSegmentationPointRadius = segmentationPointRadius;
            _originalSegmentationHoleDropFraction = segmentationHoleDropFraction;
            _originalSegmentationEdgeCleanupRadius = segmentationEdgeCleanupRadius;
            _originalSegmentationMaskThreshold = segmentationMaskThreshold;
            _originalSegmentationUseMaskInput = segmentationUseMaskInput;
            _originalSegmentationEdgeSnapEnabled = segmentationEdgeSnapEnabled;
            _originalSegmentationSmoothingSigma = segmentationSmoothingSigma;
            _originalSegmentationMinFeatureNanometers = segmentationMinFeatureNanometers;
            _originalAutoPolygonizeCircles = autoPolygonizeCircles;
            _originalAutoPolygonizeMinRadiusNanometers = autoPolygonizeMinRadiusNanometers;
            _originalAutoPolygonizeMaxDownsample = autoPolygonizeMaxDownsample;
            _originalAutoPolygonizeOverlayMasks = autoPolygonizeOverlayMasks;
            _originalAutoPolygonizeOverlayPrompts = autoPolygonizeOverlayPrompts;
            _originalAutoPolygonizeHideSegmentationRings = autoPolygonizeHideSegmentationRings;
            _originalAutoPolygonizeShimmerEnabled = autoPolygonizeShimmerEnabled;
            _originalSegmentLinkedAnnotations = segmentLinkedAnnotations;
            _originalAutoPolygonizeShimmerBrightness = autoPolygonizeShimmerBrightness;
            _originalAutoPolygonizeShimmerDurationMs = autoPolygonizeShimmerDurationMs;
            _originalCorrectionShimEnabled = correctionShimEnabled;
            _originalCorrectionShimShowOffset = correctionShimShowOffset;
            _originalCorrectionShimFadeStartDownsample = correctionShimFadeStartDownsample;
            _originalCorrectionShimFadeEndDownsample = correctionShimFadeEndDownsample;
            _originalPolygonPointDiameter = polygonPointDiameter;
            _originalSmallestRenderedSize = smallestRenderedSize;
            _originalPolygonVertexPointsVisibleAtWidthFraction = polygonVertexPointsVisibleAtWidthFraction;
            _originalPolygonVertexPointsHiddenAtWidthFraction = polygonVertexPointsHiddenAtWidthFraction;

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
            SegmentationHoleDropFraction = segmentationHoleDropFraction;
            SegmentationEdgeCleanupRadius = segmentationEdgeCleanupRadius;
            SegmentationMaskThreshold = segmentationMaskThreshold;
            SegmentationUseMaskInput = segmentationUseMaskInput;
            SegmentationEdgeSnapEnabled = segmentationEdgeSnapEnabled;
            SegmentationSmoothingSigma = segmentationSmoothingSigma;
            SegmentationMinFeatureNanometers = segmentationMinFeatureNanometers;
            AutoPolygonizeCircles = autoPolygonizeCircles;
            AutoPolygonizeMinRadiusNanometers = autoPolygonizeMinRadiusNanometers;
            AutoPolygonizeMaxDownsample = autoPolygonizeMaxDownsample;
            AutoPolygonizeOverlayMasks = autoPolygonizeOverlayMasks;
            AutoPolygonizeOverlayPrompts = autoPolygonizeOverlayPrompts;
            AutoPolygonizeHideSegmentationRings = autoPolygonizeHideSegmentationRings;
            AutoPolygonizeShimmerEnabled = autoPolygonizeShimmerEnabled;
            SegmentLinkedAnnotations = segmentLinkedAnnotations;
            AutoPolygonizeShimmerBrightness = autoPolygonizeShimmerBrightness;
            AutoPolygonizeShimmerDurationMs = autoPolygonizeShimmerDurationMs;
            LocationLinkWaistPixels = locationLinkWaistPixels;
            LocationLinkDrawUnderMarkers = locationLinkDrawUnderMarkers;
            LocationLinkVisibilityPercent = locationLinkVisibilityPercent;
            LocationLinkLabels = locationLinkLabels;
            CorrectionShimEnabled = correctionShimEnabled;
            CorrectionShimShowOffset = correctionShimShowOffset;
            CorrectionShimFadeStartDownsample = correctionShimFadeStartDownsample;
            CorrectionShimFadeEndDownsample = correctionShimFadeEndDownsample;
            PolygonPointDiameter = polygonPointDiameter;
            SmallestRenderedSize = smallestRenderedSize;
            PolygonVertexPointsVisibleAtWidthFraction = polygonVertexPointsVisibleAtWidthFraction;
            PolygonVertexPointsHiddenAtWidthFraction = polygonVertexPointsHiddenAtWidthFraction;

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
            _originalLocationLinkWaistPixels = _locationLinkWaistPixels;
            _originalLocationLinkDrawUnderMarkers = _locationLinkDrawUnderMarkers;
            _originalLocationLinkVisibilityPercent = _locationLinkVisibilityPercent;
            _originalLocationLinkLabels = _locationLinkLabels;
            _originalNumClosedCurveInterpolationPointsForDisplay = _numClosedCurveInterpolationPointsForDisplay;
            _originalPenSimplifyThreshold = _penSimplifyThreshold;
            _originalMinRadius = _minRadius;
            _originalPolygonOpacityParentless = _polygonOpacityParentless;
            _originalPolygonOpacityWithParent = _polygonOpacityWithParent;
            _originalCircleOpacityParentless = _circleOpacityParentless;
            _originalCircleOpacityWithParent = _circleOpacityWithParent;
            _originalSegmentationPointRadius = _segmentationPointRadius;
            _originalSegmentationHoleDropFraction = _segmentationHoleDropFraction;
            _originalSegmentationEdgeCleanupRadius = _segmentationEdgeCleanupRadius;
            _originalSegmentationMaskThreshold = _segmentationMaskThreshold;
            _originalSegmentationUseMaskInput = _segmentationUseMaskInput;
            _originalSegmentationEdgeSnapEnabled = _segmentationEdgeSnapEnabled;
            _originalSegmentationSmoothingSigma = _segmentationSmoothingSigma;
            _originalSegmentationMinFeatureNanometers = _segmentationMinFeatureNanometers;
            _originalAutoPolygonizeCircles = _autoPolygonizeCircles;
            _originalAutoPolygonizeMinRadiusNanometers = _autoPolygonizeMinRadiusNanometers;
            _originalAutoPolygonizeMaxDownsample = _autoPolygonizeMaxDownsample;
            _originalAutoPolygonizeOverlayMasks = _autoPolygonizeOverlayMasks;
            _originalAutoPolygonizeOverlayPrompts = _autoPolygonizeOverlayPrompts;
            _originalAutoPolygonizeHideSegmentationRings = _autoPolygonizeHideSegmentationRings;
            _originalAutoPolygonizeShimmerEnabled = _autoPolygonizeShimmerEnabled;
            _originalSegmentLinkedAnnotations = _segmentLinkedAnnotations;
            _originalAutoPolygonizeShimmerBrightness = _autoPolygonizeShimmerBrightness;
            _originalAutoPolygonizeShimmerDurationMs = _autoPolygonizeShimmerDurationMs;
            _originalCorrectionShimEnabled = _correctionShimEnabled;
            _originalCorrectionShimShowOffset = _correctionShimShowOffset;
            _originalCorrectionShimFadeStartDownsample = _correctionShimFadeStartDownsample;
            _originalCorrectionShimFadeEndDownsample = _correctionShimFadeEndDownsample;
            _originalPolygonPointDiameter = _polygonPointDiameter;
            _originalPolygonPointOpacity = _polygonPointOpacity;
            _originalHidePolygonPointsInPenMode = _hidePolygonPointsInPenMode;
            _originalSmallestRenderedSize = _smallestRenderedSize;
            _originalPolygonVertexPointsVisibleAtWidthFraction = _polygonVertexPointsVisibleAtWidthFraction;
            _originalPolygonVertexPointsHiddenAtWidthFraction = _polygonVertexPointsHiddenAtWidthFraction;
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
            _locationLinkWaistPixels = _originalLocationLinkWaistPixels;
            _locationLinkDrawUnderMarkers = _originalLocationLinkDrawUnderMarkers;
            _locationLinkVisibilityPercent = _originalLocationLinkVisibilityPercent;
            _locationLinkLabels = _originalLocationLinkLabels;
            _numClosedCurveInterpolationPointsForDisplay = _originalNumClosedCurveInterpolationPointsForDisplay;
            _penSimplifyThreshold = _originalPenSimplifyThreshold;
            _minRadius = _originalMinRadius;
            _polygonOpacityParentless = _originalPolygonOpacityParentless;
            _polygonOpacityWithParent = _originalPolygonOpacityWithParent;
            _circleOpacityParentless = _originalCircleOpacityParentless;
            _circleOpacityWithParent = _originalCircleOpacityWithParent;
            _segmentationPointRadius = _originalSegmentationPointRadius;
            _segmentationHoleDropFraction = _originalSegmentationHoleDropFraction;
            _segmentationEdgeCleanupRadius = _originalSegmentationEdgeCleanupRadius;
            _segmentationMaskThreshold = _originalSegmentationMaskThreshold;
            _segmentationUseMaskInput = _originalSegmentationUseMaskInput;
            _segmentationEdgeSnapEnabled = _originalSegmentationEdgeSnapEnabled;
            _segmentationSmoothingSigma = _originalSegmentationSmoothingSigma;
            _segmentationMinFeatureNanometers = _originalSegmentationMinFeatureNanometers;
            _autoPolygonizeCircles = _originalAutoPolygonizeCircles;
            _autoPolygonizeMinRadiusNanometers = _originalAutoPolygonizeMinRadiusNanometers;
            _autoPolygonizeMaxDownsample = _originalAutoPolygonizeMaxDownsample;
            _autoPolygonizeOverlayMasks = _originalAutoPolygonizeOverlayMasks;
            _autoPolygonizeOverlayPrompts = _originalAutoPolygonizeOverlayPrompts;
            _autoPolygonizeHideSegmentationRings = _originalAutoPolygonizeHideSegmentationRings;
            _autoPolygonizeShimmerEnabled = _originalAutoPolygonizeShimmerEnabled;
            _segmentLinkedAnnotations = _originalSegmentLinkedAnnotations;
            _autoPolygonizeShimmerBrightness = _originalAutoPolygonizeShimmerBrightness;
            _autoPolygonizeShimmerDurationMs = _originalAutoPolygonizeShimmerDurationMs;
            _correctionShimEnabled = _originalCorrectionShimEnabled;
            _correctionShimShowOffset = _originalCorrectionShimShowOffset;
            _correctionShimFadeStartDownsample = _originalCorrectionShimFadeStartDownsample;
            _correctionShimFadeEndDownsample = _originalCorrectionShimFadeEndDownsample;
            _polygonPointDiameter = _originalPolygonPointDiameter;
            _polygonPointOpacity = _originalPolygonPointOpacity;
            _hidePolygonPointsInPenMode = _originalHidePolygonPointsInPenMode;
            _smallestRenderedSize = _originalSmallestRenderedSize;
            _polygonVertexPointsVisibleAtWidthFraction = _originalPolygonVertexPointsVisibleAtWidthFraction;
            _polygonVertexPointsHiddenAtWidthFraction = _originalPolygonVertexPointsHiddenAtWidthFraction;

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
            _locationLinkWaistPixels = DefaultLocationLinkWaistPixels;
            _locationLinkVisibilityPercent = DefaultLocationLinkVisibilityPercent;
            _locationLinkLabels = true;
            _locationLinkDrawUnderMarkers = false;
            _numClosedCurveInterpolationPointsForDisplay = 3;
            _penSimplifyThreshold = 3;
            _minRadius = 0.5;
            _polygonOpacityParentless = 0.5;
            _polygonOpacityWithParent = 0.33;
            _circleOpacityParentless = 0.5;
            _circleOpacityWithParent = 1.0;
            _segmentationPointRadius = 5.0;
            _segmentationHoleDropFraction = DefaultSegmentationHoleDropFraction;
            _segmentationEdgeCleanupRadius = DefaultSegmentationEdgeCleanupRadius;
            _segmentationMaskThreshold = DefaultSegmentationMaskThreshold;
            _segmentationUseMaskInput = false;
            _segmentationEdgeSnapEnabled = true;
            _segmentationSmoothingSigma = DefaultSegmentationSmoothingSigma;
            _segmentationMinFeatureNanometers = DefaultSegmentationMinFeatureNanometers;
            _autoPolygonizeCircles = false;
            _autoPolygonizeMinRadiusNanometers = DefaultAutoPolygonizeMinRadiusNanometers;
            _autoPolygonizeMaxDownsample = DefaultAutoPolygonizeMaxDownsample;
#if DEBUG
            _autoPolygonizeOverlayMasks = true;
            _autoPolygonizeOverlayPrompts = true;
#else
            _autoPolygonizeOverlayMasks = false;
            _autoPolygonizeOverlayPrompts = false;
#endif
            _autoPolygonizeHideSegmentationRings = false;
            _autoPolygonizeShimmerEnabled = true;
            _segmentLinkedAnnotations = true;
            _autoPolygonizeShimmerBrightness = DefaultAutoPolygonizeShimmerBrightness;
            _autoPolygonizeShimmerDurationMs = DefaultAutoPolygonizeShimmerDurationMs;
            _correctionShimEnabled = true;
            _correctionShimShowOffset = false;
            _correctionShimFadeStartDownsample = 8;
            _correctionShimFadeEndDownsample = 4;
            _polygonPointDiameter = 12.0;
            _polygonPointOpacity = 0.75;
            _hidePolygonPointsInPenMode = true;
            _smallestRenderedSize = 10.0;
            _polygonVertexPointsVisibleAtWidthFraction = 0.0025;
            _polygonVertexPointsHiddenAtWidthFraction = 0.002;

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

