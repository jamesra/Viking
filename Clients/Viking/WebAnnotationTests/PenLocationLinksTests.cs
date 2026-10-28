using Geometry;
using GeometryTests;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Runtime.Serialization;
using Viking.AnnotationServiceTypes;
using WebAnnotation;
using WebAnnotation.UI;
using WebAnnotation.UI.Commands;
using WebAnnotation.UI.Commands.Segmentation;
using WebAnnotation.View;
using WebAnnotationModel;

namespace WebAnnotationTests
{
    /// <summary>
    /// A pen stroke and its choice buttons hide location links (the arrow markers inside a location and the link lines),
    /// keep them out of hit tests and the stroke log, and leave them in place for the Link command and the idle pen. Pins
    /// which commands hide them (<see cref="PenLocationLinks.HidesLocationLinks"/>) and what a hidden link no longer does.
    /// </summary>
    [TestClass]
    public class PenLocationLinksTests
    {
        private Func<Viking.UI.Commands.Command?> savedActiveCommand = PenLocationLinks.DefaultActiveCommand;

        [TestInitialize]
        public void SaveActiveCommand() => savedActiveCommand = PenLocationLinks.ActiveCommand;

        [TestCleanup]
        public void RestoreActiveCommand() => PenLocationLinks.ActiveCommand = savedActiveCommand;

        /// <summary>A command of type <typeparamref name="T"/> without running its constructor, which needs a viewer.</summary>
        private static T Command<T>() where T : Viking.UI.Commands.Command => (T)FormatterServices.GetUninitializedObject(typeof(T));

        private static void Activate(Viking.UI.Commands.Command? command) => PenLocationLinks.ActiveCommand = () => command;

        [TestMethod]
        public void PenStrokesAndTheirChoiceButtonsHideLinks()
        {
            Assert.IsTrue(PenLocationLinks.HidesLocationLinks(Command<AnnotationOverlayPenFreeDrawCommandV2>()), "freehand stroke");
            Assert.IsTrue(PenLocationLinks.HidesLocationLinks(Command<CutHoleWithPenCommand>()), "pen cut hole");
            Assert.IsTrue(PenLocationLinks.HidesLocationLinks(Command<RetraceAndReplacePathCommand>()), "retrace and replace");
            Assert.IsTrue(PenLocationLinks.HidesLocationLinks(Command<PlaceClosedCurveWithPenCommand>()), "closed curve with the pen");
            Assert.IsTrue(PenLocationLinks.HidesLocationLinks(Command<PlaceOpenCurveWithPenCommand>()), "open curve with the pen");
            Assert.IsTrue(PenLocationLinks.HidesLocationLinks(Command<ActionSelectionCanvasControl>()), "choice buttons after a stroke");
        }

        [TestMethod]
        public void TheLinkCommandTheIdlePenAndOtherCommandsKeepLinks()
        {
            Assert.IsFalse(PenLocationLinks.HidesLocationLinks(Command<LinkAnnotationsCommand>()), "making a link needs the links");
            Assert.IsFalse(PenLocationLinks.HidesLocationLinks(Command<Viking.UI.Commands.DefaultCommand>()), "idle pen or mouse");
            Assert.IsFalse(PenLocationLinks.HidesLocationLinks(null));
            Assert.IsFalse(PenLocationLinks.HidesLocationLinks(Command<TranslateClosedCurveCommand>()));
            Assert.IsFalse(PenLocationLinks.HidesLocationLinks(Command<SegmentationCommand>()), "segmentation takes mouse or pen prompts");
            Assert.IsFalse(PenLocationLinks.HidesLocationLinks(Command<SegmentationMaskEditCommand>()), "mask edit after segmentation");
        }

        [TestMethod]
        public void WithoutAViewerLinksAreShown()
        {
            PenLocationLinks.ActiveCommand = PenLocationLinks.DefaultActiveCommand;
            Assert.IsFalse(PenLocationLinks.Hidden);
        }

        [TestMethod]
        public void MarkersCannotBeHitWhileAPenStrokeHidesThem()
        {
            OverlappedLinkCircleView markers = Markers(Marker(1, 2, new Vector2(0, 0)));
            Vector2 onMarker = new(0, 0.5);

            Activate(Command<AnnotationOverlayPenFreeDrawCommandV2>());
            Assert.IsFalse(markers.Contains(onMarker));
            Assert.IsFalse(markers.Intersects(new LineSegment(new Vector2(-3, 0), new Vector2(3, 0))));
            Assert.IsNull(markers.GetAnnotationAtPosition(onMarker));
            Assert.AreEqual(double.MaxValue, markers.Distance(onMarker));

            Activate(Command<LinkAnnotationsCommand>());
            Assert.IsTrue(markers.Contains(onMarker), "the Link command shows the markers");
            Assert.IsInstanceOfType(markers.GetAnnotationAtPosition(onMarker), typeof(OverlappedLocationLinkView));

            Activate(null);
            Assert.IsTrue(markers.Contains(onMarker), "no command shows the markers");
        }

