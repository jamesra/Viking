using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Collections.Specialized;
using System.Configuration;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Net;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using System.Xml;
using System.Xml.Linq;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Viking;
using Viking.Common;
using Viking.UI;
using WebAnnotationModel;
using WebAnnotationModel.Service;
using System.Net.Http;
using rogue1.codepharm.net.XSD.WebAnnotationUserSettings.xsd;
using Utils;
using Viking.DependencyInjection;
using Viking.Services.Grpc;
using VikingXNAGraphics;
using WebAnnotation.UI.Commands.Segmentation;
using WebAnnotation.View;

namespace WebAnnotation
{
    public class Global : IModuleServiceRegistrar, IModuleInitializer
    {
        /// <summary>
        /// Jumping to a location causes it's diameter to occupy 1/8 the width of the screen
        /// </summary>
        internal static double DefaultLocationJumpDownsample => AnnotationSettings.DefaultLocationJumpDownsample;

        /// <summary>
        /// Number of sections we should be attempting to load at the same time before cancelling a request
        /// </summary>
        internal static int NumSectionsLoading => AnnotationSettings.NumSectionsLoading;

        internal static Export? Export = null;

        private static bool? _isSegmentationServiceAvailable;

        /// <summary>
        /// Static method called by ExtensionManager to determine if this extension should be loaded.
        /// Returns false if the VolumeToEndpoint element with Endpoint attribute is not found in the VikingXML.
        /// </summary>
        /// <param name="context">The extension load context providing access to VikingXML</param>
        /// <returns>True if the extension should load, false otherwise</returns>
        public static bool ShouldLoad(Viking.Common.IExtensionLoadContext context)
        {
            if (context is null)
            {
                return false;
            }

            XElement volumeElement = context.VolumeElement;
            if (volumeElement is null)
            {
                return false;
            }

            // Check for VolumeToEndpoint element with Endpoint attribute
            IEnumerable<XElement> mappingElements = volumeElement.Elements().Where(e => e.Name.LocalName == "VolumeToEndpoint");

            if (!mappingElements.Any())
            {
                return false;
            }

            XElement volumeToEndpointElement = mappingElements.First();
            XAttribute endpointAttribute = volumeToEndpointElement.Attribute("Endpoint");

            if (endpointAttribute is null || string.IsNullOrWhiteSpace(endpointAttribute.Value))
            {
                return false;
            }

            // Both VolumeToEndpoint element and Endpoint attribute are present
            return true;
        }

        /// <summary>
        /// Returns true if a SegmentationService is configured and available with a valid URL format.
        /// Null and whitespace endpoints cache as unavailable so a later Select can clear the cache
        /// via <see cref="InvalidateSegmentationServiceAvailability"/>.
        /// </summary>
        public static bool IsSegmentationServiceAvailable
        {
            get
            {
                if (_isSegmentationServiceAvailable.HasValue)
                {
                    return _isSegmentationServiceAvailable.Value;
                }

                var segmentationService = ServiceLocator.ServiceProvider.GetRequiredService<IGrpcServiceConfiguration>();
                if (segmentationService is null)
                {
                    _isSegmentationServiceAvailable = false;
                    return false;
                }

                _isSegmentationServiceAvailable = HasValidSegmentationServiceUrl(segmentationService.Endpoint());
                return _isSegmentationServiceAvailable.Value;
            }
        }

        /// <summary>
        /// Drops the cached availability result so the next read re-evaluates
        /// <see cref="IGrpcServiceConfiguration.Endpoint"/>. Called after mid-session Select/None.
        /// </summary>
        public static void InvalidateSegmentationServiceAvailability()
        {
            _isSegmentationServiceAvailable = null;
        }

        /// <summary>
        /// True when <paramref name="serviceUrl"/> is a usable http(s) host for gRPC.
        /// Host:port without a scheme is accepted.
        /// </summary>
        internal static bool HasValidSegmentationServiceUrl(string? serviceUrl)
        {
            if (string.IsNullOrWhiteSpace(serviceUrl))
                return false;

            string urlToValidate = serviceUrl.Contains("://") ? serviceUrl : $"http://{serviceUrl}";
            return Uri.TryCreate(urlToValidate, UriKind.Absolute, out Uri result) &&
                   (result.Scheme == Uri.UriSchemeHttp || result.Scheme == Uri.UriSchemeHttps);
        }

        /// <summary>
        /// Gets the SegmentationServiceUrl from configuration (set at login from the web service).
        /// </summary>
        public static string GetSegmentationServiceUrl() => AnnotationSettings.SegmentationServiceUrl;

        internal static int NumSectionsInMemory => AnnotationSettings.NumSectionsInMemory;

        /// <summary>
        /// Make radius of annotations on adjacent sections half of the normal value
        /// </summary>
        public static double AdjacentLocationRadiusScalar => AnnotationSettings.AdjacentLocationRadiusScalar;

        public static uint NumCurveInterpolationPoints(bool Closed) => Geometry.Global.NumCurveInterpolationPoints(Closed);

        //TODO: Choose number of points based on distance between control points
        public static uint NumOpenCurveInterpolationPoints => Geometry.Global.NumOpenCurveInterpolationPoints;
        public static uint NumClosedCurveInterpolationPoints => Geometry.Global.NumClosedCurveInterpolationPoints;

        public static uint NumClosedCurveInterpolationPointsForDisplay => AnnotationSettings.NumClosedCurveInterpolationPointsForDisplay;

        public static int PenSimplifyThreshold => AnnotationSettings.PenSimplifyThreshold;

        public static double DefaultClosedLineWidth => AnnotationSettings.DefaultClosedLineWidth;

        public static double MinRadius => AnnotationSettings.MinRadius;

        public static WebAnnotation.UI.Forms.PenAnnotationViewForm? PenAnnotationForm = null;

        /// <summary>
        /// Wrapper class for annotation settings with validation
        /// </summary>
        public static class AnnotationSettings
        {
            static AnnotationSettings()
            {
                Properties.Settings.UpgradeFromPreviousVersionIfNeeded();
                CircleView.SmallestRenderedSizeAccessor = () => SmallestRenderedSize;
                LocationCanvasView.SmallestRenderedSizeAccessor = () => SmallestRenderedSize;
            }

