using System;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using System.Linq; 
using Geometry;
using System.Diagnostics;
using System.Collections.Generic;
using FsCheck;
using Geometry.JSON;

namespace GeometryTests
{
    [TestClass]
    public class GridPolygonTest
    {
        public delegate void OnPolygonIntersectionProgress(GridPolygon[] polygons, List<GridVector2> foundPoints, List<GridVector2> expectedPoints);

        /// <summary>
        /// Create a box, note I've added an extra vertex on the X:-1 vertical line
        /// 
        ///  * - - - *
        ///  |       |
        ///  *       |
        ///  |       |
        ///  * - - - *
        /// 
        /// </summary>
        /// <param name="scale"></param>
        /// <returns></returns>
        public static GridVector2[] BoxVerticies(double scale)
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


        public static GridVector2[] ConcaveUVerticies(double scale)
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

        public static GridVector2[] ConcaveCheckVerticies(double scale)
        {
            //          *
            //         /|
            //  *_    / /
            //   \ \ / /
            //    \ * /
            //     \ / 
            //      *

            GridVector2[] ExteriorPoints =
            {
                new GridVector2(-1, 0),
                new GridVector2(0, -0.5),
                new GridVector2(1, 1),
                new GridVector2(0, -1),
                new GridVector2(-1, 0)
            };

            GridVector2[] ExteriorPointsScaled = ExteriorPoints.Scale(scale, new GridVector2(0, 0)).ToArray();
            return ExteriorPointsScaled;
        }

        public static GridVector2[] DiamondVerticies(double scale)
        {
            //          *
            //        _/|  
            //      _/  |
            //    _/    |
            //   *    _-*
            //   | _--
            //   *-  
            //    

            GridVector2[] ExteriorPoints =
            {
                new GridVector2(-1, 0),
                new GridVector2(-1, -0.5),
                new GridVector2(1, 0),
                new GridVector2(1, 1),
                new GridVector2(-1, 0)
            };

            GridVector2[] ExteriorPointsScaled = ExteriorPoints.Scale(scale, new GridVector2(0, 0)).ToArray();
            return ExteriorPointsScaled;
        }

