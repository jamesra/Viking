using Geometry;
using Geometry.Transforms;
using MathNet.Numerics.LinearAlgebra;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using System.Diagnostics;
using System.Linq;

namespace GeometryTests
{
    [TestClass]
    public class RBFTransformTest
    {
        [TestMethod]
        public void TestMethod1()
        {
            Vector2[] ControlPoints = [ new(104.8445,  75.1144),
                                                           new(102.7622,   163.9576),
                                                           new(257.5437,  79.9730),
                                                           new(258.2378,  168.1221)];

            Vector2[] MappedPoints = [ new(68.7519, 127.1710),
                                                           new(87.4923,   199.3560),
                                                           new(263.7905, 77.8907),
                                                           new(281.1427, 149.3817)];


            Matrix<double> BetaMatrix = Geometry.Transforms.RBFTransform.CreateBetaMatrixWithLinear(ControlPoints,
                                                                                               Geometry.Transforms.RBFTransform.StandardBasisFunction);

            double[] SolutionMatrix = Geometry.Transforms.RBFTransform.CreateSolutionMatrixWithLinear(MappedPoints);

            //double[] Weights = GridMatrix.LinSolve(BetaMatrix, SolutionMatrix); 
            double[] Weights = RBFTransform.CalculateRBFWeights(MappedPoints, ControlPoints, RBFTransform.StandardBasisFunction);

            MappingVector2[] Points = new MappingVector2[ControlPoints.Length];
            for (int i = 0; i < ControlPoints.Length; i++)
            {
                Points[i] = new MappingVector2(ControlPoints[i], MappedPoints[i]);
            }

            RBFTransform transform = new(Points, new TransformBasicInfo());

            for (int i = 0; i < ControlPoints.Length; i++)
            {
                Vector2 tPoint = transform.Transform(MappedPoints[i]);
                Trace.WriteLine(tPoint.ToString() + " should equal " + ControlPoints[i]);
                Assert.IsTrue(Vector2.Distance(tPoint, ControlPoints[i]) < 1.0);
            }

            for (int i = 0; i < ControlPoints.Length; i++)
            {
                Vector2 tPoint = transform.InverseTransform(ControlPoints[i]);
                Trace.WriteLine(tPoint.ToString() + " should equal " + MappedPoints[i]);
                Assert.IsTrue(Vector2.Distance(tPoint, MappedPoints[i]) < 1.0);
            }



        }

        /// <summary>
        /// Volume coordinates reach several hundred thousand pixels and mapping must stay within one pixel. An RBF fitted to
        /// control points spread over 500,000 pixels must reproduce every control point, and a forward then inverse round
        /// trip of points between them must return within a pixel. A single-precision solve fails this.
        /// </summary>
        [TestMethod]
        public void RBFHoldsSinglePixelAccuracyAtVolumeScale()
        {
            const double extent = 500000;
            System.Random random = new(645);
            MappingVector2[] points = new MappingVector2[150];
            for (int i = 0; i < points.Length; i++)
            {
                Vector2 mapped = new(random.NextDouble() * extent, random.NextDouble() * extent);
                Vector2 warp = new(250 * System.Math.Sin(mapped.Y / 150000.0), 250 * System.Math.Cos(mapped.X / 200000.0));
                Vector2 control = mapped + new Vector2(1234.5, -987.25) + warp;
                points[i] = new MappingVector2(control, mapped);
            }

            RBFTransform transform = new(points, new TransformBasicInfo());

            double worstAtControlPoints = 0;
            foreach (MappingVector2 p in points)
            {
                worstAtControlPoints = System.Math.Max(worstAtControlPoints, Vector2.Distance(transform.Transform(p.MappedPoint), p.ControlPoint));
                worstAtControlPoints = System.Math.Max(worstAtControlPoints, Vector2.Distance(transform.InverseTransform(p.ControlPoint), p.MappedPoint));
            }
            Assert.IsTrue(worstAtControlPoints < 1.0, $"Control points reproduced within {worstAtControlPoints:F4} px; must be under 1 px");

            double worstRoundTrip = 0;
            for (int i = 0; i < 200; i++)
            {
                Vector2 q = new(extent * (0.2 + (0.6 * random.NextDouble())), extent * (0.2 + (0.6 * random.NextDouble())));
                Vector2 back = transform.InverseTransform(transform.Transform(q));
                worstRoundTrip = System.Math.Max(worstRoundTrip, Vector2.Distance(back, q));
            }
            Assert.IsTrue(worstRoundTrip < 1.0, $"Round trip within {worstRoundTrip:F4} px; must be under 1 px");
        }

