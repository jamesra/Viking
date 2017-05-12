using System;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using System.Linq; 
using Geometry;
using System.Diagnostics;
using System.Collections.Generic;

namespace GeometryTests
{
    [TestClass]
    public class GridPolygonTest
    {
        GridVector2[] BoxVerticies(double scale)
        {
            GridVector2[] ExteriorPoints =
            {
                new GridVector2(-1, -1),
                new GridVector2(-1, 0),
                new GridVector2(-1, 1),
                new GridVector2(1,1),
                new GridVector2(1,-1),
                new GridVector2(-1,-1)
            };

            GridVector2[] ExteriorPointsScaled = ExteriorPoints.Scale(scale, new GridVector2(0, 0)).ToArray();
            return ExteriorPointsScaled;
        }


        GridVector2[] ConcaveUVerticies(double scale)
        {
            //  *--*    *--*
            //  |  |    |  |
            //  |  |    |  |  
            //  |  *----*  |
            //  *----------*
            GridVector2[] ExteriorPoints =
            {
                new GridVector2(-1, -1),
                new GridVector2(-1, 1),
                new GridVector2(-0.5, 1),
                new GridVector2(-0.5, -0.5),
                new GridVector2(0.5,-0.5),
                new GridVector2(0.5,1),
                new GridVector2(1,1),
                new GridVector2(1,-1),
                new GridVector2(-1,-1)
            };

            GridVector2[] ExteriorPointsScaled = ExteriorPoints.Scale(scale, new GridVector2(0, 0)).ToArray();
            return ExteriorPointsScaled;
        }

        GridPolygon CreateBoxPolygon(double scale)
        {
            GridVector2[] ExteriorPointsScaled = BoxVerticies(scale);

            return new GridPolygon(ExteriorPointsScaled);
        }

        GridPolygon CreateTrianglePolygon(double scale)
        {
            GridVector2[] ExteriorPoints =
            {
                new GridVector2(-1, -1),
                new GridVector2(-1, 1),
                new GridVector2(1, -1),
                new GridVector2(-1,-1)
            };

            return new GridPolygon(ExteriorPoints);

        }

        GridPolygon CreateUPolygon(double scale)
        {
            GridVector2[] ExteriorPointsScaled = ConcaveUVerticies(scale);
            
            return new GridPolygon(ExteriorPointsScaled);
        }

        /// <summary>
        /// Ensure our Clockwise function works and that polygons are created Counter-Clockwise
        /// </summary>
        [TestMethod]
        public void ClockwiseTest()
        {
            GridVector2[] clockwisePoints = BoxVerticies(1);
            Assert.IsTrue(clockwisePoints.AreClockwise());

            GridVector2[] counterClockwisePoints = clockwisePoints.Reverse().ToArray();

            Assert.IsTrue(clockwisePoints[1] == counterClockwisePoints[counterClockwisePoints.Length - 2]);

            Assert.IsFalse(counterClockwisePoints.AreClockwise());

            GridPolygon clockwisePoly = new GridPolygon(clockwisePoints);
            GridPolygon counterClockwisePoly = new GridPolygon(clockwisePoints);

            Assert.IsFalse(clockwisePoly.ExteriorRing.AreClockwise());
            Assert.IsFalse(counterClockwisePoly.ExteriorRing.AreClockwise());
        }

        [TestMethod]
        public void AreaTest()
        {
            GridPolygon box = CreateBoxPolygon(10);
            Assert.AreEqual(box.Area, box.BoundingBox.Area);
            Assert.AreEqual(box.Area, 400);

            GridPolygon translated_box = box.Translate(new GridVector2(10, 10));
            Assert.AreEqual(translated_box.Area, translated_box.BoundingBox.Area);
            Assert.AreEqual(translated_box.Area, 400);
            Assert.AreEqual(translated_box.Area, box.Area);

            GridPolygon tri = CreateTrianglePolygon(10);
            Assert.AreEqual(tri.Area, tri.BoundingBox.Area / 2.0);
            Assert.AreEqual(tri.Area, 2);

            GridPolygon translated_tri = tri.Translate(new GridVector2(10, -10));
            Assert.AreEqual(translated_tri.Area, translated_tri.BoundingBox.Area / 2.0);
            Assert.AreEqual(translated_tri.Area, 2);
            Assert.AreEqual(translated_tri.Area, tri.Area);
        }

        [TestMethod]
        public void CentroidTest()
        {
            GridPolygon box = CreateBoxPolygon(10);
            Assert.AreEqual(box.Centroid, box.BoundingBox.Center);
        }

        [TestMethod]
        public void ContainsTest()
        {
            GridPolygon box = CreateBoxPolygon(10);
            Assert.IsFalse(box.Contains(new GridVector2(-15, 5)));
            Assert.IsTrue(box.Contains(new GridVector2(-5, 5)));
            Assert.IsTrue(box.Contains(new GridVector2(0, 0)));

            GridPolygon inner_box = CreateBoxPolygon(5);

            box.AddInteriorRing(inner_box.ExteriorRing);

            Assert.IsFalse(box.Contains(new GridVector2(-15, 5)));
            Assert.IsFalse(box.Contains(new GridVector2(0, 0)));
        }

        [TestMethod]
        public void ConcaveContainsTest()
        {
            GridPolygon box = CreateUPolygon(10);
            Assert.IsFalse(box.Contains(new GridVector2(-15, 5)));
            Assert.IsTrue(box.Contains(new GridVector2(-6.6, -6.6)));
            Assert.IsFalse(box.Contains(new GridVector2(0, 0)));
        }

        [TestMethod]
        public void AddRemoveVertexTest()
        {
            GridPolygon original_box = CreateBoxPolygon(10);
            GridPolygon box = CreateBoxPolygon(10);
            int numOriginalVerticies = box.ExteriorRing.Length;
            GridVector2 newVertex = new GridVector2(-10, -5);
            box.AddVertex(newVertex);
            Assert.AreEqual(box.ExteriorRing.Length, numOriginalVerticies + 1);
            Assert.AreEqual(box.ExteriorRing[box.ExteriorRing.Length-2], newVertex);

            box.RemoveVertex(newVertex);
            Assert.AreEqual(box.ExteriorRing.Length, numOriginalVerticies);

            box = CreateBoxPolygon(10);
            newVertex = new GridVector2(-5, -10);
            box.AddVertex(newVertex);
            Assert.AreEqual(box.ExteriorRing.Length, numOriginalVerticies + 1);
            Assert.AreEqual(box.ExteriorRing[1], newVertex);

            box.RemoveVertex(newVertex - new GridVector2(1,1));
            Assert.AreEqual(box.ExteriorRing.Length, numOriginalVerticies);
            Assert.IsTrue(box.ExteriorRing.All(p => p != newVertex));
        }
    }
}
