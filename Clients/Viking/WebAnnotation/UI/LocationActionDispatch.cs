using Geometry;
using System;
using System.Diagnostics;
using System.Windows.Forms;

namespace WebAnnotation
{
    /// <summary>
    /// Maps a canvas hit to a <see cref="LocationAction"/> and location id.
    /// Interior hole views implement the action interfaces but are not
    /// <c>LocationCanvasView</c>; callers must not require that type.
    /// </summary>
    internal static class LocationActionDispatch
    {
        /// <summary>
        /// True when the hit can start a location command. Used by mouse-down and pen contact
        /// before free-draw. Unimplemented pen or mouse methods are logged and treated as no action
        /// so hover and stroke start keep running.
        /// </summary>
        public static bool TryGetAction(
            object? hit,
            Vector2 worldPosition,
            int visibleSectionNumber,
            Keys modifierKeys,
            bool penContact,
            out LocationAction action,
            out long locationId)
        {
            action = GetActionForCursor(hit, worldPosition, visibleSectionNumber, modifierKeys, penContact, out locationId);
            return action != LocationAction.NONE;
        }

        /// <summary>
        /// Action advertised for the cursor over <paramref name="hit"/>. Used by
        /// <see cref="AnnotationOverlay"/> mouse and pen hover so unimplemented view methods
        /// cannot crash the cursor path. Non-action hits and throws yield <see cref="LocationAction.NONE"/>.
        /// </summary>
        public static LocationAction GetActionForCursor(
            object? hit,
            Vector2 worldPosition,
            int visibleSectionNumber,
            Keys modifierKeys,
            bool penContact)
            => GetActionForCursor(hit, worldPosition, visibleSectionNumber, modifierKeys, penContact, out _);

        private static LocationAction GetActionForCursor(
            object? hit,
            Vector2 worldPosition,
            int visibleSectionNumber,
            Keys modifierKeys,
            bool penContact,
            out long locationId)
        {
            locationId = 0;

            try
            {
                if (penContact)
                {
                    if (hit is not IPenActionSupport pen)
                        return LocationAction.NONE;

                    return pen.GetPenContactActionForPositionOnAnnotation(
                        worldPosition, visibleSectionNumber, modifierKeys, out locationId);
                }

                if (hit is not IMouseActionSupport mouse)
                    return LocationAction.NONE;

                return mouse.GetMouseClickActionForPositionOnAnnotation(
                    worldPosition, visibleSectionNumber, modifierKeys, out locationId);
            }
            catch (NotImplementedException ex)
            {
                Trace.WriteLine($"Location action is not implemented for {hit?.GetType().Name}: {ex.Message}");
                locationId = 0;
                return LocationAction.NONE;
            }
        }
    }
}
