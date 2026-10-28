#nullable enable
using Geometry;
using System;
using System.Collections.Generic;
using System.Linq;
using WebAnnotation.UI.Commands;

namespace WebAnnotation.UI
{
    /// <summary>
    /// Decides when the viewer hides location links for the pen: the arrow markers drawn inside a location for its links to
    /// overlapping locations on adjacent sections (<see cref="View.OverlappedLinkCircleView"/>) and the link lines with their
    /// labels (<see cref="View.LocationLinkView"/>). Links are hidden while a pen stroke command draws and while its choice
    /// buttons are up, because those commands never act on an existing link and the markers in the stroke log once kept the
    /// pen cut from finding its crossings. Links stay visible while the pen is idle and during
    /// <see cref="LinkAnnotationsCommand"/>, whose purpose is making a link. The locations on adjacent sections stay drawn
    /// and hit-testable either way: a stroke that touches one still offers to link to it
    /// (<see cref="PenInteractionLogExtensions.IdentifyPossibleLocationLinkActions"/>).
    /// </summary>
    internal static class PenLocationLinks
    {
        /// <summary>
        /// True when <paramref name="command"/> hides location links: a freehand pen stroke
        /// (<see cref="PlaceGeometryWithPenCommandBase"/>, which covers the overlay's free draw, the pen cut and retracing) or
        /// the choice buttons that follow a stroke (<see cref="ActionSelectionCanvasControl"/>). Every other command, the
        /// idle default command and null keep links visible.
        /// </summary>
        public static bool HidesLocationLinks(Viking.UI.Commands.Command? command) =>
            command is PlaceGeometryWithPenCommandBase or ActionSelectionCanvasControl;

        /// <summary>
        /// The command of the viewer being drawn and hit-tested. Reads <see cref="AnnotationOverlay.CurrentOverlay"/>, as
        /// the Del/Insert link hiding does; tests replace it and must restore it. Read on the UI thread.
        /// </summary>
        internal static Func<Viking.UI.Commands.Command?> ActiveCommand { get; set; } = DefaultActiveCommand;

        /// <summary>The default <see cref="ActiveCommand"/>: the current command of the active viewer, or null without one.</summary>
        internal static Viking.UI.Commands.Command? DefaultActiveCommand() => AnnotationOverlay.CurrentOverlay?.Parent?.CurrentCommand;

        /// <summary>True while the active viewer's command hides location links; see <see cref="HidesLocationLinks"/>.</summary>
        public static bool Hidden => HidesLocationLinks(ActiveCommand());

        /// <summary>Returns <paramref name="hits"/> without location link lines and link markers (<see cref="IViewLocationLink"/>).</summary>
        public static List<HitTestResult> ExcludeLinks(List<HitTestResult> hits) =>
            hits is null || !hits.Any(h => h.obj is IViewLocationLink) ? hits! : [.. hits.Where(h => h.obj is not IViewLocationLink)];
    }

    /// <summary>
    /// Wraps another <see cref="ICanvasViewHitTesting"/> and removes location link lines and link markers from every result.
    /// Used by the stroke log of <see cref="AnnotationOverlayPenFreeDrawCommandV2"/>, which only runs while
    /// <see cref="PenLocationLinks.HidesLocationLinks"/> holds, so a stroke never logs a link the viewer is not drawing.
    /// Markers inside a location are also kept out by <see cref="View.OverlappedLinkCircleView"/> itself, so a location
    /// is not expanded into one of its markers.
    /// </summary>
    /// <param name="inner">The hit-testing source to filter.</param>
    internal sealed class LocationLinkHitTestFilter(ICanvasViewHitTesting inner) : ICanvasViewHitTesting
    {
        /// <inheritdoc/>
        public List<HitTestResult> GetAnnotations(Vector2 WorldPosition) => PenLocationLinks.ExcludeLinks(inner.GetAnnotations(WorldPosition));

        /// <inheritdoc/>
        public List<HitTestResult> GetAnnotations(LineSegment line) => PenLocationLinks.ExcludeLinks(inner.GetAnnotations(line));

        /// <inheritdoc/>
        public List<HitTestResult> GetAnnotations(Rectangle rect) => PenLocationLinks.ExcludeLinks(inner.GetAnnotations(rect));
    }
}