            private const int MIN_SECTIONS_IN_MEMORY = 1;
            private const int MAX_SECTIONS_IN_MEMORY = 100;
            private const int MIN_SECTIONS_LOADING = 1;
            private const int MAX_SECTIONS_LOADING = 50;
            private const double MIN_SCALE_FACTOR = 0.1;
            private const double MAX_SCALE_FACTOR = 50.0;
            private const double MIN_LINE_WIDTH = 1.0;
            private const double MAX_LINE_WIDTH = 100.0;
            private const double MIN_DOWNSAMPLE = 1.0;
            private const double MAX_DOWNSAMPLE = 64.0;
            private const double MIN_RADIUS_SCALAR = 0.1;
            private const double MAX_RADIUS_SCALAR = 2.0;
            private const int MIN_CURVE_POINTS = 2;
            private const int MAX_CURVE_POINTS = 20;
            private const int MIN_PEN_THRESHOLD = 1;
            private const int MAX_PEN_THRESHOLD = 100;
            private const double MIN_RADIUS = 0.1;
            private const double MAX_RADIUS = 10.0;
            private const double MIN_OPACITY = 0.0;
            private const double MAX_OPACITY = 1.0;
            private const double MIN_SEGMENTATION_POINT_RADIUS = 1.0;
            private const double MIN_SEGMENTATION_HOLE_DROP_FRACTION = 0.0;
            private const double MAX_SEGMENTATION_HOLE_DROP_FRACTION = 1.0;
            private const int MIN_SEGMENTATION_EDGE_CLEANUP_RADIUS = 0;
            private const int MAX_SEGMENTATION_EDGE_CLEANUP_RADIUS = 10;
            private const double MIN_POLYGON_POINT_RADIUS = 1.0;
            private const double MIN_SMALLEST_RENDERED_SIZE = 0.5;
            private const double MIN_AUTOPOLYGONIZE_SCREEN_AREA_PERCENT = 0.0;
            private const double MAX_AUTOPOLYGONIZE_SCREEN_AREA_PERCENT = 10.0;
            private const double MIN_AUTOPOLYGONIZE_RADIUS_PIXELS = 0.0;
            private const double MAX_AUTOPOLYGONIZE_RADIUS_PIXELS = 256.0;
            private const double MIN_AUTOPOLYGONIZE_RADIUS_NANOMETERS = 0.0;
            private const double MAX_AUTOPOLYGONIZE_RADIUS_NANOMETERS = 10000.0;
            private const double MIN_AUTOPOLYGONIZE_MAX_DOWNSAMPLE = 1.0;
            private const double MAX_AUTOPOLYGONIZE_MAX_DOWNSAMPLE = 256.0;

            // Use shared MathUtils.Clamp methods (Math.Clamp not available in .NET Framework 4.8)

            public static int NumSectionsInMemory
            {
                get => MathUtils.Clamp(Properties.Settings.Default.NumSectionsInMemory, MIN_SECTIONS_IN_MEMORY, MAX_SECTIONS_IN_MEMORY);
                set
                {
                    Properties.Settings.Default.NumSectionsInMemory = MathUtils.Clamp(value, MIN_SECTIONS_IN_MEMORY, MAX_SECTIONS_IN_MEMORY);
                    Properties.Settings.Default.Save();
                    OnSettingsChanged();
                }
            }

            public static int NumSectionsLoading
            {
                get => MathUtils.Clamp(Properties.Settings.Default.NumSectionsLoading, MIN_SECTIONS_LOADING, MAX_SECTIONS_LOADING);
                set
                {
                    Properties.Settings.Default.NumSectionsLoading = MathUtils.Clamp(value, MIN_SECTIONS_LOADING, MAX_SECTIONS_LOADING);
                    Properties.Settings.Default.Save();
                }
            }

            public static float LocationTextScaleFactor
            {
                get => MathUtils.Clamp(Properties.Settings.Default.LocationTextScaleFactor, MIN_SCALE_FACTOR, MAX_SCALE_FACTOR);
                set
                {
                    Properties.Settings.Default.LocationTextScaleFactor = MathUtils.Clamp(value, MIN_SCALE_FACTOR, MAX_SCALE_FACTOR);
                    Properties.Settings.Default.Save();
                }
            }

            public static float ReferenceLocationTextScaleFactor
            {
                get => MathUtils.Clamp(Properties.Settings.Default.ReferenceLocationTextScaleFactor, MIN_SCALE_FACTOR, MAX_SCALE_FACTOR);
                set
                {
                    Properties.Settings.Default.ReferenceLocationTextScaleFactor = MathUtils.Clamp(value, MIN_SCALE_FACTOR, MAX_SCALE_FACTOR);
                    Properties.Settings.Default.Save();
                }
            }

            public static double DefaultClosedLineWidth
            {
                get => MathUtils.Clamp(Properties.Settings.Default.DefaultClosedLineWidth, MIN_LINE_WIDTH, MAX_LINE_WIDTH);
                set
                {
                    Properties.Settings.Default.DefaultClosedLineWidth = MathUtils.Clamp(value, MIN_LINE_WIDTH, MAX_LINE_WIDTH);
                    Properties.Settings.Default.Save();
                }
            }

            public static double DefaultLocationJumpDownsample
            {
                get => MathUtils.Clamp(Properties.Settings.Default.DefaultLocationJumpDownsample, MIN_DOWNSAMPLE, MAX_DOWNSAMPLE);
                set
                {
                    Properties.Settings.Default.DefaultLocationJumpDownsample = MathUtils.Clamp(value, MIN_DOWNSAMPLE, MAX_DOWNSAMPLE);
                    Properties.Settings.Default.Save();
                }
            }

            public static double AdjacentLocationRadiusScalar
            {
                get => MathUtils.Clamp(Properties.Settings.Default.AdjacentLocationRadiusScalar, MIN_RADIUS_SCALAR, MAX_RADIUS_SCALAR);
                set
                {
                    Properties.Settings.Default.AdjacentLocationRadiusScalar = MathUtils.Clamp(value, MIN_RADIUS_SCALAR, MAX_RADIUS_SCALAR);
                    Properties.Settings.Default.Save();
                }
            }

            public static uint NumClosedCurveInterpolationPointsForDisplay
            {
                get => (uint)MathUtils.Clamp((int)Properties.Settings.Default.NumClosedCurveInterpolationPointsForDisplay, MIN_CURVE_POINTS, MAX_CURVE_POINTS);
                set
                {
                    Properties.Settings.Default.NumClosedCurveInterpolationPointsForDisplay = (uint)MathUtils.Clamp((int)value, MIN_CURVE_POINTS, MAX_CURVE_POINTS);
                    Properties.Settings.Default.Save();
                }
            }

