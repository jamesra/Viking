using FsCheck;
using Geometry;
using GeometryTests.FSCheck;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using System;
using System.Collections.Generic;
using System.Linq;

namespace GeometryTests
{
    [TestClass]
    public class RibbonTangentSpec
    {
        const double TwoPi = Math.PI * 2.0;

        [TestMethod]
        public void UnwrapAngleIsShortestArcAndCongruentToRaw() =>
            CoreCheck.Run(
                Prop.ForAll(ArbAngle(), ArbAngle(), (previous, raw) =>
                {
                    double unwrapped = Vector2.UnwrapAngle(previous, raw);
                    return Math.Abs(unwrapped - previous) <= Math.PI + 1e-12 &&
                           AnglesCongruent(unwrapped, raw);
                }),
                nameof(UnwrapAngleIsShortestArcAndCongruentToRaw));

        [TestMethod]
        public void UnwrapAngleIsIdempotentForTheSamePrevious() =>
            CoreCheck.Run(
                Prop.ForAll(ArbAngle(), ArbAngle(), (previous, raw) =>
                {
                    double once = Vector2.UnwrapAngle(previous, raw);
                    double twice = Vector2.UnwrapAngle(previous, once);
                    return Tolerance.AreClose(once, twice);
                }),
                nameof(UnwrapAngleIsIdempotentForTheSamePrevious));

        [TestMethod]
        public void UnwrapAndLimitTangentsClampsEveryAdjacentStep() =>
            CoreCheck.Run(
                Prop.ForAll(ArbTangentSeries(), ArbPositiveDelta(), ArbClosed(), (tangents, maxDelta, closed) =>
                {
                    double[] copy = (double[])tangents.Clone();
                    Vector2.UnwrapAndLimitTangents(copy, closed, maxDelta);
                    return AdjacentStepsWithinLimit(copy, maxDelta);
                }),
                nameof(UnwrapAndLimitTangentsClampsEveryAdjacentStep));

        [TestMethod]
        public void UnwrapAndLimitTangentsPreservesLength() =>
            CoreCheck.Run(
                Prop.ForAll(ArbTangentSeries(), ArbPositiveDelta(), ArbClosed(), (tangents, maxDelta, closed) =>
                {
                    int length = tangents.Length;
                    Vector2.UnwrapAndLimitTangents(tangents, closed, maxDelta);
                    return tangents.Length == length;
                }),
                nameof(UnwrapAndLimitTangentsPreservesLength));

        [TestMethod]
        public void CalculateRibbonTangentsMatchesInputLengthAndLimit() =>
            CoreCheck.Run(
                Prop.ForAll(ArbPolyline(), ArbClosed(), ArbPositiveDelta(), (points, closed, maxDelta) =>
                {
                    double[] tangents = Vector2.CalculateRibbonTangents(points, closed, maxDelta);
                    return tangents.Length == points.Length && AdjacentStepsWithinLimit(tangents, maxDelta);
                }),
                nameof(CalculateRibbonTangentsMatchesInputLengthAndLimit));

        [TestMethod]
        public void CalculateRibbonTangentsLeavesFirstSampleUnchangedBeforeLimit() =>
            CoreCheck.Run(
                Prop.ForAll(ArbOpenPolyline(), points =>
                {
                    if (points.Length < 2)
                        return true;

                    double[] tangents = Vector2.CalculateRibbonTangents(points, closed: false);
                    double expectedFirst = Vector2.Angle(points[0], points[1]);
                    return Tolerance.AreClose(tangents[0], expectedFirst);
                }),
                nameof(CalculateRibbonTangentsLeavesFirstSampleUnchangedBeforeLimit));

        [TestMethod]
        public void ClosedConvexRingTangentsStayWithinDefaultLimit() =>
            CoreCheck.Run(
                Prop.ForAll(CoreArbitraries.ArbConvexPolygon(), polygon =>
                {
                    Vector2[] ring = polygon.ExteriorRing;
                    double[] tangents = Vector2.CalculateRibbonTangents(ring, closed: true);
                    return tangents.Length == ring.Length &&
                           AdjacentStepsWithinLimit(tangents, Vector2.DefaultMaxRibbonTangentDelta);
                }),
                nameof(ClosedConvexRingTangentsStayWithinDefaultLimit));

        [TestMethod]
        public void RibbonTangentsClampSquareCornersBelowMiterLimit()
        {
            Vector2[] square =
            [
                new(0, 0),
                new(10, 0),
                new(10, 10),
                new(0, 10),
                new(0, 0)
            ];

            double[] tangents = Vector2.CalculateRibbonTangents(square, closed: true);
            Assert.IsTrue(AdjacentStepsWithinLimit(tangents, Vector2.DefaultMaxRibbonTangentDelta));
        }

        [TestMethod]
        public void RibbonTangentsUnwrapCircleMonotonically()
        {
            double[] tangents = CircleTangents(32, clockwise: false);
            Assert.IsTrue(AdjacentStepsWithinLimit(tangents, Vector2.DefaultMaxRibbonTangentDelta));
            for (int i = 1; i < tangents.Length; i++)
            {
                Assert.IsTrue(tangents[i] > tangents[i - 1],
                    $"CCW circle tangent[{i}]={tangents[i]} should increase from {tangents[i - 1]} after unwrap");
            }
        }

        [TestMethod]
        public void RibbonTangentsUnwrapClockwiseCircleMonotonically()
        {
            double[] tangents = CircleTangents(32, clockwise: true);
            Assert.IsTrue(AdjacentStepsWithinLimit(tangents, Vector2.DefaultMaxRibbonTangentDelta));
            for (int i = 1; i < tangents.Length; i++)
            {
                Assert.IsTrue(tangents[i] < tangents[i - 1],
                    $"CW circle tangent[{i}]={tangents[i]} should decrease from {tangents[i - 1]} after unwrap");
            }
        }

