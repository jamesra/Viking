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
        /// <summary>
        /// Create a box, note I've added an extra vertex on the X:-1 vertical line
        /// </summary>
        /// <param name="scale"></param>
        /// <returns></returns>
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
        public void ConvexContainsTest()
        {
            GridPolygon box = CreateBoxPolygon(10);
            Assert.IsFalse(box.Contains(new GridVector2(-15, 5)));
            Assert.IsTrue(box.Contains(new GridVector2(-5, 5)));
            Assert.IsTrue(box.Contains(new GridVector2(0, 0)));

            GridPolygon inner_box = CreateBoxPolygon(5);
            Assert.IsTrue(box.Contains(inner_box));

            //OK, add an inner ring and make sure contains works
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
            Assert.IsTrue(box.Contains(box.ExteriorRing.First()));

            GridPolygon outside = CreateUPolygon(1);
            Assert.IsFalse(box.Contains(outside));

            GridPolygon inside = outside.Translate(new GridVector2(0, -7.5));
            Assert.IsTrue(box.Contains(inside));
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
            Assert.AreEqual(box.ExteriorRing[box.ExteriorRing.Length - 2], newVertex);

            box.RemoveVertex(newVertex);
            Assert.AreEqual(box.ExteriorRing.Length, numOriginalVerticies);

            box = CreateBoxPolygon(10);
            newVertex = new GridVector2(-5, -10);
            box.AddVertex(newVertex);
            Assert.AreEqual(box.ExteriorRing.Length, numOriginalVerticies + 1);
            Assert.AreEqual(box.ExteriorRing[1], newVertex);

            box.RemoveVertex(newVertex - new GridVector2(1, 1));
            Assert.AreEqual(box.ExteriorRing.Length, numOriginalVerticies);
            Assert.IsTrue(box.ExteriorRing.All(p => p != newVertex));
        }

        [TestMethod]
        public void AddPointsAtIntersectionsTest()
        {
            GridPolygon box = CreateBoxPolygon(10);
            GridPolygon U = CreateUPolygon(10);

            //Move the box so the top line is along Y=0 
            box = box.Translate(new GridVector2(0, -10));

            //This should add four verticies
            int OriginalVertCount = U.ExteriorRing.Length;
            U.AddPointsAtIntersections(box);

            Assert.IsTrue(OriginalVertCount + 4 == U.ExteriorRing.Length);
            Assert.IsTrue(U.ExteriorRing.Contains(new GridVector2(-10, 0)));
            Assert.IsTrue(U.ExteriorRing.Contains(new GridVector2(10, 0)));
            Assert.IsTrue(U.ExteriorRing.Contains(new GridVector2(-5, 0)));
            Assert.IsTrue(U.ExteriorRing.Contains(new GridVector2(5, 0)));
        }

        [TestMethod]
        public void AddPointsAtIntersectionsTest2()
        {
            GridPolygon box = CreateBoxPolygon(10);
            GridPolygon OuterBox = CreateBoxPolygon(15);
            GridPolygon U = CreateUPolygon(10);

            //Add the U polygon as an interior polygon
            OuterBox.AddInteriorRing(U);

            //Move the box so the top line is along Y=0 
            box = box.Translate(new GridVector2(0, -10));

            //This should add four verticies
            int OriginalExteriorVertCount = OuterBox.ExteriorRing.Length;
            int OriginalInnerVertCount = U.ExteriorRing.Length;
            OuterBox.AddPointsAtIntersections(box);

            GridPolygon NewU = OuterBox.InteriorPolygons.First();

            //Check that the interior ring was correctly appended
            Assert.IsTrue(OriginalInnerVertCount + 4 == NewU.ExteriorRing.Length);
            Assert.IsTrue(NewU.ExteriorRing.Contains(new GridVector2(-10, 0)));
            Assert.IsTrue(NewU.ExteriorRing.Contains(new GridVector2(10, 0)));
            Assert.IsTrue(NewU.ExteriorRing.Contains(new GridVector2(-5, 0)));
            Assert.IsTrue(NewU.ExteriorRing.Contains(new GridVector2(5, 0)));

            //Check that the exterior ring was correctly appended
            Assert.IsTrue(OriginalExteriorVertCount + 2 == OuterBox.ExteriorRing.Length);
            Assert.IsTrue(OuterBox.ExteriorRing.Contains(new GridVector2(-10, -15)));
            Assert.IsTrue(OuterBox.ExteriorRing.Contains(new GridVector2(10, -15)));

            //OK, now test from the other direction 

            OriginalExteriorVertCount = box.ExteriorRing.Length;
            box.AddPointsAtIntersections(OuterBox);

            //We should add 5 new verticies since the box had an extra vertex at -1,0 originally.  See CreateBoxPolygon
            Assert.IsTrue(OriginalExteriorVertCount + 5 == box.ExteriorRing.Length);
            Assert.IsTrue(box.ExteriorRing.Contains(new GridVector2(-10, -15)));
            Assert.IsTrue(box.ExteriorRing.Contains(new GridVector2(10, -15)));
            Assert.IsTrue(box.ExteriorRing.Contains(new GridVector2(-10, -10)));
            Assert.IsTrue(box.ExteriorRing.Contains(new GridVector2(-5, 0)));
            Assert.IsTrue(box.ExteriorRing.Contains(new GridVector2(5, 0)));
            Assert.IsTrue(box.ExteriorRing.Contains(new GridVector2(10, -10)));
        }

        [TestMethod]
        public void EnumeratePolygonIndiciesTest()
        {
            GridPolygon box = CreateBoxPolygon(10);
            GridPolygon OuterBox = CreateBoxPolygon(15);
            GridPolygon U = CreateUPolygon(10);
            GridPolygon U2 = CreateBoxPolygon(1);

            //Move the box so it doesn't overlap
            box = box.Translate(new GridVector2(50, 0));
            
            //Check a single polygon with no interior verticies
            PolyVertexEnum enumerator = new PolyVertexEnum(new GridPolygon[] { box });

            PointIndex[] indicies = enumerator.ToArray();
            Assert.IsTrue(indicies.Length == box.ExteriorRing.Length-1);
            Assert.IsTrue(indicies.Select(p => p.Point).Distinct().Count() == box.ExteriorRing.Length - 1); //Make sure all indicies are unique and not repeating

            for(int i = 0; i < indicies.Length; i++)
            {
                Assert.AreEqual(i, indicies[i].iVertex);
            }

            //Check a polygon with interior polygon
            OuterBox.AddInteriorRing(U);

            enumerator = new PolyVertexEnum(new GridPolygon[] { OuterBox });
            indicies = enumerator.ToArray();
            int numUniqueVerticies = (OuterBox.ExteriorRing.Length - 1) + OuterBox.InteriorPolygons.Sum(ip => ip.ExteriorRing.Length - 1);
            Assert.IsTrue(indicies.Length == numUniqueVerticies);
            Assert.IsTrue(indicies.Select(p => p.Point).Distinct().Count() == numUniqueVerticies); //Make sure all indicies are unique and not repeating

            //Check a polygon with two interior polygon
            OuterBox.AddInteriorRing(U2);

            enumerator = new PolyVertexEnum(new GridPolygon[] { OuterBox });
            indicies = enumerator.ToArray();
            numUniqueVerticies = (OuterBox.ExteriorRing.Length - 1) + OuterBox.InteriorPolygons.Sum(ip => ip.ExteriorRing.Length - 1);
            Assert.IsTrue(indicies.Length == numUniqueVerticies);
            Assert.IsTrue(indicies.Select(p => p.Point).Distinct().Count() == numUniqueVerticies); //Make sure all indicies are unique and not repeating

            //Check a polygon with two interior polygons and two polygons in the array

            enumerator = new PolyVertexEnum(new GridPolygon[] { OuterBox, box });
            indicies = enumerator.ToArray();
            numUniqueVerticies = (box.ExteriorRing.Length -1) + (OuterBox.ExteriorRing.Length - 1) + OuterBox.InteriorPolygons.Sum(ip => ip.ExteriorRing.Length - 1);
            Assert.IsTrue(indicies.Length == numUniqueVerticies);
            Assert.IsTrue(indicies.Select(p => p.Point).Distinct().Count() == numUniqueVerticies); //Make sure all indicies are unique and not repeating
        }
    }
}