            public static int PenSimplifyThreshold
            {
                get => MathUtils.Clamp(Properties.Settings.Default.PenSimplifyThreshold, MIN_PEN_THRESHOLD, MAX_PEN_THRESHOLD);
                set
                {
                    Properties.Settings.Default.PenSimplifyThreshold = MathUtils.Clamp(value, MIN_PEN_THRESHOLD, MAX_PEN_THRESHOLD);
                    Properties.Settings.Default.Save();
                }
            }

            public static double MinRadius
            {
                get => MathUtils.Clamp(Properties.Settings.Default.MinRadius, MIN_RADIUS, MAX_RADIUS);
                set
                {
                    Properties.Settings.Default.MinRadius = MathUtils.Clamp(value, MIN_RADIUS, MAX_RADIUS);
                    Properties.Settings.Default.Save();
                }
            }

            public static float PolygonOpacityParentless
            {
                get => MathUtils.Clamp(Properties.Settings.Default.PolygonOpacityParentless, MIN_OPACITY, MAX_OPACITY);
                set
                {
                    Properties.Settings.Default.PolygonOpacityParentless = MathUtils.Clamp(value, MIN_OPACITY, MAX_OPACITY);
                    Properties.Settings.Default.Save();
                }
            }

            public static float PolygonOpacityWithParent
            {
                get => MathUtils.Clamp(Properties.Settings.Default.PolygonOpacityWithParent, MIN_OPACITY, MAX_OPACITY);
                set
                {
                    Properties.Settings.Default.PolygonOpacityWithParent = MathUtils.Clamp(value, MIN_OPACITY, MAX_OPACITY);
                    Properties.Settings.Default.Save();
                }
            }

            public static float CircleOpacityParentless
            {
                get => MathUtils.Clamp(Properties.Settings.Default.CircleOpacityParentless, MIN_OPACITY, MAX_OPACITY);
                set
                {
                    Properties.Settings.Default.CircleOpacityParentless = MathUtils.Clamp(value, MIN_OPACITY, MAX_OPACITY);
                    Properties.Settings.Default.Save();
                }
            }

            public static float CircleOpacityWithParent
            {
                get => MathUtils.Clamp(Properties.Settings.Default.CircleOpacityWithParent, MIN_OPACITY, MAX_OPACITY);
                set
                {
                    Properties.Settings.Default.CircleOpacityWithParent = MathUtils.Clamp(value, MIN_OPACITY, MAX_OPACITY);
                    Properties.Settings.Default.Save();
                }
            }

            /// <summary>
            /// Gets the appropriate opacity value for an annotation based on its type and whether its structure has a parent.
            /// </summary>
            /// <param name="typeCode">The location type of the annotation</param>
            /// <param name="hasParent">True if the annotation's structure has a parent structure, false otherwise</param>
            /// <returns>The opacity value (0.0 to 1.0) based on the annotation type and parent status</returns>
            public static float GetOpacityForAnnotationType(Viking.AnnotationServiceTypes.Interfaces.LocationType typeCode, bool hasParent)
            {
                // Determine which opacity settings to use based on annotation type
                // Circles use circle opacity, polygons and curves use polygon opacity
                switch (typeCode)
                {
                    case Viking.AnnotationServiceTypes.Interfaces.LocationType.CIRCLE:
                        return hasParent ? CircleOpacityWithParent : CircleOpacityParentless;

                    case Viking.AnnotationServiceTypes.Interfaces.LocationType.POLYGON:
                    case Viking.AnnotationServiceTypes.Interfaces.LocationType.CURVEPOLYGON:
                    case Viking.AnnotationServiceTypes.Interfaces.LocationType.OPENCURVE:
                    case Viking.AnnotationServiceTypes.Interfaces.LocationType.CLOSEDCURVE:
                        return hasParent ? PolygonOpacityWithParent : PolygonOpacityParentless;

                    default:
                        // Default to polygon opacity for unknown types
                        return hasParent ? PolygonOpacityWithParent : PolygonOpacityParentless;
                }
            }

            /// <summary>
            /// Gets the appropriate opacity value for an annotation view based on its type and whether its structure has a parent.
            /// </summary>
            /// <param name="view">The location canvas view</param>
            /// <returns>The opacity value (0.0 to 1.0) based on the annotation type and parent status</returns>
            public static float GetOpacityForAnnotation(View.LocationCanvasView view)
            {
                bool hasParent = view.Parent?.ParentID.HasValue ?? false;
                return GetOpacityForAnnotationType(view.TypeCode, hasParent);
            }

            public static string SegmentationServiceUrl
            {
                get => Properties.Settings.Default.SegmentationServiceUrl;
                set
                {
                    Properties.Settings.Default.SegmentationServiceUrl = value;
                    Properties.Settings.Default.Save();
                    // Clear cached availability check when URL changes
                    _isSegmentationServiceAvailable = null;
                    // Reset the shared channel so it reconnects to the new URL
                    if (ServiceLocator.IsInitialized)
                    {
                        ServiceLocator.GrpcChannelManager?.ResetChannel();
                    }
                }
            }

            public static double SegmentationPointRadius
            {
                get => Math.Max(MIN_SEGMENTATION_POINT_RADIUS, Properties.Settings.Default.SegmentationPointRadius);
                set
                {
                    Properties.Settings.Default.SegmentationPointRadius = Math.Max(MIN_SEGMENTATION_POINT_RADIUS, value);
                    Properties.Settings.Default.Save();
                    RefreshActiveSegmentationPromptPoints();
                }
            }

            public static double SegmentationHoleDropFraction
            {
                get => MathUtils.Clamp(
                    Properties.Settings.Default.SegmentationHoleDropFraction,
                    MIN_SEGMENTATION_HOLE_DROP_FRACTION,
                    MAX_SEGMENTATION_HOLE_DROP_FRACTION);
                set
                {
                    Properties.Settings.Default.SegmentationHoleDropFraction = MathUtils.Clamp(
                        value,
                        MIN_SEGMENTATION_HOLE_DROP_FRACTION,
                        MAX_SEGMENTATION_HOLE_DROP_FRACTION);
                    Properties.Settings.Default.Save();
                    RefreshActiveSegmentationPolygons();
                }
            }

            public static int SegmentationEdgeCleanupRadius
            {
                get => MathUtils.Clamp(
                    Properties.Settings.Default.SegmentationEdgeCleanupRadius,
                    MIN_SEGMENTATION_EDGE_CLEANUP_RADIUS,
                    MAX_SEGMENTATION_EDGE_CLEANUP_RADIUS);
                set
                {
                    Properties.Settings.Default.SegmentationEdgeCleanupRadius = MathUtils.Clamp(
                        value,
                        MIN_SEGMENTATION_EDGE_CLEANUP_RADIUS,
                        MAX_SEGMENTATION_EDGE_CLEANUP_RADIUS);
                    Properties.Settings.Default.Save();
                    RefreshActiveSegmentationPolygons();
                }
            }

