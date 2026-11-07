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

        /// <summary>The canvases the crossing tests above draw over: a box, a box with a hole, and a box holding a link marker.</summary>
        private static IEnumerable<DummyCanvas> EraseTestCanvases()
        {
            DummyCanvas box = new DummyCanvas();
            box.Add(new DummyAnnotation(new Polygon(Primitives.BoxVerticies(5))));
            yield return box;

            DummyCanvas hollow = new DummyCanvas();
            hollow.Add(new DummyContainerAnnotation(new Polygon(Primitives.BoxVerticies(5)), new Polygon[] { new Polygon(Primitives.BoxVerticies(2)) }));
            yield return hollow;

            DummyCanvas marker = new DummyCanvas();
            marker.Add(new DummyMarkerHost(new Polygon(Primitives.BoxVerticies(5)), new DummyLinkMarker(new Polygon(Primitives.BoxVerticies(2)))));
            yield return marker;
        }

        /// <summary>
        /// Erasing a stroke at every vertex, including back to one point or none, never throws and leaves no log entry past
        /// the end of the path. Erasing across a stroke's first segment cuts it back to one point, which used to
        /// throw from the logger because no segment was left; popping the last point threw on the emptied log.
        /// </summary>
        [TestMethod]
        public void EraseAtEveryVertex_KeepsLogWithinPath()
        {
            List<Vector2> points = StraightIntegerXAxisPath(-8, 8);
            foreach (DummyCanvas dc in EraseTestCanvases())
            {
                for (int keep = 0; keep < points.Count; keep++)
                {
                    Path path = new Path();
                    PathInteractionLogger log = new PathInteractionLogger(path, dc);
                    points.ForEach(path.Push);

                    path.Erase(keep);

                    Assert.AreEqual(keep, path.Points.Count);
                    foreach (InteractionLogEvent e in log.Log.Entries)
                    {
                        Assert.IsTrue(e.Index < keep, $"Erase to {keep} points left {e}");
                    }
                }
            }
        }

        /// <summary>
        /// A stroke erased back to one point or none and drawn on again logs exactly what drawing it once logs, so the
        /// logger's idea of what the pen is over is reset with the path rather than left from the erased points.
        /// </summary>
        [TestMethod]
        public void EraseToOnePointOrNone_ThenRedraw_LogsAsOneStroke()
        {
            //Strokes that start and end outside every annotation, inside the box, and inside the hole or marker
            List<Vector2>[] strokes = { StraightIntegerXAxisPath(-8, 8), StraightIntegerXAxisPath(-3, 4), StraightIntegerXAxisPath(-1, 4) };
            foreach (List<Vector2> points in strokes)
            {
                foreach (DummyCanvas dc in EraseTestCanvases())
                {
                    Path once = new Path();
                    PathInteractionLogger expected = new PathInteractionLogger(once, dc);
                    points.ForEach(once.Push);

                    for (int keep = 0; keep <= 1; keep++)
                    {
                        Path path = new Path();
                        PathInteractionLogger log = new PathInteractionLogger(path, dc);
                        points.ForEach(path.Push);

                        path.Erase(keep);
                        points.GetRange(keep, points.Count - keep).ForEach(path.Push);

                        CompareLogEntries(expected.Log.Entries, log.Log.Entries);
                    }
                }
            }
        }

        /// <summary>Popping every point of a stroke, one at a time, empties the log without throwing.</summary>
        [TestMethod]
        public void PopEveryPoint_EmptiesLog()
        {
            foreach (DummyCanvas dc in EraseTestCanvases())
            {
                Path path = new Path();
                PathInteractionLogger log = new PathInteractionLogger(path, dc);
                StraightIntegerXAxisPath(-8, 8).ForEach(path.Push);

                while (path.Points.Count > 0)
                {
                    path.Pop();
                }

                Assert.AreEqual(0, log.Log.Entries.Count);
            }
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