        public static GridVector2[] NotchedBoxVerticies(double scale)
        {
            /// 
            ///  *     *
            ///  |\   /|
            ///  | \ / |
            ///  *  *  |
            ///  |     |
            ///  *-----*
            /// 

            GridVector2[] ExteriorPoints =
            {
                new GridVector2(-1, -1),
                new GridVector2(-1, 0),
                new GridVector2(-1, 1),
                new GridVector2(0, 0),
                new GridVector2(1, 1),
                new GridVector2(1, -1),
                new GridVector2(-1, -1)
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

        [TestMethod]
        public void TestPolygonGenerator()
        {
            GeometryArbitraries.Register();

            Prop.ForAll<GridPolygon>((pl) =>
            {
                return pl.Area > 0;
            }).QuickCheckThrowOnFailure();
        }

        [TestMethod]
        public  void TestPolygonIntersectionGenerator()
        {
            TestPolygonIntersectionGenerator(null);
        }

        public static void TestPolygonIntersectionGenerator(OnPolygonIntersectionProgress OnProgress =null)
        {
            GeometryArbitraries.Register();

            var configuration = Configuration.QuickThrowOnFailure;
            configuration.MaxNbOfTest = 100;
            configuration.QuietOnSuccess = false;
            configuration.StartSize = 3;
            configuration.Replay = Global.StdGenSeed;

            Global.ResetRollingSeed();

            Prop.ForAll<GridPolygon, GridPolygon>((p1, p2) =>
            {
                p1 = p1.Clone() as GridPolygon; //Clone our input shapes so we don't edit them.
                p2 = p2.Clone() as GridPolygon; //Clone our input shapes so we don't edit them.

                var p1Copy = p1.Clone() as GridPolygon;
                var p2Copy = p2.Clone() as GridPolygon;

                GridPolygon[] polygons = { p1, p2 };


                if (OnProgress != null)
                {
                    OnProgress(polygons, new List<GridVector2>(), new List<GridVector2>());
                }

                var ExpectedIntersectionSegments = p1.ExteriorSegments.Intersections(p2.ExteriorSegments, false);

                var ExpectedIntersections = ExpectedIntersectionSegments.Select((i) =>
                {
                    i.A.Intersects(i.B, out GridVector2 Intersection);
                    return Intersection;
                }).Distinct().ToList();


                if (OnProgress != null)
                {
                    OnProgress(polygons, new List<GridVector2>(), ExpectedIntersections);
                }

                List<GridVector2> Intersections;
                try { 
                    Intersections = p1.AddPointsAtIntersections(p2);
                }
                catch(ArgumentException e)
                {
                    return false.Label(e.ToString());
                }

                var ExactMissingIntersections = ExpectedIntersections.Where(e => Intersections.Contains(e) == false).ToArray();
                var ExactMissingExpected = Intersections.Where(e => ExpectedIntersections.Contains(e) == false).ToArray();

                var ApproxMissingIntersections = ExactMissingIntersections.Where(i => ExpectedIntersections.Any(e => e == i) == false).ToArray();
                var ApproxMissingExpected = ExactMissingExpected.Where(i => Intersections.Any(e => e == i) == false).ToArray();

                bool IntersectionsInExpected = ApproxMissingIntersections.Length == 0;
                bool ExpectedInIntersections = ApproxMissingExpected.Length == 0;

                bool Success = IntersectionsInExpected && ExpectedInIntersections;

                if(Success == false && OnProgress != null)
                {
                    OnProgress(polygons, Intersections, ExpectedIntersections);
                }

                return IntersectionsInExpected.Label("Polygon intersections all expected")
                        .And(ExpectedInIntersections.Label("Expected intersections all found"))
                        .Label(string.Format("p1 = {0}",p1.ToJSON()))
                        .Label(string.Format("p2 = {0}", p2.ToJSON()));
            }).QuickCheckThrowOnFailure();
        }

        /*
        [TestMethod]
        public void TestPolygonOverlap()
        {
            GeometryArbitraries.Register();

            Prop.ForAll<GridPolygon[]>((polyArray) =>
            {
                List<GridVector2> listMissingIntersections = new List<GridVector2>();

                foreach (var combo in polyArray.CombinationPairs())
                {
                    GridPolygon A = combo.A;
                    GridPolygon B = combo.B;

                    var added_intersections = A.AddPointsAtIntersections(B);
#if DEBUG
                    foreach (GridVector2 p in added_intersections)
                    {
                        if(A.IsVertex(p) == false)
                        {
                            listMissingIntersections.Add(p);
                        }

                        if(B.IsVertex(p) == false)
                        {
                            listMissingIntersections.Add(p);
                        } 

                        //Debug.Assert(A.IsVertex(p));
                        //Debug.Assert(B.IsVertex(p));
                    }
#endif 
                }

                return listMissingIntersections.Count == 0;
            }).QuickCheckThrowOnFailure();
        }
        */

            
        [TestMethod]
        public void TestPolygonOverlap()
        {
            GeometryArbitraries.Register();

            Prop.ForAll<GridPolygon, GridPolygon>((A,B) =>
            {
                List<GridVector2> listMissingIntersections = new List<GridVector2>();

                var added_intersections = A.AddPointsAtIntersections(B);

                bool PolysIntersect = A.Intersects(B);

                //Throw out tests where the polygons do not intersect
                if (!PolysIntersect)
                    return (PolysIntersect == false)
                            .Trivial(true)
                            .Classify(true, "Polygons do not intersect");

                bool polysContainAddedIntersections = PolygonContainsIntersections(A, added_intersections) && PolygonContainsIntersections(B, added_intersections);
                var IntersectionsIncludingEndpoints = A.ExteriorSegments.Intersections(B.ExteriorSegments, false);

                //Ensure all of our intersection points are endpoints, there is an edge case of perfectly overlapped exterior rings that must be handled.
                var IntersectionsExcludingEndpoints = GetPolygonIntersectionsExcludingEndpoings(A, B);

                bool polysOnlyIntersectAtEndpoints = IntersectionsExcludingEndpoints.Count == 0 && IntersectionsIncludingEndpoints.Count > 0;
                bool pass = false == PolysIntersect || (polysContainAddedIntersections && polysOnlyIntersectAtEndpoints);
                return (PolysIntersect.Label("Polygons intersect"))
                       .And((IntersectionsIncludingEndpoints.Count > 0).Label("Intersection points are all endpoints"))
                       .And((IntersectionsExcludingEndpoints.Count == 0).Label("Intersections points are not all at endpoints"));
                           
            }).QuickCheckThrowOnFailure();
        }
        
        

        public static bool PolygonContainsIntersections(GridPolygon poly, List<GridVector2> points)
        {
            if (points == null)
                return true;
            if (points.Count == 0)
                return true;

            return points.All(p => poly.IsVertex(p));
        }

        /// <summary>
        /// Returns all of the places two polygons intersect, excluding the endpoints
        /// If we have added verticies at intersection points this function should return an empty list
        /// </summary>
        /// <param name="A"></param>
        /// <param name="B"></param>
        /// <returns></returns>
        public static List<ArrayIntersection<GridLineSegment>> GetPolygonIntersectionsExcludingEndpoings(GridPolygon A, GridPolygon B)
        {
            return A.ExteriorSegments.Intersections(B.ExteriorSegments, true).Where(result =>
            {
                IPoint2D pt = result.Intersection as IPoint2D;
                if (pt != null)
                    return true;

                ILineSegment2D line = result.Intersection as ILineSegment2D;
                if (line != null)
                {
                    bool EndpointAMatched = result.A.A == line.A || result.A.B == line.A || result.B.A == line.A || result.B.B == line.A;
                    bool EndpointBMatched = result.A.A == line.B || result.A.B == line.B || result.B.A == line.B || result.B.B == line.B;
                    return !(EndpointAMatched && EndpointBMatched); //Exclude from the results if both endpoints match polygon verticies
                }

                return true;
            }).ToList();
        }

        /// <summary>
        /// Ensure our Clockwise function works and that polygons are created Counter-Clockwise
        /// </summary>
        [TestMethod]
        public void GridPolygonVertexEnumeratorTest()
        {
            // 15      O3------------------------------O2
            //          |                               |
            // 10       |   I5---I4        I3----I2     |
            //          |    |    |         |     |     |
            //  5       |    |    |         |     |     |
            //          |    |    |         |     |     |
            //  0      O4    |    |         |    B2     |
            //          |    |    |         |     |     |
            // -5       |    |   I5--------I4     |     |
            //          |    |                    |     |   
            // -10      |    I0------------------I1     |
            //          |                               |
            // -15     O0------------------------------O1
            //              
            // -20          
            //
            //        -15   -10  -5    0    5    10    15
            //

            ////////////////////////////////////////
            //Only the outer poly
            GridPolygon box = CreateBoxPolygon(10);

            CheckVertexEnumerator(box);

            /////////////////////////////////
            //Outer poly with one inner poly
            GridPolygon OuterBox = CreateBoxPolygon(15);
            GridPolygon U = CreateUPolygon(10);

            //Add the U polygon as an interior polygon
            OuterBox.AddInteriorRing(U);

            CheckVertexEnumerator(OuterBox);

            /////////////////////////////////
            //Outer poly with two inner poly
            GridPolygon mini_box = CreateUPolygon(1);

            OuterBox.AddInteriorRing(mini_box);
            CheckVertexEnumerator(OuterBox);
        }

        private void CheckVertexEnumerator(GridPolygon polygon)
        {
            PointIndex[] forward = new PolygonVertexEnum(polygon).ToArray();
            PointIndex[] backward = new PolygonVertexEnum(polygon, reverse: true).Reverse().ToArray();

            Assert.AreEqual(forward.Length, backward.Length);

            for (int i = 0; i < forward.Length; i++)
            {
                Assert.AreEqual(forward[i], backward[i]);
            }
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

            //Check adding and removing interior polygons
            GridPolygon inner_box = CreateBoxPolygon(1);
            box.AddInteriorRing(inner_box);
            Assert.AreEqual(box.Area, 396);

            box.RemoveInteriorRing(0);
            Assert.AreEqual(box.Area, box.BoundingBox.Area);

            GridPolygon translated_box = box.Translate(new GridVector2(10, 10));
            Assert.AreEqual(Math.Round(translated_box.Area), translated_box.BoundingBox.Area);
            Assert.AreEqual(Math.Round(translated_box.Area), 400);
            Assert.AreEqual(Math.Round(translated_box.Area), box.Area);

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
        public void PolygonConvexContainsTest()
        {
            GridPolygon box = CreateBoxPolygon(10);
            Assert.IsFalse(box.Contains(new GridVector2(-15, 5)));
            Assert.IsTrue(box.Contains(new GridVector2(-5, 5)));
            Assert.IsTrue(box.Contains(new GridVector2(0, 0)));
            Assert.IsTrue(box.Contains(new GridVector2(-10, 0))); //Point exactly on the line
            Assert.IsTrue(box.Contains(new GridVector2(10, 0))); //Point exactly on the line
            Assert.IsTrue(box.Contains(new GridVector2(0, 10))); //Point exactly on the line
            Assert.IsTrue(box.Contains(new GridVector2(0, -10))); //Point exactly on the line

            GridPolygon inner_box = CreateBoxPolygon(5);
            Assert.IsTrue(box.Contains(inner_box));

            //OK, add an inner ring and make sure contains works
            box.AddInteriorRing(inner_box.ExteriorRing);

            Assert.IsFalse(box.Contains(new GridVector2(-15, 5)));
            Assert.IsFalse(box.Contains(new GridVector2(0, 0)));

            //Test points exactly on the inner ring
            Assert.IsTrue(box.Contains(new GridVector2(-5, 0)));
            Assert.IsTrue(box.Contains(new GridVector2(5, 0)));
            Assert.IsTrue(box.Contains(new GridVector2(0, -5)));
            Assert.IsTrue(box.Contains(new GridVector2(0, 5))); 
        }

        [TestMethod]
        public void PolygonConcaveContainsTest()
        {
            GridPolygon box = CreateUPolygon(10);
            Assert.IsFalse(box.Contains(new GridVector2(0, 10)));
            Assert.IsFalse(box.Contains(new GridVector2(-15, 5)));
            Assert.IsTrue(box.Contains(new GridVector2(-6.6, -6.6)));
            Assert.IsFalse(box.Contains(new GridVector2(0, 0)));
            Assert.IsFalse(box.Contains(new GridVector2(20, 0)));
            Assert.IsTrue(box.Contains(box.ExteriorRing.First()));
            Assert.IsTrue(box.Contains(new GridVector2(-7.5, 10)));

            GridPolygon outside = CreateUPolygon(1);
            Assert.IsFalse(box.Contains(outside));

            GridPolygon inside = outside.Translate(new GridVector2(0, -7.5));
            Assert.IsTrue(box.Contains(inside));
        }

        [TestMethod]
        public void PolygonContainsReproTest()
        {
            //Test for an edge case I hit once 
            GridPolygon diamond = new GridPolygon(DiamondVerticies(10));

            Assert.IsFalse(diamond.Contains(new GridVector2(-11, 0)));
            Assert.IsTrue(diamond.Contains(new GridVector2(-9, 0)));
            Assert.IsTrue(diamond.Contains(new GridVector2(9, 0)));
            Assert.IsFalse(diamond.Contains(new GridVector2(11, 0)));
        }

        [TestMethod]
        public void PolygonContainsReproTest2()
        {
            //Test for an edge case I hit once 
            GridPolygon shape = new GridPolygon(NotchedBoxVerticies(10));

            Assert.IsFalse(shape.Contains(new GridVector2(0, 10)));
            Assert.IsTrue(shape.Contains(new GridVector2(-10, 10)));
            Assert.IsTrue(shape.Contains(new GridVector2(10, 10)));
        }

        [TestMethod]
        public void PolygonTestLineIntersection()
        {
            GridPolygon OuterBox = CreateBoxPolygon(15);
            GridPolygon U = CreateUPolygon(10);
            OuterBox.AddInteriorRing(U);

            //Line entirely outside outer polygon
            GridLineSegment line = new GridLineSegment(new GridVector2(-16, -16), new GridVector2(16, -16));
            Assert.IsFalse(OuterBox.Intersects(line));
            
            //Line entirely inside polygon
            line = new GridLineSegment(new GridVector2(-14, -14), new GridVector2(14, 14));
            Assert.IsTrue(OuterBox.Intersects(line));

            //Line falls exactly over outside polygon segment
            line = new GridLineSegment(new GridVector2(-14, -15), new GridVector2(14, -15));
            Assert.IsTrue(OuterBox.Intersects(line));
            Assert.IsTrue(line.Intersects(OuterBox, false));
            Assert.IsFalse(line.Intersects(OuterBox, true));

            //Line falls exactly over inner polygon segment
            line = new GridLineSegment(new GridVector2(-10, -10), new GridVector2(10, -10));
            Assert.IsTrue(OuterBox.Intersects(line));
            Assert.IsTrue(line.Intersects(OuterBox, false));
            Assert.IsFalse(line.Intersects(OuterBox, true));

            //Line inside inner polygon
            line = new GridLineSegment(new GridVector2(-7.5, -7.5), new GridVector2(7.5, -7.5));
            Assert.IsFalse(OuterBox.Intersects(line));
            Assert.IsFalse(line.Intersects(OuterBox));

            //Line is outside the polygon, but touches a vertex
            line = new GridLineSegment(new GridVector2(-20, -15), new GridVector2(-15, -15));
            Assert.IsTrue(OuterBox.Intersects(line));
            Assert.IsTrue(line.Intersects(OuterBox));
            Assert.IsFalse(line.Intersects(OuterBox, true));

            //Line inside inner polygon but touches a vertex
            line = new GridLineSegment(new GridVector2(-10, -10), new GridVector2(-7.5, -7.5));
            Assert.IsTrue(OuterBox.Intersects(line));
            Assert.IsTrue(line.Intersects(OuterBox));
            Assert.IsFalse(line.Intersects(OuterBox, true));
        }

        [TestMethod]
        public void PolygonTestLineCrossesPolygon()
        {
            GridPolygon OuterBox = CreateBoxPolygon(15);
            GridPolygon U = CreateUPolygon(10);
            OuterBox.AddInteriorRing(U);

            //Line entirely outside outer polygon
            GridLineSegment line = new GridLineSegment(new GridVector2(-16, -16), new GridVector2(16, -16));
            Assert.IsFalse(line.Crosses(OuterBox));

            //Line entirely inside polygon
            line = new GridLineSegment(new GridVector2(-14, -14), new GridVector2(14, 14));
            Assert.IsTrue(line.Crosses(OuterBox));

            //Line falls exactly over outside polygon segment
            line = new GridLineSegment(new GridVector2(-14, -15), new GridVector2(14, -15));
            Assert.IsFalse(line.Crosses(OuterBox));

            //Line falls exactly over inner polygon segment
            line = new GridLineSegment(new GridVector2(-10, -10), new GridVector2(10, -10));
            Assert.IsFalse(line.Crosses(OuterBox));

            //Line falls exactly over part of the inner polygon segment, then enters the polygon
            line = new GridLineSegment(new GridVector2(-12.5, -10), new GridVector2(10, -10));
            Assert.IsTrue(line.Crosses(OuterBox));

            //Line inside inner polygon
            line = new GridLineSegment(new GridVector2(-7.5, -7.5), new GridVector2(7.5, -7.5));
            Assert.IsFalse(line.Crosses(OuterBox));

            //Line is outside the polygon, but touches a vertex
            line = new GridLineSegment(new GridVector2(-20, -15), new GridVector2(-15, -15));
            Assert.IsFalse(line.Crosses(OuterBox));  

            //Line inside inner polygon but touches a vertex
            line = new GridLineSegment(new GridVector2(-10, -10), new GridVector2(-7.5, -7.5));
            Assert.IsFalse(line.Crosses(OuterBox));

            //Line touches two segments of the exterior ring
            line = new GridLineSegment(new GridVector2(-15, -14), new GridVector2(15, -14));
            Assert.IsTrue(line.Crosses(OuterBox));
        }


        [TestMethod]
        public void PolygonAddRemoveVertexTest()
        {
            GridPolygon original_box = CreateBoxPolygon(10);
            GridPolygon box = CreateBoxPolygon(10);
            int numOriginalVerticies = box.ExteriorRing.Length;
            GridVector2 newVertex = new GridVector2(-10, -5);
            box.AddVertex(newVertex);
            Assert.AreEqual(box.ExteriorRing.Length, numOriginalVerticies + 1);
            Assert.AreEqual(box.ExteriorRing[0], newVertex);

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

            //Finally, remove a point that is not a vertex at all
            box.RemoveVertex(new GridVector2(100, 100));
        }

        [TestMethod]
        public void PolygonAddRemoveInternalVertexTest()
        {
            GridPolygon box = CreateBoxPolygon(10);
            GridPolygon interior_poly_A = CreateBoxPolygon(1);
            GridPolygon interior_poly_B = CreateBoxPolygon(7);

            interior_poly_A = interior_poly_A.Translate(new GridVector2(8.5, 8.5));
            Assert.AreEqual(interior_poly_A.Centroid, new GridVector2(8.5, 8.5));

            box.AddInteriorRing(interior_poly_A);
            box.AddInteriorRing(interior_poly_B);

            GridPolygon original_box = (GridPolygon)box.Clone();

            GridVector2[] new_external_verts = new GridVector2[]
            {
                new GridVector2(-10, -5), //Exactly on an existing segment
                new GridVector2(10,10),  //This is already a vertex, so we should silently do nothing
                new GridVector2(0,11), //Slightly outside our external bounds
                new GridVector2(0,-9.2), //slightly inside our external bounds
                new GridVector2(9.2,-1), //Slightly inside our external bounds
                new GridVector2(-10,1) //Exactly on an existing segment
            };

            GridVector2[] new_internal_A_vertex = new GridVector2[]
            {
                new GridVector2(9.5, 9.5), // An existing vertex 
                new GridVector2(7.5, 8.5), // A point on the midline of a segment
                new GridVector2(8.5, 7), //slightly below and outside the polygon segment
                new GridVector2(8.5,9.0) //Slighly below and inside the poly segment
            };

            GridVector2[] new_internal_B_vertex = new GridVector2[]
            {
                new GridVector2(7, 7), // An existing vertex
                new GridVector2(0, 6), //slightly above and inside the poly segment
                new GridVector2(0, -8), //Slighly below and outside the poly segment
                new GridVector2(7, 0)  // A point on an existing segment
            };

            foreach(GridVector2 p in new_external_verts)
            {
                box.AddVertex(p);
                Assert.IsTrue(box.IsVertex(p));
                Assert.IsTrue(box.ExteriorRing.Contains(p));
            }

            foreach (GridVector2 p in new_internal_A_vertex)
            {
                box.AddVertex(p);
                Assert.IsTrue(box.IsVertex(p));
                Assert.IsTrue(box.InteriorPolygons[0].ExteriorRing.Contains(p));
            }

            foreach (GridVector2 p in new_internal_B_vertex)
            {
                box.AddVertex(p);
                Assert.IsTrue(box.IsVertex(p));
                Assert.IsTrue(box.InteriorPolygons[1].ExteriorRing.Contains(p));
            }

            foreach(GridVector2 p in new_external_verts)
            {
                if (original_box.IsVertex(p)) //Do not remove verts that were in the original polygon to prevent errors later in the test
                    continue;

                box.RemoveVertex(p);
                Assert.IsFalse(box.IsVertex(p));
                Assert.IsFalse(box.ExteriorRing.Contains(p));
            }

            foreach (GridVector2 p in new_internal_A_vertex)
            {
                if (original_box.IsVertex(p)) //Do not remove verts that were in the original polygon to prevent errors later in the test
                    continue;

                box.RemoveVertex(p);
                Assert.IsFalse(box.IsVertex(p));
                Assert.IsFalse(box.InteriorPolygons[0].ExteriorRing.Contains(p));
            }

            foreach (GridVector2 p in new_internal_B_vertex)
            {
                if (original_box.IsVertex(p)) //Do not remove verts that were in the original polygon to prevent errors later in the test
                    continue;

                box.RemoveVertex(p);
                Assert.IsFalse(box.IsVertex(p));
                Assert.IsFalse(box.InteriorPolygons[1].ExteriorRing.Contains(p));
            }

            for(int i=0; i<box.ExteriorRing.Length;i++)
            {
                Assert.AreEqual(box.ExteriorRing[i], original_box.ExteriorRing[i]);
            }

            foreach(GridVector2 p in new_external_verts)
            {
                box.AddVertex(p);
                Assert.IsTrue(box.IsVertex(p));
                Assert.IsTrue(box.ExteriorRing.Contains(p));
            }

            foreach (GridVector2 p in new_internal_A_vertex)
            {
                box.AddVertex(p);
                Assert.IsTrue(box.IsVertex(p));
                Assert.IsTrue(box.InteriorPolygons[0].ExteriorRing.Contains(p));
            }

            foreach (GridVector2 p in new_internal_B_vertex)
            {
                box.AddVertex(p);
                Assert.IsTrue(box.IsVertex(p));
                Assert.IsTrue(box.InteriorPolygons[1].ExteriorRing.Contains(p));
            } 
        }

        [TestMethod]
        public void PolygonRemoveVertexToInvalidStateTest()
        {
            GridPolygon box = CreateBoxPolygon(10);
            GridPolygon interior_poly_A = CreateBoxPolygon(1);
            GridPolygon interior_poly_B = CreateBoxPolygon(7);

            interior_poly_A = interior_poly_A.Translate(new GridVector2(8.5, 8.5));
            Assert.AreEqual(interior_poly_A.Centroid, new GridVector2(8.5, 8.5));

            box.AddInteriorRing(interior_poly_A);
            box.AddInteriorRing(interior_poly_B);

            //OK, if we remove a corner of the outer box then the new segment will intersect the internal verticies.  We should see an error. 
            try
            {
                box.RemoveVertex(new GridVector2(10, -10));
                Assert.Fail("Removing a vertex that results in an invalid polygon should throw an exception.");
            }
            catch(ArgumentException)
            {
                return;
            }
        }

        [TestMethod]
        public void PolygonAddPointsAtIntersectionsTest()
        {
            GridPolygon box = CreateBoxPolygon(10);
            GridPolygon U = CreateUPolygon(10);

            //Move the box so the top line is along Y=0 
            box = box.Translate(new GridVector2(0, -10));

            //This should add four verticies
            int OriginalVertCount = U.ExteriorRing.Length;
            //Generate the SegmentRTree
            Assert.IsTrue(box.Intersects(new GridVector2(0, 0)));
            Assert.IsFalse(U.Intersects(new GridVector2(0, 0)));
            U.AddPointsAtIntersections(box);

            Assert.IsTrue(OriginalVertCount + 4 == U.ExteriorRing.Length);
            Assert.IsTrue(U.ExteriorRing.Contains(new GridVector2(-10, 0)));
            Assert.IsTrue(U.ExteriorRing.Contains(new GridVector2(10, 0)));
            Assert.IsTrue(U.ExteriorRing.Contains(new GridVector2(-5, 0)));
            Assert.IsTrue(U.ExteriorRing.Contains(new GridVector2(5, 0)));
        }


        [TestMethod]
        public void PolygonAddPointsAtIntersectionsTest2()
        {
            // 15      O3==============================O2
            //          |                               |
            // 10       |   I5---I4        I3----I2     |
            //          |    |    |         |     |     |
            //  5       |    |    |         |     |     |
            //          |    |    |         |     |     |
            //  0      O4   B3----+---------+----B2     |
            //          |   ||    |         |    ||     |
            // -5       |   ||   I5========I4    ||     |
            //          |   ||                   ||     |   
            // -10      |   B4/I0================I1   |
            //          |   |                    |      |
            // -15     O0===B0===================+=====O1
            //              |                    |
            // -20          B0-------------------B1
            //
            //        -15   -10  -5    0    5    10    15
            //
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
            GridPolygon[] polyArray = new GridPolygon[] { box };
            PolySetVertexEnum enumerator = new PolySetVertexEnum(polyArray);

            PointIndex[] indicies = enumerator.ToArray();
            Assert.IsTrue(indicies.Length == box.ExteriorRing.Length-1);
            Assert.IsTrue(indicies.Last().IsLastIndexInRing());
            Assert.IsTrue(indicies.Select(p => p.Point(polyArray)).Distinct().Count() == box.ExteriorRing.Length - 1); //Make sure all indicies are unique and not repeating

            for(int i = 0; i < indicies.Length; i++)
            {
                Assert.AreEqual(i, indicies[i].iVertex);
            }

            //Check a polygon with interior polygon
            OuterBox.AddInteriorRing(U);

            polyArray = new GridPolygon[] { OuterBox };
            enumerator = new PolySetVertexEnum(polyArray);
            indicies = enumerator.ToArray();
            int numUniqueVerticies = (OuterBox.ExteriorRing.Length - 1) + OuterBox.InteriorPolygons.Sum(ip => ip.ExteriorRing.Length - 1);
            Assert.IsTrue(indicies.Length == numUniqueVerticies);
            Assert.IsTrue(indicies.Select(p => p.Point(polyArray)).Distinct().Count() == numUniqueVerticies); //Make sure all indicies are unique and not repeating

            //Check a polygon with two interior polygon
            OuterBox.AddInteriorRing(U2);

            polyArray = new GridPolygon[] { OuterBox };
            enumerator = new PolySetVertexEnum(polyArray);
            indicies = enumerator.ToArray();
            numUniqueVerticies = (OuterBox.ExteriorRing.Length - 1) + OuterBox.InteriorPolygons.Sum(ip => ip.ExteriorRing.Length - 1);
            Assert.IsTrue(indicies.Length == numUniqueVerticies);
            Assert.IsTrue(indicies.Select(p => p.Point(polyArray)).Distinct().Count() == numUniqueVerticies); //Make sure all indicies are unique and not repeating

            //Check a polygon with two interior polygons and two polygons in the array

            polyArray = new GridPolygon[] { OuterBox, box };
            enumerator = new PolySetVertexEnum(polyArray);
            indicies = enumerator.ToArray();
            numUniqueVerticies = (box.ExteriorRing.Length -1) + (OuterBox.ExteriorRing.Length - 1) + OuterBox.InteriorPolygons.Sum(ip => ip.ExteriorRing.Length - 1);
            Assert.IsTrue(indicies.Length == numUniqueVerticies);
            Assert.IsTrue(indicies.Select(p => p.Point(polyArray)).Distinct().Count() == numUniqueVerticies); //Make sure all indicies are unique and not repeating
        }

        [TestMethod]
        public void SortPointIndexTest1()
        {
            //Test sorting when we need to prevent breaks at the wraparound at the 0 index..

            //Create an array where the first and last index are adjacent, but there is a gap in the center
            PointIndex[] points = new PointIndex[] {new PointIndex(0,0,6),
                                                    new PointIndex(0,1,6),
                                                    new PointIndex(0,2,6),
                                                    new PointIndex(0,4,6),
                                                    new PointIndex(0,5,6)};
            PointIndex[] sorted = PointIndex.SortByRing(points);

            Assert.IsTrue(sorted.First().iVertex == 4);
            Assert.IsTrue(sorted[1].iVertex == 5);
            Assert.IsTrue(sorted.Last().iVertex == 2);
        }

        [TestMethod]
        public void SortPointIndexTest2()
        {
            //Test sorting when we need to prevent breaks at the wraparound at the 0 index..

            //Create an array where the first and last index are adjacent, but there is a gap in the center
            PointIndex[] points = new PointIndex[] {new PointIndex(0,0,8),
                                                    new PointIndex(0,1,8),
                                                    new PointIndex(0,2,8),
                                                    new PointIndex(0,4,8),
                                                    new PointIndex(0,5,8),
                                                    new PointIndex(0,7,8)};
            PointIndex[] sorted = PointIndex.SortByRing(points);

            Assert.IsTrue(sorted.First().iVertex == 4);
            Assert.IsTrue(sorted[1].iVertex == 5);
            Assert.IsTrue(sorted[2].iVertex == 7);
            Assert.IsTrue(sorted.Last().iVertex == 2);
        }

        [TestMethod]
        public void SortPointIndexTest3()
        {
            //Test sorting when we need to prevent breaks at the wraparound at the 0 index..

            //Create an array where the first and last index are adjacent, but there is a gap in the center
            PointIndex[] points = new PointIndex[] {new PointIndex(0,0,8),
                                                    new PointIndex(0,1,8),
                                                    new PointIndex(0,2,8),
                                                    new PointIndex(0,4,8),
                                                    new PointIndex(0,5,8),
                                                    new PointIndex(0,7,8),

                                                    new PointIndex(0, 1, 0,8),
                                                    new PointIndex(0, 1, 1,8),
                                                    new PointIndex(0,1,2,8),
                                                    new PointIndex(0,1,4,8),
                                                    new PointIndex(0,1,5,8),
                                                    new PointIndex(0,1,7,8),};
            PointIndex[] sorted = PointIndex.SortByRing(points);

            Assert.IsTrue(sorted.Take(6).All(p => p.IsInner == false));
            Assert.IsTrue(sorted.Skip(6).All(p => p.IsInner));
            Assert.IsTrue(sorted.First().iVertex == 4);
            Assert.IsTrue(sorted[1].iVertex == 5);
            Assert.IsTrue(sorted[2].iVertex == 7);
            Assert.IsTrue(sorted[5].iVertex == 2);

            Assert.IsTrue(sorted[6].iVertex == 4);
            Assert.IsTrue(sorted[7].iVertex == 5);
            Assert.IsTrue(sorted[8].iVertex == 7);
            Assert.IsTrue(sorted[11].iVertex == 2);

        }
        /*
        [TestMethod]
        public void Theorem4Test()
        {
            GridLineSegment line;
            GridPolygon U = CreateUPolygon(10);

            //Line passes along the entire length of exterior ring
            line = new GridLineSegment(new GridVector2(-11, -10), new GridVector2(11, -10));
            Assert.IsTrue(Theorem4(U, line));

            //Line passes through part of the lenght of exterior ring
            line = new GridLineSegment(new GridVector2(-9, -10), new GridVector2(11, -10));
            Assert.IsTrue(Theorem4(U, line));

            //Line crosses the exterior ring
            line = new GridLineSegment(new GridVector2(-9, -11), new GridVector2(-9, -9));
            Assert.IsFalse(Theorem4(U, line));
        }*/

        /// <summary>
        ///
        ///     Test cutting the box polygon along the equals line:
        ///     
        ///     * - - - - - - - *
        ///     |               |
        /// A ======================== B
        ///     |               |
        ///     *               |
        ///     |               |
        ///     |               |
        ///     |               |
        ///     * - - - * - - - *
        ///  
        /// </summary>
        [TestMethod]
        public void TestInternalPolygonCut_NoInteriorCutPoint()
        {
            GridPolygon box = CreateBoxPolygon(10);

            GridVector2 A = new GridVector2(-15, 1);
            GridVector2 B = new GridVector2(15, 1); 

            GridVector2 expected_start = new GridVector2(-10, 1);
            GridVector2 expected_end = new GridVector2(10, 1);

            GridVector2[] expected_ring_counterclockwise = new GridVector2[] {expected_start,
                                                             new GridVector2(-10,0),
                                                             new GridVector2(-10,-10),
                                                             new GridVector2(10,-10),
                                                             expected_end,
                                                             expected_start};

            GridVector2[] expected_ring_clockwise = new GridVector2[] {expected_start,
                                                             expected_end,
                                                             new GridVector2(10,10),
                                                             new GridVector2(-10,10),
                                                             expected_start};

            GridPolygon clockwise_output = GridPolygon.WalkPolygonCut(box, RotationDirection.CLOCKWISE, new GridVector2[] { A, B });
            ValidatePolygonCut(clockwise_output, new GridPolygon(expected_ring_clockwise), expected_start, expected_end);

            GridPolygon counterclockwise_output = GridPolygon.WalkPolygonCut(box, RotationDirection.COUNTERCLOCKWISE, new GridVector2[] { A, B });
            ValidatePolygonCut(counterclockwise_output, new GridPolygon(expected_ring_counterclockwise), expected_start, expected_end);
        }

        /// <summary>
        ///
        ///     Test cutting the box polygon along the equals line:
        ///     
        ///     * - - - - - - - *
        ///     |               |
        /// A ======================== B
        ///     |               |
        ///     *      *-*      |
        ///     |      | |      |
        ///     |      *-*      |
        ///     |               |
        ///     * - - - * - - - *
        ///  
        /// </summary>
        [TestMethod]
        public void TestInternalPolygonCut_NoInteriorCutPoint_InnerPoly()
        {
            GridPolygon box = CreateBoxPolygon(10);

            GridPolygon inner = CreateBoxPolygon(1).Translate(new GridVector2(0, -2));

            box.AddInteriorRing(inner);

            GridVector2 A = new GridVector2(-15, 1);
            GridVector2 B = new GridVector2(15, 1);

            GridVector2 expected_start = new GridVector2(-10, 1);
            GridVector2 expected_end = new GridVector2(10, 1);

            GridVector2[] expected_ring_counterclockwise = new GridVector2[] {expected_start,
                                                             new GridVector2(-10,0),
                                                             new GridVector2(-10,-10),
                                                             new GridVector2(10,-10),
                                                             expected_end,
                                                             expected_start};

            GridVector2[] expected_ring_clockwise = new GridVector2[] {expected_start,
                                                             expected_end,
                                                             new GridVector2(10,10),
                                                             new GridVector2(-10,10),
                                                             expected_start};

            GridPolygon clockwise_output = GridPolygon.WalkPolygonCut(box, RotationDirection.CLOCKWISE, new GridVector2[] { A, B });
            ValidatePolygonCut(clockwise_output, new GridPolygon(expected_ring_clockwise), expected_start, expected_end);

            GridPolygon counterclockwise_output = GridPolygon.WalkPolygonCut(box, RotationDirection.COUNTERCLOCKWISE, new GridVector2[] { A, B });
            ValidatePolygonCut(counterclockwise_output, new GridPolygon(expected_ring_counterclockwise), expected_start, expected_end);

            Assert.IsTrue(counterclockwise_output.InteriorPolygons.Count == 1);
        }

        /// <summary>
        ///
        ///     Test cutting the box polygon along the equals line:
        ///     
        ///     * - - - - - - - *
        ///     |               |
        /// A ===========B============ C
        ///     |               |
        ///     *               |
        ///     |               |
        ///     |               |
        ///     |               |
        ///     * - - - * - - - *
        ///  
        /// </summary>
        [TestMethod]
        public void TestInternalPolygonCut_OneInteriorCutPoint()
        {
            GridPolygon box = CreateBoxPolygon(10);

            GridVector2 A = new GridVector2(-15, 1);
            GridVector2 B = new GridVector2(0, 1);
            GridVector2 C = new GridVector2(15, 1); 

            GridVector2 expected_start = new GridVector2(-10, 1);
            GridVector2 expected_end = new GridVector2(10, 1);

            GridVector2[] expected_ring_counterclockwise = new GridVector2[] {expected_start,
                                                             new GridVector2(-10,0),
                                                             new GridVector2(-10,-10),
                                                             new GridVector2(10,-10),
                                                             expected_end,
                                                             B,
                                                             expected_start};

            GridVector2[] expected_ring_clockwise = new GridVector2[] {expected_start,
                                                             B,
                                                             expected_end,
                                                             new GridVector2(10,10),
                                                             new GridVector2(-10,10),
                                                             expected_start};

            GridPolygon clockwise_output = GridPolygon.WalkPolygonCut(box, RotationDirection.CLOCKWISE, new GridVector2[] { A, B, C});
            ValidatePolygonCut(clockwise_output, new GridPolygon(expected_ring_clockwise), expected_start, expected_end);

            GridPolygon counterclockwise_output = GridPolygon.WalkPolygonCut(box, RotationDirection.COUNTERCLOCKWISE, new GridVector2[] { A, B, C });
            ValidatePolygonCut(counterclockwise_output, new GridPolygon(expected_ring_counterclockwise), expected_start, expected_end);
        }

        /// <summary>
        ///
        ///     Test cutting the box polygon along the equals line:
        ///     
        ///     * - - - - - - - *
        ///     |               |
        /// A ===========B      |
        ///     |       ||      |
        ///     *       ||      |
        ///     |       ||      |
        ///     |        C =========== D
        ///     |               |
        ///     * - - - * - - - *
        ///  
        /// </summary>
        [TestMethod]
        public void TestInternalPolygonCut_TwoInteriorCutPoints()
        {
            GridPolygon box = CreateBoxPolygon(10);

            GridVector2 A = new GridVector2(-15, 1);
            GridVector2 B = new GridVector2(0, 1);
            GridVector2 C = new GridVector2(0, -5);
            GridVector2 D = new GridVector2(15,-5); 

            GridVector2 expected_start = new GridVector2(-10, 1);
            GridVector2 expected_end = new GridVector2(10, -5);

            GridVector2[] expected_ring_counterclockwise = new GridVector2[] {expected_start,
                                                             new GridVector2(-10,0),
                                                             new GridVector2(-10,-10),
                                                             new GridVector2(10,-10),
                                                             expected_end,
                                                             C,
                                                             B,
                                                             expected_start};

            GridVector2[] expected_ring_clockwise = new GridVector2[] {expected_start,
                                                             B,
                                                             C,
                                                             expected_end,
                                                             new GridVector2(10,10),
                                                             new GridVector2(-10,10),
                                                             expected_start};

            GridPolygon counterclockwise_output = GridPolygon.WalkPolygonCut(box, RotationDirection.COUNTERCLOCKWISE, new GridVector2[] { A, B, C, D });
            ValidatePolygonCut(counterclockwise_output, new GridPolygon(expected_ring_counterclockwise), expected_start, expected_end);

            GridPolygon clockwise_output = GridPolygon.WalkPolygonCut(box, RotationDirection.CLOCKWISE, new GridVector2[] { A, B, C, D });
            ValidatePolygonCut(clockwise_output, new GridPolygon(expected_ring_clockwise), expected_start, expected_end);

        }

        /// <summary>
        ///
        ///     Test cutting the box polygon along the equals line:
        ///     
        ///  A
        ///   \
        ///     * - - - - - - - *
        ///     | \             |
        ///     |   \            |
        ///     |     \          |
        ///     *       \        |
        ///     |         \      |
        ///     |           \    |
        ///     |             \  |
        ///     * - - - * - - - *
        ///                       \
        ///                         B
        /// </summary>
        [TestMethod]
        public void TestInternalPolygonCut_NoInteriorCutPointsThroughPolygonVerts()
        {
            GridPolygon box = CreateBoxPolygon(10);

            GridVector2 A = new GridVector2(-15, 15); 
            GridVector2 B = new GridVector2(15, -15);

            GridVector2 expected_start = new GridVector2(-10, 10);
            GridVector2 expected_end = new GridVector2(10, -10);

            GridVector2[] expected_ring_counterclockwise = new GridVector2[] {expected_start,
                                                             new GridVector2(-10,0),
                                                             new GridVector2(-10,-10),
                                                             expected_end,
                                                             expected_start};

            GridVector2[] expected_ring_clockwise = new GridVector2[] {expected_start,
                                                             new GridVector2(10,10),
                                                             expected_end,
                                                             expected_start};

            GridPolygon clockwise_output = GridPolygon.WalkPolygonCut(box, RotationDirection.CLOCKWISE, new GridVector2[] { A, B });
            ValidatePolygonCut(clockwise_output, new GridPolygon(expected_ring_clockwise), expected_start, expected_end);

            GridPolygon counterclockwise_output = GridPolygon.WalkPolygonCut(box, RotationDirection.COUNTERCLOCKWISE, new GridVector2[] { A, B});
            ValidatePolygonCut(counterclockwise_output, new GridPolygon(expected_ring_counterclockwise), expected_start, expected_end);

            
        }

        /// <summary>
        ///
        ///     Test cutting the box polygon along the equals line:
        ///     
        ///  A
        ///   \
        ///     * - - - - - - - *
        ///     | \             |
        ///     |   \            |
        ///     |     \          |
        ///     *       B        |
        ///     |         \      |
        ///     |           \    |
        ///     |             \  |
        ///     * - - - * - - - *
        ///                       \
        ///                         B
        /// </summary>
        [TestMethod]
        public void TestInternalPolygonCut_OneInteriorCutPointsThroughPolygonVerts()
        {
            GridPolygon box = CreateBoxPolygon(10);

            GridVector2 A = new GridVector2(-15, 15);
            GridVector2 B = new GridVector2(0, 0);
            GridVector2 C = new GridVector2(15, -15);

            GridVector2 expected_start = new GridVector2(-10, 10);
            GridVector2 expected_end = new GridVector2(10, -10);

            GridVector2[] expected_ring_counterclockwise = new GridVector2[] {expected_start,
                                                             new GridVector2(-10,0),
                                                             new GridVector2(-10,-10),
                                                             expected_end,
                                                             B,
                                                             expected_start};

            GridVector2[] expected_ring_clockwise = new GridVector2[] {expected_start,
                                                             new GridVector2(10,10),
                                                             expected_end,
                                                             B,
                                                             expected_start};

            GridPolygon clockwise_output = GridPolygon.WalkPolygonCut(box, RotationDirection.CLOCKWISE, new GridVector2[] { A, B, C });
            ValidatePolygonCut(clockwise_output, new GridPolygon(expected_ring_clockwise), expected_start, expected_end);

            GridPolygon counterclockwise_output = GridPolygon.WalkPolygonCut(box, RotationDirection.COUNTERCLOCKWISE, new GridVector2[] { A, B, C });
            ValidatePolygonCut(counterclockwise_output, new GridPolygon(expected_ring_counterclockwise), expected_start, expected_end);
        }

        /// <summary>
        ///
        ///     Test cutting the box polygon along the equals line:
        ///     
        ///  A
        ///   \
        ///     * - - - - - - - *
        ///     | \             |
        ///     |   \            |
        ///     |     \          |
        ///     *       B        |
        ///     |         \      |
        ///     |           \    |
        ///     |             \  |
        ///     * - - - * - - - *
        ///                       \
        ///                         B
        /// </summary>
        [TestMethod]
        public void TestInternalPolygonCut_ExtraExteriorVerts_OneInteriorCutPointsThroughPolygonVerts()
        {
            GridPolygon box = CreateBoxPolygon(10);

            GridVector2 A = new GridVector2(-15, 15);
            GridVector2 B = new GridVector2(0, 0);
            GridVector2 C = new GridVector2(15, -15);

            GridVector2[] path = new GridVector2[] {new GridVector2(-45,15),
                                                    new GridVector2(-30,15),
                                                    A,
                                                    B,
                                                    C,
                                                    new GridVector2(30,-15),
                                                    new GridVector2(45,-15)
                                                    };

            GridVector2 expected_start = new GridVector2(-10, 10);
            GridVector2 expected_end = new GridVector2(10, -10);

            GridVector2[] expected_ring_counterclockwise = new GridVector2[] {expected_start,
                                                             new GridVector2(-10,0),
                                                             new GridVector2(-10,-10),
                                                             expected_end,
                                                             B,
                                                             expected_start};

            GridVector2[] expected_ring_clockwise = new GridVector2[] {expected_start,
                                                             new GridVector2(10,10),
                                                             expected_end,
                                                             B,
                                                             expected_start};

            GridPolygon clockwise_output = GridPolygon.WalkPolygonCut(box, RotationDirection.CLOCKWISE, path);
            ValidatePolygonCut(clockwise_output, new GridPolygon(expected_ring_clockwise), expected_start, expected_end);

            GridPolygon counterclockwise_output = GridPolygon.WalkPolygonCut(box, RotationDirection.COUNTERCLOCKWISE, path);
            ValidatePolygonCut(counterclockwise_output, new GridPolygon(expected_ring_counterclockwise), expected_start, expected_end);
        }

        /// <summary>
        ///
        ///     Test cutting the box polygon along the equals line:
        /// C ======================== D    
        /// |                          |
        /// |   * - - - - - - - *      |
        /// |   |               |      |
        /// B =====A          F======= E
        ///     |               |
        ///     *               |
        ///     |               |
        ///     |               |
        ///     |               |
        ///     * - - - * - - - *
        ///  
        /// </summary>
        [TestMethod]
        public void TestExternalPolygonCut()
        {
            GridPolygon box = CreateBoxPolygon(10);

            GridVector2 A = new GridVector2(-9,  1);
            GridVector2 B = new GridVector2(-15, 1);
            GridVector2 C = new GridVector2(-15, 15);
            GridVector2 D = new GridVector2(15, 15);
            GridVector2 E = new GridVector2(15, 1);
            GridVector2 F = new GridVector2(9, 1);

            GridVector2 expected_start = new GridVector2(-10, 1);
            GridVector2 expected_end = new GridVector2(10, 1);

            GridVector2[] path = new GridVector2[] { A,B,C,D,E,F};

            GridVector2[] expected_ring_counterclockwise = new GridVector2[] {expected_start,
                                                             new GridVector2(-10,0),
                                                             new GridVector2(-10,-10),
                                                             new GridVector2(10,-10),
                                                             expected_end,
                                                             E,
                                                             D,
                                                             C,
                                                             B,
                                                             expected_start};

            GridVector2[] expected_ring_clockwise = new GridVector2[] {expected_start,
                                                             new GridVector2(-10,10),
                                                             new GridVector2(10,10),
                                                             expected_end,
                                                             E,
                                                             D,
                                                             C,
                                                             B,
                                                             expected_start};

            GridPolygon clockwise_output = GridPolygon.WalkPolygonCut(box, RotationDirection.CLOCKWISE, path);
            ValidatePolygonCut(clockwise_output, new GridPolygon(expected_ring_clockwise), expected_start, expected_end);

            GridPolygon counterclockwise_output = GridPolygon.WalkPolygonCut(box, RotationDirection.COUNTERCLOCKWISE, path);
            ValidatePolygonCut(counterclockwise_output, new GridPolygon(expected_ring_counterclockwise), expected_start, expected_end);
        }

        /// <summary>
        ///
        ///     Test cutting the box polygon along the equals line:
        //      *-------*       *-------*
        //      |       |       |       |
        //      |       |       |       |
        //      |   A===============B   |
        //      |       |       |       |
        //      |       |       |       |
        //      |       |       |       |
        //      |       |       |       |
        //      |       *-------*       |
        //      |                       |
        //      |                       |
        //      *-----------------------*
        /// </summary>
        [TestMethod]
        public void TestExternalPolygonCut_NoExternalVerts()
        {
            GridPolygon uBox = new GridPolygon(ConcaveUVerticies(10));

            GridVector2 A = new GridVector2(-7.5, 7.5);
            GridVector2 B = new GridVector2(7.5, 7.5);

            GridVector2 expected_start = new GridVector2(-5, 7.5);
            GridVector2 expected_end = new GridVector2(5, 7.5);

            GridVector2[] path = new GridVector2[] { A, B};

            GridVector2[] expected_ring_counterclockwise = new GridVector2[] {expected_start,
                                                             new GridVector2(-5,  10),
                                                             new GridVector2(-10, 10),
                                                             new GridVector2(-10,-10),
                                                             new GridVector2( 10,-10),
                                                             new GridVector2( 10, 10),
                                                             new GridVector2( 5,  10),
                                                             expected_end, 
                                                             expected_start};

            GridVector2[] expected_ring_clockwise = new GridVector2[] {expected_start,
                                                             new GridVector2(-5,-5),
                                                             new GridVector2(5,-5),
                                                             expected_end, 
                                                             expected_start};

            GridPolygon clockwise_output = GridPolygon.WalkPolygonCut(uBox, RotationDirection.CLOCKWISE, path);
            ValidatePolygonCut(clockwise_output, new GridPolygon(expected_ring_clockwise), expected_start, expected_end);

            GridPolygon counterclockwise_output = GridPolygon.WalkPolygonCut(uBox, RotationDirection.COUNTERCLOCKWISE, path);
            ValidatePolygonCut(counterclockwise_output, new GridPolygon(expected_ring_counterclockwise), expected_start, expected_end);
        }

        /// <summary>
        ///
        ///     Test cutting the box polygon along the equals line:
        //      *-------*       *-------*
        //      |       |       |       |
        //      |       |       |       |
        //      | A=B==============C=D  |
        //      |       |       |       |
        //      |       |       |       |
        //      |       |       |       |
        //      |       |       |       |
        //      |       *-------*       |
        //      |                       |
        //      |                       |
        //      *-----------------------*
        /// </summary>
        [TestMethod]
        public void TestExternalPolygonCut_NoExternalVerts_ExtraVerts()
        {
            GridPolygon uBox = new GridPolygon(ConcaveUVerticies(10));

            GridVector2 A = new GridVector2(-8  , 7.5);
            GridVector2 B = new GridVector2(-7.5, 7.5);
            GridVector2 C = new GridVector2( 7.5, 7.5);
            GridVector2 D = new GridVector2( 9  , 7.5);

            GridVector2 expected_start = new GridVector2(-5, 7.5);
            GridVector2 expected_end = new GridVector2(5, 7.5);

            GridVector2[] path = new GridVector2[] { A, B, C, D };

            GridVector2[] expected_ring_counterclockwise = new GridVector2[] {expected_start,
                                                             new GridVector2(-5,  10),
                                                             new GridVector2(-10, 10),
                                                             new GridVector2(-10,-10),
                                                             new GridVector2( 10,-10),
                                                             new GridVector2( 10, 10),
                                                             new GridVector2( 5,  10),
                                                             expected_end,
                                                             expected_start};

            GridVector2[] expected_ring_clockwise = new GridVector2[] {expected_start,
                                                             new GridVector2(-5,-5),
                                                             new GridVector2(5,-5),
                                                             expected_end,
                                                             expected_start};

            GridPolygon clockwise_output = GridPolygon.WalkPolygonCut(uBox, RotationDirection.CLOCKWISE, path);
            ValidatePolygonCut(clockwise_output, new GridPolygon(expected_ring_clockwise), expected_start, expected_end);

            GridPolygon counterclockwise_output = GridPolygon.WalkPolygonCut(uBox, RotationDirection.COUNTERCLOCKWISE, path);
            ValidatePolygonCut(counterclockwise_output, new GridPolygon(expected_ring_counterclockwise), expected_start, expected_end);
        }

        private void ValidatePolygonCut(GridPolygon cut, GridPolygon expected_cut, GridVector2 expected_start, GridVector2 expected_end)
        {
            Assert.IsTrue(cut.Contains(expected_start));
            Assert.IsTrue(cut.Contains(expected_end));

            Assert.IsTrue(expected_cut.ExteriorRing.SequenceEqual(cut.ExteriorRing));
             
            for(int iRing = 0; iRing < expected_cut.InteriorRings.Count; iRing++)
            {
                Assert.IsTrue(expected_cut.InteriorRings[iRing].SequenceEqual(cut.InteriorRings[iRing]));
            }
        }


        /// <summary>
        /// Theorem 4 requries that a line segment does not occupy space both internal and external to the polygon.
        /// Lines that fall over a polygon segment are acceptable as long as the rest of the line qualifies.
        /// </summary>
        /// <param name="poly"></param>
        /// <param name="line"></param>
        /// <returns></returns>
        public bool Theorem4(GridPolygon poly, GridLineSegment line)
        {
            List<GridVector2> intersections;

            return !LineIntersectionExtensions.Intersects(line, poly, true, out intersections);
        }
        
    }
}
