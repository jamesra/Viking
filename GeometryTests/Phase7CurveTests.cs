using Geometry;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using System;
using System.Collections.Generic;
using System.Linq;

namespace GeometryTests
{
    /// <summary>
    /// Phase 7 equivalence tests for Catmull-Rom, the adaptive t-point recursion, smoothing kernels and the
    /// Douglas-Peucker preserve list. Every comparison is on the raw bits of the doubles.
    /// </summary>
    [TestClass]
    public class Phase7CurveTests
    {
        private static readonly double[] Scales = [1e-3, 0.5, 40, 5000, 500000];

        private static Vector2 RandomPoint(Random random, double scale, double origin) => new(origin + (random.NextDouble() * scale), origin + (random.NextDouble() * scale));

        /// <summary>
        /// Four control points: random, or with a coincident pair, which drives a knot interval to zero and the
        /// formulas to NaN or infinity. Those results must also match bit for bit.
        /// </summary>
        private static Vector2[] ControlPoints(Random random, int variant, double scale, double origin)
        {
            Vector2[] p = [RandomPoint(random, scale, origin), RandomPoint(random, scale, origin), RandomPoint(random, scale, origin), RandomPoint(random, scale, origin)];
            switch (variant)
            {
                case 1:
                    p[1] = p[0];
                    break;
                case 2:
                    p[2] = p[1];
                    break;
                case 3:
                    p[3] = p[2];
                    break;
                case 4:
                    p[1] = p[0];
                    p[2] = p[0];
                    break;
                case 5:
                    for (int i = 0; i < 4; i++)
                        p[i] = new Vector2(origin + (i * scale), origin + (i * scale));
                    break;
            }

            return p;
        }

        private static (bool Threw, string Exception, T Result) Capture<T>(Func<T> run)
        {
            try
            {
                return (false, null, run());
            }
            catch (Exception ex)
            {
                return (true, ex.GetType().Name, default);
            }
        }

        /// <summary>
        /// Runs both versions. If either throws, both must throw the same exception type; otherwise the results are compared.
        /// </summary>
        private static void AssertSame<T>(Func<T> expected, Func<T> actual, Action<T, T> compare, string context)
        {
            var e = Capture(expected);
            var a = Capture(actual);
            Assert.AreEqual(e.Threw, a.Threw, context + ": threw");
            if (e.Threw)
            {
                Assert.AreEqual(e.Exception, a.Exception, context);
                return;
            }

            compare(e.Result, a.Result);
        }

        private static void AssertSameCurve(Func<Vector2[]> expected, Func<Vector2[]> actual, string context) =>
            AssertSame(expected, actual, (e, a) => Phase7MeshCurveTests.AssertBitIdentical(e, a, context), context);
        [TestMethod]
        public void FitCurveSegmentByCountMatchesReference()
        {
            Random random = new(31);
            foreach (double scale in Scales)
            {
                for (int variant = 0; variant <= 5; variant++)
                {
                    for (int rep = 0; rep < 6; rep++)
                    {
                        Vector2[] p = ControlPoints(random, variant, scale, rep % 2 == 0 ? 0 : 400000);
                        for (int count = 0; count <= 12; count++)
                        {
                            int n = count;
                            AssertSameCurve(
                                () => Phase7ReferenceImplementations.FitCurveSegment(in p[0], in p[1], in p[2], in p[3], n),
                                () => CatmullRom.FitCurveSegment(in p[0], in p[1], in p[2], in p[3], n),
                                $"scale {scale} variant {variant} rep {rep} count {n}");
                        }
                    }
                }
            }
        }

        [TestMethod]
        public void FitCurveSegmentByCountRejectsNegativeLikeBefore()
        {
            Vector2[] p = [new(0, 0), new(1, 0), new(2, 1), new(3, 1)];
            AssertSameCurve(
                () => Phase7ReferenceImplementations.FitCurveSegment(in p[0], in p[1], in p[2], in p[3], -1),
                () => CatmullRom.FitCurveSegment(in p[0], in p[1], in p[2], in p[3], -1),
                "negative count");
        }

        [TestMethod]
        public void FitCurveSegmentByFractionsMatchesReference()
        {
            Random random = new(32);
            foreach (double scale in Scales)
            {
                for (int variant = 0; variant <= 5; variant++)
                {
                    for (int rep = 0; rep < 6; rep++)
                    {
                        Vector2[] p = ControlPoints(random, variant, scale, rep % 2 == 0 ? 0 : 400000);
                        int length = random.Next(0, 24);
                        double[] fractions = new double[length];
                        for (int i = 0; i < length; i++)
                            fractions[i] = random.NextDouble() * 1.2 - 0.1;
                        Array.Sort(fractions);

                        AssertSameCurve(
                            () => Phase7ReferenceImplementations.FitCurveSegment(in p[0], in p[1], in p[2], in p[3], fractions),
                            () => CatmullRom.FitCurveSegment(in p[0], in p[1], in p[2], in p[3], fractions),
                            $"scale {scale} variant {variant} rep {rep}");
                    }
                }
            }
        }