            /// <summary>
            /// Enables idle-driven SAM2 proposals on circles. Until the user toggles this,
            /// review/admin sessions default on and annotate sessions default off. After a
            /// toggle the chosen value persists across restarts.
            /// </summary>
            public static bool AutoPolygonizeCircles
            {
                get
                {
                    if (!Properties.Settings.Default.AutoPolygonizeCirclesUserSet)
                        return VolumeAccessRoles.HasReviewAccess();

                    return Properties.Settings.Default.AutoPolygonizeCircles;
                }
                set
                {
                    bool previous = AutoPolygonizeCircles;
                    Properties.Settings.Default.AutoPolygonizeCirclesUserSet = true;
                    Properties.Settings.Default.AutoPolygonizeCircles = value;
                    Properties.Settings.Default.Save();
                    if (previous == value)
                        return;

                    AnnotationOverlay.CurrentOverlay?.SetAutoPolygonizeEnabled(value);
                }
            }

            /// <summary>
            /// Circles smaller than this percent of the viewport area are skipped. 0 accepts any positive radius.
            /// Matches the preferences PercentOfScreen preview (area fraction of the view).
            /// </summary>
            public static double AutoPolygonizeMinScreenAreaPercent
            {
                get => MathUtils.Clamp(
                    Properties.Settings.Default.AutoPolygonizeMinScreenAreaPercent,
                    MIN_AUTOPOLYGONIZE_SCREEN_AREA_PERCENT,
                    MAX_AUTOPOLYGONIZE_SCREEN_AREA_PERCENT);
                set
                {
                    Properties.Settings.Default.AutoPolygonizeMinScreenAreaPercent = MathUtils.Clamp(
                        value,
                        MIN_AUTOPOLYGONIZE_SCREEN_AREA_PERCENT,
                        MAX_AUTOPOLYGONIZE_SCREEN_AREA_PERCENT);
                    Properties.Settings.Default.Save();
                }
            }

            /// <summary>
            /// Minimum on-screen radius, in device pixels, for a circle to be auto-segmented.
            /// 0 accepts any positive radius. Preferences show this as nanometers at the current zoom.
            /// </summary>
            public static double AutoPolygonizeMinRadiusPixels
            {
                get => MathUtils.Clamp(
                    Properties.Settings.Default.AutoPolygonizeMinRadiusPixels,
                    MIN_AUTOPOLYGONIZE_RADIUS_PIXELS,
                    MAX_AUTOPOLYGONIZE_RADIUS_PIXELS);
                set
                {
                    Properties.Settings.Default.AutoPolygonizeMinRadiusPixels = MathUtils.Clamp(
                        value,
                        MIN_AUTOPOLYGONIZE_RADIUS_PIXELS,
                        MAX_AUTOPOLYGONIZE_RADIUS_PIXELS);
                    Properties.Settings.Default.Save();
                }
            }

            /// <summary>
            /// Minimum circle radius, in nanometers, for auto-polygonize. Default 75.
            /// 0 accepts any positive radius. Preview size uses the current zoom.
            /// </summary>
            public static double AutoPolygonizeMinRadiusNanometers
            {
                get => MathUtils.Clamp(
                    Properties.Settings.Default.AutoPolygonizeMinRadiusNanometers,
                    MIN_AUTOPOLYGONIZE_RADIUS_NANOMETERS,
                    MAX_AUTOPOLYGONIZE_RADIUS_NANOMETERS);
                set
                {
                    Properties.Settings.Default.AutoPolygonizeMinRadiusNanometers = MathUtils.Clamp(
                        value,
                        MIN_AUTOPOLYGONIZE_RADIUS_NANOMETERS,
                        MAX_AUTOPOLYGONIZE_RADIUS_NANOMETERS);
                    Properties.Settings.Default.Save();
                }
            }

            /// <summary>
            /// Coarsest camera downsample at which auto-segment still runs. Default 8.
            /// A view coarser than this is not sent to the segmentation service.
            /// Changing it restarts the idle timer so a view that is now allowed can run without a camera nudge.
            /// </summary>
            public static double AutoPolygonizeMaxDownsample
            {
                get => MathUtils.Clamp(
                    Properties.Settings.Default.AutoPolygonizeMaxDownsample,
                    MIN_AUTOPOLYGONIZE_MAX_DOWNSAMPLE,
                    MAX_AUTOPOLYGONIZE_MAX_DOWNSAMPLE);
                set
                {
                    double clamped = MathUtils.Clamp(
                        value,
                        MIN_AUTOPOLYGONIZE_MAX_DOWNSAMPLE,
                        MAX_AUTOPOLYGONIZE_MAX_DOWNSAMPLE);
                    if (Math.Abs(Properties.Settings.Default.AutoPolygonizeMaxDownsample - clamped) < 0.001)
                        return;

                    Properties.Settings.Default.AutoPolygonizeMaxDownsample = clamped;
                    Properties.Settings.Default.Save();
                    AnnotationOverlay.CurrentOverlay?.RequestAutoPolygonizeIdlePass();
                }
            }

            /// <summary>
            /// Debug overlay of the raw SAM2 mask on auto-polygonize proposals.
            /// Debug builds start on until the user toggles this. After a toggle the choice
            /// is saved and copied into the next Viking version.
            /// </summary>
            public static bool AutoPolygonizeOverlayMasks
            {
                get
                {
#if DEBUG
                    if (!Properties.Settings.Default.AutoPolygonizeOverlayMasksUserSet)
                        return true;
#endif
                    return Properties.Settings.Default.AutoPolygonizeOverlayMasks;
                }
                set
                {
                    bool previous = AutoPolygonizeOverlayMasks;
                    Properties.Settings.Default.AutoPolygonizeOverlayMasksUserSet = true;
                    Properties.Settings.Default.AutoPolygonizeOverlayMasks = value;
                    Properties.Settings.Default.Save();
                    if (previous == value)
                        return;

                    AnnotationOverlay.CurrentOverlay?.SetAutoPolygonizeOverlayMasksEnabled(value);
                }
            }

