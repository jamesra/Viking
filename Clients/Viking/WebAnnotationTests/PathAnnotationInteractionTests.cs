using System;
using System.Collections.Generic;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using Geometry;
using Viking;
using WebAnnotation;
using GeometryTests;
using WebAnnotation.UI;

namespace WebAnnotationTests
{

    [TestClass]
    public class PathAnnotationInteractionTests
    {
        public static List<GridVector2> StraightIntegerXAxisPath(int minx, int maxx)
        {
            List<GridVector2> path_points = new List<GridVector2>();

            for (int x = minx; x < maxx; x++)
            {
                GridVector2 p = new GridVector2(x, 0);
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
            GridPolygon poly = new GridPolygon(Primitives.BoxVerticies(5));

            DummyAnnotation da = new DummyAnnotation(poly);

            DummyCanvas dc = new DummyCanvas();
            dc.Add(da);

            List<GridVector2> path_points = StraightIntegerXAxisPath(-8, 8);

            Path path = new Path();

            PathInteractionLogger log = new PathInteractionLogger(path, dc);

            foreach (GridVector2 p in path_points)
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
            GridPolygon poly = new GridPolygon(Primitives.BoxVerticies(5));

            GridPolygon hole = new GridPolygon(Primitives.BoxVerticies(2));
            
            DummyContainerAnnotation da = new DummyContainerAnnotation(poly, new GridPolygon[] { hole });

            DummyCanvas dc = new DummyCanvas();
            dc.Add(da);
             
            List<GridVector2> path_points = StraightIntegerXAxisPath(-8, 8);

            Path path = new Path();

            PathInteractionLogger log = new PathInteractionLogger(path, dc);

            foreach (GridVector2 p in path_points)
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
            GridPolygon poly = new GridPolygon(Primitives.BoxVerticies(5.5));

            GridPolygon hole = new GridPolygon(Primitives.BoxVerticies(2.5));

            DummyContainerAnnotation da = new DummyContainerAnnotation(poly, new GridPolygon[] { hole });

            DummyCanvas dc = new DummyCanvas();
            dc.Add(da);

            List<GridVector2> path_points = StraightIntegerXAxisPath(-8, 8);

            Path path = new Path();

            PathInteractionLogger log = new PathInteractionLogger(path, dc);

            foreach (GridVector2 p in path_points)
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
    }
}