        [TestMethod]
        public void FitCurveSegmentFromPointListMatchesReference()
        {
            Random random = new(33);
            for (int rep = 0; rep < 60; rep++)
            {
                int count = random.Next(2, 12);
                double scale = Scales[1 + (rep % (Scales.Length - 1))];
                List<Vector2> points = [.. Enumerable.Range(0, count).Select(_ => RandomPoint(random, scale, 1000))];
                int iStart = random.Next(0, count - 1);
                double[] fractions = [0, 0.25, 0.5, 0.75, 1.0];

                Vector2 p0 = iStart - 1 >= 0 ? points[iStart - 1] : CatmullRom.GetStartingPointForOpenCurve(points);
                Vector2 p1 = points[iStart];
                Vector2 p2 = points[iStart + 1];
                Vector2 p3 = iStart + 2 < count ? points[iStart + 2] : CatmullRom.GetEndingPointForOpenCurve(points);

                AssertSameCurve(
                    () => Phase7ReferenceImplementations.FitCurveSegment(in p0, in p1, in p2, in p3, fractions),
                    () => CatmullRom.FitCurveSegment(points, iStart, fractions),
                    $"rep {rep} start {iStart} of {count}");
            }
        }

        [TestMethod]
        public void RecursivelyFitCurveSegmentMatchesReference()
        {
            Random random = new(34);
            foreach (double scale in Scales)
            {
                for (int variant = 0; variant <= 5; variant++)
                {
                    for (int rep = 0; rep < 8; rep++)
                    {
                        Vector2[] p = ControlPoints(random, variant, scale, rep % 2 == 0 ? 0 : 400000);
                        foreach (uint count in new uint[] { 0, 1, 2, 3, 5, 8, 12 })
                        {
                            uint n = count;
                            AssertSameCurve(
                                () => Phase7ReferenceImplementations.RecursivelyFitCurveSegment(in p[0], in p[1], in p[2], in p[3], n),
                                () => CatmullRom.RecursivelyFitCurveSegment(in p[0], in p[1], in p[2], in p[3], n),
                                $"scale {scale} variant {variant} rep {rep} count {n}");
                        }
                    }
                }
            }
        }

        /// <summary>
        /// The caller's t-point set is mutated as the recursion adds points; the set left behind has to match too.
        /// </summary>
        [TestMethod]
        public void RecursivelyFitCurveSegmentLeavesTheSameTPoints()
        {
            Random random = new(35);
            for (int rep = 0; rep < 80; rep++)
            {
                double scale = Scales[rep % Scales.Length];
                Vector2[] p = ControlPoints(random, rep % 6, scale, 1000);
                int length = random.Next(2, 12);
                SortedSet<double> seed = [.. Enumerable.Range(0, length).Select(i => (double)i / (length - 1))];
                SortedSet<double> expectedSet = new(seed);
                SortedSet<double> actualSet = new(seed);

                AssertSameCurve(
                    () => Phase7ReferenceImplementations.RecursivelyFitCurveSegment(in p[0], in p[1], in p[2], in p[3], expectedSet),
                    () => CatmullRom.RecursivelyFitCurveSegment(in p[0], in p[1], in p[2], in p[3], actualSet),
                    $"rep {rep}");

                Phase7MeshCurveTests.AssertBitIdentical(expectedSet.ToArray(), actualSet.ToArray(), $"t points rep {rep}");
            }
        }

        [TestMethod]
        public void TryAddTPointsAboveThresholdMatchesReference()
        {
            Random random = new(36);
            for (int rep = 0; rep < 300; rep++)
            {
                double scale = Scales[rep % Scales.Length];
                int length = random.Next(1, 40);
                Vector2[] output = new Vector2[length];
                Vector2 position = RandomPoint(random, scale, 0);
                for (int i = 0; i < length; i++)
                {
                    double roll = random.NextDouble();
                    if (roll < 0.15 && i > 0)
                        position += new Vector2((random.NextDouble() - 0.5) * 1e-4, (random.NextDouble() - 0.5) * 1e-4);
                    else if (roll < 0.30)
                        position = RandomPoint(random, scale, 0);
                    else
                        position += new Vector2((random.NextDouble() - 0.3) * scale * 0.1, (random.NextDouble() - 0.5) * scale * 0.1);
                    output[i] = position;
                }

                SortedSet<double> seed = [.. Enumerable.Range(0, length).Select(i => (double)i / Math.Max(1, length - 1) + (i * 1e-9))];
                double threshold = rep % 3 == 0 ? 10.0 : (rep % 3 == 1 ? 1.0 : 45.0);

                SortedSet<double> expectedSet = new(seed);
                SortedSet<double> actualSet = new(seed);
                bool expectedThrew = false, actualThrew = false;
                bool expectedResult = false, actualResult = false;
                string expectedName = null, actualName = null;
                try { expectedResult = Phase7ReferenceImplementations.TryAddTPointsAboveThreshold([.. output], ref expectedSet, threshold); }
                catch (Exception ex) { expectedThrew = true; expectedName = ex.GetType().Name; }
                try { actualResult = CurveExtensions.TryAddTPointsAboveThreshold([.. output], ref actualSet, threshold); }
                catch (Exception ex) { actualThrew = true; actualName = ex.GetType().Name; }

                Assert.AreEqual(expectedThrew, actualThrew, $"rep {rep}: threw");
                Assert.AreEqual(expectedName, actualName, $"rep {rep}: exception");
                if (expectedThrew)
                    continue;

                Assert.AreEqual(expectedResult, actualResult, $"rep {rep}: result");
                Phase7MeshCurveTests.AssertBitIdentical(expectedSet.ToArray(), actualSet.ToArray(), $"rep {rep}: t points");
            }
        }