            /// <summary>
            /// Green foreground and red background clicks on auto-polygonize proposals.
            /// Debug builds start on until the user toggles this. After a toggle the choice
            /// is saved and copied into the next Viking version. Independent of the mask overlay.
            /// </summary>
            public static bool AutoPolygonizeOverlayPrompts
            {
                get
                {
#if DEBUG
                    if (!Properties.Settings.Default.AutoPolygonizeOverlayPromptsUserSet)
                        return true;
#endif
                    return Properties.Settings.Default.AutoPolygonizeOverlayPrompts;
                }
                set
                {
                    bool previous = AutoPolygonizeOverlayPrompts;
                    Properties.Settings.Default.AutoPolygonizeOverlayPromptsUserSet = true;
                    Properties.Settings.Default.AutoPolygonizeOverlayPrompts = value;
                    Properties.Settings.Default.Save();
                    if (previous == value)
                        return;

                    AnnotationOverlay.CurrentOverlay?.SetAutoPolygonizeOverlayPromptsEnabled(value);
                }
            }

            /// <summary>
            /// Omits auto-polygonize outlines while the mask overlay is visible.
            /// Saved across restarts. Has no effect while masks are off.
            /// </summary>
            public static bool AutoPolygonizeHideSegmentationRings
            {
                get => Properties.Settings.Default.AutoPolygonizeHideSegmentationRings;
                set
                {
                    if (Properties.Settings.Default.AutoPolygonizeHideSegmentationRings == value)
                        return;

                    Properties.Settings.Default.AutoPolygonizeHideSegmentationRings = value;
                    Properties.Settings.Default.Save();
                    AnnotationOverlay.CurrentOverlay?.RefreshAutoPolygonizeOverlay();
                }
            }

            /// <summary>
            /// Proposal outlines draw while auto-polygonize is on.
            /// They are omitted only when the mask overlay is visible and <see cref="AutoPolygonizeHideSegmentationRings"/> is set.
            /// </summary>
            public static bool AutoPolygonizeShowSegmentationRings =>
                !AutoPolygonizeOverlayMasks || !AutoPolygonizeHideSegmentationRings;

            public static double PolygonPointRadius
            {
                get => Math.Max(MIN_POLYGON_POINT_RADIUS, Properties.Settings.Default.PolygonPointRadius);
                set
                {
                    Properties.Settings.Default.PolygonPointRadius = Math.Max(MIN_POLYGON_POINT_RADIUS, value);
                    Properties.Settings.Default.Save();
                }
            }

            public static double SmallestRenderedSize
            {
                get => Math.Max(MIN_SMALLEST_RENDERED_SIZE, Properties.Settings.Default.SmallestRenderedSize);
                set
                {
                    Properties.Settings.Default.SmallestRenderedSize = Math.Max(MIN_SMALLEST_RENDERED_SIZE, value);
                    Properties.Settings.Default.Save();
                }
            }

            public static void ResetToDefaults()
            {
                Properties.Settings.Default.NumSectionsInMemory = 10;
                Properties.Settings.Default.NumSectionsLoading = 5;
                Properties.Settings.Default.LocationTextScaleFactor = 5;
                Properties.Settings.Default.ReferenceLocationTextScaleFactor = 2.5f;
                Properties.Settings.Default.DefaultClosedLineWidth = 24.0;
                Properties.Settings.Default.DefaultLocationJumpDownsample = 4.0;
                Properties.Settings.Default.AdjacentLocationRadiusScalar = 0.5;
                Properties.Settings.Default.NumClosedCurveInterpolationPointsForDisplay = 4;
                Properties.Settings.Default.PenSimplifyThreshold = 8;
                Properties.Settings.Default.MinRadius = 0.5;
                Properties.Settings.Default.PolygonOpacityParentless = 0.5f;
                Properties.Settings.Default.PolygonOpacityWithParent = 0.33f;
                Properties.Settings.Default.CircleOpacityParentless = 0.5f;
                Properties.Settings.Default.CircleOpacityWithParent = 1.0f;
                Properties.Settings.Default.SegmentationPointRadius = 5.0;
                Properties.Settings.Default.SegmentationHoleDropFraction = 0.03;
                Properties.Settings.Default.SegmentationEdgeCleanupRadius = 2;
                Properties.Settings.Default.AutoPolygonizeCircles = false;
                Properties.Settings.Default.AutoPolygonizeCirclesUserSet = false;
                Properties.Settings.Default.AutoPolygonizeMinScreenAreaPercent = 1.0;
                Properties.Settings.Default.AutoPolygonizeMinRadiusPixels = 8.0;
                Properties.Settings.Default.AutoPolygonizeMinRadiusNanometers = 75.0;
                Properties.Settings.Default.AutoPolygonizeMaxDownsample = 8.0;
                Properties.Settings.Default.AutoPolygonizeOverlayMasks = false;
                Properties.Settings.Default.AutoPolygonizeOverlayMasksUserSet = false;
                Properties.Settings.Default.AutoPolygonizeOverlayPrompts = false;
                Properties.Settings.Default.AutoPolygonizeOverlayPromptsUserSet = false;
                Properties.Settings.Default.AutoPolygonizeHideSegmentationRings = false;
                Properties.Settings.Default.PolygonPointRadius = 6.0;
                Properties.Settings.Default.SmallestRenderedSize = 0.5;
                Properties.Settings.Default.Save();
                OnSettingsChanged();
            }

            private static void OnSettingsChanged()
            {
                if (AnnotationOverlay.CurrentOverlay != null)
                {
                    AnnotationOverlay.UpdateCacheSize(NumSectionsInMemory);
                    AnnotationOverlay.CurrentOverlay.SetAutoPolygonizeEnabled(AutoPolygonizeCircles);
                    AnnotationOverlay.CurrentOverlay.SetAutoPolygonizeOverlayMasksEnabled(AutoPolygonizeOverlayMasks);
                    AnnotationOverlay.CurrentOverlay.SetAutoPolygonizeOverlayPromptsEnabled(AutoPolygonizeOverlayPrompts);
                    AnnotationOverlay.CurrentOverlay.RefreshAutoPolygonizeOverlay();
                    RefreshActiveSegmentationPolygons();
                    RefreshActiveSegmentationPromptPoints();
                }
            }

            private static void RefreshActiveSegmentationPolygons(double? holeDropFraction = null)
            {
                InvokeOnActiveSegmentationCommand(command => command.RefreshPolygonsFromLastMask(holeDropFraction));
            }

            private static void RefreshActiveSegmentationPromptPoints(double? pointRadiusPixels = null)
            {
                InvokeOnActiveSegmentationCommand(command => command.RefreshPromptPointViews(pointRadiusPixels));
            }

            private static void InvokeOnActiveSegmentationCommand(System.Action<SegmentationCommand> action)
            {
                var parent = AnnotationOverlay.CurrentOverlay?.Parent;
                if (parent?.CurrentCommand is not SegmentationCommand command)
                    return;

                if (parent.InvokeRequired)
                    parent.BeginInvoke(new System.Action(() => action(command)));
                else
                    action(command);
            }
        }

