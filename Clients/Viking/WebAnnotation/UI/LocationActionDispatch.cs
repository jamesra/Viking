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
            action = LocationAction.NONE;
            locationId = 0;

            try
            {
                if (penContact)
                {
                    if (hit is not IPenActionSupport pen)
                        return false;

                    action = pen.GetPenContactActionForPositionOnAnnotation(
                        worldPosition, visibleSectionNumber, modifierKeys, out locationId);
                    return action != LocationAction.NONE;
                }

                if (hit is not IMouseActionSupport mouse)
                    return false;

                action = mouse.GetMouseClickActionForPositionOnAnnotation(
                    worldPosition, visibleSectionNumber, modifierKeys, out locationId);
                return action != LocationAction.NONE;
            }
            catch (NotImplementedException ex)
            {
                Trace.WriteLine($"Location action is not implemented for {hit?.GetType().Name}: {ex.Message}");
                action = LocationAction.NONE;
                locationId = 0;
                return false;
            }
        }
    }
}