        [TestMethod]
        public void ApplyKernelMatchesReference()
        {
            double[][] kernels =
            [
                [1.0],
                [0.25, 0.5, 0.25],
                [0.0625, 0.25, 0.375, 0.25, 0.0625],
                [0.125, 0.125, 0.125, 0.25, 0.125, 0.125, 0.125],
            ];

            Random random = new(37);
            foreach (double[] kernel in kernels)
            {
                for (int rep = 0; rep < 60; rep++)
                {
                    int length = random.Next(0, 50);
                    double scale = Scales[rep % Scales.Length];
                    double[] values = new double[length];
                    for (int i = 0; i < length; i++)
                        values[i] = (random.NextDouble() - 0.5) * scale;
                    if (length > 3 && rep % 4 == 0)
                        values[length / 2] = -0.0;

                    AssertSame(
                        () => Phase7ReferenceImplementations.ApplyKernel(values, kernel),
                        () => values.ApplyKernel(kernel),
                        (e, a) => Phase7MeshCurveTests.AssertBitIdentical(e, a, $"kernel {kernel.Length} rep {rep} length {length}"),
                        $"kernel {kernel.Length} rep {rep} length {length}");
                }
            }
        }

        [TestMethod]
        public void GaussianSmoothingMatchesReference()
        {
            Random random = new(38);
            for (int rep = 0; rep < 80; rep++)
            {
                int length = random.Next(0, 60);
                double scale = Scales[rep % Scales.Length];
                Vector2[] points = Enumerable.Range(0, length).Select(_ => RandomPoint(random, scale, rep % 2 == 0 ? 0 : 450000)).ToArray();
                AssertSame(
                    () => Phase7ReferenceImplementations.Gaussian(points),
                    () => Smoothing.Gaussian(points),
                    (e, a) => Phase7MeshCurveTests.AssertBitIdentical(e, a, $"rep {rep} length {length}"),
                    $"rep {rep} length {length}");
            }
        }

        [TestMethod]
        public void DouglasPeuckerPreserveListMatchesReference()
        {
            Random random = new(39);
            for (int rep = 0; rep < 120; rep++)
            {
                int length = random.Next(3, 70);
                double scale = Scales[rep % Scales.Length];
                List<Vector2> path = [];
                Vector2 position = RandomPoint(random, scale, 0);
                for (int i = 0; i < length; i++)
                {
                    position += new Vector2(random.NextDouble() * scale * 0.05, (random.NextDouble() - 0.5) * scale * 0.05);
                    path.Add(position);
                }

                if (rep % 5 == 0 && length > 6)
                    path[length / 2] = path[2];

                List<Vector2> preserve = [];
                int preserveCount = random.Next(0, 12);
                for (int i = 0; i < preserveCount; i++)
                {
                    preserve.Add(random.NextDouble() < 0.7 ? path[random.Next(length)] : RandomPoint(random, scale, 0));
                }

                double tolerance = scale * 0.01;
                IList<Vector2> asList = path;
                IList<Vector2> asArray = path.ToArray();

                foreach (IList<Vector2> points in new[] { asList, asArray })
                {
                    int[] indices = Phase7ReferenceImplementations.PointsToPreserveIndices(points, preserve);
                    AssertSame(
                        () => points.DouglasPeuckerReduction(tolerance, indices),
                        () => points.DouglasPeuckerReduction(tolerance, preserve),
                        (e, a) => Phase7MeshCurveTests.AssertBitIdentical(e, a, $"rep {rep} {points.GetType().Name}"),
                        $"rep {rep} {points.GetType().Name}");
                }
            }
        }

        [TestMethod]
        public void DouglasPeuckerPreserveListIsReadLazilyLikeBefore()
        {
            List<Vector2> two = [new(0, 0), new(1, 1)];
            List<Vector2> preserve = [new(5, 5)];
            Assert.AreEqual(2, two.DouglasPeuckerReduction(1.0, preserve).Count);
            Assert.ThrowsException<ArgumentNullException>(() => two.DouglasPeuckerReduction(1.0, (ICollection<Vector2>)null));
        }
    }
}