        /// <summary>
        /// Number of interpolations to place between curve control points, determines distance between control points
        /// </summary>
        //static public double CurveInterpolationPointSpacing = 100.0;

        //static public int NumCurveInterpolationPoints(double distance)
        //{
        //return (int)Math.Round(distance / CurveInterpolationPointSpacing);
        //}

        /// <summary>
        /// This is hardcoded for now, but should be read from the VikingXML file
        /// </summary>
        internal static Geometry.Vector3 Scale;
        private static readonly string WebAnnotationPath = Viking.UI.State.VolumeCachePath + System.IO.Path.DirectorySeparatorChar + "WebAnnotation";

        /// <summary>
        /// Bookmark filename only
        /// </summary>
        private static readonly string UserSettingsFileName = "UserSettings.xml";

        /// <summary>
        /// The full name of the settings file including filename and path
        /// </summary>
        private static readonly string UserSettingsFilePath = WebAnnotationPath + System.IO.Path.DirectorySeparatorChar + UserSettingsFileName;
        private static XElement? UserSettingsElement = null;

        public static bool PenMode
        {
            get => WebAnnotation.Properties.Settings.Default.PenMode;
            set
            {
                WebAnnotation.Properties.Settings.Default.PenMode = value;
                WebAnnotation.Properties.Settings.Default.Save();
            }
        }

        private static System.Collections.ObjectModel.ObservableCollection<ulong> _UserFavoriteStructureTypes;

        public static System.Collections.ObjectModel.ObservableCollection<ulong> UserFavoriteStructureTypes
        {
            get
            {
                if (_UserFavoriteStructureTypes is null)
                {
                    _UserFavoriteStructureTypes = [];
                    foreach (string ID_str in Properties.Settings.Default.FavoriteStructureIDs)
                    {
                        try
                        {
                            ulong ID = System.Convert.ToUInt64(ID_str);
                            if (_UserFavoriteStructureTypes.Contains(ID) == false) //Do not add accidental duplicates
                            {
                                _UserFavoriteStructureTypes.Add(ID);
                            }
                        }
                        catch (ArgumentException)
                        {
                            Trace.WriteLine($"Unable to convert Favorite StructureID to long {ID_str}");
                        }
                    }

                    _UserFavoriteStructureTypes.CollectionChanged += OnFavoriteStructureTypesChanged;
                }

                return _UserFavoriteStructureTypes;
            }
        }

        private static void OnFavoriteStructureTypesChanged(object sender, System.Collections.Specialized.NotifyCollectionChangedEventArgs e)
        {
            switch (e.Action)
            {
                case NotifyCollectionChangedAction.Add:
                    foreach (object item in e.NewItems)
                    {
                        Properties.Settings.Default.FavoriteStructureIDs.Add($"{item}");
                    }
                    break;
                case NotifyCollectionChangedAction.Remove:
                    foreach (object item in e.OldItems)
                    {
                        Properties.Settings.Default.FavoriteStructureIDs.Remove($"{item}");
                    }
                    break;
                case NotifyCollectionChangedAction.Reset:
                    Properties.Settings.Default.FavoriteStructureIDs.Clear();
                    break;
                case NotifyCollectionChangedAction.Move:
                    break;
                case NotifyCollectionChangedAction.Replace:
                    foreach (object item in e.OldItems)
                    {
                        Properties.Settings.Default.FavoriteStructureIDs.Remove($"{item}");
                    }
                    foreach (object item in e.NewItems)
                    {
                        Properties.Settings.Default.FavoriteStructureIDs.Add($"{item}");
                    }
                    break;
            }

            /* The brute force approach */
            /*
            StringCollection newList = new System.Collections.Specialized.StringCollection();
            foreach(long ID in _UserFavoriteStructureTypes)
            {
                newList.Add(string.Format("{0}", ID));
            }

            Properties.Settings.Default.FavoriteStructureIDs = newList;
            */
            Properties.Settings.Default.Save(); //Persist the updated list
        }

        private static Uri UserSettingsUri
        {
            get
            {
                if (UserSettingsElement != null)
                {
                    XAttribute UriAttrib = UserSettingsElement.Attribute("Uri");
                    if (UriAttrib != null)
                    {
                        return new Uri(UriAttrib.Value);
                    }
                    else
                    {
                        throw new XMLMissingDataException("The <DefaultWebAnnotationUserSettings> element under the <Volume> element is missing the uri attribute.");
                    }
                }

                return null;
            }
        }

        /// <summary>6
        /// The home of the user settings XSD file
        /// </summary>
//        static internal readonly string XSDUri = "http://connectomes.utah.edu/XSD/BookmarkSchema.xsd";

        private static XRoot _userSettingsDoc;
        private static Task? _loadUserPreferencesTask = null;

        public static string EndpointName
        {
            get;
            internal set;
        }


        internal static UserSettings UserSettings
        {
            get
            {
                if (_userSettingsDoc is null)
                {

                    LoadUserPreferencesAsync();
                    return null;
                }

                return _userSettingsDoc?.UserSettings;
            }
        }

        /// <summary>
        /// LastEditedAnnotationID can have no value if no location has been editted
        /// It can also have the ID of a deleted location.  Deleted locations return
        /// null objects when requested from the server.
        /// </summary>
        public static long? LastEditedAnnotationID;

        /// <summary>
        /// Return true if the last annotation can be continued on the section number. 
        /// Continuation creates a new annotation on the section and links to the last.
        /// </summary>
        /// <param name="SectionNumber"></param>
        /// <returns></returns>
        internal static bool CanContinueLastTrace(int SectionNumber)
        {
            if (LastEditedAnnotationID is null)
                return false;

            if (_userSettingsDoc is null)
                return false;

            WebAnnotationModel.LocationObj lastLoc = WebAnnotationModel.Store.Locations.GetObjectByID(Global.LastEditedAnnotationID.Value, false);
            if (lastLoc is null)
                return false;

            return (int)Math.Round(lastLoc.Z) != SectionNumber;
        }

        #region IInitExtensions Members

        /*
         //This function was intended to determine what access level the user had to the annotations
        bool ValidateUser()
        {
            
            AuthenticationServiceClient proxy = new AuthenticationServiceClient("BasicHttpBinding_AuthenticationService",
                                                                                Global.AuthenticationAddress);

            
            proxy.Open();

            if (proxy.IsLoggedIn())
                return true;

            proxy.Login(Viking.UI.State.UserCredentials.UserName, Viking.UI.State.UserCredentials.Password, "", true);
            
           
            return false;
        }
         */