        [TestMethod]
        public void OpenPolylineTangentsStayWithinLimit()
        {
            Vector2[] line =
            [
                new(0, 0),
                new(10, 0),
                new(10, 10),
                new(20, 10)
            ];

            double[] tangents = Vector2.CalculateRibbonTangents(line, closed: false);
            Assert.AreEqual(line.Length, tangents.Length);
            Assert.IsTrue(AdjacentStepsWithinLimit(tangents, Vector2.DefaultMaxRibbonTangentDelta));
            Assert.IsTrue(Tolerance.AreClose(tangents[0], 0));
        }

        [TestMethod]
        public void StraightOpenLineKeepsAConstantTangent()
        {
            Vector2[] line = [new(0, 0), new(5, 0), new(11, 0), new(20, 0)];
            double[] tangents = Vector2.CalculateRibbonTangents(line, closed: false);
            Assert.IsTrue(tangents.All(t => Tolerance.AreClose(t, 0)));
        }

        [TestMethod]
        public void CalculateRibbonTangentsEmptyAndSingleton()
        {
            CollectionAssert.AreEqual(Array.Empty<double>(), Vector2.CalculateRibbonTangents([], closed: false));
            CollectionAssert.AreEqual(new[] { 0.0 }, Vector2.CalculateRibbonTangents([new Vector2(3, 4)], closed: true));
        }

        [TestMethod]
        public void UnwrapAndLimitTangentsShortSeriesIsNoOp()
        {
            double[] empty = [];
            Vector2.UnwrapAndLimitTangents(empty, closed: true, 0.5);
            CollectionAssert.AreEqual(Array.Empty<double>(), empty);

            double[] one = [1.25];
            Vector2.UnwrapAndLimitTangents(one, closed: false, 0.5);
            Assert.AreEqual(1.25, one[0]);
        }

        [TestMethod]
        public void UnwrapAndLimitTangentsClampsALargeStep()
        {
            double[] tangents = [0, 2];
            Vector2.UnwrapAndLimitTangents(tangents, closed: false, 0.4);
            Assert.AreEqual(0, tangents[0]);
            Assert.AreEqual(0.4, tangents[1], 1e-12);
        }

        [TestMethod]
        public void UnwrapAnglePullsAcrossTheBranchCut()
        {
            Assert.AreEqual(-0.2, Vector2.UnwrapAngle(0.1, Math.PI * 2.0 - 0.2), 1e-12);
            Assert.AreEqual(Math.PI * 2.0 + 0.2, Vector2.UnwrapAngle(Math.PI * 2.0 - 0.1, 0.2), 1e-12);
        }

        [TestMethod]
        public void CalculateRibbonTangentsNullPointsThrows() =>
            Assert.ThrowsException<ArgumentNullException>(() => Vector2.CalculateRibbonTangents(null, false));

        [TestMethod]
        public void UnwrapAndLimitTangentsNullOrNonPositiveDeltaThrows()
        {
            Assert.ThrowsException<ArgumentNullException>(() => Vector2.UnwrapAndLimitTangents(null, false, 0.5));
            Assert.ThrowsException<ArgumentOutOfRangeException>(() => Vector2.UnwrapAndLimitTangents([0, 1], false, 0));
            Assert.ThrowsException<ArgumentOutOfRangeException>(() => Vector2.UnwrapAndLimitTangents([0, 1], false, -1));
        }

        static double[] CircleTangents(int samples, bool clockwise)
        {
            Vector2[] circle = new Vector2[samples + 1];
            for (int i = 0; i < samples; i++)
            {
                double t = (clockwise ? -1 : 1) * TwoPi * i / samples;
                circle[i] = new Vector2(Math.Cos(t), Math.Sin(t));
            }

            circle[samples] = circle[0];
            return Vector2.CalculateRibbonTangents(circle, closed: true);
        }

        static bool AdjacentStepsWithinLimit(IReadOnlyList<double> tangents, double maxDelta)
        {
            for (int i = 1; i < tangents.Count; i++)
            {
                if (Math.Abs(tangents[i] - tangents[i - 1]) > maxDelta + 1e-9)
                    return false;
            }

            return true;
        }

        static bool AnglesCongruent(double a, double b)
        {
            double turns = (a - b) / TwoPi;
            return Math.Abs(turns - Math.Round(turns)) < 1e-9;
        }

        static Arbitrary<double> ArbAngle() =>
            Arb.From(Gen.Choose(-2000, 2000).Select(i => i / 100.0));

        static Arbitrary<double> ArbPositiveDelta() =>
            Arb.From(Gen.Choose(1, 314).Select(i => i / 100.0));

        static Arbitrary<bool> ArbClosed() => Arb.From(Arb.Default.Bool().Generator);

        static Arbitrary<double[]> ArbTangentSeries() =>
            Arb.From(
                from n in Gen.Choose(2, 16)
                from values in ArbAngle().Generator.ArrayOf(n)
                select values);

        static Arbitrary<Vector2[]> ArbPolyline() =>
            Arb.From(Gen.OneOf(GenOpenPolyline(), GenClosedRing()));

        static Arbitrary<Vector2[]> ArbOpenPolyline() => Arb.From(GenOpenPolyline());

        static Gen<Vector2[]> GenOpenPolyline() =>
            from n in Gen.Choose(2, 10)
            from points in CoreArbitraries.FiniteVector2().ArrayOf(n)
            select points;

        static Gen<Vector2[]> GenClosedRing() =>
            from n in Gen.Choose(3, 10)
            from points in CoreArbitraries.FiniteVector2().ArrayOf(n)
            select points.EnsureClosedRing();
    }
}
