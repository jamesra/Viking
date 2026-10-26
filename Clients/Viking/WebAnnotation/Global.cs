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
using Grpc.Net.Client;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.Extensions.Options;
using Viking;
using Viking.Common;
using WebAnnotationModel;
using WebAnnotationModel.gRPC;
using System.Net.Http;
using System.Net.Security;
using System.Security.Cryptography.X509Certificates;
using rogue1.codepharm.net.XSD.WebAnnotationUserSettings.xsd;
using Utils;
using VikingXNAGraphics;
using WebAnnotation.UI.Commands.Segmentation;
using WebAnnotation.View;
using Viking.UI;
using Viking.DependencyInjection;
using Viking.Services.Grpc;
using WebAnnotation.Services;
using SegmentationGrpcChannelManager = Viking.Services.Grpc.IGrpcChannelManager;

namespace WebAnnotation
{
    /// <summary>
    /// Viking WinForms module entry (settings, credentials, overlay cache size). Jotunn/net10 store init is AnnotationBootstrap.
    /// </summary>
    public class Global
        : IModuleServiceRegistrar, IModuleInitializer
    {
        /// <summary>
        /// Jumping to a location causes it's diameter to occupy 1/8 the width of the screen
        /// </summary>
        internal static double DefaultLocationJumpDownsample => AnnotationSettings.DefaultLocationJumpDownsample;

        /// <summary>Max concurrent section loads. Distinct from NumSectionsInMemory (how many section views stay cached).</summary>
        internal static int NumSectionsLoading => AnnotationSettings.NumSectionsLoading;

        internal static Export? Export = null;

        private static bool? _isSegmentationServiceAvailable;

        /// <summary>
        /// Static method called by ExtensionManager to determine if this extension should be loaded.
        /// Loads when Identity supplied an annotation endpoint for the session, or when VikingXML
        /// has a <c>VolumeToEndpoint</c> with an Endpoint attribute.
        /// </summary>
        /// <param name="context">The extension load context providing access to VikingXML</param>
        /// <returns>True if the extension should load, false otherwise</returns>
        public static bool ShouldLoad(Viking.Common.IExtensionLoadContext context)
        {
            if (AccessibleVolumeSession.AnnotationServiceEndpoint != null)
                return true;

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
                    return _isSegmentationServiceAvailable.Value;

                string serviceUrl = AnnotationSettings.SegmentationServiceUrl;
                try
                {
                    IGrpcServiceConfiguration segmentationService = ServiceLocator.ServiceProvider?.GetService<IGrpcServiceConfiguration>();
                    if (segmentationService != null)
                        serviceUrl = segmentationService.Endpoint() ?? serviceUrl;
                }
                catch (Exception)
                {
                }
                if (string.IsNullOrWhiteSpace(serviceUrl))
                {
                    _isSegmentationServiceAvailable = false;
                    return false;
                }

                _isSegmentationServiceAvailable = HasValidSegmentationServiceUrl(serviceUrl);
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
            UI.AnnotationStatusChips.Refresh();
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
            private const int DEFAULT_NUM_SECTIONS_IN_MEMORY = 10;
            private const int MIN_SECTIONS_LOADING = 1;
            private const int MAX_SECTIONS_LOADING = 50;
            private const int DEFAULT_NUM_SECTIONS_LOADING = 5;
            private const double MIN_SCALE_FACTOR = 0.1;
            private const double MAX_SCALE_FACTOR = 50.0;
            private const double DEFAULT_LOCATION_TEXT_SCALE_FACTOR = 5.0;
            private const double DEFAULT_REFERENCE_LOCATION_TEXT_SCALE_FACTOR = 2.5;
            private const double MIN_LINE_WIDTH = 1.0;
            private const double MAX_LINE_WIDTH = 100.0;
            private const double DEFAULT_CLOSED_LINE_WIDTH = 24.0;
            private const double MIN_DOWNSAMPLE = 1.0;
            private const double MAX_DOWNSAMPLE = 64.0;
            private const double DEFAULT_LOCATION_JUMP_DOWNSAMPLE = 4.0;
            private const double MIN_RADIUS_SCALAR = 0.1;
            private const double MAX_RADIUS_SCALAR = 2.0;
            private const double DEFAULT_ADJACENT_LOCATION_RADIUS_SCALAR = 0.5;
            private const int MIN_CURVE_POINTS = 2;
            private const int MAX_CURVE_POINTS = 20;
            private const int DEFAULT_NUM_CLOSED_CURVE_INTERPOLATION_POINTS = 3;
            private const int MIN_PEN_THRESHOLD = 1;
            private const int MAX_PEN_THRESHOLD = 100;
            private const int DEFAULT_PEN_SIMPLIFY_THRESHOLD = 3;
            private const double MIN_RADIUS = 0.1;
            private const double MAX_RADIUS = 10.0;
            private const double DEFAULT_MIN_RADIUS = 0.5;
            private const double MIN_OPACITY = 0.0;
            private const double MAX_OPACITY = 1.0;
            private const double DEFAULT_POLYGON_OPACITY_PARENTLESS = 0.5;
            private const double DEFAULT_POLYGON_OPACITY_WITH_PARENT = 0.33;
            private const double DEFAULT_CIRCLE_OPACITY_PARENTLESS = 0.5;
            private const double DEFAULT_CIRCLE_OPACITY_WITH_PARENT = 1.0;
            private const double MIN_SEGMENTATION_POINT_RADIUS = 1.0;
            private const double MAX_SEGMENTATION_POINT_RADIUS = 15.0;
            private const double DEFAULT_SEGMENTATION_POINT_RADIUS = 5.0;
            private const double MIN_SEGMENTATION_HOLE_DROP_FRACTION = 0.0;
            private const double MAX_SEGMENTATION_HOLE_DROP_FRACTION = 1.0;
            private const int MIN_SEGMENTATION_EDGE_CLEANUP_RADIUS = 0;
            private const int MAX_SEGMENTATION_EDGE_CLEANUP_RADIUS = 32;
            private const double MIN_POLYGON_POINT_RADIUS = 1.0;
            private const double MAX_POLYGON_POINT_RADIUS = 50.0;
            private const double DEFAULT_POLYGON_POINT_RADIUS = 6.0;
            private const double MIN_POLYGON_POINT_DIAMETER = 2.0;
            private const double MAX_POLYGON_POINT_DIAMETER = 100.0;
            private const double DEFAULT_POLYGON_POINT_DIAMETER = 12.0;
            private const double MIN_SMALLEST_RENDERED_SIZE = 0.5;
            private const double MAX_SMALLEST_RENDERED_SIZE = 10.0;
            private const double DEFAULT_SMALLEST_RENDERED_SIZE = 10.0;
            private const double MIN_POLYGON_VERTEX_VISIBLE_FRACTION = 0.0001;  // 0.01%
            private const double MAX_POLYGON_VERTEX_VISIBLE_FRACTION = 0.02;     // 2%
            private const double DEFAULT_POLYGON_VERTEX_VISIBLE_FRACTION = 0.0025;  // 0.25%
            private const double MIN_POLYGON_VERTEX_HIDDEN_FRACTION = 0.00005;   // 0.005%
            private const double MAX_POLYGON_VERTEX_HIDDEN_FRACTION = 0.015;     // 1.5%
            private const double DEFAULT_POLYGON_VERTEX_HIDDEN_FRACTION = 0.002;   // 0.2%
            private const double MIN_AUTOPOLYGONIZE_RADIUS_NANOMETERS = 0.0;
            private const double MAX_AUTOPOLYGONIZE_RADIUS_NANOMETERS = 10000.0;
            private const double MIN_AUTOPOLYGONIZE_MAX_DOWNSAMPLE = 1.0;
            private const double MAX_AUTOPOLYGONIZE_MAX_DOWNSAMPLE = 4.0;

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

            /// <summary>Lowest and highest downsample accepted for the correction shim fade range.</summary>
            const double MIN_CORRECTION_DOWNSAMPLE = 0.25;
            const double MAX_CORRECTION_DOWNSAMPLE = 256;

            /// <summary>
            /// Whether the section-correction shim shifts the view when the open context has a correction service.
            /// Saved across restarts; also toggled from the viewer's status bar.
            /// </summary>
            public static bool CorrectionShimEnabled
            {
                get => Properties.Settings.Default.CorrectionShimEnabled;
                set
                {
                    Properties.Settings.Default.CorrectionShimEnabled = value;
                    Properties.Settings.Default.Save();
                    Viking.SectionCorrectionClient.SectionShimContext.Settings.Enabled = value;
                }
            }

            /// <summary>
            /// Whether the viewer's status bar shows the correction offset after "Corrections: On". Saved across restarts.
            /// </summary>
            public static bool CorrectionShimShowOffset
            {
                get => Properties.Settings.Default.CorrectionShimShowOffset;
                set
                {
                    Properties.Settings.Default.CorrectionShimShowOffset = value;
                    Properties.Settings.Default.Save();
                    Viking.SectionCorrectionClient.SectionShimContext.Settings.ShowOffset = value;
                }
            }

            /// <summary>
            /// Downsample at and above which the correction shim has no effect. The shim fades in from here, reaching
            /// full effect at <see cref="CorrectionShimFadeEndDownsample"/>.
            /// </summary>
            public static double CorrectionShimFadeStartDownsample
            {
                get => MathUtils.Clamp(Properties.Settings.Default.CorrectionShimFadeStartDownsample, MIN_CORRECTION_DOWNSAMPLE, MAX_CORRECTION_DOWNSAMPLE);
                set
                {
                    value = MathUtils.Clamp(value, MIN_CORRECTION_DOWNSAMPLE, MAX_CORRECTION_DOWNSAMPLE);
                    Properties.Settings.Default.CorrectionShimFadeStartDownsample = value;
                    Properties.Settings.Default.Save();
                    Viking.SectionCorrectionClient.SectionShimContext.Settings.FadeStartDownsample = value;
                }
            }

            /// <summary>Downsample at and below which the correction shim is fully applied.</summary>
            public static double CorrectionShimFadeEndDownsample
            {
                get => MathUtils.Clamp(Properties.Settings.Default.CorrectionShimFadeEndDownsample, MIN_CORRECTION_DOWNSAMPLE, MAX_CORRECTION_DOWNSAMPLE);
                set
                {
                    value = MathUtils.Clamp(value, MIN_CORRECTION_DOWNSAMPLE, MAX_CORRECTION_DOWNSAMPLE);
                    Properties.Settings.Default.CorrectionShimFadeEndDownsample = value;
                    Properties.Settings.Default.Save();
                    Viking.SectionCorrectionClient.SectionShimContext.Settings.FadeEndDownsample = value;
                }
            }

            static bool _correctionShimPersistenceHooked;

            /// <summary>
            /// Copies the saved correction preferences into the settings the viewers read, and saves the on/off
            /// state when a viewer's status-bar item changes it. Safe to call more than once.
            /// </summary>
            public static void ApplyCorrectionShimSettings()
            {
                var shim = Viking.SectionCorrectionClient.SectionShimContext.Settings;
                shim.Enabled = Properties.Settings.Default.CorrectionShimEnabled;
                shim.ShowOffset = Properties.Settings.Default.CorrectionShimShowOffset;
                shim.FadeStartDownsample = CorrectionShimFadeStartDownsample;
                shim.FadeEndDownsample = CorrectionShimFadeEndDownsample;

                if (!_correctionShimPersistenceHooked)
                {
                    _correctionShimPersistenceHooked = true;
                    Viking.SectionCorrectionClient.SectionShimContext.SettingsChanged += () =>
                    {
                        Properties.Settings.Default.CorrectionShimEnabled = Viking.SectionCorrectionClient.SectionShimContext.Settings.Enabled;
                        Properties.Settings.Default.Save();
                    };
                }

                // Viewers created before the module loaded re-read the settings now.
                Viking.SectionCorrectionClient.SectionShimContext.NotifySettingsChanged();
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

            /// <summary>Narrowest accepted <see cref="LocationLinkWaistPixels"/>.</summary>
            public const int MIN_LOCATION_LINK_WAIST_PIXELS = 2;

            /// <summary>Widest accepted <see cref="LocationLinkWaistPixels"/>.</summary>
            public const int MAX_LOCATION_LINK_WAIST_PIXELS = 32;

            /// <summary>Starting <see cref="LocationLinkWaistPixels"/>. Must match the DefaultSettingValue in Settings.LocationLinks.cs.</summary>
            public const int DEFAULT_LOCATION_LINK_WAIST_PIXELS = 8;

            /// <summary>
            /// Width in screen pixels of the narrowest part of a link line between two locations on different sections,
            /// clamped to the MIN/MAX_LOCATION_LINK_WAIST_PIXELS range.
            /// </summary>
            public static int LocationLinkWaistPixels
            {
                get => MathUtils.Clamp(
                    Properties.Settings.Default.LocationLinkWaistPixels,
                    MIN_LOCATION_LINK_WAIST_PIXELS,
                    MAX_LOCATION_LINK_WAIST_PIXELS);
                set
                {
                    Properties.Settings.Default.LocationLinkWaistPixels = MathUtils.Clamp(
                        value,
                        MIN_LOCATION_LINK_WAIST_PIXELS,
                        MAX_LOCATION_LINK_WAIST_PIXELS);
                    Properties.Settings.Default.Save();
                }
            }

            /// <summary>
            /// Whether a location link is also drawn under its two location markers, as a round end the size of each marker.
            /// False draws nothing under the markers.
            /// </summary>
            public static bool LocationLinkDrawUnderMarkers
            {
                get => Properties.Settings.Default.LocationLinkDrawUnderMarkers;
                set
                {
                    Properties.Settings.Default.LocationLinkDrawUnderMarkers = value;
                    Properties.Settings.Default.Save();
                }
            }

            /// <summary>Faintest accepted <see cref="LocationLinkVisibilityPercent"/>.</summary>
            public const int MIN_LOCATION_LINK_VISIBILITY_PERCENT = 10;

            /// <summary>Strongest accepted <see cref="LocationLinkVisibilityPercent"/>.</summary>
            public const int MAX_LOCATION_LINK_VISIBILITY_PERCENT = 100;

            /// <summary>Starting <see cref="LocationLinkVisibilityPercent"/>. Must match the DefaultSettingValue in Settings.LocationLinks.cs.</summary>
            public const int DEFAULT_LOCATION_LINK_VISIBILITY_PERCENT = 60;

            /// <summary>
            /// How strongly location links are drawn, as a percent: the link's opacity next to its markers and the strength of
            /// its contrasting shadow. Clamped to the MIN/MAX_LOCATION_LINK_VISIBILITY_PERCENT range.
            /// </summary>
            public static int LocationLinkVisibilityPercent
            {
                get => MathUtils.Clamp(
                    Properties.Settings.Default.LocationLinkVisibilityPercent,
                    MIN_LOCATION_LINK_VISIBILITY_PERCENT,
                    MAX_LOCATION_LINK_VISIBILITY_PERCENT);
                set
                {
                    Properties.Settings.Default.LocationLinkVisibilityPercent = MathUtils.Clamp(
                        value,
                        MIN_LOCATION_LINK_VISIBILITY_PERCENT,
                        MAX_LOCATION_LINK_VISIBILITY_PERCENT);
                    Properties.Settings.Default.Save();
                }
            }

            /// <summary><see cref="LocationLinkVisibilityPercent"/> as a fraction, 0.1 to 1.</summary>
            public static float LocationLinkVisibility => LocationLinkVisibilityPercent / 100f;

            /// <summary>Whether each end of a location link is labelled with the ID of the location at its other end.</summary>
            public static bool LocationLinkLabels
            {
                get => Properties.Settings.Default.LocationLinkLabels;
                set
                {
                    Properties.Settings.Default.LocationLinkLabels = value;
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
                    // Re-applying the same URL (picker confirmed again, volume reload) must not drop the shared
                    // channel: that cancels in-flight segmentation for no reason.
                    if (string.Equals(Properties.Settings.Default.SegmentationServiceUrl, value, StringComparison.Ordinal))
                        return;

                    Properties.Settings.Default.SegmentationServiceUrl = value;
                    Properties.Settings.Default.Save();
                    _isSegmentationServiceAvailable = null;
                    if (ServiceLocator.IsInitialized)
                        ServiceLocator.GrpcChannelManager?.ResetChannel();
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

            /// <summary>Smallest accepted <see cref="SegmentationMaskThreshold"/>, in SAM2 logit units.</summary>
            public const double MIN_SEGMENTATION_MASK_THRESHOLD = -2.5;

            /// <summary>Largest accepted <see cref="SegmentationMaskThreshold"/>, in SAM2 logit units.</summary>
            public const double MAX_SEGMENTATION_MASK_THRESHOLD = 5.0;

            /// <summary>Starting <see cref="SegmentationMaskThreshold"/>. Must match the DefaultSettingValue in Settings.Segmentation.cs.</summary>
            public const double DEFAULT_SEGMENTATION_MASK_THRESHOLD = 0.5;

            private const int SEGMENTATION_REQUEST_SETTINGS_DEBOUNCE_MS = 400;

            private static System.Threading.Timer? _segmentationRequestSettingsTimer;

            /// <summary>
            /// Logit a pixel must exceed for SAM2 to call it object, sent with every SegmentTiles request.
            /// 0 is SAM2's own default; higher values shrink the mask. A change re-segments the active
            /// segmentation command and the auto-polygonize view once the value has stopped moving.
            /// </summary>
            public static double SegmentationMaskThreshold
            {
                get => MathUtils.Clamp(
                    Properties.Settings.Default.SegmentationMaskThreshold,
                    MIN_SEGMENTATION_MASK_THRESHOLD,
                    MAX_SEGMENTATION_MASK_THRESHOLD);
                set
                {
                    double clamped = MathUtils.Clamp(
                        value,
                        MIN_SEGMENTATION_MASK_THRESHOLD,
                        MAX_SEGMENTATION_MASK_THRESHOLD);
                    if (Math.Abs(Properties.Settings.Default.SegmentationMaskThreshold - clamped) < 0.0001)
                        return;

                    Properties.Settings.Default.SegmentationMaskThreshold = clamped;
                    Properties.Settings.Default.Save();
                    UI.AnnotationModeToolbar.SyncMaskThreshold();
                    ScheduleSegmentationRequestSettingsRefresh();
                }
            }

            /// <summary>
            /// When true the server predicts twice per cell, feeding the first pass's logits back to SAM2
            /// as mask_input. Off by default. A change re-segments like <see cref="SegmentationMaskThreshold"/>.
            /// </summary>
            public static bool SegmentationUseMaskInput
            {
                get => Properties.Settings.Default.SegmentationUseMaskInput;
                set
                {
                    if (Properties.Settings.Default.SegmentationUseMaskInput == value)
                        return;

                    Properties.Settings.Default.SegmentationUseMaskInput = value;
                    Properties.Settings.Default.Save();
                    ScheduleSegmentationRequestSettingsRefresh();
                }
            }

            /// <summary>Largest accepted <see cref="SegmentationMinFeatureNanometers"/>.</summary>
            public const double MAX_SEGMENTATION_MIN_FEATURE_NANOMETERS = 100.0;

            /// <summary>
            /// Narrowest part of a SAM2 mask that is kept, in nanometers; thinner bridges and specks are cut before the outline
            /// is traced. 0 keeps everything. A change retraces the proposals on screen from their stored masks.
            /// </summary>
            public static double SegmentationMinFeatureNanometers
            {
                get => MathUtils.Clamp(Properties.Settings.Default.SegmentationMinFeatureNanometers, 0, MAX_SEGMENTATION_MIN_FEATURE_NANOMETERS);
                set
                {
                    double clamped = MathUtils.Clamp(value, 0, MAX_SEGMENTATION_MIN_FEATURE_NANOMETERS);
                    if (Math.Abs(Properties.Settings.Default.SegmentationMinFeatureNanometers - clamped) < 0.0001)
                        return;

                    Properties.Settings.Default.SegmentationMinFeatureNanometers = clamped;
                    Properties.Settings.Default.Save();
                    RefreshActiveSegmentationPolygons();
                }
            }

            /// <summary>
            /// <see cref="SegmentationMinFeatureNanometers"/> in world units (volume pixels at downsample 1), using the volume's
            /// nanometers per pixel. 0 when the setting is off or the scale is not known.
            /// </summary>
            public static double SegmentationMinFeatureWorld
            {
                get
                {
                    double nanometersPerWorld = Scale.X;
                    double nanometers = SegmentationMinFeatureNanometers;
                    return nanometers > 0 && nanometersPerWorld > 0 ? nanometers / nanometersPerWorld : 0;
                }
            }

            /// <summary>Largest accepted <see cref="SegmentationSmoothingSigma"/>, in logit samples.</summary>
            public const double MAX_SEGMENTATION_SMOOTHING_SIGMA = 2.0;

            /// <summary>
            /// Smoothing, in logit samples (four pixels each), applied to a response's logits before they are traced; 0 traces
            /// them as sent. Not retroactive: a response is decoded once, so the value applies to the next segmentation.
            /// </summary>
            public static double SegmentationSmoothingSigma
            {
                get => MathUtils.Clamp(Properties.Settings.Default.SegmentationSmoothingSigma, 0, MAX_SEGMENTATION_SMOOTHING_SIGMA);
                set
                {
                    double clamped = MathUtils.Clamp(value, 0, MAX_SEGMENTATION_SMOOTHING_SIGMA);
                    if (Math.Abs(Properties.Settings.Default.SegmentationSmoothingSigma - clamped) < 0.0001)
                        return;

                    Properties.Settings.Default.SegmentationSmoothingSigma = clamped;
                    Properties.Settings.Default.Save();
                }
            }

            /// <summary>
            /// When true each traced contour is moved onto the nearest strong bright-to-dark image edge. Client-side, so a
            /// change retraces the active command and the on-screen auto-polygonize proposals from their stored masks
            /// without asking the server again.
            /// </summary>
            public static bool SegmentationEdgeSnapEnabled
            {
                get => Properties.Settings.Default.SegmentationEdgeSnapEnabled;
                set
                {
                    if (Properties.Settings.Default.SegmentationEdgeSnapEnabled == value)
                        return;

                    Properties.Settings.Default.SegmentationEdgeSnapEnabled = value;
                    Properties.Settings.Default.Save();
                    RefreshActiveSegmentationPolygons();
                }
            }

            /// <summary>
            /// Waits for the mask-threshold slider to stop moving, then re-requests on the UI thread.
            /// Each change restarts the wait so a drag sends one request, not one per tick.
            /// </summary>
            private static void ScheduleSegmentationRequestSettingsRefresh()
            {
                _segmentationRequestSettingsTimer?.Dispose();
                _segmentationRequestSettingsTimer = new System.Threading.Timer(
                    _ => RefreshAfterSegmentationRequestSettingsChange(),
                    null,
                    SEGMENTATION_REQUEST_SETTINGS_DEBOUNCE_MS,
                    System.Threading.Timeout.Infinite);
            }

            private static void RefreshAfterSegmentationRequestSettingsChange()
            {
                var overlay = AnnotationOverlay.CurrentOverlay;
                if (overlay is null)
                    return;

                var parent = overlay.Parent;
                if (parent is null || parent.IsDisposed)
                    return;

                void Refresh()
                {
                    overlay.OnSegmentationRequestSettingsChanged();
                    if (parent.CurrentCommand is SegmentationCommand command)
                        command.ResegmentAfterRequestSettingsChange();
                }

                if (parent.InvokeRequired)
                    parent.BeginInvoke(new System.Action(Refresh));
                else
                    Refresh();
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
                    {
                        return VolumeAccessRoles.HasReviewAccess();
                    }

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
            /// Minimum circle radius, in nanometers, for auto-polygonize.
            /// Default <see cref="Properties.Settings.DefaultAutoPolygonizeMinRadiusNanometers"/>.
            /// 0 accepts any positive radius. Preview size uses the current zoom.
            /// A saved value is kept across restarts. The old 75 nm default is replaced once at startup.
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
            /// Coarsest camera downsample at which auto-segment and tile submit still run. Default 2.
            /// The preference allows 1 to 4: the value itself still submits and anything coarser does not.
            /// A saved value above 4 is clamped to 4; a lower saved value still wins.
            /// Changing it restarts the idle timer so a view that is now allowed can run without a camera nudge.
            /// </summary>
            public static double AutoPolygonizeMaxDownsample
            {
                get => MathUtils.Clamp(
                    Math.Round(Properties.Settings.Default.AutoPolygonizeMaxDownsample * 4.0) / 4.0,
                    MIN_AUTOPOLYGONIZE_MAX_DOWNSAMPLE,
                    MAX_AUTOPOLYGONIZE_MAX_DOWNSAMPLE);
                set
                {
                    // Preference slider steps by 0.25; keep stored values on those ticks.
                    double clamped = MathUtils.Clamp(
                        Math.Round(value * 4.0) / 4.0,
                        MIN_AUTOPOLYGONIZE_MAX_DOWNSAMPLE,
                        MAX_AUTOPOLYGONIZE_MAX_DOWNSAMPLE);
                    if (Math.Abs(Properties.Settings.Default.AutoPolygonizeMaxDownsample - clamped) < 0.001)
                        return;

                    Properties.Settings.Default.AutoPolygonizeMaxDownsample = clamped;
                    Properties.Settings.Default.Save();
                    AnnotationOverlay.CurrentOverlay?.RequestAutoPolygonizeIdlePass();
                }
            }

            /// <summary>Smallest accepted <see cref="AutoPolygonizeShimmerBrightness"/>.</summary>
            public const double MIN_AUTOPOLYGONIZE_SHIMMER_BRIGHTNESS = 0.1;

            /// <summary>Largest accepted <see cref="AutoPolygonizeShimmerBrightness"/>.</summary>
            public const double MAX_AUTOPOLYGONIZE_SHIMMER_BRIGHTNESS = 1.0;

            /// <summary>Starting <see cref="AutoPolygonizeShimmerBrightness"/>. Must match the DefaultSettingValue in Settings.Segmentation.cs.</summary>
            public const double DEFAULT_AUTOPOLYGONIZE_SHIMMER_BRIGHTNESS = 0.6;

            /// <summary>Shortest accepted <see cref="AutoPolygonizeShimmerDurationMs"/>.</summary>
            public const int MIN_AUTOPOLYGONIZE_SHIMMER_DURATION_MS = 250;

            /// <summary>Longest accepted <see cref="AutoPolygonizeShimmerDurationMs"/>.</summary>
            public const int MAX_AUTOPOLYGONIZE_SHIMMER_DURATION_MS = 1000;

            /// <summary>Starting <see cref="AutoPolygonizeShimmerDurationMs"/>. Must match the DefaultSettingValue in Settings.Segmentation.cs.</summary>
            public const int DEFAULT_AUTOPOLYGONIZE_SHIMMER_DURATION_MS = 750;

            /// <summary>
            /// Whether a newly shown auto-polygonize proposal plays the one-shot shimmer. Read when the proposal is
            /// first drawn, so turning it off does not cut a shimmer already running.
            /// </summary>
            public static bool AutoPolygonizeShimmerEnabled
            {
                get => Properties.Settings.Default.AutoPolygonizeShimmerEnabled;
                set
                {
                    Properties.Settings.Default.AutoPolygonizeShimmerEnabled = value;
                    Properties.Settings.Default.Save();
                }
            }

            /// <summary>
            /// Whether dragging a linked annotation onto the next section opens the segmentation step before the polygon is
            /// saved. Read when the drag is released, so changing it does not affect a segmentation already open.
            /// </summary>
            public static bool SegmentLinkedAnnotations
            {
                get => Properties.Settings.Default.SegmentLinkedAnnotations;
                set
                {
                    Properties.Settings.Default.SegmentLinkedAnnotations = value;
                    Properties.Settings.Default.Save();
                }
            }

            /// <summary>Peak alpha of the shimmer lines, clamped to the MIN/MAX_AUTOPOLYGONIZE_SHIMMER_BRIGHTNESS range.</summary>
            public static double AutoPolygonizeShimmerBrightness
            {
                get => MathUtils.Clamp(
                    Properties.Settings.Default.AutoPolygonizeShimmerBrightness,
                    MIN_AUTOPOLYGONIZE_SHIMMER_BRIGHTNESS,
                    MAX_AUTOPOLYGONIZE_SHIMMER_BRIGHTNESS);
                set
                {
                    Properties.Settings.Default.AutoPolygonizeShimmerBrightness = MathUtils.Clamp(
                        value,
                        MIN_AUTOPOLYGONIZE_SHIMMER_BRIGHTNESS,
                        MAX_AUTOPOLYGONIZE_SHIMMER_BRIGHTNESS);
                    Properties.Settings.Default.Save();
                }
            }

            /// <summary>Milliseconds the shimmer takes to cross a proposal, clamped to the MIN/MAX_AUTOPOLYGONIZE_SHIMMER_DURATION_MS range.</summary>
            public static int AutoPolygonizeShimmerDurationMs
            {
                get => MathUtils.Clamp(
                    Properties.Settings.Default.AutoPolygonizeShimmerDurationMs,
                    MIN_AUTOPOLYGONIZE_SHIMMER_DURATION_MS,
                    MAX_AUTOPOLYGONIZE_SHIMMER_DURATION_MS);
                set
                {
                    Properties.Settings.Default.AutoPolygonizeShimmerDurationMs = MathUtils.Clamp(
                        value,
                        MIN_AUTOPOLYGONIZE_SHIMMER_DURATION_MS,
                        MAX_AUTOPOLYGONIZE_SHIMMER_DURATION_MS);
                    Properties.Settings.Default.Save();
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

            public static double PolygonPointDiameter
            {
                get
                {
                    MigratePolygonPointDiameterIfNeeded();
                    return MathUtils.Clamp(Properties.Settings.Default.PolygonPointDiameter, MIN_POLYGON_POINT_DIAMETER, MAX_POLYGON_POINT_DIAMETER);
                }
                set
                {
                    Properties.Settings.Default.PolygonPointDiameter = MathUtils.Clamp(value, MIN_POLYGON_POINT_DIAMETER, MAX_POLYGON_POINT_DIAMETER);
                    Properties.Settings.Default.Save();
                }
            }

            /// <summary>
            /// Opacity (0..1) of the control-point circles drawn on polygon annotations. Independent of the
            /// polygon fill opacity. Read per frame by <c>LocationPolygonView.Draw</c>.
            /// </summary>
            public static double PolygonPointOpacity
            {
                get => MathUtils.Clamp(Properties.Settings.Default.PolygonPointOpacity, 0.0, 1.0);
                set
                {
                    Properties.Settings.Default.PolygonPointOpacity = MathUtils.Clamp(value, 0.0, 1.0);
                    Properties.Settings.Default.Save();
                }
            }

            /// <summary>
            /// When true (the default), polygon control points are not drawn while pen mode is on.
            /// </summary>
            public static bool HidePolygonPointsInPenMode
            {
                get => Properties.Settings.Default.HidePolygonPointsInPenMode;
                set
                {
                    Properties.Settings.Default.HidePolygonPointsInPenMode = value;
                    Properties.Settings.Default.Save();
                }
            }

            /// <summary>
            /// Polygon point radius in world units (computed as diameter / 2). Used by rendering code.
            /// Setting it stores twice the value as <see cref="PolygonPointDiameter"/> (the preference the Legacy
            /// dialog edited as a radius).
            /// </summary>
            public static double PolygonPointRadius
            {
                get => PolygonPointDiameter / 2.0;
                set => PolygonPointDiameter = 2.0 * value;
            }

            private static void MigratePolygonPointDiameterIfNeeded()
            {
                if (Properties.Settings.Default.PolygonPointDiameterMigrated)
                    return;
                double legacyRadius = Properties.Settings.Default.PolygonPointRadius;
                Properties.Settings.Default.PolygonPointDiameter = 2.0 * legacyRadius;
                Properties.Settings.Default.PolygonPointDiameterMigrated = true;
                Properties.Settings.Default.Save();
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

            public static double PolygonVertexPointsVisibleAtWidthFraction
            {
                get => MathUtils.Clamp(Properties.Settings.Default.PolygonVertexPointsVisibleAtWidthFraction, MIN_POLYGON_VERTEX_VISIBLE_FRACTION, MAX_POLYGON_VERTEX_VISIBLE_FRACTION);
                set
                {
                    double clamped = MathUtils.Clamp(value, MIN_POLYGON_VERTEX_VISIBLE_FRACTION, MAX_POLYGON_VERTEX_VISIBLE_FRACTION);
                    if (clamped <= Properties.Settings.Default.PolygonVertexPointsHiddenAtWidthFraction)
                        clamped = Math.Min(MAX_POLYGON_VERTEX_VISIBLE_FRACTION, Properties.Settings.Default.PolygonVertexPointsHiddenAtWidthFraction + 0.0001);
                    Properties.Settings.Default.PolygonVertexPointsVisibleAtWidthFraction = clamped;
                    Properties.Settings.Default.Save();
                }
            }

            public static double PolygonVertexPointsHiddenAtWidthFraction
            {
                get => MathUtils.Clamp(Properties.Settings.Default.PolygonVertexPointsHiddenAtWidthFraction, MIN_POLYGON_VERTEX_HIDDEN_FRACTION, MAX_POLYGON_VERTEX_HIDDEN_FRACTION);
                set
                {
                    double clamped = MathUtils.Clamp(value, MIN_POLYGON_VERTEX_HIDDEN_FRACTION, MAX_POLYGON_VERTEX_HIDDEN_FRACTION);
                    if (clamped >= Properties.Settings.Default.PolygonVertexPointsVisibleAtWidthFraction)
                        clamped = Math.Max(MIN_POLYGON_VERTEX_HIDDEN_FRACTION, Properties.Settings.Default.PolygonVertexPointsVisibleAtWidthFraction - 0.0001);
                    Properties.Settings.Default.PolygonVertexPointsHiddenAtWidthFraction = clamped;
                    Properties.Settings.Default.Save();
                }
            }

            /// <summary>
            /// Resets all annotation settings exposed in the Annotation Preferences dialog to their default values.
            /// SegmentationServiceUrl is intentionally not reset so the user's configured URL is preserved.
            /// </summary>
            public static void ResetToDefaults()
            {
                bool segmentationRequestSettingsChanged =
                    Math.Abs(Properties.Settings.Default.SegmentationMaskThreshold - DEFAULT_SEGMENTATION_MASK_THRESHOLD) > 0.0001 ||
                    Properties.Settings.Default.SegmentationUseMaskInput;
                Properties.Settings.Default.NumSectionsInMemory = DEFAULT_NUM_SECTIONS_IN_MEMORY;
                Properties.Settings.Default.NumSectionsLoading = DEFAULT_NUM_SECTIONS_LOADING;
                Properties.Settings.Default.LocationTextScaleFactor = (float)DEFAULT_LOCATION_TEXT_SCALE_FACTOR;
                Properties.Settings.Default.ReferenceLocationTextScaleFactor = (float)DEFAULT_REFERENCE_LOCATION_TEXT_SCALE_FACTOR;
                Properties.Settings.Default.DefaultClosedLineWidth = DEFAULT_CLOSED_LINE_WIDTH;
                Properties.Settings.Default.DefaultLocationJumpDownsample = DEFAULT_LOCATION_JUMP_DOWNSAMPLE;
                CorrectionShimEnabled = true;
                CorrectionShimShowOffset = false;
                CorrectionShimFadeStartDownsample = Viking.SectionCorrectionClient.SectionShimSettings.DefaultFadeStartDownsample;
                CorrectionShimFadeEndDownsample = Viking.SectionCorrectionClient.SectionShimSettings.DefaultFadeEndDownsample;
                Properties.Settings.Default.AdjacentLocationRadiusScalar = DEFAULT_ADJACENT_LOCATION_RADIUS_SCALAR;
                Properties.Settings.Default.LocationLinkWaistPixels = DEFAULT_LOCATION_LINK_WAIST_PIXELS;
                Properties.Settings.Default.LocationLinkDrawUnderMarkers = false;
                Properties.Settings.Default.LocationLinkVisibilityPercent = DEFAULT_LOCATION_LINK_VISIBILITY_PERCENT;
                Properties.Settings.Default.LocationLinkLabels = true;
                Properties.Settings.Default.NumClosedCurveInterpolationPointsForDisplay = (uint)DEFAULT_NUM_CLOSED_CURVE_INTERPOLATION_POINTS;
                Properties.Settings.Default.PenSimplifyThreshold = DEFAULT_PEN_SIMPLIFY_THRESHOLD;
                Properties.Settings.Default.MinRadius = DEFAULT_MIN_RADIUS;
                Properties.Settings.Default.PolygonOpacityParentless = (float)DEFAULT_POLYGON_OPACITY_PARENTLESS;
                Properties.Settings.Default.PolygonOpacityWithParent = (float)DEFAULT_POLYGON_OPACITY_WITH_PARENT;
                Properties.Settings.Default.CircleOpacityParentless = (float)DEFAULT_CIRCLE_OPACITY_PARENTLESS;
                Properties.Settings.Default.CircleOpacityWithParent = (float)DEFAULT_CIRCLE_OPACITY_WITH_PARENT;
                Properties.Settings.Default.SegmentationPointRadius = DEFAULT_SEGMENTATION_POINT_RADIUS;
                Properties.Settings.Default.SegmentationHoleDropFraction = 0.03;
                Properties.Settings.Default.SegmentationEdgeCleanupRadius = 8;
                Properties.Settings.Default.SegmentationMaskThreshold = DEFAULT_SEGMENTATION_MASK_THRESHOLD;
                Properties.Settings.Default.SegmentationUseMaskInput = false;
                Properties.Settings.Default.SegmentationEdgeSnapEnabled = true;
                Properties.Settings.Default.SegmentationSmoothingSigma = 0.5;
                Properties.Settings.Default.SegmentationMinFeatureNanometers = 10;
                Properties.Settings.Default.AutoPolygonizeCircles = false;
                Properties.Settings.Default.AutoPolygonizeCirclesUserSet = false;
                Properties.Settings.Default.AutoPolygonizeMinRadiusNanometers = Properties.Settings.DefaultAutoPolygonizeMinRadiusNanometers;
                Properties.Settings.Default.AutoPolygonizeMaxDownsample = 2.0;
                Properties.Settings.Default.AutoPolygonizeShimmerEnabled = true;
                Properties.Settings.Default.SegmentLinkedAnnotations = true;
                Properties.Settings.Default.AutoPolygonizeShimmerBrightness = DEFAULT_AUTOPOLYGONIZE_SHIMMER_BRIGHTNESS;
                Properties.Settings.Default.AutoPolygonizeShimmerDurationMs = DEFAULT_AUTOPOLYGONIZE_SHIMMER_DURATION_MS;
                Properties.Settings.Default.AutoPolygonizeOverlayMasks = false;
                Properties.Settings.Default.AutoPolygonizeOverlayMasksUserSet = false;
                Properties.Settings.Default.AutoPolygonizeOverlayPrompts = false;
                Properties.Settings.Default.AutoPolygonizeOverlayPromptsUserSet = false;
                Properties.Settings.Default.AutoPolygonizeHideSegmentationRings = false;
                Properties.Settings.Default.PolygonPointDiameter = DEFAULT_POLYGON_POINT_DIAMETER;
                Properties.Settings.Default.PolygonPointOpacity = 0.75;
                Properties.Settings.Default.HidePolygonPointsInPenMode = true;
                Properties.Settings.Default.SmallestRenderedSize = DEFAULT_SMALLEST_RENDERED_SIZE;
                Properties.Settings.Default.PolygonVertexPointsVisibleAtWidthFraction = DEFAULT_POLYGON_VERTEX_VISIBLE_FRACTION;
                Properties.Settings.Default.PolygonVertexPointsHiddenAtWidthFraction = DEFAULT_POLYGON_VERTEX_HIDDEN_FRACTION;
                Properties.Settings.Default.Save();
                OnSettingsChanged();
                if (segmentationRequestSettingsChanged)
                    ScheduleSegmentationRequestSettingsRefresh();
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
                AnnotationOverlay.CurrentOverlay?.RefreshAutoPolygonizeFromStoredMasks();
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
        /// <summary>
        /// Computed on use: Viking's volume is not known when this type initializes, and hosts without one (Jotunn,
        /// tests) must still be able to use <see cref="Global"/>.
        /// </summary>
        private static string WebAnnotationPath =>
            Viking.UI.State.VolumeCachePath + System.IO.Path.DirectorySeparatorChar + "WebAnnotation";

        /// <summary>
        /// Bookmark filename only
        /// </summary>
        private static readonly string UserSettingsFileName = "UserSettings.xml";

        /// <summary>
        /// The full name of the settings file including filename and path
        /// </summary>
        private static string UserSettingsFilePath => WebAnnotationPath + System.IO.Path.DirectorySeparatorChar + UserSettingsFileName;
        private static XElement? UserSettingsElement = null;

        public static bool PenMode
        {
            get => WebAnnotation.Properties.Settings.Default.PenMode;
            set
            {
                bool previous = WebAnnotation.Properties.Settings.Default.PenMode;
                WebAnnotation.Properties.Settings.Default.PenMode = value;
                WebAnnotation.Properties.Settings.Default.Save();
                if (previous != value)
                    UI.AnnotationStatusChips.Refresh();
            }
        }

        /// <summary>
        /// When true, the circular choice buttons are drawn after a pen stroke.
        /// The Annotation menu toggles this. Shape previews stay clickable when the buttons are hidden.
        /// </summary>
        public static bool ShowPenActionButtons
        {
            get => WebAnnotation.Properties.Settings.Default.ShowPenActionButtons;
            set
            {
                WebAnnotation.Properties.Settings.Default.ShowPenActionButtons = value;
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
                        if (WebAnnotation.UI.StructureIdText.TryParseUnsigned(ID_str, out ulong ID))
                        {
                            if (_UserFavoriteStructureTypes.Contains(ID) == false) //Do not add accidental duplicates
                            {
                                _UserFavoriteStructureTypes.Add(ID);
                            }
                        }
                        else
                        {
                            Trace.WriteLine($"Unable to convert Favorite StructureID to ulong {ID_str}");
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
        /// True when a left-click, F3, or Enter on <paramref name="SectionNumber"/> should place a location linked to <see cref="LastEditedAnnotationID"/>.
        /// Called from the annotation overlay before continue-last-trace.
        /// A local-cache miss still returns true: the location may have been dropped after the user left its section, including after skipping a bad section. The click handler loads it from the server.
        /// Same-section last edits return false so the click does not duplicate the disc where it was just placed.
        /// A deleted id stays set until that server load fails; this method does not treat a cache miss as deleted.
        /// </summary>
        internal static bool CanContinueLastTrace(int SectionNumber)
        {
            if (LastEditedAnnotationID is null)
                return false;

            if (_userSettingsDoc is null)
                return false;

            WebAnnotationModel.Store.Locations.TryGetObjectByID(Global.LastEditedAnnotationID.Value, out WebAnnotationModel.Objects.LocationObj lastLoc);
            if (lastLoc is null)
                return true;

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

            services.AddSingleton<IAnnotationAccessTokenProvider, VikingAnnotationAccessTokenProvider>();

            // Endpoint is populated from VikingXML in InitializeModule before stores are resolved.
            services.ConfigureAnnotationModel(
                opts => { },
                channelOpts =>
                {
                    // Grpc.Net.Client on .NET Framework requires WinHttpHandler, which
                    // only supports gRPC over TLS (https://localhost:5011, not h2c :5010).
                    channelOpts.HttpHandler = CreateWinHttpHandler();
                });
        }

        /// <summary>
        /// WinHttpHandler is required for Grpc.Net.Client on .NET Framework and only
        /// supports gRPC over TLS. Accept the Docker localhost dev cert on loopback.
        /// </summary>
        private static WinHttpHandler CreateWinHttpHandler()
        {
            return new WinHttpHandler
            {
                EnableMultipleHttp2Connections = true,
                ServerCertificateValidationCallback = (request, certificate, chain, errors) =>
                {
                    if (errors == SslPolicyErrors.None)
                        return true;

                    var host = request?.RequestUri?.Host;
                    return string.Equals(host, "localhost", StringComparison.OrdinalIgnoreCase)
                        || host == "127.0.0.1"
                        || host == "::1";
                }
            };
        }

        Task IModuleInitializer.InitializeAsync(IServiceProvider serviceProvider, CancellationToken cancellationToken)
        {
#if DEBUG
            //           return Task.CompletedTask;
#endif
            cancellationToken.ThrowIfCancellationRequested();

            ServiceLocator.RebuildServiceProvider(collection =>
            {
                collection.RemoveAll<SegmentationGrpcChannelManager>();
                collection.AddSingleton<SegmentationGrpcChannelManager>(sp =>
                {
                    var configuration = sp.GetRequiredService<IGrpcServiceConfiguration>();
                    return new Viking.Services.Grpc.GrpcChannelManager(configuration);
                });
            });

            var refreshedProvider = ServiceLocator.ServiceProvider ?? serviceProvider;

            return InitializeModuleAndStoresAsync(refreshedProvider, cancellationToken);
        }

        static async Task InitializeModuleAndStoresAsync(IServiceProvider serviceProvider, CancellationToken cancellationToken)
        {
            if (!await InitializeModuleAsync(serviceProvider, cancellationToken).ConfigureAwait(false))
            {
                throw new InvalidOperationException("WebAnnotation initialization failed.");
            }

            if (serviceProvider.GetService<WebAnnotationModel.IAnnotationStores>() is WebAnnotationModel.IAnnotationStores annotationStores)
            {
                try
                {
                    await WebAnnotationModel.Store.InitializeAsync(annotationStores, cancellationToken).ConfigureAwait(false);
                }
                catch (Exception ex) when (ex is not OperationCanceledException)
                {
                    // The viewer stays open without annotations rather than failing module start.
                    Trace.WriteLine("[WebAnnotation] Annotation store failed to start: " + ex);
                    System.Windows.Forms.MessageBox.Show(
                        "Viking could not load annotations.\n\n" + ex.Message +
                        "\n\nThe viewer will stay open. Annotations stay unavailable until a later launch is accepted by the annotation service.",
                        "Viking",
                        System.Windows.Forms.MessageBoxButtons.OK,
                        System.Windows.Forms.MessageBoxIcon.Warning);
                }
            }
        }

        private static async Task<bool> InitializeModuleAsync(IServiceProvider serviceProvider, CancellationToken cancellationToken)
        {  
            //Find the server hosting the volume.  Look for an XML file mapping the volume to an endpoint.
            Viking.ViewModels.VolumeViewModel volume = Viking.UI.State.volume;

            if (volume is null)
            {
                return false;
            }

            //Section Thickness is hard-coded, should be pulled from server.
            Scale = new Geometry.Vector3(volume.DefaultXYScale.Value, volume.DefaultXYScale.Value, 90.0);
            AnnotationSettings.ApplyCorrectionShimSettings();

            // The section-correction shim reuses the annotation channels (WinHttp handler, https rewrite, bearer token).
            if (serviceProvider?.GetService<WebAnnotationModel.gRPC.IGrpcChannelManager>() is WebAnnotationModel.gRPC.IGrpcChannelManager correctionChannels)
            {
                Viking.SectionCorrectionClient.SectionShimContext.Source = new Viking.SectionCorrectionClient.GrpcSectionShimSource(
                    uri => correctionChannels.GetOrCreate(uri).CreateCallInvoker());
            }

            WebAnnotationModel.State.UserCredentials = Viking.UI.State.UserCredentials;

            if (serviceProvider?.GetService<ApplicationSettings>() is ApplicationSettings applicationSettings)
            {
                AnnotationSettings.SegmentationServiceUrl = applicationSettings.SegmentationURL;
            }

            serviceProvider?.GetService<SegmentationGrpcChannelManager>();

            if (GetEndpointFromXML(volume.VolumeElement))
            {
                try
                {
                    await LoadUserPreferencesAsync().ConfigureAwait(false);
                }
                catch (OperationCanceledException)
                {
                    Trace.WriteLine("LoadUserPreferences timed out during initialization.");
                }

                cancellationToken.ThrowIfCancellationRequested();

                if (serviceProvider?.GetService<IOptions<GrpcRepositorySettings>>() is IOptions<GrpcRepositorySettings> grpcSettings
                    && WebAnnotationModel.State.Endpoint != null)
                {
                    grpcSettings.Value.Endpoint = WebAnnotationModel.State.Endpoint;
                }

                if (serviceProvider?.GetService<WebAnnotationModel.IAnnotationStores>() is null)
                {
                    Trace.WriteLine("WebAnnotationModel.IAnnotationStores is not registered with the service provider. Store.X will throw until the gRPC composition root is completed.", "WebAnnotation");
                }

                // A startup Location ID is applied once, by AnnotationOverlay.TryApplyStartupLocation.
                return true;
            }

            return false;
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

                    if (MappingElements.Count() > 0)
                        Global.PopulateEndpointStateFromVolumeToEndpointElement(MappingElements.First());
                    else
                        Global.ApplyIdentityAnnotationEndpointOnly();

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

        /// <summary>
        /// Applies Identity session annotation / export URLs when VikingXML has no VolumeToEndpoint.
        /// </summary>
        private static void ApplyIdentityAnnotationEndpointOnly()
        {
            Uri? resolvedEndpoint = IdentityEndpoints.ResolveAnnotationServiceEndpoint(
                null,
                AccessibleVolumeSession.AnnotationServiceEndpoint?.ToString());
            if (resolvedEndpoint != null)
                WebAnnotationModel.State.Endpoint = resolvedEndpoint;

            if (!string.IsNullOrWhiteSpace(AccessibleVolumeSession.AnnotationServerName))
                Global.EndpointName = AccessibleVolumeSession.AnnotationServerName;

            if (AccessibleVolumeSession.ExportEndpoint != null)
                Global.Export = new WebAnnotation.Export(AccessibleVolumeSession.ExportEndpoint);
        }

        private static void PopulateEndpointStateFromVolumeToEndpointElement(XElement MappingElement)
        {
            XAttribute NameAttribute = MappingElement.Attribute("Name");
            if (NameAttribute != null)
            {
                Global.EndpointName = NameAttribute.Value;
            }
            else if (!string.IsNullOrWhiteSpace(AccessibleVolumeSession.AnnotationServerName))
            {
                Global.EndpointName = AccessibleVolumeSession.AnnotationServerName;
            }

            Uri? volumeXmlEndpoint = null;
            XAttribute EndpointAttribute = MappingElement.Attribute("Endpoint");
            if (EndpointAttribute != null)
            {
#if DEBUG
                volumeXmlEndpoint = new Uri(EndpointAttribute.Value);
                //                        WebAnnotationModel.State.EndpointAddress = new EndpointAddress("https://connectomes.utah.edu/Services/TestBinary/Annotate.svc");
#else
                volumeXmlEndpoint = new Uri(EndpointAttribute.Value);
#endif
            }

            Uri? resolvedEndpoint = IdentityEndpoints.ResolveAnnotationServiceEndpoint(
                volumeXmlEndpoint,
                AccessibleVolumeSession.AnnotationServiceEndpoint?.ToString());
            if (resolvedEndpoint != null)
                WebAnnotationModel.State.Endpoint = resolvedEndpoint;

            if (AccessibleVolumeSession.ExportEndpoint != null)
            {
                Global.Export = new WebAnnotation.Export(AccessibleVolumeSession.ExportEndpoint);
            }
            else
            {
                XAttribute ExportURLAttribute = MappingElement.Attribute("ExportURL");
                if (ExportURLAttribute != null)
                {
                    Global.Export = new WebAnnotation.Export(new Uri(ExportURLAttribute.Value));
                }
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

        /// <summary>
        /// Downloads a replacement settings file when the local one cannot be parsed.
        /// Leaves the previous in-memory settings unchanged when the download fails, so a
        /// bad server response does not erase a shortcuts file the user already had.
        /// </summary>
        private static async Task ReplaceUnreadableUserSettingsFromServerAsync(CancellationToken cancellationToken)
        {
            bool success = await LoadServerUserSettingsAsync(cancellationToken).ConfigureAwait(false);
            if (!success)
            {
                Trace.WriteLine("User settings could not be parsed and the server copy was not downloaded.");
                return;
            }

            cancellationToken.ThrowIfCancellationRequested();
            TryLoadLocalUserSettings();
        }

        /// <summary>
        /// Loads <see cref="UserSettingsFilePath"/> after correcting the legacy namespace.
        /// Called from startup. A missing file leaves <see cref="_userSettingsDoc"/> unchanged.
        /// </summary>
        private static bool TryLoadLocalUserSettings()
        {
            if (!System.IO.File.Exists(UserSettingsFilePath))
                return false;

            _userSettingsDoc = XRoot.Load(UserSettingsFilePath);
            return _userSettingsDoc != null;
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
                        // A failed download must not drop the shortcuts already on disk. That is the C hotkey.
                        Trace.WriteLine("User settings download failed; loading the local file.");
                    }
                }

                cancellationToken.ThrowIfCancellationRequested();
                TryLoadLocalUserSettings();
            }
            catch (OperationCanceledException)
            {
                Trace.WriteLine("LoadUserPreferencesAsync timed out after 10 seconds.");
                throw;
            }
            catch (Xml.Schema.Linq.LinqToXsdException)
            {
                // The local cache did not match the schema. Download the volume's settings file again.
                await ReplaceUnreadableUserSettingsFromServerAsync(cancellationToken).ConfigureAwait(false);
            }
            catch (System.Xml.XmlException)
            {
                await ReplaceUnreadableUserSettingsFromServerAsync(cancellationToken).ConfigureAwait(false);
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

        private static async Task<bool> LoadServerUserSettingsAsync(CancellationToken cancellationToken = default)
        {
            //Try to download the default user settings file
            Uri uri = UserSettingsUri;
            uri ??= new Uri("http://rouge1.codepharm.net/RABBIT/WebAnnotationUserSettings.xml");

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