        void IModuleServiceRegistrar.RegisterServices(IServiceCollection services)
        {
            if (services is null)
            {
                throw new ArgumentNullException(nameof(services));
            }

            services.AddSingleton<IGrpcServiceConfiguration>(provider =>
            {
                var appSettings = provider.GetService<ApplicationSettings>();
                return new WebAnnotationGrpcServiceConfiguration(appSettings);
            });
        }

        Task IModuleInitializer.InitializeAsync(IServiceProvider serviceProvider, CancellationToken cancellationToken)
        {
#if DEBUG
            //           return Task.CompletedTask;
#endif
            cancellationToken.ThrowIfCancellationRequested();

            ServiceLocator.RebuildServiceProvider(collection =>
            {
                collection.RemoveAll<IGrpcChannelManager>();
                collection.AddSingleton<IGrpcChannelManager>(sp =>
                {
                    var configuration = sp.GetRequiredService<IGrpcServiceConfiguration>();
                    return new GrpcChannelManager(configuration);
                });
            });

            var refreshedProvider = ServiceLocator.ServiceProvider ?? serviceProvider;

            if (!InitializeModule(refreshedProvider))
            {
                throw new InvalidOperationException("WebAnnotation initialization failed.");
            }

            return Task.CompletedTask;
        }

        private static bool InitializeModule(IServiceProvider serviceProvider)
        {
            AnnotationService.Types.Settings.PrepareSerializers();

            //Find the server hosting the volume.  Look for an XML file mapping the volume to an endpoint.
            Viking.ViewModels.VolumeViewModel volume = Viking.UI.State.volume;

            if (volume is null)
            {
                return false;
            }

            //Section Thickness is hard-coded, should be pulled from server.
            Scale = new Geometry.Vector3(volume.DefaultXYScale.Value, volume.DefaultXYScale.Value, 90.0);

            WebAnnotationModel.State.UserCredentials = Viking.UI.State.UserCredentials;

            if (serviceProvider?.GetService<ApplicationSettings>() is ApplicationSettings applicationSettings)
            {
                AnnotationSettings.SegmentationServiceUrl = applicationSettings.SegmentationURL;
            }

            serviceProvider?.GetService<IGrpcChannelManager>();

            if (GetEndpointFromXML(volume.VolumeElement))
            {
                try
                {
                    // Use Task.Run to avoid blocking the thread pool, with proper synchronization
                    var loadTask = Task.Run(async () => await LoadUserPreferencesAsync().ConfigureAwait(false));
                    loadTask.GetAwaiter().GetResult();
                }
                catch (OperationCanceledException)
                {
                    Trace.WriteLine("LoadUserPreferences timed out during initialization.");
                }
                WebAnnotationModel.Store.Init();
                if (Store.InitializationError != null)
                {
                    Trace.WriteLine("[WebAnnotation] Annotation store failed to start: " + Store.InitializationError);
                    System.Windows.Forms.MessageBox.Show(
                        "Viking could not load annotations.\n\n" + Store.InitializationError.Message +
                        "\n\nThe viewer will stay open. Annotations stay unavailable until a later launch is accepted by the annotation service.",
                        "Viking",
                        System.Windows.Forms.MessageBoxButtons.OK,
                        System.Windows.Forms.MessageBoxIcon.Warning);
                }
                return true;
            }

            return false;
        }

        private static XDocument GetAboutXML(Uri AboutURI)
        {
            // Use Task.Run to avoid blocking the thread pool when called from synchronous context
            return Task.Run(async () => await GetAboutXMLAsync(AboutURI).ConfigureAwait(false)).GetAwaiter().GetResult();
        }

        private static async Task<XDocument> GetAboutXMLAsync(Uri AboutURI)
        {
            using HttpClient httpClient = HttpClientFactory.CreateClient(AboutURI, Viking.UI.State.UserCredentials);
            try
            {
                var response = await httpClient.GetAsync(AboutURI).ConfigureAwait(false);
                response.EnsureSuccessStatusCode();

                var content = await response.Content.ReadAsStringAsync().ConfigureAwait(false);
                return XDocument.Parse(content);
            }
            catch (HttpRequestException)
            {
                Trace.WriteLine("Could not locate WebAnnotationMapping.XML, disabling WebAnnotations.", "WebAnnotation");
                return null;
            }
        }

        private static bool GetEndpointFromXML(XElement elem)
        {
            //Fetch the name if we know it
            switch (elem.Name.LocalName)
            {
                case "Volume":
                    IEnumerable<XElement> SettingsElements = elem.Elements().Where(e => e.Name.LocalName == "DefaultWebAnnotationUserSettings");
                    UserSettingsElement = SettingsElements.Count() > 0
                        ? SettingsElements.First()
                        : throw new XMLMissingDataException("The Volume Element is missing the <DefaultWebAnnotationUserSettings> element");

                    IEnumerable<XElement> MappingElements = elem.Elements().Where(e => e.Name.LocalName == "VolumeToEndpoint");

                    if (MappingElements.Count() == 0)
                    {
                        break;
                    }

                    Global.PopulateEndpointStateFromVolumeToEndpointElement(MappingElements.First());

                    break;
                default:
                    break;
            }

            //If we have an endpoint address then give the OK to load
            if (WebAnnotationModel.State.Endpoint != null)
            {
                return true;
            }

            //We don't have an endpoint to read/write annotations.  Do not load.
            return false;
        }

        private static void PopulateEndpointStateFromVolumeToEndpointElement(XElement MappingElement)
        {
            XAttribute NameAttribute = MappingElement.Attribute("Name");
            if (NameAttribute != null)
            {
                Global.EndpointName = NameAttribute.Value;
            }

            XAttribute EndpointAttribute = MappingElement.Attribute("Endpoint");
            if (EndpointAttribute != null)
            {
#if DEBUG
                WebAnnotationModel.State.Endpoint = new Uri(EndpointAttribute.Value);
                //                        WebAnnotationModel.State.EndpointAddress = new EndpointAddress("https://connectomes.utah.edu/Services/TestBinary/Annotate.svc");
#else
                WebAnnotationModel.State.Endpoint = new Uri(EndpointAttribute.Value);
#endif
            }

            XAttribute ExportURLAttribute = MappingElement.Attribute("ExportURL");
            if (ExportURLAttribute != null)
            {
                Global.Export = new WebAnnotation.Export(new Uri(ExportURLAttribute.Value));
            }

            /*
            XAttribute AuthenticationAttribute = MappingElement.Attribute("Authentication");
            if (AuthenticationAttribute != null)
            {
                Global._AuthenticationAddress = new EndpointAddress(AuthenticationAttribute.Value);
                ValidateUser(); 
            }
            */

            return;
        }