        /// <summary>
        /// A stroke across a location with a link marker at its centre logs the location only while the stroke hides the
        /// markers; with the markers shown the stroke also logs entering and leaving the marker.
        /// </summary>
        [TestMethod]
        public void TheStrokeLogSkipsMarkersWhileAPenStrokeHidesThem()
        {
            MarkerHost host = new(new Polygon(Primitives.BoxVerticies(5)), Markers(Marker(1, 2, new Vector2(0, 0))));

            Activate(Command<AnnotationOverlayPenFreeDrawCommandV2>());
            IReadOnlyList<InteractionLogEvent> hidden = StrokeAcross(host, filterLinks: false);
            Assert.IsFalse(hidden.Any(e => e.Annotation is IViewLocationLink), string.Join("\n", hidden));
            CollectionAssert.AreEqual(new (AnnotationRegionInteraction, ICanvasView)[] { (AnnotationRegionInteraction.ENTER, host), (AnnotationRegionInteraction.EXIT, host) },
                                      hidden.Where(e => e.Annotation is not null).Select(e => (e.Interaction, e.Annotation)).ToArray());

            Activate(Command<LinkAnnotationsCommand>());
            IReadOnlyList<InteractionLogEvent> shown = StrokeAcross(host, filterLinks: false);
            Assert.IsTrue(shown.Any(e => e.Annotation is OverlappedLocationLinkView), "shown markers are logged: " + string.Join("\n", shown));
        }

        /// <summary>The stroke log's filter drops link lines, which are not containers and reach the log as hits of their own.</summary>
        [TestMethod]
        public void TheStrokeLogFilterDropsLinkLines()
        {
            DummyAnnotation location = new(new Polygon(Primitives.BoxVerticies(5)));
            DummyLink line = new(new Polygon(Primitives.BoxVerticies(1)));

            IReadOnlyList<InteractionLogEvent> unfiltered = StrokeAcross([location, line], filterLinks: false);
            Assert.IsTrue(unfiltered.Any(e => ReferenceEquals(e.Annotation, line)), "without the filter the line is logged");

            IReadOnlyList<InteractionLogEvent> filtered = StrokeAcross([location, line], filterLinks: true);
            Assert.IsFalse(filtered.Any(e => ReferenceEquals(e.Annotation, line)), string.Join("\n", filtered));
            Assert.IsTrue(filtered.Any(e => ReferenceEquals(e.Annotation, location)));
        }

        [TestMethod]
        public void ExcludeLinksKeepsEverythingElseAndAcceptsNull()
        {
            DummyAnnotation location = new(new Polygon(Primitives.BoxVerticies(5)));
            DummyLink line = new(new Polygon(Primitives.BoxVerticies(1)));
            List<HitTestResult> onlyLocation = [new(location, 0, 0, 0)];

            Assert.AreSame(onlyLocation, PenLocationLinks.ExcludeLinks(onlyLocation), "nothing to drop, nothing allocated");
            CollectionAssert.AreEqual(new object[] { location }, PenLocationLinks.ExcludeLinks([new(line, 0, 0, 0), new(location, 0, 0, 0)]).Select(h => h.obj).ToArray());
            Assert.IsNull(PenLocationLinks.ExcludeLinks(null!));
        }

        private static IReadOnlyList<InteractionLogEvent> StrokeAcross(DummyAnnotation annotation, bool filterLinks) => StrokeAcross([annotation], filterLinks);

        /// <summary>Logs a stroke along the x axis from -8 to 7 over <paramref name="annotations"/>.</summary>
        private static IReadOnlyList<InteractionLogEvent> StrokeAcross(DummyAnnotation[] annotations, bool filterLinks)
        {
            DummyCanvas canvas = new();
            foreach (DummyAnnotation annotation in annotations)
                canvas.Add(annotation);

            Path path = new();
            PathInteractionLogger logger = new(path, filterLinks ? new LocationLinkHitTestFilter(canvas) : canvas);
            foreach (Vector2 p in PathAnnotationInteractionTests.StraightIntegerXAxisPath(-8, 8))
                path.Push(p);

            return logger.Log.Entries;
        }

        /// <summary>A marker of radius 1 at <paramref name="center"/>, skipping the constructor (which reads the linked location's structure).</summary>
        private static OverlappedLocationLinkView Marker(long locationId, long linkedId, Vector2 center)
        {
            OverlappedLocationLinkView marker = (OverlappedLocationLinkView)FormatterServices.GetUninitializedObject(typeof(OverlappedLocationLinkView));
            SetField(marker, "circleView", new VikingXNAGraphics.TextureCircleView(null!, new Circle(center, 1), Microsoft.Xna.Framework.Color.White));
            SetField(marker, "linkKey", new LocationLinkKey(locationId, linkedId));
            marker.LocationID = locationId;
            return marker;
        }

        private static OverlappedLinkCircleView Markers(params OverlappedLocationLinkView[] markers)
        {
            OverlappedLinkCircleView links = (OverlappedLinkCircleView)FormatterServices.GetUninitializedObject(typeof(OverlappedLinkCircleView));
            SetField(links, "linkViews", new List<OverlappedLocationLinkView>(markers));
            return links;
        }

        private static void SetField(object target, string name, object value)
        {
            FieldInfo? field = target.GetType().GetField(name, BindingFlags.Instance | BindingFlags.NonPublic);
            Assert.IsNotNull(field, name);
            field.SetValue(target, value);
        }

        /// <summary>A location with arrow markers that returns the marker under the pen, like a circle, polygon or closed curve.</summary>
        private sealed class MarkerHost(IShape2D shape, OverlappedLinkCircleView markers) : DummyAnnotation(shape), ICanvasViewContainer, IOverlappedLinkHost
        {
            public OverlappedLinkCircleView? OverlappedLinkView => markers;

            public ICanvasView GetAnnotationAtPosition(Vector2 position) =>
                markers.GetAnnotationAtPosition(position) ?? (Contains(position) ? this : null!);
        }

        /// <summary>A location link line: not a container, so it reaches the stroke log as a hit of its own.</summary>
        private sealed class DummyLink(IShape2D shape) : DummyAnnotation(shape), IViewLocationLink
        {
            public LocationLinkKey Key => new(1, 2);
        }
    }
}
