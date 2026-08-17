using FsCheck;
using Geometry;
using Geometry.JSON;
using GeometryTests.FSCheck;
using Microsoft.FSharp.Collections;
using Microsoft.FSharp.Core;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using System;
using System.Collections;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Threading.Tasks;

namespace GeometryTests
{
    class MyFSCheckRunner : IRunner
    {
        public void OnArguments(int value1, FSharpList<object> value2, FSharpFunc<int, FSharpFunc<FSharpList<object>, string>> value3) => Runner.consoleRunner.OnArguments(value1, value2, value3);

        public void OnFinished(string value1, FsCheck.TestResult value2)
        {
            Runner.consoleRunner.OnFinished(value1, value2);

            if (value2 is FsCheck.TestResult.Exhausted exhausted)
            {
                Console.Write($"{value1} test options exhausted");
            }
            else if (value2 is FsCheck.TestResult.False falseResult)
            {
                Console.Write($"{value1} test failed");
                throw new AssertFailedException(value1);
            }
            else if (value2 is FsCheck.TestResult.True trueResult)
            {
                Console.WriteLine($"{value1} passed");
            }
        }

        public void OnShrink(FSharpList<object> args, FSharpFunc<FSharpList<object>, string> argsToString)
        {
            string output = argsToString?.Invoke(args);
            Debug.WriteLine(output);
            Console.WriteLine(output);
        }

        public void OnStartFixture(Type value) => Runner.consoleRunner.OnStartFixture(value);
    }

    [TestClass]
    public class PolygonTest
    {
        public delegate void OnPolygonIntersectionProgress(Polygon[] polygons, List<Vector2> foundPoints, List<Vector2> expectedPoints);

        static Polygon CreateTrianglePolygon(double scale)
        {
            Vector2[] ExteriorPoints =
            [
                new(-1, -1),
                new(-1, 1),
                new(1, -1),
                new(-1,-1)
            ];

            return new Polygon(ExteriorPoints).Scale(scale);
        }

        private static Configuration GetPolygonGeneratorConfiguration(FsCheck.Random.StdGen seed = null)
        {
            var configuration = Configuration.QuickThrowOnFailure; //The Configuration.QuickThrowOnFailure implementation returned a new instance every time when I first wrote this...
            configuration.MaxNbOfTest = 100;
            configuration.QuietOnSuccess = false;
            configuration.StartSize = 250;
            configuration.EndSize = 750;
            configuration.EveryShrink = (args) =>
            {
                if (args.Length > 0 && args is object[] parameters)
                {
                    string output = $"Shrunk\n";
                    if (args[0] is Tuple<Vector2[], int> paramTuple)
                    {
                        output = $"Shrunk to {paramTuple.Item1.Length} points & {paramTuple.Item2} lines.";
                    }

                    return output;
                }

                return $"Shrunk {args}";
            };
            configuration.Replay = seed ?? Global.StdGenSeed;
            configuration.Name = nameof(TestPolygonGeneratorUnderpinnings);
            return configuration;
        }

        [TestMethod]
        public void TestPolygonGeneratorUnderpinnings()
        {
            //GeometryArbitraries.Register(); 
            Arb.Register<Vector2Generators>();

            MyFSCheckRunner myRunner = new();

            try
            {
                Prop.ForAll<Vector2[], int>(AssessPolygonGeneration).Check(GetPolygonGeneratorConfiguration());
            }
            catch (Exception e)
            {
                Assert.Fail(e.ToString());
            }
        }

        public static void TestPolygonGeneratorUnderpinnings(OnPolygonIntersectionProgress OnProgress = null, FsCheck.Random.StdGen seed = null)
        {
            Arb.Register<Vector2Generators>();

            Global.ResetRollingSeed();

            Prop.ForAll<Polygon, Polygon>((p1, p2) => AssessPolygonIntersectionAndCorrespondancePoints(p1, p2, OnProgress)).Check(GetPolygonGeneratorConfiguration(seed));
        }

        public static Property AssessPolygonGeneration(Vector2[] points, int nLines)
        {
            if (points.Length < 3)
                return true.Trivial(points.Length < 3);

            if (nLines < 3)
                nLines = 3;

            try
            {
                var poly =
                    LineSegmentGenerators.GenConcavePolygonWithInteriorHolesFromPoints(points, nLines);
                bool NonZeroArea = poly.Area > 0;
                bool IsValid = poly.IsValid();

                return NonZeroArea.Label("Non zero area")
                    .And(IsValid).Label("Generated Polygon is valid")
                    .Label($"{poly.ToJSON()}");
            }
            catch (Exception e)
            {
                return false.Label($"{e}")
                    .Label($"points: {points.ToJSON()}")
                    .Label($"nLines: {nLines}");
            }
        }

        [TestMethod]
        public void TestPolygonGenerator()
        {
            GeometryArbitraries.Register();

            try
            {
                Prop.ForAll<Polygon>((poly) =>
                {
                    try
                    {
                        bool NonZeroArea = poly.Area > 0;
                        bool IsValid = poly.IsValid();

                        return NonZeroArea.Label("Non zero area")
                            .And(IsValid).Label("Generated Polygon is valid")
                            .Label($"{poly.ToJSON()}");
                    }
                    catch (Exception e)
                    {
                        return false.Label($"{e}")
                            .Label($"points: {poly.ToJSON()}");
                    }

                }).QuickCheckThrowOnFailure();
            }
            catch (Exception e)
            {
                Assert.Fail(e.ToString());
            }
        }

        [TestMethod]
        public void TestPolygonIntersectionGenerator()
        {
            try
            {
                TestPolygonIntersectionGenerator(null);
            }
            catch (Exception e)
            {
                Assert.Fail(e.ToString());
            }

            return;
        }

        public static void TestPolygonIntersectionGenerator(OnPolygonIntersectionProgress OnProgress = null)
        {
            GeometryArbitraries.Register();

            var configuration = Configuration.QuickThrowOnFailure;
            configuration.MaxNbOfTest = 100;
            configuration.QuietOnSuccess = false;
            configuration.StartSize = 3;
            configuration.Replay = Global.StdGenSeed;

            Global.ResetRollingSeed();

            Prop.ForAll<Polygon, Polygon>((p1, p2) => AssessPolygonIntersectionAndCorrespondancePoints(p1, p2, OnProgress)).VerboseCheckThrowOnFailure();
        }

