using Geometry;
using GeometryTests;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using System.Collections.Generic;
using WebAnnotation.UI;

namespace WebAnnotationTests
{

    [TestClass]
    public class PathAnnotationInteractionTests
    {
        public static List<Vector2> StraightIntegerXAxisPath(int minx, int maxx)
        {
            List<Vector2> path_points = new List<Vector2>();

            for (int x = minx; x < maxx; x++)
            {
                Vector2 p = new Vector2(x, 0);
                path_points.Add(p);
            }

            return path_points;
        }

        private void CompareLogEntries(IReadOnlyList<InteractionLogEvent> expected, IReadOnlyList<InteractionLogEvent> actual)
        {
            Assert.AreEqual(expected.Count, actual.Count, "Expected and actual logs should have same number of entries");

            for(int i = 0; i < expected.Count; i++)
            {
                Assert.AreEqual(expected[i], actual[i], string.Format("Expected {0} != Actual {1}", expected[i], actual[i]));
            }
        }

        /// <summary>
        /// Test path intersection where the points fall on a annotation border 
        /// </summary>

        [TestMethod]
        public void TestPathAcrossBox()
        {
            Polygon poly = new Polygon(Primitives.BoxVerticies(5));

            DummyAnnotation da = new DummyAnnotation(poly);

            DummyCanvas dc = new DummyCanvas();
            dc.Add(da);

            List<Vector2> path_points = StraightIntegerXAxisPath(-8, 8);

            Path path = new Path();

            PathInteractionLogger log = new PathInteractionLogger(path, dc);

            foreach (Vector2 p in path_points)
            {
                path.Push(p);
            }

            InteractionLogEvent[] expected = new InteractionLogEvent[]
            {
                new InteractionLogEvent(AnnotationRegionInteraction.ENTER, null, 0),
                new InteractionLogEvent(AnnotationRegionInteraction.EXIT,  null, 3),
                new InteractionLogEvent(AnnotationRegionInteraction.ENTER, da, 3),
                new InteractionLogEvent(AnnotationRegionInteraction.EXIT, da, 14),
                new InteractionLogEvent(AnnotationRegionInteraction.ENTER, null, 14)
            };

            CompareLogEntries(expected, log.Log.Entries);
        }

        /// <summary>
        /// Test path intersection where the points fall on a annotation border with another child annotation contained in the larger annotation
        /// </summary>
        [TestMethod]
        public void TestPathAcrossHollowBox()
        {
            Polygon poly = new Polygon(Primitives.BoxVerticies(5));

            Polygon hole = new Polygon(Primitives.BoxVerticies(2));
            
            DummyContainerAnnotation da = new DummyContainerAnnotation(poly, new Polygon[] { hole });

            DummyCanvas dc = new DummyCanvas();
            dc.Add(da);
             
            List<Vector2> path_points = StraightIntegerXAxisPath(-8, 8);

            Path path = new Path();

            PathInteractionLogger log = new PathInteractionLogger(path, dc);

            foreach (Vector2 p in path_points)
            {
                path.Push(p);
            }

            InteractionLogEvent[] expected = new InteractionLogEvent[]
            {
                new InteractionLogEvent(AnnotationRegionInteraction.ENTER, null, 0),
                new InteractionLogEvent(AnnotationRegionInteraction.EXIT,  null, 3),
                new InteractionLogEvent(AnnotationRegionInteraction.ENTER, da, 3),
                new InteractionLogEvent(AnnotationRegionInteraction.EXIT, da, 6),
                new InteractionLogEvent(AnnotationRegionInteraction.ENTER, da.Children[0], 6),
                new InteractionLogEvent(AnnotationRegionInteraction.EXIT, da.Children[0], 11),
                new InteractionLogEvent(AnnotationRegionInteraction.ENTER, da, 11),
                new InteractionLogEvent(AnnotationRegionInteraction.EXIT, da, 14),
                new InteractionLogEvent(AnnotationRegionInteraction.ENTER, null, 14)
            };

            CompareLogEntries(expected, log.Log.Entries);
        }