        /// <summary>Warped control points on a jittered grid, so the RBF and a Delaunay mesh over them are both well conditioned.</summary>
        private static MappingVector2[] CreateWarpedGrid(int columns, int rows, double spacing, int seed)
        {
            System.Random random = new(seed);
            MappingVector2[] points = new MappingVector2[columns * rows];
            for (int y = 0; y < rows; y++)
            {
                for (int x = 0; x < columns; x++)
                {
                    Vector2 mapped = new((x * spacing) + (random.NextDouble() * spacing * 0.3), (y * spacing) + (random.NextDouble() * spacing * 0.3));
                    Vector2 control = mapped + new Vector2(120.5, -75.25) + new Vector2(8 * System.Math.Sin(mapped.Y / 90.0), 6 * System.Math.Cos(mapped.X / 70.0));
                    points[(y * columns) + x] = new MappingVector2(control, mapped);
                }
            }

            return points;
        }

        private static Vector2[] CreateQueryPoints(int count, double min, double max, int seed)
        {
            System.Random random = new(seed);
            Vector2[] queries = new Vector2[count];
            for (int i = 0; i < queries.Length; i++)
                queries[i] = new Vector2(min + (random.NextDouble() * (max - min)), min + (random.NextDouble() * (max - min)));

            return queries;
        }

        /// <summary>
        /// Factoring once and solving X and Y from the factorization must match MathNet's own two-call <c>Solve</c>, which uses
        /// LU for a square matrix. Same decomposition, so the weights agree far inside 1e-6.
        /// </summary>
        [TestMethod]
        public void CalculateRBFWeightsMatchesSeparateSolves()
        {
            MappingVector2[] points = CreateWarpedGrid(8, 8, 60, 11);
            Vector2[] mapped = MappingVector2.MappedPoints(points);
            Vector2[] control = MappingVector2.ControlPoints(points);

            double[] weights = RBFTransform.CalculateRBFWeights(mapped, control, null);

            Matrix<double> beta = RBFTransform.CreateBetaMatrixWithLinear(mapped, null);
            double[] expectedX = beta.Solve(RBFTransform.CreateSolutionMatrix_X_WithLinear(control)).ToArray();
            double[] expectedY = beta.Solve(RBFTransform.CreateSolutionMatrix_Y_WithLinear(control)).ToArray();

            Assert.AreEqual(expectedX.Length + expectedY.Length, weights.Length);
            for (int i = 0; i < expectedX.Length; i++)
            {
                Assert.AreEqual(expectedX[i], weights[i], 1e-9, $"X weight {i}");
                Assert.AreEqual(expectedY[i], weights[expectedX.Length + i], 1e-9, $"Y weight {i}");
            }
        }

        /// <summary>
        /// Batch mapping must equal point-by-point mapping in both directions, on a batch small enough to run serially and one
        /// large enough to run in parallel, and the Try batch calls must report every point as mapped.
        /// </summary>
        [TestMethod]
        public void RBFBatchMatchesSinglePoints()
        {
            RBFTransform transform = new(CreateWarpedGrid(10, 10, 50, 21), new TransformBasicInfo());

            foreach (int count in new[] { 0, 1, 5, 20000 })
            {
                Vector2[] queries = CreateQueryPoints(count, -50, 600, 100 + count);

                Vector2[] forward = transform.Transform(queries);
                Vector2[] inverse = transform.InverseTransform(queries);
                bool[] forwardMapped = transform.TryTransform(queries, out Vector2[] tryForward);
                bool[] inverseMapped = transform.TryInverseTransform(queries, out Vector2[] tryInverse);

                Assert.AreEqual(count, forward.Length);
                Assert.AreEqual(count, forwardMapped.Length);
                Assert.AreEqual(count, inverseMapped.Length);
                for (int i = 0; i < count; i++)
                {
                    Assert.AreEqual(transform.Transform(queries[i]), forward[i], $"forward {i} of {count}");
                    Assert.AreEqual(transform.InverseTransform(queries[i]), inverse[i], $"inverse {i} of {count}");
                    Assert.AreEqual(forward[i], tryForward[i]);
                    Assert.AreEqual(inverse[i], tryInverse[i]);
                    Assert.IsTrue(forwardMapped[i]);
                    Assert.IsTrue(inverseMapped[i]);
                }
            }
        }

        /// <summary>
        /// The cached point arrays and weights are copies of the map points, so moving the points in place with
        /// <c>Translate</c> must discard them. After a translate by <c>v</c>, a mapped point lands <c>v</c> further on in control space.
        /// </summary>
        [TestMethod]
        public void RBFTranslateInvalidatesCachedPointsAndWeights()
        {
            MappingVector2[] points = CreateWarpedGrid(6, 6, 80, 31);
            RBFTransform transform = new(points, new TransformBasicInfo());
            Vector2[] queries = CreateQueryPoints(50, 0, 400, 5);

            Vector2[] before = transform.Transform(queries);
            Vector2[] inverseBefore = transform.InverseTransform(points.Select(p => p.ControlPoint).ToArray());

            Vector2 shift = new(1000.25, -333.5);
            transform.Translate(shift);

            Vector2[] after = transform.Transform(queries);
            for (int i = 0; i < queries.Length; i++)
                Assert.IsTrue(Vector2.Distance(before[i] + shift, after[i]) < 1e-2, $"point {i} moved by the translation");

            Vector2[] inverseAfter = transform.InverseTransform(points.Select(p => p.ControlPoint + shift).ToArray());
            for (int i = 0; i < inverseAfter.Length; i++)
                Assert.IsTrue(Vector2.Distance(inverseBefore[i], inverseAfter[i]) < 1e-2, $"inverse {i} follows the translation");
        }