        public static Property AssessPolygonIntersectionAndCorrespondancePoints(Polygon p1, Polygon p2, OnPolygonIntersectionProgress OnProgress = null)
        {
            p1 = p1.Clone() as Polygon; //Clone our input shapes so we don't edit them.
            p2 = p2.Clone() as Polygon; //Clone our input shapes so we don't edit them.

            var AllOriginalP1Verts = p1.AllVertices.ToArray();
            var AllOriginalP2Verts = p2.AllVertices.ToArray();

            Polygon p1Copy = p1.Clone() as Polygon;
            Polygon p2Copy = p2.Clone() as Polygon;

            Polygon[] polygons = [p1, p2];

            OnProgress?.Invoke(polygons, [], []);

            //var ExpectedExteriorIntersectionSegments = p1.ExteriorSegments.Intersections(p2.ExteriorSegments, false);

            var ExpectedIntersectionSegments = p1.AllSegments.Intersections(p2.AllSegments, false);

            List<Vector2> ExpectedIntersections = [.. ExpectedIntersectionSegments.Select((i) =>
            {
                i.A.Intersects(i.B, out Vector2 Intersection);
                return Intersection;
            }).Distinct()];

            OnProgress?.Invoke(polygons, [], ExpectedIntersections);

            List<Vector2> Intersections = [];
            try
            {
                Intersections = p1Copy.AddPointsAtIntersections(p2Copy);
            }
            catch (ArgumentException e)
            {
                OnProgress(polygons, Intersections, ExpectedIntersections);
                Task.Delay(50).Wait();
                return false
                    .Label(e.ToString())
                    .Label($"{polygons.ToJArray()}");
            }
            catch (Exception e)
            {
                OnProgress(polygons, Intersections, ExpectedIntersections);
                Task.Delay(50).Wait();
                return false
                    .Label(e.ToString())
                    .Label($"{polygons.ToJArray()}");
            }

            var ExactMissingIntersections = ExpectedIntersections.Where(e => Intersections.Contains(e) == false).ToArray();
            var ExactMissingExpected = Intersections.Where(e => ExpectedIntersections.Contains(e) == false).ToArray();

            var ApproxMissingIntersections = ExactMissingIntersections.Where(i => ExpectedIntersections.Any(e => e == i) == false).ToArray();
            var ApproxMissingExpected = ExactMissingExpected.Where(i => Intersections.Any(e => e == i) == false).ToArray();

            List<Vector2> correspondingIntersections;

            var ExpectedCorrespondingPoints = ExpectedIntersections; //ExpectedIntersections.Where(i => AllOriginalP1Verts.Contains(i) == false).ToList();
            try
            {
                List<IShape2D> shapes =
                [
                    p1.Clone() as IShape2D,
                    p2.Clone() as IShape2D
                ];
                correspondingIntersections = shapes.AddCorrespondingVertices();
            }
            catch (ArgumentException e)
            {
                OnProgress(polygons, Intersections, ExpectedIntersections);
                Task.Delay(50).Wait();
                return false
                    .Label(e.ToString())
                    .Label($"{polygons.ToJArray()}");
            }
            catch (Exception e)
            {
                OnProgress(polygons, Intersections, ExpectedIntersections);
                Task.Delay(50).Wait();
                return false
                    .Label(e.ToString())
                    .Label($"{polygons.ToJArray()}");
            }

            bool IntersectionsInExpected = ApproxMissingIntersections.Length == 0;
            bool ExpectedInIntersections = ApproxMissingExpected.Length == 0;

            //bool CorrespondingCountMatch = correspondingIntersections.Count == ExpectedCorrespondingPoints.Count;
            bool CorrespondingPointsMatchExpected = correspondingIntersections.All(c => ExpectedCorrespondingPoints.Contains(c));

            bool Success = IntersectionsInExpected && ExpectedInIntersections /* && CorrespondingCountMatch */ && CorrespondingPointsMatchExpected;

            if (Success == false && OnProgress != null)
            {
                OnProgress(polygons, Intersections, ExpectedIntersections);
                Task.Delay(500).Wait();
            }

            return IntersectionsInExpected.Label("Polygon intersections all expected")
                    .And(ExpectedInIntersections.Label("Expected intersections all found"))
                    //.And(CorrespondingCountMatch.Label("Number of corresponding points are equal"))
                    .And(CorrespondingPointsMatchExpected.Label("Corresponding point positions match"))
                    .Label($"p1 = {p1.ToJSON()}")
                    .Label($"p2 = {p2.ToJSON()}")
                    .Label($"{polygons.ToJArray()}");
        }
        /*
        [TestMethod]
        public void TestPolygonOverlap()
        {
            GeometryArbitraries.Register();

            Prop.ForAll<Polygon[]>((polyArray) =>
            {
                List<Vector2> listMissingIntersections = new List<Vector2>();

                foreach (var combo in polyArray.CombinationPairs())
                {
                    Polygon A = combo.A;
                    Polygon B = combo.B;

                    var added_intersections = A.AddPointsAtIntersections(B);
#if DEBUG
                    foreach (Vector2 p in added_intersections)
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

            Prop.ForAll<Polygon, Polygon>((A, B) =>
            {
                List<Vector2> listMissingIntersections = [];

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

            }).VerboseCheckThrowOnFailure();
        }

        public static bool PolygonContainsIntersections(Polygon poly, List<Vector2> points)
        {
            if (points is null)
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
        public static List<ArrayIntersection<LineSegment>> GetPolygonIntersectionsExcludingEndpoings(Polygon A, Polygon B)
        {
            return [.. A.ExteriorSegments.Intersections(B.ExteriorSegments, true).Where(result =>
            {
                if (result.Intersection is IPoint2D pt)
                    return true;

                if (result.Intersection is ILineSegment2D line)
                {
                    bool EndpointAMatched = result.A.A == line.A || result.A.B == line.A || result.B.A == line.A || result.B.B == line.A;
                    bool EndpointBMatched = result.A.A == line.B || result.A.B == line.B || result.B.A == line.B || result.B.B == line.B;
                    return !(EndpointAMatched && EndpointBMatched); //Exclude from the results if both endpoints match polygon verticies
                }

                return true;
            })];
        }

        /// <summary>
        /// Ensure our Clockwise function works and that polygons are created Counter-Clockwise
        /// </summary>


        private static double AreaDiff(IShape2D A, IShape2D B) => Math.Abs(A.Area - B.Area);

        private static double AreaDiff(IShape2D A, double B) => Math.Abs(A.Area - B);

        private static bool AreaApproxEqual(IShape2D A, IShape2D B, double epsilon = Geometry.Global.Epsilon) => AreaDiff(A, B) <= epsilon;

        private static bool AreaApproxEqual(IShape2D A, double B, double epsilon = Geometry.Global.Epsilon) => AreaDiff(A, B) <= epsilon;

        /// <summary>
        /// Ensure our Clockwise function works and that polygons are created Counter-Clockwise
        /// </summary>
        [TestMethod]
        public void ClockwiseTest()
        {
            Vector2[] clockwisePoints = Primitives.BoxVerticies(1);
            Assert.IsTrue(clockwisePoints.AreClockwise());


            Vector2[] counterClockwisePoints = new Vector2[clockwisePoints.Length];
            Array.Copy(clockwisePoints, counterClockwisePoints, clockwisePoints.Length);
            counterClockwisePoints.Reverse();

            Assert.IsTrue(clockwisePoints[1] == counterClockwisePoints[counterClockwisePoints.Length - 2]);

            Assert.IsFalse(counterClockwisePoints.AreClockwise());

            Polygon clockwisePoly = new(clockwisePoints);
            Polygon counterClockwisePoly = new(clockwisePoints);

            Assert.IsFalse(clockwisePoly.ExteriorRing.AreClockwise());
            Assert.IsFalse(counterClockwisePoly.ExteriorRing.AreClockwise());
        }

        [TestMethod]
        public void AreaTest()
        {
            Polygon box = Primitives.BoxPolygon(10);
            Assert.AreEqual(box.Area, box.BoundingBox.Area);
            Assert.AreEqual(400, box.Area);

            //Check adding and removing interior polygons
            Polygon inner_box = Primitives.BoxPolygon(1);
            Assert.AreEqual(inner_box.Area, inner_box.BoundingBox.Area);
            box.AddInteriorRing(inner_box);
            Assert.AreEqual(396, box.Area);

            box.RemoveInteriorRing(0);
            Assert.AreEqual(box.Area, box.BoundingBox.Area);

            Polygon inner_box_2 = Primitives.BoxPolygon(2).Translate(new Vector2(6, 6));
            Assert.AreEqual(inner_box_2.Area, inner_box_2.BoundingBox.Area);
            box.AddInteriorRing(inner_box);
            box.AddInteriorRing(inner_box_2);
            Assert.AreEqual(380, box.Area);

            box.RemoveInteriorRing(0);
            Assert.AreEqual(384, box.Area);
            box.RemoveInteriorRing(0);
            Assert.AreEqual(400, box.Area);


            //Check that translation doesn't break area somehow
            Polygon translated_box = box.Translate(new Vector2(10, 10));
            Assert.AreEqual(Math.Round(translated_box.Area), translated_box.BoundingBox.Area);
            Assert.AreEqual(400, Math.Round(translated_box.Area));
            Assert.AreEqual(Math.Round(translated_box.Area), box.Area);
        }

        [TestMethod]
        public void AreaTest2()
        {
            Polygon tri = PolygonTest.CreateTrianglePolygon(10);
            Assert.IsTrue(AreaApproxEqual(tri, tri.BoundingBox.Area / 2));
            Assert.IsTrue(AreaApproxEqual(tri, 200));

            //Check translating the shape
            var translated_tri = tri.Translate(new Vector2(10, -10));
            Assert.IsTrue(AreaApproxEqual(translated_tri, translated_tri.BoundingBox.Area / 2));
            Assert.IsTrue(AreaApproxEqual(translated_tri, 200));
            Assert.IsTrue(AreaApproxEqual(translated_tri, tri));

            //Check adding and removing interior polygons
            Polygon inner = PolygonTest.CreateTrianglePolygon(1).Translate(new Vector2(-2, -2));
            Assert.IsTrue(AreaApproxEqual(inner, inner.BoundingBox.Area / 2));
            tri.AddInteriorRing(inner);
            Assert.IsTrue(AreaApproxEqual(tri, 198));

            //Check translating the shape with the interior poly
            translated_tri = tri.Translate(new Vector2(10, -10));
            Assert.IsTrue(AreaApproxEqual(translated_tri, 198));
            Assert.IsTrue(AreaApproxEqual(translated_tri, tri));

            //Check removing the interior ring
            tri.RemoveInteriorRing(0);
            translated_tri.RemoveInteriorRing(0);
            Assert.IsTrue(AreaApproxEqual(tri, 200));
            Assert.IsTrue(AreaApproxEqual(tri, translated_tri));
        }

        [TestMethod]
        public void CentroidTest()
        {
            Polygon box = Primitives.BoxPolygon(10);
            Assert.AreEqual(box.Centroid, box.BoundingBox.Center);
        }

        [TestMethod]
        public void PolygonConvexContainsTest()
        {
            Polygon box = Primitives.BoxPolygon(10);
            Assert.IsFalse(box.Contains(new Vector2(-15, 5)));
            Assert.IsTrue(box.Contains(new Vector2(-5, 5)));
            Assert.IsTrue(box.Contains(new Vector2(0, 0)));
            Assert.IsFalse(box.Contains(new Vector2(-10, 0)));
            Assert.IsTrue(box.Covers(new Vector2(-10, 0)));
            Assert.IsTrue(box.Covers(new Vector2(10, 0)));
            Assert.IsTrue(box.Covers(new Vector2(0, 10)));
            Assert.IsTrue(box.Covers(new Vector2(0, -10)));

            Polygon inner_box = Primitives.BoxPolygon(5);
            Assert.IsTrue(box.Contains(inner_box));
            Assert.IsTrue(box.Covers(inner_box));

            box.AddInteriorRing(inner_box.ExteriorRing);

            Assert.IsFalse(box.Contains(new Vector2(-15, 5)));
            Assert.IsFalse(box.Contains(new Vector2(0, 0)));

            Assert.IsFalse(box.Contains(new Vector2(-5, 0)));
            Assert.IsTrue(box.Covers(new Vector2(-5, 0)));
            Assert.IsTrue(box.Covers(new Vector2(5, 0)));
            Assert.IsTrue(box.Covers(new Vector2(0, -5)));
            Assert.IsTrue(box.Covers(new Vector2(0, 5)));
        }

        [TestMethod]
        public void PolygonConvexContainsExtTest()
        {
            Polygon box = Primitives.BoxPolygon(10);
            Assert.AreEqual(ShapeRelation.None, box.GetRelation(new Vector2(-15, 5)));
            Assert.AreEqual(ShapeRelation.Contained, box.GetRelation(new Vector2(-5, 5)));
            Assert.AreEqual(ShapeRelation.Contained, box.GetRelation(new Vector2(0, 0)));
            Assert.AreEqual(ShapeRelation.Touching, box.GetRelation(new Vector2(-10, 0))); //Point exactly on the line
            Assert.AreEqual(ShapeRelation.Touching, box.GetRelation(new Vector2(10, 0))); //Point exactly on the line
            Assert.AreEqual(ShapeRelation.Touching, box.GetRelation(new Vector2(0, 10))); //Point exactly on the line
            Assert.AreEqual(ShapeRelation.Touching, box.GetRelation(new Vector2(0, -10))); //Point exactly on the line

            Polygon inner_box = Primitives.BoxPolygon(5);
            Assert.AreEqual(ShapeRelation.Contained, box.GetRelation(inner_box));

            //OK, add an inner ring and make sure contains works
            box.AddInteriorRing(inner_box.ExteriorRing);

            Assert.AreEqual(ShapeRelation.None, box.GetRelation(new Vector2(-15, 5))); //Point inside inner box
            Assert.AreEqual(ShapeRelation.None, box.GetRelation(new Vector2(0, 0))); //Point inside inner box
            Assert.AreEqual(ShapeRelation.Contained, box.GetRelation(new Vector2(-7.5, 7.5)));
            Assert.AreEqual(ShapeRelation.Contained, box.GetRelation(new Vector2(-7.5, 5))); //x-axis perfectly overlapped with inner polygon

            //Test points exactly on the inner ring
            Assert.AreEqual(ShapeRelation.Touching, box.GetRelation(new Vector2(-5, 0)));
            Assert.AreEqual(ShapeRelation.Touching, box.GetRelation(new Vector2(5, 0)));
            Assert.AreEqual(ShapeRelation.Touching, box.GetRelation(new Vector2(0, -5)));
            Assert.AreEqual(ShapeRelation.Touching, box.GetRelation(new Vector2(0, 5)));

            //Test points exactly on corners of external and inner ring
            Assert.AreEqual(ShapeRelation.Touching, box.GetRelation(new Vector2(-5, -5)));
            Assert.AreEqual(ShapeRelation.Touching, box.GetRelation(new Vector2(5, 5)));
            Assert.AreEqual(ShapeRelation.Touching, box.GetRelation(new Vector2(5, -5)));
            Assert.AreEqual(ShapeRelation.Touching, box.GetRelation(new Vector2(-5, 5)));

            Assert.AreEqual(ShapeRelation.Touching, box.GetRelation(new Vector2(-10, -10)));
            Assert.AreEqual(ShapeRelation.Touching, box.GetRelation(new Vector2(10, 10)));
            Assert.AreEqual(ShapeRelation.Touching, box.GetRelation(new Vector2(10, -10)));
            Assert.AreEqual(ShapeRelation.Touching, box.GetRelation(new Vector2(-10, 10)));
        }

        [TestMethod]
        public void PolygonConcaveContainsTest()
        {
            Polygon box = Primitives.UPolygon(10);
            Assert.IsFalse(box.Contains(new Vector2(0, 10)));
            Assert.IsFalse(box.Contains(new Vector2(-15, 5)));
            Assert.IsTrue(box.Contains(new Vector2(-6.6, -6.6)));
            Assert.IsFalse(box.Contains(new Vector2(0, 0)));
            Assert.IsFalse(box.Contains(new Vector2(20, 0)));
            Assert.IsTrue(box.Covers(box.ExteriorRing.First()));
            Assert.IsFalse(box.Contains(box.ExteriorRing.First()));
            Assert.IsTrue(box.Covers(new Vector2(-7.5, 10)));
            Assert.IsFalse(box.Contains(new Vector2(-7.5, 10)));

            Polygon outside = Primitives.UPolygon(1);
            Assert.IsFalse(box.Contains(outside));

            Polygon inside = outside.Translate(new Vector2(0, -7.5));
            Assert.IsTrue(box.Contains(inside));
        }

        [TestMethod]
        public void PolygonContainsReproTest()
        {
            //Test for an edge case I hit once 
            Polygon diamond = new(Primitives.TrapezoidVerticies(10));

            Assert.IsFalse(diamond.Contains(new Vector2(-11, 0)));
            Assert.IsTrue(diamond.Contains(new Vector2(-9, 0)));
            Assert.IsTrue(diamond.Contains(new Vector2(9, 0)));
            Assert.IsFalse(diamond.Contains(new Vector2(11, 0)));
        }

        [TestMethod]
        public void PolygonContainsReproTest2()
        {
            //Test for an edge case I hit once 
            Polygon shape = new(Primitives.NotchedBoxVerticies(10));

            Assert.IsFalse(shape.Contains(new Vector2(0, 10)));
            Assert.IsFalse(shape.Contains(new Vector2(-10, 10)));
            Assert.IsTrue(shape.Covers(new Vector2(-10, 10)));
            Assert.IsTrue(shape.Covers(new Vector2(10, 10)));
        }

        [TestMethod]
        public void PolygonTestLineIntersection()
        {
            Polygon OuterBox = Primitives.BoxPolygon(15);
            Polygon U = Primitives.UPolygon(10);
            OuterBox.AddInteriorRing(U);

            //Line entirely outside outer polygon
            LineSegment line = new(new Vector2(-16, -16), new Vector2(16, -16));
            Assert.IsFalse(OuterBox.Intersects(line));

            //Line entirely inside polygon
            line = new LineSegment(new Vector2(-14, -14), new Vector2(14, 14));
            Assert.IsTrue(OuterBox.Intersects(line));

            //Line falls exactly over outside polygon segment
            line = new LineSegment(new Vector2(-14, -15), new Vector2(14, -15));
            Assert.IsTrue(OuterBox.Intersects(line));
            Assert.IsTrue(line.Intersects(OuterBox, false));
            Assert.IsFalse(line.Intersects(OuterBox, true));

            //Line falls exactly over inner polygon segment
            line = new LineSegment(new Vector2(-10, -10), new Vector2(10, -10));
            Assert.IsTrue(OuterBox.Intersects(line));
            Assert.IsTrue(line.Intersects(OuterBox, false));
            Assert.IsFalse(line.Intersects(OuterBox, true));

            //Line inside inner polygon
            line = new LineSegment(new Vector2(-7.5, -7.5), new Vector2(7.5, -7.5));
            Assert.IsFalse(OuterBox.Intersects(line));
            Assert.IsFalse(line.Intersects(OuterBox));

            //Line is outside the polygon, but touches a vertex
            line = new LineSegment(new Vector2(-20, -15), new Vector2(-15, -15));
            Assert.IsTrue(OuterBox.Intersects(line));
            Assert.IsTrue(line.Intersects(OuterBox));
            Assert.IsFalse(line.Intersects(OuterBox, true));

            //Line inside inner polygon but touches a vertex
            line = new LineSegment(new Vector2(-10, -10), new Vector2(-7.5, -7.5));
            Assert.IsTrue(OuterBox.Intersects(line));
            Assert.IsTrue(line.Intersects(OuterBox));
            Assert.IsFalse(line.Intersects(OuterBox, true));
        }

        [TestMethod]
        public void PolygonTestLineCrossesPolygon()
        {
            Polygon OuterBox = Primitives.BoxPolygon(15);
            Polygon U = Primitives.UPolygon(10);
            OuterBox.AddInteriorRing(U);

            //Line entirely outside outer polygon
            LineSegment line = new(new Vector2(-16, -16), new Vector2(16, -16));
            Assert.IsFalse(line.Crosses(OuterBox));

            //Line entirely inside polygon
            line = new LineSegment(new Vector2(-14, -14), new Vector2(14, 14));
            Assert.IsTrue(line.Crosses(OuterBox));

            //Line falls exactly over outside polygon segment
            line = new LineSegment(new Vector2(-14, -15), new Vector2(14, -15));
            Assert.IsFalse(line.Crosses(OuterBox));

            //Line falls exactly over inner polygon segment
            line = new LineSegment(new Vector2(-10, -10), new Vector2(10, -10));
            Assert.IsFalse(line.Crosses(OuterBox));

            //Line falls exactly over part of the inner polygon segment, then enters the polygon
            line = new LineSegment(new Vector2(-12.5, -10), new Vector2(10, -10));
            Assert.IsTrue(line.Crosses(OuterBox));

            //Line inside inner polygon
            line = new LineSegment(new Vector2(-7.5, -7.5), new Vector2(7.5, -7.5));
            Assert.IsFalse(line.Crosses(OuterBox));

            //Line is outside the polygon, but touches a vertex
            line = new LineSegment(new Vector2(-20, -15), new Vector2(-15, -15));
            Assert.IsFalse(line.Crosses(OuterBox));

            //Line inside inner polygon but touches a vertex
            line = new LineSegment(new Vector2(-10, -10), new Vector2(-7.5, -7.5));
            Assert.IsFalse(line.Crosses(OuterBox));

            //Line touches two segments of the exterior ring
            line = new LineSegment(new Vector2(-15, -14), new Vector2(15, -14));
            Assert.IsTrue(line.Crosses(OuterBox));
        }


        [TestMethod]
        public void PolygonAddRemoveVertexTest()
        {
            Polygon original_box = Primitives.BoxPolygon(10);
            Polygon box = Primitives.BoxPolygon(10);
            int numOriginalVerticies = box.ExteriorRing.Length;
            Vector2 newVertex = new(-10, -5);
            box.AddVertex(newVertex);
            Assert.AreEqual(box.ExteriorRing.Length, numOriginalVerticies + 1);
            Assert.AreEqual(box.ExteriorRing[0], newVertex);

            box.RemoveVertex(newVertex);
            Assert.AreEqual(box.ExteriorRing.Length, numOriginalVerticies);

            box = Primitives.BoxPolygon(10);
            newVertex = new Vector2(-5, -10);
            box.AddVertex(newVertex);
            Assert.AreEqual(box.ExteriorRing.Length, numOriginalVerticies + 1);
            Assert.AreEqual(box.ExteriorRing[1], newVertex);

            box.RemoveVertex(newVertex - new Vector2(1, 1));
            Assert.AreEqual(box.ExteriorRing.Length, numOriginalVerticies);
            Assert.IsTrue(box.ExteriorRing.All(p => p != newVertex));

            //Finally, remove a point that is not a vertex at all
            box.RemoveVertex(new Vector2(100, 100));
        }

        [TestMethod]
        public void PolygonAddRemoveInternalVertexTest()
        {
            Polygon box = Primitives.BoxPolygon(10);
            Polygon interior_poly_A = Primitives.BoxPolygon(1);
            Polygon interior_poly_B = Primitives.BoxPolygon(7);

            interior_poly_A = interior_poly_A.Translate(new Vector2(8.5, 8.5));
            Assert.AreEqual(interior_poly_A.Centroid, new Vector2(8.5, 8.5));

            box.AddInteriorRing(interior_poly_A);
            box.AddInteriorRing(interior_poly_B);

            Polygon original_box = (Polygon)box.Clone();

            Vector2[] new_external_verts =
            [
                new(-10, -5), //Exactly on an existing segment
                new(10,10),  //This is already a vertex, so we should silently do nothing
                new(0,11), //Slightly outside our external bounds
                new(0,-9.2), //slightly inside our external bounds
                new(9.2,-1), //Slightly inside our external bounds
                new(-10,1) //Exactly on an existing segment
            ];

            Vector2[] new_internal_A_vertex =
            [
                new(9.5, 9.5), // An existing vertex 
                new(7.5, 8.5), // A point on the midline of a segment
                new(8.5, 7), //slightly below and outside the polygon segment
                new(8.5,9.0) //Slighly below and inside the poly segment
            ];

            Vector2[] new_internal_B_vertex =
            [
                new(7, 7), // An existing vertex
                new(0, 6), //slightly above and inside the poly segment
                new(0, -8), //Slighly below and outside the poly segment
                new(7, 0)  // A point on an existing segment
            ];

            foreach (Vector2 p in new_external_verts)
            {
                box.AddVertex(p);
                Assert.IsTrue(box.IsVertex(p));
                Assert.IsTrue(box.ExteriorRing.Contains(p));
            }

            foreach (Vector2 p in new_internal_A_vertex)
            {
                box.AddVertex(p);
                Assert.IsTrue(box.IsVertex(p));
                Assert.IsTrue(box.InteriorPolygons[0].ExteriorRing.Contains(p));
            }

            foreach (Vector2 p in new_internal_B_vertex)
            {
                box.AddVertex(p);
                Assert.IsTrue(box.IsVertex(p));
                Assert.IsTrue(box.InteriorPolygons[1].ExteriorRing.Contains(p));
            }

            foreach (Vector2 p in new_external_verts)
            {
                if (original_box.IsVertex(p)) //Do not remove verts that were in the original polygon to prevent errors later in the test
                    continue;

                box.RemoveVertex(p);
                Assert.IsFalse(box.IsVertex(p));
                Assert.IsFalse(box.ExteriorRing.Contains(p));
            }

            foreach (Vector2 p in new_internal_A_vertex)
            {
                if (original_box.IsVertex(p)) //Do not remove verts that were in the original polygon to prevent errors later in the test
                    continue;

                box.RemoveVertex(p);
                Assert.IsFalse(box.IsVertex(p));
                Assert.IsFalse(box.InteriorPolygons[0].ExteriorRing.Contains(p));
            }

            foreach (Vector2 p in new_internal_B_vertex)
            {
                if (original_box.IsVertex(p)) //Do not remove verts that were in the original polygon to prevent errors later in the test
                    continue;

                box.RemoveVertex(p);
                Assert.IsFalse(box.IsVertex(p));
                Assert.IsFalse(box.InteriorPolygons[1].ExteriorRing.Contains(p));
            }

            for (int i = 0; i < box.ExteriorRing.Length; i++)
            {
                Assert.AreEqual(box.ExteriorRing[i], original_box.ExteriorRing[i]);
            }

            foreach (Vector2 p in new_external_verts)
            {
                box.AddVertex(p);
                Assert.IsTrue(box.IsVertex(p));
                Assert.IsTrue(box.ExteriorRing.Contains(p));
            }

            foreach (Vector2 p in new_internal_A_vertex)
            {
                box.AddVertex(p);
                Assert.IsTrue(box.IsVertex(p));
                Assert.IsTrue(box.InteriorPolygons[0].ExteriorRing.Contains(p));
            }

            foreach (Vector2 p in new_internal_B_vertex)
            {
                box.AddVertex(p);
                Assert.IsTrue(box.IsVertex(p));
                Assert.IsTrue(box.InteriorPolygons[1].ExteriorRing.Contains(p));
            }
        }

        [TestMethod]
        public void PolygonRemoveVertexToInvalidStateTest()
        {
            Polygon box = Primitives.BoxPolygon(10);
            Polygon interior_poly_A = Primitives.BoxPolygon(1);
            Polygon interior_poly_B = Primitives.BoxPolygon(7);

            interior_poly_A = interior_poly_A.Translate(new Vector2(8.5, 8.5));
            Assert.AreEqual(interior_poly_A.Centroid, new Vector2(8.5, 8.5));

            box.AddInteriorRing(interior_poly_A);
            box.AddInteriorRing(interior_poly_B);

            //OK, if we remove a corner of the outer box then the new segment will intersect the internal verticies.  We should see an error. 
            try
            {
                box.RemoveVertex(new Vector2(10, -10));
                Assert.Fail("Removing a vertex that results in an invalid polygon should throw an exception.");
            }
            catch (ArgumentException)
            {
                return;
            }
        }

        [TestMethod]
        public void PolygonAddPointsAtIntersectionsTest()
        {
            Polygon box = Primitives.BoxPolygon(10);
            Polygon U = Primitives.UPolygon(10);

            //Move the box so the top line is along Y=0 
            box = box.Translate(new Vector2(0, -10));

            //This should add four verticies
            int OriginalVertCount = U.ExteriorRing.Length;
            //Generate the SegmentRTree
            Assert.IsTrue(box.Intersects(new Vector2(0, 0)));
            Assert.IsFalse(U.Intersects(new Vector2(0, 0)));
            U.AddPointsAtIntersections(box);

            Assert.AreEqual(U.ExteriorRing.Length, OriginalVertCount + 4);
            Assert.IsTrue(U.ExteriorRing.Contains(new Vector2(-10, 0)));
            Assert.IsTrue(U.ExteriorRing.Contains(new Vector2(10, 0)));
            Assert.IsTrue(U.ExteriorRing.Contains(new Vector2(-5, 0)));
            Assert.IsTrue(U.ExteriorRing.Contains(new Vector2(5, 0)));
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
            Polygon box = Primitives.BoxPolygon(10);
            Polygon OuterBox = Primitives.BoxPolygon(15);
            Polygon U = Primitives.UPolygon(10);

            //Add the U polygon as an interior polygon
            OuterBox.AddInteriorRing(U);

            //Move the box so the top line is along Y=0 
            box = box.Translate(new Vector2(0, -10));

            //This should add four verticies
            int OriginalExteriorVertCount = OuterBox.ExteriorRing.Length;
            int OriginalInnerVertCount = U.ExteriorRing.Length;
            OuterBox.AddPointsAtIntersections(box);

            Polygon NewU = OuterBox.InteriorPolygons.First();

            //Check that the interior ring was correctly appended
            Assert.AreEqual(NewU.ExteriorRing.Length, OriginalInnerVertCount + 4);
            Assert.IsTrue(NewU.ExteriorRing.Contains(new Vector2(-10, 0)));
            Assert.IsTrue(NewU.ExteriorRing.Contains(new Vector2(10, 0)));
            Assert.IsTrue(NewU.ExteriorRing.Contains(new Vector2(-5, 0)));
            Assert.IsTrue(NewU.ExteriorRing.Contains(new Vector2(5, 0)));

            //Check that the exterior ring was correctly appended
            Assert.AreEqual(OuterBox.ExteriorRing.Length, OriginalExteriorVertCount + 2);
            Assert.IsTrue(OuterBox.ExteriorRing.Contains(new Vector2(-10, -15)));
            Assert.IsTrue(OuterBox.ExteriorRing.Contains(new Vector2(10, -15)));

            //OK, now test from the other direction 
            box.AddPointsAtIntersections(OuterBox);

            //We should add 5 new verticies since the box had an extra vertex at -1,0 originally.  See Primitives.BoxPolygon
            Assert.AreEqual(box.ExteriorRing.Length, OriginalExteriorVertCount + 5);
            Assert.IsTrue(box.ExteriorRing.Contains(new Vector2(-10, -15)));
            Assert.IsTrue(box.ExteriorRing.Contains(new Vector2(10, -15)));
            Assert.IsTrue(box.ExteriorRing.Contains(new Vector2(-10, -10)));
            Assert.IsTrue(box.ExteriorRing.Contains(new Vector2(-5, 0)));
            Assert.IsTrue(box.ExteriorRing.Contains(new Vector2(5, 0)));
            Assert.IsTrue(box.ExteriorRing.Contains(new Vector2(10, -10)));
        }

        [TestMethod]
        public void EnumeratePolygonIndiciesTest()
        {
            Polygon box = Primitives.BoxPolygon(10);
            Polygon OuterBox = Primitives.BoxPolygon(15);
            Polygon U = Primitives.UPolygon(10);
            Polygon U2 = Primitives.BoxPolygon(1);

            //Move the box so it doesn't overlap
            box = box.Translate(new Vector2(50, 0));

            //Check a single polygon with no interior verticies
            Polygon[] polyArray = [box];
            PolySetVertexEnum enumerator = new(polyArray);

            PolygonIndex[] indicies = enumerator.ToArray();
            Assert.AreEqual(box.ExteriorRing.Length - 1, indicies.Length);
            Assert.IsTrue(indicies.Last().IsLastIndexInRing());
            Assert.AreEqual(box.ExteriorRing.Length - 1, indicies.Select(p => p.Point(polyArray)).Distinct().Count()); //Make sure all indicies are unique and not repeating

            for (int i = 0; i < indicies.Length; i++)
            {
                Assert.AreEqual(i, indicies[i].VertexIndex);
            }

            //Check a polygon with interior polygon
            OuterBox.AddInteriorRing(U);

            polyArray = [OuterBox];
            enumerator = new PolySetVertexEnum(polyArray);
            indicies = enumerator.ToArray();
            int numUniqueVerticies = (OuterBox.ExteriorRing.Length - 1) + OuterBox.InteriorPolygons.Sum(ip => ip.ExteriorRing.Length - 1);
            Assert.AreEqual(numUniqueVerticies, indicies.Length);
            Assert.AreEqual(numUniqueVerticies, indicies.Select(p => p.Point(polyArray)).Distinct().Count()); //Make sure all indicies are unique and not repeating

            //Check a polygon with two interior polygon
            OuterBox.AddInteriorRing(U2);

            polyArray = [OuterBox];
            enumerator = new PolySetVertexEnum(polyArray);
            indicies = enumerator.ToArray();
            numUniqueVerticies = (OuterBox.ExteriorRing.Length - 1) + OuterBox.InteriorPolygons.Sum(ip => ip.ExteriorRing.Length - 1);
            Assert.AreEqual(numUniqueVerticies, indicies.Length);
            Assert.AreEqual(numUniqueVerticies, indicies.Select(p => p.Point(polyArray)).Distinct().Count()); //Make sure all indicies are unique and not repeating

            //Check a polygon with two interior polygons and two polygons in the array

            polyArray = [OuterBox, box];
            enumerator = new PolySetVertexEnum(polyArray);
            indicies = enumerator.ToArray();
            numUniqueVerticies = (box.ExteriorRing.Length - 1) + (OuterBox.ExteriorRing.Length - 1) + OuterBox.InteriorPolygons.Sum(ip => ip.ExteriorRing.Length - 1);
            Assert.AreEqual(numUniqueVerticies, indicies.Length);
            Assert.AreEqual(numUniqueVerticies, indicies.Select(p => p.Point(polyArray)).Distinct().Count()); //Make sure all indicies are unique and not repeating
        }

        [TestMethod]
        public void SortPointIndexTest1()
        {
            //Test sorting when we need to prevent breaks at the wraparound at the 0 index..

            //Create an array where the first and last index are adjacent, but there is a gap in the center
            PolygonIndex[] points = [new(0,0,6),
                                                    new(0,1,6),
                                                    new(0,2,6),
                                                    new(0,4,6),
                                                    new(0,5,6)];
            PolygonIndex[] sorted = PolygonIndex.SortByRing(points);

            Assert.AreEqual(4, sorted.First().VertexIndex);
            Assert.AreEqual(5, sorted[1].VertexIndex);
            Assert.AreEqual(2, sorted.Last().VertexIndex);
        }

        [TestMethod]
        public void SortPointIndexTest2()
        {
            //Test sorting when we need to prevent breaks at the wraparound at the 0 index..

            //Create an array where the first and last index are adjacent, but there is a gap in the center
            PolygonIndex[] points = [new(0,0,8),
                                                    new(0,1,8),
                                                    new(0,2,8),
                                                    new(0,4,8),
                                                    new(0,5,8),
                                                    new(0,7,8)];
            PolygonIndex[] sorted = PolygonIndex.SortByRing(points);

            Assert.AreEqual(4, sorted.First().VertexIndex);
            Assert.AreEqual(5, sorted[1].VertexIndex);
            Assert.AreEqual(7, sorted[2].VertexIndex);
            Assert.AreEqual(2, sorted.Last().VertexIndex);
        }

        [TestMethod]
        public void SortPointIndexTest3()
        {
            //Test sorting when we need to prevent breaks at the wraparound at the 0 index..

            //Create an array where the first and last index are adjacent, but there is a gap in the center
            PolygonIndex[] points = [new(0,0,8),
                                                    new(0,1,8),
                                                    new(0,2,8),
                                                    new(0,4,8),
                                                    new(0,5,8),
                                                    new(0,7,8),

                                                    new(0, 1, 0,8),
                                                    new(0, 1, 1,8),
                                                    new(0,1,2,8),
                                                    new(0,1,4,8),
                                                    new(0,1,5,8),
                                                    new(0,1,7,8),];
            PolygonIndex[] sorted = PolygonIndex.SortByRing(points);

            Assert.IsTrue(sorted.Take(6).All(p => p.IsInner == false));
            Assert.IsTrue(sorted.Skip(6).All(p => p.IsInner));
            Assert.AreEqual(4, sorted.First().VertexIndex);
            Assert.AreEqual(5, sorted[1].VertexIndex);
            Assert.AreEqual(7, sorted[2].VertexIndex);
            Assert.AreEqual(2, sorted[5].VertexIndex);

            Assert.AreEqual(4, sorted[6].VertexIndex);
            Assert.AreEqual(5, sorted[7].VertexIndex);
            Assert.AreEqual(7, sorted[8].VertexIndex);
            Assert.AreEqual(2, sorted[11].VertexIndex);

        }
        /*
        [TestMethod]
        public void Theorem4Test()
        {
            LineSegment line;
            Polygon U = Primitives.UPolygon(10);

            //Line passes along the entire length of exterior ring
            line = new LineSegment(new Vector2(-11, -10), new Vector2(11, -10));
            Assert.IsTrue(Theorem4(U, line));

            //Line passes through part of the lenght of exterior ring
            line = new LineSegment(new Vector2(-9, -10), new Vector2(11, -10));
            Assert.IsTrue(Theorem4(U, line));

            //Line crosses the exterior ring
            line = new LineSegment(new Vector2(-9, -11), new Vector2(-9, -9));
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
            Polygon box = Primitives.BoxPolygon(10);

            Vector2 A = new(-15, 1);
            Vector2 B = new(15, 1);

            Vector2 expected_start = new(-10, 1);
            Vector2 expected_end = new(10, 1);

            Vector2[] expected_ring_counterclockwise = [expected_start,
                                                             new(-10,0),
                                                             new(-10,-10),
                                                             new(10,-10),
                                                             expected_end,
                                                             expected_start];

            Vector2[] expected_ring_clockwise = [expected_start,
                                                             expected_end,
                                                             new(10,10),
                                                             new(-10,10),
                                                             expected_start];

            Polygon clockwise_output = Polygon.WalkPolygonCut(box, RotationDirection.Clockwise, [A, B]);
            PolygonTest.ValidatePolygonCut(clockwise_output, new Polygon(expected_ring_clockwise), expected_start, expected_end);

            Polygon counterclockwise_output = Polygon.WalkPolygonCut(box, RotationDirection.Counterclockwise, [A, B]);
            PolygonTest.ValidatePolygonCut(counterclockwise_output, new Polygon(expected_ring_counterclockwise), expected_start, expected_end);
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
            Polygon box = Primitives.BoxPolygon(10);

            Polygon inner = Primitives.BoxPolygon(1).Translate(new Vector2(0, -2));

            box.AddInteriorRing(inner);

            Vector2 A = new(-15, 1);
            Vector2 B = new(15, 1);

            Vector2 expected_start = new(-10, 1);
            Vector2 expected_end = new(10, 1);

            Vector2[] expected_ring_counterclockwise = [expected_start,
                                                             new(-10,0),
                                                             new(-10,-10),
                                                             new(10,-10),
                                                             expected_end,
                                                             expected_start];

            Vector2[] expected_ring_clockwise = [expected_start,
                                                             expected_end,
                                                             new(10,10),
                                                             new(-10,10),
                                                             expected_start];

            Polygon clockwise_output = Polygon.WalkPolygonCut(box, RotationDirection.Clockwise, [A, B]);
            PolygonTest.ValidatePolygonCut(clockwise_output, new Polygon(expected_ring_clockwise), expected_start, expected_end);

            Polygon counterclockwise_output = Polygon.WalkPolygonCut(box, RotationDirection.Counterclockwise, [A, B]);
            PolygonTest.ValidatePolygonCut(counterclockwise_output, new Polygon(expected_ring_counterclockwise), expected_start, expected_end);

            Assert.AreEqual(1, counterclockwise_output.InteriorPolygons.Count);
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
            Polygon box = Primitives.BoxPolygon(10);

            Vector2 A = new(-15, 1);
            Vector2 B = new(0, 1);
            Vector2 C = new(15, 1);

            Vector2 expected_start = new(-10, 1);
            Vector2 expected_end = new(10, 1);

            Vector2[] expected_ring_counterclockwise = [expected_start,
                                                             new(-10,0),
                                                             new(-10,-10),
                                                             new(10,-10),
                                                             expected_end,
                                                             B,
                                                             expected_start];

            Vector2[] expected_ring_clockwise = [expected_start,
                                                             B,
                                                             expected_end,
                                                             new(10,10),
                                                             new(-10,10),
                                                             expected_start];

            Polygon clockwise_output = Polygon.WalkPolygonCut(box, RotationDirection.Clockwise, [A, B, C]);
            PolygonTest.ValidatePolygonCut(clockwise_output, new Polygon(expected_ring_clockwise), expected_start, expected_end);

            Polygon counterclockwise_output = Polygon.WalkPolygonCut(box, RotationDirection.Counterclockwise, [A, B, C]);
            PolygonTest.ValidatePolygonCut(counterclockwise_output, new Polygon(expected_ring_counterclockwise), expected_start, expected_end);
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
            Polygon box = Primitives.BoxPolygon(10);

            Vector2 A = new(-15, 1);
            Vector2 B = new(0, 1);
            Vector2 C = new(0, -5);
            Vector2 D = new(15, -5);

            Vector2 expected_start = new(-10, 1);
            Vector2 expected_end = new(10, -5);

            Vector2[] expected_ring_counterclockwise = [expected_start,
                                                             new(-10,0),
                                                             new(-10,-10),
                                                             new(10,-10),
                                                             expected_end,
                                                             C,
                                                             B,
                                                             expected_start];

            Vector2[] expected_ring_clockwise = [expected_start,
                                                             B,
                                                             C,
                                                             expected_end,
                                                             new(10,10),
                                                             new(-10,10),
                                                             expected_start];

            Polygon counterclockwise_output = Polygon.WalkPolygonCut(box, RotationDirection.Counterclockwise, [A, B, C, D]);
            PolygonTest.ValidatePolygonCut(counterclockwise_output, new Polygon(expected_ring_counterclockwise), expected_start, expected_end);

            Polygon clockwise_output = Polygon.WalkPolygonCut(box, RotationDirection.Clockwise, [A, B, C, D]);
            PolygonTest.ValidatePolygonCut(clockwise_output, new Polygon(expected_ring_clockwise), expected_start, expected_end);

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
            Polygon box = Primitives.BoxPolygon(10);

            Vector2 A = new(-15, 15);
            Vector2 B = new(15, -15);

            Vector2 expected_start = new(-10, 10);
            Vector2 expected_end = new(10, -10);

            Vector2[] expected_ring_counterclockwise = [expected_start,
                                                             new(-10,0),
                                                             new(-10,-10),
                                                             expected_end,
                                                             expected_start];

            Vector2[] expected_ring_clockwise = [expected_start,
                                                             new(10,10),
                                                             expected_end,
                                                             expected_start];

            Polygon clockwise_output = Polygon.WalkPolygonCut(box, RotationDirection.Clockwise, [A, B]);
            PolygonTest.ValidatePolygonCut(clockwise_output, new Polygon(expected_ring_clockwise), expected_start, expected_end);

            Polygon counterclockwise_output = Polygon.WalkPolygonCut(box, RotationDirection.Counterclockwise, [A, B]);
            PolygonTest.ValidatePolygonCut(counterclockwise_output, new Polygon(expected_ring_counterclockwise), expected_start, expected_end);


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
            Polygon box = Primitives.BoxPolygon(10);

            Vector2 A = new(-15, 15);
            Vector2 B = new(0, 0);
            Vector2 C = new(15, -15);

            Vector2 expected_start = new(-10, 10);
            Vector2 expected_end = new(10, -10);

            Vector2[] expected_ring_counterclockwise = [expected_start,
                                                             new(-10,0),
                                                             new(-10,-10),
                                                             expected_end,
                                                             B,
                                                             expected_start];

            Vector2[] expected_ring_clockwise = [expected_start,
                                                             new(10,10),
                                                             expected_end,
                                                             B,
                                                             expected_start];

            Polygon clockwise_output = Polygon.WalkPolygonCut(box, RotationDirection.Clockwise, [A, B, C]);
            PolygonTest.ValidatePolygonCut(clockwise_output, new Polygon(expected_ring_clockwise), expected_start, expected_end);

            Polygon counterclockwise_output = Polygon.WalkPolygonCut(box, RotationDirection.Counterclockwise, [A, B, C]);
            PolygonTest.ValidatePolygonCut(counterclockwise_output, new Polygon(expected_ring_counterclockwise), expected_start, expected_end);
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
            Polygon box = Primitives.BoxPolygon(10);

            Vector2 A = new(-15, 15);
            Vector2 B = new(0, 0);
            Vector2 C = new(15, -15);

            Vector2[] path = [new(-45,15),
                                                    new(-30,15),
                                                    A,
                                                    B,
                                                    C,
                                                    new(30,-15),
                                                    new(45,-15)
                                                    ];

            Vector2 expected_start = new(-10, 10);
            Vector2 expected_end = new(10, -10);

            Vector2[] expected_ring_counterclockwise = [expected_start,
                                                             new(-10,0),
                                                             new(-10,-10),
                                                             expected_end,
                                                             B,
                                                             expected_start];

            Vector2[] expected_ring_clockwise = [expected_start,
                                                             new(10,10),
                                                             expected_end,
                                                             B,
                                                             expected_start];

            Polygon clockwise_output = Polygon.WalkPolygonCut(box, RotationDirection.Clockwise, path);
            PolygonTest.ValidatePolygonCut(clockwise_output, new Polygon(expected_ring_clockwise), expected_start, expected_end);

            Polygon counterclockwise_output = Polygon.WalkPolygonCut(box, RotationDirection.Counterclockwise, path);
            PolygonTest.ValidatePolygonCut(counterclockwise_output, new Polygon(expected_ring_counterclockwise), expected_start, expected_end);
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
            Polygon box = Primitives.BoxPolygon(10);

            Vector2 A = new(-9, 1);
            Vector2 B = new(-15, 1);
            Vector2 C = new(-15, 15);
            Vector2 D = new(15, 15);
            Vector2 E = new(15, 1);
            Vector2 F = new(9, 1);

            Vector2 expected_start = new(-10, 1);
            Vector2 expected_end = new(10, 1);

            Vector2[] path = [A, B, C, D, E, F];

            Vector2[] expected_ring_counterclockwise = [expected_start,
                                                             new(-10,0),
                                                             new(-10,-10),
                                                             new(10,-10),
                                                             expected_end,
                                                             E,
                                                             D,
                                                             C,
                                                             B,
                                                             expected_start];

            Vector2[] expected_ring_clockwise = [expected_start,
                                                             new(-10,10),
                                                             new(10,10),
                                                             expected_end,
                                                             E,
                                                             D,
                                                             C,
                                                             B,
                                                             expected_start];

            Polygon clockwise_output = Polygon.WalkPolygonCut(box, RotationDirection.Clockwise, path);
            PolygonTest.ValidatePolygonCut(clockwise_output, new Polygon(expected_ring_clockwise), expected_start, expected_end);

            Polygon counterclockwise_output = Polygon.WalkPolygonCut(box, RotationDirection.Counterclockwise, path);
            PolygonTest.ValidatePolygonCut(counterclockwise_output, new Polygon(expected_ring_counterclockwise), expected_start, expected_end);
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
            Polygon uBox = new(Primitives.ConcaveUVerticies(10));

            Vector2 A = new(-7.5, 7.5);
            Vector2 B = new(7.5, 7.5);

            Vector2 expected_start = new(-5, 7.5);
            Vector2 expected_end = new(5, 7.5);

            Vector2[] path = [A, B];

            Vector2[] expected_ring_counterclockwise = [expected_start,
                                                             new(-5,  10),
                                                             new(-10, 10),
                                                             new(-10,-10),
                                                             new( 10,-10),
                                                             new( 10, 10),
                                                             new( 5,  10),
                                                             expected_end,
                                                             expected_start];

            Vector2[] expected_ring_clockwise = [expected_start,
                                                             new(-5,-5),
                                                             new(5,-5),
                                                             expected_end,
                                                             expected_start];

            Polygon clockwise_output = Polygon.WalkPolygonCut(uBox, RotationDirection.Clockwise, path);
            PolygonTest.ValidatePolygonCut(clockwise_output, new Polygon(expected_ring_clockwise), expected_start, expected_end);

            Polygon counterclockwise_output = Polygon.WalkPolygonCut(uBox, RotationDirection.Counterclockwise, path);
            PolygonTest.ValidatePolygonCut(counterclockwise_output, new Polygon(expected_ring_counterclockwise), expected_start, expected_end);
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
            Polygon uBox = new(Primitives.ConcaveUVerticies(10));

            Vector2 A = new(-8, 7.5);
            Vector2 B = new(-7.5, 7.5);
            Vector2 C = new(7.5, 7.5);
            Vector2 D = new(9, 7.5);

            Vector2 expected_start = new(-5, 7.5);
            Vector2 expected_end = new(5, 7.5);

            Vector2[] path = [A, B, C, D];

            Vector2[] expected_ring_counterclockwise = [expected_start,
                                                             new(-5,  10),
                                                             new(-10, 10),
                                                             new(-10,-10),
                                                             new( 10,-10),
                                                             new( 10, 10),
                                                             new( 5,  10),
                                                             expected_end,
                                                             expected_start];

            Vector2[] expected_ring_clockwise = [expected_start,
                                                             new(-5,-5),
                                                             new(5,-5),
                                                             expected_end,
                                                             expected_start];

            Polygon clockwise_output = Polygon.WalkPolygonCut(uBox, RotationDirection.Clockwise, path);
            PolygonTest.ValidatePolygonCut(clockwise_output, new Polygon(expected_ring_clockwise), expected_start, expected_end);

            Polygon counterclockwise_output = Polygon.WalkPolygonCut(uBox, RotationDirection.Counterclockwise, path);
            PolygonTest.ValidatePolygonCut(counterclockwise_output, new Polygon(expected_ring_counterclockwise), expected_start, expected_end);
        }

        private static void ValidatePolygonCut(Polygon cut, Polygon expected_cut, Vector2 expected_start, Vector2 expected_end)
        {
            Assert.IsTrue(cut.Covers(expected_start));
            Assert.IsTrue(cut.Covers(expected_end));

            Assert.IsTrue(expected_cut.ExteriorRing.SequenceEqual(cut.ExteriorRing));

            for (int iRing = 0; iRing < expected_cut.InteriorRings.Count; iRing++)
            {
                Assert.IsTrue(expected_cut.InteriorRings[iRing].SequenceEqual(cut.InteriorRings[iRing]));
            }
        }

        [TestMethod]
        public void TestCorrespondingPointsSimple()
        {
            var A = Primitives.BoxPolygon(10);
            var AInner = Primitives.BoxPolygon(1);
            A.AddInteriorRing(AInner);

            var B = Primitives.BoxPolygon(20).Translate(Vector2.UnitY * 20);

            Vector2[] expectedCorresponding = [new(-10,0),
                                              new(-1,0),
                                              new(1,0),
                                              new(10,0)];

            //Simplified view, '+' are corresponding locations I expect
            //      *---------*
            //      |         |
            //      |  *---*  |
            //  *---+--+---+--+----*      
            //  |   |  *---*  |    |
            //  |   |         |    |
            //  |   *---------*    |
            //  |                  |

            TestFindingCorrespondingPoints(A, B, expectedCorresponding);
        }

        [TestMethod]
        public void TestCorrespondingPointsExactOverlap()
        {
            var A = Primitives.BoxPolygon(10);
            var AInner = Primitives.BoxPolygon(1);
            A.AddInteriorRing(AInner);

            Polygon B = A.Clone() as Polygon;

            var expectedCorresponding = A.AllVertices.ToArray();

            //Simplified view, '+' are corresponding locations I expect
            //      *---------*
            //      |         |
            //      |  *---*  |
            //  *---+--+---+--+----*      
            //  |   |  *---*  |    |
            //  |   |         |    |
            //  |   *---------*    |
            //  |                  |

            TestFindingCorrespondingPoints(A, B, expectedCorresponding);
        }

        [TestMethod]
        public void TestCorrespondingPointsSimple2()
        {
            var A = Primitives.BoxPolygon(5);
            var AInner = Primitives.BoxPolygon(1);
            A.AddInteriorRing(AInner);

            var B = Primitives.DiamondPolygon(5).Translate(Vector2.UnitY * 5);

            Vector2[] expectedCorresponding = [new(-1,1),
                new(1,1),
                new(-5,5),
                new(5,5)];

            //Simplified view, '+' are corresponding locations I expect
            //      *---------*
            //      |         |
            //      |  *---*  |
            //      |  | + |  |
            //      |  +---+  |
            //      |/       \|
            //      +---------+
            //       \       /
            //        \     /
            //         \   /
            //          \ /
            //           *

            TestFindingCorrespondingPoints(A, B, expectedCorresponding);
        }

        [TestMethod]
        public void TestCorrespondingPointsSimple3()
        {
            var A = Primitives.BoxPolygon(5);
            var AInner = Primitives.BoxPolygon(1);
            A.AddInteriorRing(AInner);

            var B = Primitives.DiamondPolygon(5).Translate(Vector2.UnitY * 6);

            Vector2[] expectedCorresponding = [new(0,1),
                new(-4,5),
                new(4,5)];

            //Simplified view, '+' are corresponding locations I expect
            //      *---------*
            //      |         |
            //      |  *---*  |
            //      |  |   |  |
            //      |  *-+-*  |
            //      |   / \   |
            //      *--+---+--*
            //        /     \
            //       *       *
            //        \     /
            //         \   /
            //          \ /
            //           *

            TestFindingCorrespondingPoints(A, B, expectedCorresponding);
        }


        private void TestFindingCorrespondingPoints(Polygon A, Polygon B, Vector2[] expectedCorresponding)
        {
            //Ensure test setup does not expect duplicate corresponding points
            Assert.AreEqual(expectedCorresponding.Distinct().Count(), expectedCorresponding.Length);

            Polygon[] list = [A.Clone() as Polygon, B.Clone() as Polygon];
            var corresponding = list.AddCorrespondingVertices().ToArray();
            PolygonTest.EvaluateCorrespondingPointsResults(list[0], list[1], expectedCorresponding, corresponding);

            //Reverse the order the polygons are passed, check that we get the same result
            Polygon[] listReversed = [B.Clone() as Polygon, A.Clone() as Polygon];
            var reversedCorresponding = listReversed.AddCorrespondingVertices().ToArray();
            PolygonTest.EvaluateCorrespondingPointsResults(listReversed[0], listReversed[1], expectedCorresponding, reversedCorresponding);
        }

        private static void EvaluateCorrespondingPointsResults(Polygon A, Polygon B, Vector2[] expectedCorresponding, Vector2[] foundCorresponding)
        {
            //Ensure we found the correct number of corresponding points
            Assert.AreEqual(foundCorresponding.Length, expectedCorresponding.Length);

            //Ensure we do not have duplicate points in the output
            Assert.AreEqual(foundCorresponding.Distinct().Count(), foundCorresponding.Length);

            //Ensure the expected corresponding points are verticies in both polygons
            var allAVerts = A.AllVertices;
            var allBVerts = B.AllVertices;
            foreach (var p in expectedCorresponding)
            {
                Assert.IsTrue(allAVerts.Contains(p));
                Assert.IsTrue(allBVerts.Contains(p));
            }
        }

        /// <summary>
        /// Replace an existing vertex in a polygon with one less than an epsilon distance away
        /// </summary>
        [TestMethod]
        public void TestSetVertexEpsilonChange()
        {
            PolygonTest.DoSetVertexFromOffsetPosition(Geometry.Global.Epsilon / 2);
            PolygonTest.DoSetVertexFromOffsetPosition(Geometry.Global.Epsilon * 2);
            PolygonTest.DoSetVertexFromOffsetPosition(1);
        }

        /// <summary>
        /// Creates a polygon, translates it by a set amount, and the tests SetVertex on each polygon ensuring the 
        /// SetVertex function works.
        /// </summary>
        /// <param name="offset"></param>
        private static void DoSetVertexFromOffsetPosition(double offset)
        {
            var A = Primitives.BoxPolygon(10);
            var AInner = Primitives.BoxPolygon(1);
            A.AddInteriorRing(AInner);

            var AEpsilon = (A.Clone() as Polygon).Translate(Vector2.UnitX * Geometry.Global.Epsilon / 2.0);

            var expectedPoints = A.AllVertices;

            PolygonVertexEnum enumerator = new(AEpsilon, reverse: true);
            foreach (PolygonIndex pIndex in enumerator)
            {
                var desiredValue = A[pIndex];
                AEpsilon.SetVertex(pIndex, desiredValue);
                Assert.IsTrue(AEpsilon[pIndex] == desiredValue);
            }
        }


        /// <summary>
        /// Theorem 4 requries that a line segment does not occupy space both internal and external to the polygon.
        /// Lines that fall over a polygon segment are acceptable as long as the rest of the line qualifies.
        /// </summary>
        /// <param name="poly"></param>
        /// <param name="line"></param>
        /// <returns></returns>
        public static bool Theorem4(Polygon poly, LineSegment line) => !line.Intersects(poly, true, out List<Vector2> intersections);

    }
}