        private static Task LoadUserPreferencesAsync()
        {
            // Check if a load is already in progress
            Task existingTask = _loadUserPreferencesTask;
            if (existingTask != null && !existingTask.IsCompleted)
            {
                return existingTask;
            }

            // Create a new task with timeout
            CancellationTokenSource cts = new();
            cts.CancelAfter(TimeSpan.FromSeconds(10));

            _loadUserPreferencesTask = LoadUserPreferencesAsyncInternal(cts.Token);

            // Clear the task reference when it completes
            _loadUserPreferencesTask.ContinueWith(t =>
            {
                if (t == _loadUserPreferencesTask)
                {
                    _loadUserPreferencesTask = null;
                }
            }, TaskContinuationOptions.ExecuteSynchronously);

            return _loadUserPreferencesTask;
        }

        private static async Task LoadUserPreferencesAsyncInternal(CancellationToken cancellationToken)
        {
            try
            {
                bool LoadFromServer = false;
                if (false == System.IO.Directory.Exists(WebAnnotationPath))
                {
                    System.IO.Directory.CreateDirectory(WebAnnotationPath);
                    LoadFromServer = true;
                }

                if (!await CachedResourceIsValidAsync(UserSettingsFilePath, UserSettingsUri, cancellationToken))
                {
                    LoadFromServer = true;
                }

                if (LoadFromServer)
                {
                    bool success = await LoadServerUserSettingsAsync(cancellationToken).ConfigureAwait(false);
                    if (!success)
                    {
                        return;
                    }
                }

                cancellationToken.ThrowIfCancellationRequested();

                if (System.IO.File.Exists(UserSettingsFilePath))
                {
                    _userSettingsDoc = XRoot.Load(UserSettingsFilePath);
                }
            }
            catch (OperationCanceledException)
            {
                Trace.WriteLine("LoadUserPreferencesAsync timed out after 10 seconds.");
                throw;
            }
            catch (Xml.Schema.Linq.LinqToXsdException)
            {
                //We found it locally, but could not parse it
                bool success = await LoadServerUserSettingsAsync(cancellationToken);
                if (!success)
                {
                    throw;
                }

                cancellationToken.ThrowIfCancellationRequested();

                if (System.IO.File.Exists(UserSettingsFilePath))
                {
                    _userSettingsDoc = XRoot.Load(UserSettingsFilePath);
                }
            }
            catch (System.Xml.XmlException)
            {
                //We found it locally, but could not parse it
                bool success = await LoadServerUserSettingsAsync(cancellationToken);
                if (!success)
                {
                    throw;
                }

                cancellationToken.ThrowIfCancellationRequested();

                if (System.IO.File.Exists(UserSettingsFilePath))
                {
                    _userSettingsDoc = XRoot.Load(UserSettingsFilePath);
                }
            }
            /*
            catch (Exception )
            {
                //We found it, but could not parse it
  //              HandleIncorrectXSDMessage();
   //             LoadBookmarksFromBackup();
            }*/
        }

        /// <summary>
        /// Validates the provide file against the last modified date of the web resource
        /// </summary>
        /// <param name="CacheFilename"></param>
        /// <param name="textureUri"></param>
        /// <returns></returns>
        private static Task<bool> CachedResourceIsValid(string CacheFilename, Uri uri) => CachedResourceIsValidAsync(CacheFilename, uri);

        private static async Task<bool> CachedResourceIsValidAsync(string CacheFilename, Uri uri, CancellationToken cancellationToken = default)
        {
            if (uri is null)
            {
                return true;
            }

            if (!System.IO.File.Exists(CacheFilename))
            {
                return false;
            }

            using HttpClient httpClient = new();
            try
            {
                HttpRequestMessage request = new(HttpMethod.Head, uri);
                var response = await httpClient.SendAsync(request, cancellationToken).ConfigureAwait(false);

                if (!response.Content.Headers.LastModified.HasValue) return false;
                bool valid = response.Content.Headers.LastModified.Value.UtcDateTime <= System.IO.File.GetLastWriteTimeUtc(CacheFilename);
                return valid;
            }
            catch
            {
                return false;
            }
        }

        private static Task<bool> LoadServerUserSettings() => LoadServerUserSettingsAsync();

        private static async Task<bool> LoadServerUserSettingsAsync(CancellationToken cancellationToken = default)
        {
            //Try to download the default user settings file
            Uri uri = UserSettingsUri;
            uri ??= new Uri("http://rogue1.codepharm.net/RABBIT/WebAnnotationUserSettings.xml");

            try
            {
                using HttpClient httpClient = new();
                var response = await httpClient.GetAsync(uri, cancellationToken).ConfigureAwait(false);
                response.EnsureSuccessStatusCode();

                byte[] data = await response.Content.ReadAsByteArrayAsync().ConfigureAwait(false);

                cancellationToken.ThrowIfCancellationRequested();

                try
                {
                    if (System.IO.File.Exists(UserSettingsFilePath))
                    {
                        System.IO.File.Delete(UserSettingsFilePath);
                    }
                }
                catch (System.IO.IOException)
                {
                }

                using FileStream file = File.Open(UserSettingsFilePath, FileMode.OpenOrCreate, FileAccess.ReadWrite);
                await file.WriteAsync(data, 0, data.Length, cancellationToken).ConfigureAwait(false);
                return true;
            }
            catch (Exception)
            {
                Trace.WriteLine("Could not load server user settings: " + uri.ToString());
                return false;
            }


        }

        private static void CreateNewUserSettingsFile()
        {
            Global._userSettingsDoc = new XRoot(new UserSettings());
            SaveUserSettings();
        }

        public static void SaveUserSettings() => Global._userSettingsDoc.Save(UserSettingsFilePath);

        #endregion
    }

    internal sealed class WebAnnotationGrpcServiceConfiguration(ApplicationSettings applicationSettings) : IGrpcServiceConfiguration
    {
        private readonly ApplicationSettings _applicationSettings = applicationSettings;

        public string Endpoint()
        {
            if (!string.IsNullOrWhiteSpace(_applicationSettings?.SegmentationURL))
            {
                return _applicationSettings.SegmentationURL;
            }

            var persistedUrl = Global.AnnotationSettings.SegmentationServiceUrl;
            if (!string.IsNullOrWhiteSpace(persistedUrl))
            {
                return persistedUrl;
            }

            return Global.GetSegmentationServiceUrl();
        }
    }
}