        /// <summary>
        /// The constructor copies the caller's array, so a change to that array after construction does not reach the transform.
        /// </summary>
        [TestMethod]
        public void RBFDoesNotShareCallerPointArray()
        {
            MappingVector2[] points = CreateWarpedGrid(5, 5, 80, 41);
            RBFTransform transform = new(points, new TransformBasicInfo());
            Vector2[] queries = CreateQueryPoints(20, 0, 300, 6);
            Vector2[] before = transform.Transform(queries);

            for (int i = 0; i < points.Length; i++)
                points[i] = new MappingVector2(points[i].ControlPoint + new Vector2(500, 500), points[i].MappedPoint);

            Vector2[] after = transform.Transform(queries);
            for (int i = 0; i < queries.Length; i++)
                Assert.AreEqual(before[i], after[i]);
        }

        /// <summary>MinimizeMemory drops the caches; the next call rebuilds them and gets the same answers.</summary>
        [TestMethod]
        public void RBFMinimizeMemoryRebuildsIdenticalResults()
        {
            RBFTransform transform = new(CreateWarpedGrid(6, 6, 80, 51), new TransformBasicInfo());
            Vector2[] queries = CreateQueryPoints(30, 0, 400, 7);
            Vector2[] forward = transform.Transform(queries);
            Vector2[] inverse = transform.InverseTransform(queries);

            transform.MinimizeMemory();

            CollectionAssert.AreEqual(forward, transform.Transform(queries));
            CollectionAssert.AreEqual(inverse, transform.InverseTransform(queries));
        }

        /// <summary>
        /// The fallback transform's batch calls must return exactly what mapping each point separately returns, for queries inside
        /// the mesh (discrete) and outside it (RBF), in both directions.
        /// </summary>
        [TestMethod]
        public void FallbackBatchMatchesPerPointPath()
        {
            MappingVector2[] points = CreateWarpedGrid(8, 8, 50, 61);
            TransformBasicInfo info = new();
            MeshTransform mesh = new(points, info);
            RBFTransform rbf = new(points, info);
            DiscreteTransformWithContinuousFallback fallback = new(mesh, rbf, info);

            Vector2[] mappedQueries = CreateQueryPoints(400, -200, 600, 71);
            Vector2[] controlQueries = [.. CreateQueryPoints(400, -200, 800, 72)];

            int discreteMapped = mappedQueries.Count(p => mesh.TryTransform(p, out _));
            int discreteInverseMapped = controlQueries.Count(p => mesh.TryInverseTransform(p, out _));
            Assert.IsTrue(discreteMapped > 0 && discreteMapped < mappedQueries.Length, $"queries must mix discrete and fallback points, got {discreteMapped}");
            Assert.IsTrue(discreteInverseMapped > 0 && discreteInverseMapped < controlQueries.Length, $"inverse queries must mix discrete and fallback points, got {discreteInverseMapped}");

            Vector2[] batchForward = fallback.Transform(mappedQueries);
            bool[] tryForward = fallback.TryTransform(mappedQueries, out Vector2[] tryBatchForward);
            Vector2[] batchInverse = fallback.InverseTransform(controlQueries);
            bool[] tryInverse = fallback.TryInverseTransform(controlQueries, out Vector2[] tryBatchInverse);

            Assert.AreEqual(mappedQueries.Length, tryForward.Length);
            Assert.AreEqual(controlQueries.Length, tryInverse.Length);
            for (int i = 0; i < mappedQueries.Length; i++)
            {
                Assert.AreEqual(fallback.Transform(mappedQueries[i]), batchForward[i], $"forward {i}");
                Assert.AreEqual(batchForward[i], tryBatchForward[i], $"try forward {i}");
                Assert.IsTrue(tryForward[i]);
            }

            for (int i = 0; i < controlQueries.Length; i++)
            {
                Assert.AreEqual(fallback.InverseTransform(controlQueries[i]), batchInverse[i], $"inverse {i}");
                Assert.AreEqual(batchInverse[i], tryBatchInverse[i], $"try inverse {i}");
                Assert.IsTrue(tryInverse[i]);
            }
        }
    }
}
