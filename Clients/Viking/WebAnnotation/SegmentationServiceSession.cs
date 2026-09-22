using System;
using System.Collections.Generic;
using Viking;
using Viking.DependencyInjection;
using Viking.UI;

namespace WebAnnotation
{
    /// <summary>
    /// Applies a mid-session segmentation endpoint (or None) to runtime settings and persisted history.
    /// Called from the Annotation menu after the picker closes with Select or None.
    /// </summary>
    internal static class SegmentationServiceSession
    {
        /// <summary>
        /// Raised when Select/None changes the endpoint. The argument is true when the new URL
        /// can accept a segment request. Auto-polygonize uses it to recapture the current view.
        /// Subscribers run before the gRPC channel is reset.
        /// </summary>
        internal static event Action<bool>? AutoPolygonizeResubmit;

        /// <summary>
        /// Updates <see cref="ApplicationSettings.SegmentationURL"/> and
        /// <see cref="Global.AnnotationSettings.SegmentationServiceUrl"/>, clears the availability cache,
        /// and asks the host to persist last/recent URLs. Null or whitespace means None.
        /// A different URL drops the previous server's auto-segment image and, when the new URL
        /// is usable, starts a new pass on the current view.
        /// </summary>
        public static void ApplyEndpoint(string? endpoint)
        {
            string normalized = string.IsNullOrWhiteSpace(endpoint) ? string.Empty : endpoint!.Trim();
            string previous = CurrentEndpoint();
            bool endpointChanged = !string.Equals(previous, normalized, StringComparison.OrdinalIgnoreCase);
            if (endpointChanged)
            {
                bool newEndpointIsUsable = Global.HasValidSegmentationServiceUrl(normalized);
                AutoPolygonizeResubmit?.Invoke(newEndpointIsUsable);
                AnnotationOverlay.CurrentOverlay?.OnSegmentationEndpointChanged(newEndpointIsUsable);
            }

            if (ServiceLocator.IsInitialized)
            {
                ApplicationSettings? appSettings = ServiceLocator.GetService<ApplicationSettings>();
                if (appSettings != null)
                    appSettings.SegmentationURL = normalized;
            }

            // Setter clears availability cache and resets the gRPC channel.
            Global.AnnotationSettings.SegmentationServiceUrl = normalized;
            Global.InvalidateSegmentationServiceAvailability();

            if (!string.IsNullOrEmpty(normalized))
            {
                List<string> recent = State.RecentSegmentationServiceUrls ?? [];
                recent.RemoveAll(u => string.Equals(u, normalized, StringComparison.OrdinalIgnoreCase));
                recent.Insert(0, normalized);
                State.RecentSegmentationServiceUrls = recent;
            }

            State.PersistSegmentationServiceSelection?.Invoke(string.IsNullOrEmpty(normalized) ? null : normalized);
        }

        /// <summary>
        /// Current segmentation URL from ApplicationSettings when set, otherwise AnnotationSettings.
        /// </summary>
        public static string CurrentEndpoint()
        {
            if (ServiceLocator.IsInitialized)
            {
                ApplicationSettings? appSettings = ServiceLocator.GetService<ApplicationSettings>();
                if (appSettings != null && !string.IsNullOrWhiteSpace(appSettings.SegmentationURL))
                    return appSettings.SegmentationURL.Trim();
            }

            string persisted = Global.AnnotationSettings.SegmentationServiceUrl;
            return string.IsNullOrWhiteSpace(persisted) ? string.Empty : persisted.Trim();
        }
    }
}