        /// <summary>
        /// Test path intersection where the points do not fall on a annotation border
        /// </summary>
        [TestMethod]
        public void TestPathAcrossOffsetHollowBox()
        {
            Polygon poly = new Polygon(Primitives.BoxVerticies(5.5));

            Polygon hole = new Polygon(Primitives.BoxVerticies(2.5));

            DummyContainerAnnotation da = new DummyContainerAnnotation(poly, new Polygon[] { hole });

            DummyCanvas dc = new DummyCanvas();
            dc.Add(da);

            List<Vector2> path_points = StraightIntegerXAxisPath(-8, 8);

            Path path = new Path();

            PathInteractionLogger log = new PathInteractionLogger(path, dc);

            foreach (Vector2 p in path_points)
            {
                path.Push(p);
            }

            InteractionLogEvent[] expected = new InteractionLogEvent[]
            {
                new InteractionLogEvent(AnnotationRegionInteraction.ENTER, null, 0),
                new InteractionLogEvent(AnnotationRegionInteraction.EXIT,  null, 3),
                new InteractionLogEvent(AnnotationRegionInteraction.ENTER, da, 3),
                new InteractionLogEvent(AnnotationRegionInteraction.EXIT, da, 6),
                new InteractionLogEvent(AnnotationRegionInteraction.ENTER, da.Children[0], 6),
                new InteractionLogEvent(AnnotationRegionInteraction.EXIT, da.Children[0], 11),
                new InteractionLogEvent(AnnotationRegionInteraction.ENTER, da, 11),
                new InteractionLogEvent(AnnotationRegionInteraction.EXIT, da, 14),
                new InteractionLogEvent(AnnotationRegionInteraction.ENTER, null, 14)
            };

            CompareLogEntries(expected, log.Log.Entries);
        }

        /// <summary>
        /// A link marker drawn inside a location is crossed without leaving the location, so the only crossings of the
        /// location in the log are the real crossings of its outline. A stroke over a marker used to log an exit and an
        /// entry there, and the pen cut, which reads the last two crossings, then found nothing to cut.
        /// </summary>
        [TestMethod]
        public void TestPathAcrossLinkMarkerStaysInsideTheLocation()
        {
            Polygon poly = new Polygon(Primitives.BoxVerticies(5));
            DummyLinkMarker marker = new DummyLinkMarker(new Polygon(Primitives.BoxVerticies(2)));
            DummyMarkerHost host = new DummyMarkerHost(poly, marker);

            DummyCanvas dc = new DummyCanvas();
            dc.Add(host);

            Path path = new Path();
            PathInteractionLogger log = new PathInteractionLogger(path, dc);
            foreach (Vector2 p in StraightIntegerXAxisPath(-8, 8))
            {
                path.Push(p);
            }

            InteractionLogEvent[] expected = new InteractionLogEvent[]
            {
                new InteractionLogEvent(AnnotationRegionInteraction.ENTER, null, 0),
                new InteractionLogEvent(AnnotationRegionInteraction.EXIT,  null, 3),
                new InteractionLogEvent(AnnotationRegionInteraction.ENTER, host, 3),
                new InteractionLogEvent(AnnotationRegionInteraction.ENTER, marker, 6),
                new InteractionLogEvent(AnnotationRegionInteraction.EXIT, marker, 11),
                new InteractionLogEvent(AnnotationRegionInteraction.EXIT, host, 14),
                new InteractionLogEvent(AnnotationRegionInteraction.ENTER, null, 14)
            };

            CompareLogEntries(expected, log.Log.Entries);
        }

        /// <summary>An arrow marker for a location link, like the ones drawn inside a circle or polygon.</summary>
        private sealed class DummyLinkMarker : DummyAnnotation, WebAnnotation.IViewLocationLink
        {
            public DummyLinkMarker(IShape2D shape) : base(shape) { }

            public Viking.AnnotationServiceTypes.LocationLinkKey Key => new Viking.AnnotationServiceTypes.LocationLinkKey(1, 2);
        }

        /// <summary>A location that returns its marker when the point is on it, as a circle or polygon does.</summary>
        private sealed class DummyMarkerHost : DummyAnnotation, WebAnnotation.ICanvasViewContainer
        {
            private readonly DummyLinkMarker marker;

            public DummyMarkerHost(IShape2D shape, DummyLinkMarker marker) : base(shape)
            {
                this.marker = marker;
            }

            public WebAnnotation.ICanvasView GetAnnotationAtPosition(Vector2 position) =>
                marker.Contains(position) ? marker : Contains(position) ? this : null;
        }
    }
}
