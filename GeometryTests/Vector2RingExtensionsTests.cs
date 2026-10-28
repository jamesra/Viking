using FsCheck;
using Geometry;
using GeometryTests.FSCheck;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using System;
using System.Collections.Generic;
using System.Linq;

namespace GeometryTests
{
    /// <summary>
    /// Pins the point-list cleanup and ring helpers in <see cref="Vector2Extensions"/>:
    /// RemoveAdjacentDuplicates (the <see cref="IEnumerable{T}"/>, preserved-path and <see cref="ICollection{T}"/> overloads),
    /// RemoveDuplicates, EnsureOpenRing / EnsureClosedRing, PerimeterLength and the two minimum-distance helpers.
    /// SegmentationMaskPolygonizer and SegmentationEdgeSnap clean polygons with these, and CatmullRom.FitCurve, Lagrange
    /// and SqlGeometryUtils depend on the duplicate removal.
    /// Generated points sit on a small integer lattice so repeats are common, and are shifted to volume scale so the
    /// epsilon comparison (<see cref="Tolerance.Epsilon"/>) is exercised at real magnitudes; lattice neighbours are a
    /// whole pixel apart, so "equal" means exactly equal.
    /// </summary>
    [TestClass]
    public class Vector2RingExtensionsTests
    {
        /// <summary>Volume coordinates reach the hundreds of thousands of pixels.</summary>
        private const double VolumeOffset = 400_000;

        private static Vector2 P(double x, double y) => new(x, y);

        /// <summary>Slow reference: keep a point only when it differs from the point kept before it.</summary>
        private static List<Vector2> CollapseRuns(IEnumerable<Vector2> points)
        {
            List<Vector2> result = [];
            foreach (Vector2 p in points)
            {
                if (result.Count == 0 || result.Last() != p)
                    result.Add(p);
            }

            return result;
        }

        /// <summary>Lists of minCount to 20 points on a 3x3 lattice, shifted to volume scale; repeats are likely.</summary>
        private static Gen<Vector2[]> LatticePoints(int minCount) =>
            from n in Gen.Choose(minCount, 20)
            from points in (from x in Gen.Choose(0, 2) from y in Gen.Choose(0, 2) select P(VolumeOffset + x, VolumeOffset + y)).ArrayOf(n)
            select points;

        private static Property ForAllPoints(Func<Vector2[], Property> property, int minCount = 1) =>
            Prop.ForAll(Arb.From(LatticePoints(minCount)), property);

        #region RemoveAdjacentDuplicates

        [TestMethod]
        public void RemoveAdjacentDuplicates_CollapsesRunsAndKeepsOrder()
        {
            Vector2[] input = [P(0, 0), P(0, 0), P(1, 0), P(1, 0), P(1, 0), P(2, 0), P(0, 0), P(0, 0)];
            Vector2[] expected = [P(0, 0), P(1, 0), P(2, 0), P(0, 0)];

            CollectionAssert.AreEqual(expected, input.AsEnumerable().RemoveAdjacentDuplicates());
            CollectionAssert.AreEqual(expected, input.ToList().RemoveAdjacentDuplicates());
        }

        [TestMethod]
        public void RemoveAdjacentDuplicates_DoesNotRemoveRepeatsThatAreNotAdjacent()
        {
            Vector2[] ring = [P(0, 0), P(1, 0), P(0, 0), P(1, 0)];

            CollectionAssert.AreEqual(ring, ring.AsEnumerable().RemoveAdjacentDuplicates());
            CollectionAssert.AreEqual(ring, ring.ToList().RemoveAdjacentDuplicates());
        }

        [TestMethod]
        public void RemoveAdjacentDuplicates_SinglePointAndAllEqualKeepOnePoint()
        {
            Vector2[] single = [P(5, 6)];
            Vector2[] allEqual = [P(5, 6), P(5, 6), P(5, 6)];

            CollectionAssert.AreEqual(single, single.AsEnumerable().RemoveAdjacentDuplicates());
            CollectionAssert.AreEqual(single, single.ToList().RemoveAdjacentDuplicates());
            CollectionAssert.AreEqual(single, allEqual.AsEnumerable().RemoveAdjacentDuplicates());
            CollectionAssert.AreEqual(single, allEqual.ToList().RemoveAdjacentDuplicates());
        }

        [TestMethod]
        public void RemoveAdjacentDuplicates_EmptyInputThrows()
        {
            Assert.ThrowsException<ArgumentException>(() => Array.Empty<Vector2>().AsEnumerable().RemoveAdjacentDuplicates());
            Assert.ThrowsException<ArgumentException>(() => Array.Empty<Vector2>().AsEnumerable().RemoveAdjacentDuplicates([P(0, 0)]));
            Assert.ThrowsException<InvalidOperationException>(() => new List<Vector2>().RemoveAdjacentDuplicates());
        }

        /// <summary>
        /// A step smaller than <see cref="Tolerance.Epsilon"/> compares equal to its neighbour, so a point within epsilon of
        /// the point before it is dropped.
        /// </summary>
        [TestMethod]
        public void RemoveAdjacentDuplicates_PointsWithinEpsilonAreDuplicates()
        {
            Vector2 a = P(10, 10);
            Vector2 nearA = P(10 + (Tolerance.Epsilon / 2), 10);
            Vector2 b = P(20, 10);

            List<Vector2> result = new Vector2[] { a, nearA, b }.AsEnumerable().RemoveAdjacentDuplicates();
            Assert.AreEqual(2, result.Count);
            Assert.AreEqual(a, result[0]);
            Assert.AreEqual(b, result[1]);

            List<Vector2> collectionResult = new List<Vector2> { a, nearA, b }.RemoveAdjacentDuplicates();
            Assert.AreEqual(2, collectionResult.Count);
            Assert.AreEqual(b, collectionResult[1]);
        }

        [TestMethod]
        public void RemoveAdjacentDuplicates_OverloadsAgreeWithRunCollapse() =>
            CoreCheck.Run(
                ForAllPoints(points =>
                {
                    List<Vector2> expected = CollapseRuns(points);
                    bool enumerableAgrees = points.AsEnumerable().RemoveAdjacentDuplicates().SequenceEqual(expected);
                    bool collectionAgrees = points.ToList().RemoveAdjacentDuplicates().SequenceEqual(expected);
                    return enumerableAgrees.Label("IEnumerable overload").And(collectionAgrees.Label("ICollection overload"));
                }),
                nameof(RemoveAdjacentDuplicates_OverloadsAgreeWithRunCollapse));

        [TestMethod]
        public void RemoveAdjacentDuplicates_NoAdjacentRepeatsEndpointsKeptAndIdempotent() =>
            CoreCheck.Run(
                ForAllPoints(points =>
                {
                    List<Vector2> cleaned = points.ToList().RemoveAdjacentDuplicates();
                    bool noAdjacentRepeat = cleaned.Zip(cleaned.Skip(1), (a, b) => a != b).All(different => different);
                    bool endpoints = cleaned[0] == points[0] && cleaned.Last() == points.Last();
                    bool idempotent = cleaned.RemoveAdjacentDuplicates().SequenceEqual(cleaned);
                    return noAdjacentRepeat.Label("no adjacent repeat")
                        .And(endpoints.Label("first and last preserved"))
                        .And(idempotent.Label("idempotent"));
                }),
                nameof(RemoveAdjacentDuplicates_NoAdjacentRepeatsEndpointsKeptAndIdempotent));

        #endregion

        #region RemoveAdjacentDuplicates with a preserved path

        [TestMethod]
        public void RemoveAdjacentDuplicatesPreserved_MatchesPlainOverloadWhenNothingIsPreserved()
        {
            Vector2[] input = [P(0, 0), P(0, 0), P(1, 0), P(2, 0), P(2, 0), P(3, 0)];

            CollectionAssert.AreEqual(
                input.AsEnumerable().RemoveAdjacentDuplicates().ToList(),
                input.AsEnumerable().RemoveAdjacentDuplicates(Array.Empty<Vector2>()));
        }

        [TestMethod]
        public void RemoveAdjacentDuplicatesPreserved_NullPreservedPathThrows() =>
            Assert.ThrowsException<ArgumentNullException>(() => new[] { P(0, 0), P(1, 0) }.AsEnumerable().RemoveAdjacentDuplicates(null!));

        [TestMethod]
        public void RemoveAdjacentDuplicatesPreserved_PreservedPointInsideARunSurvivesOnce()
        {
            Vector2[] input = [P(0, 0), P(1, 0), P(1, 0), P(1, 0), P(2, 0), P(3, 0)];

            List<Vector2> result = input.AsEnumerable().RemoveAdjacentDuplicates([P(1, 0)]);

            CollectionAssert.AreEqual(new[] { P(0, 0), P(1, 0), P(2, 0), P(3, 0) }, result);
        }

        [TestMethod]
        public void RemoveAdjacentDuplicatesPreserved_PreservedPointStillPresentWhenOnlyNearlyEqual()
        {
            //The curve passes within epsilon of the control point, not exactly through it: the exact control point is written
            //once the path moves away, so the output always contains it.
            Vector2 control = P(1, 0);
            Vector2 nearControl = P(1 + (Tolerance.Epsilon / 2), 0);
            Vector2[] input = [P(0, 0), nearControl, P(2, 0), P(3, 0)];

            List<Vector2> result = input.AsEnumerable().RemoveAdjacentDuplicates([control]);

            Assert.IsTrue(result.Contains(control));
            Assert.AreEqual(P(0, 0), result[0]);
            Assert.AreEqual(P(3, 0), result.Last());
        }

        /// <summary>
        /// When the input ends with repeats of a preserved point, the inner skip loop runs off the end of the input and the
        /// method then appends its NaN sentinel as the "last point": [(0,0), (1,0), (1,0)] with (1,0) preserved returns
        /// [(0,0), (1,0), (NaN,NaN)]. Recorded in the improvement ledger as a deferred candidate, not fixed here.
        /// </summary>
        [TestMethod]
        [Ignore("Defect: trailing repeats of a preserved point append a NaN point to the result.")]
        public void RemoveAdjacentDuplicatesPreserved_TrailingRepeatsOfPreservedPointDoNotAddNaN()
        {
            Vector2[] input = [P(0, 0), P(1, 0), P(1, 0)];

            List<Vector2> result = input.AsEnumerable().RemoveAdjacentDuplicates([P(1, 0)]);

            CollectionAssert.AreEqual(new[] { P(0, 0), P(1, 0) }, result);
        }

        /// <summary>
        /// Points without repeats pass through untouched, whichever of them are preserved, because the preserved-path logic
        /// only changes the handling of repeats. Preserved points are a subsequence of the input, as CatmullRom.FitCurve passes them.
        /// </summary>
        [TestMethod]
        public void RemoveAdjacentDuplicatesPreserved_DistinctPointsPassThroughWithPreservedSubsequence() =>
            CoreCheck.Run(
                Prop.ForAll(Arb.From(
                    from n in Gen.Choose(1, 15)
                    from keep in Gen.ListOf(n, Gen.Elements(true, false))
                    select (n, keep: keep.ToArray())),
                    spec =>
                    {
                        Vector2[] points = [.. Enumerable.Range(0, spec.n).Select(i => P(VolumeOffset + i, VolumeOffset - (2 * i)))];
                        Vector2[] preserved = [.. points.Where((p, i) => spec.keep[i])];

                        List<Vector2> result = points.AsEnumerable().RemoveAdjacentDuplicates(preserved);
                        return result.SequenceEqual(points).Label($"result {result.Count} of {points.Length} points");
                    }),
                nameof(RemoveAdjacentDuplicatesPreserved_DistinctPointsPassThroughWithPreservedSubsequence));

        #endregion

        #region RemoveDuplicates

        [TestMethod]
        public void RemoveDuplicates_RemovesRepeatsAnywhereKeepingFirstOccurrenceOrder()
        {
            Vector2[] input = [P(2, 0), P(1, 0), P(2, 0), P(0, 0), P(1, 0), P(2, 0)];

            CollectionAssert.AreEqual(new[] { P(2, 0), P(1, 0), P(0, 0) }, input.RemoveDuplicates());
        }

        [TestMethod]
        public void RemoveDuplicates_EmptyStaysEmpty() => Assert.AreEqual(0, Array.Empty<Vector2>().RemoveDuplicates().Length);

        [TestMethod]
        public void RemoveDuplicates_ResultHasNoRepeatsAndSameDistinctSet() =>
            CoreCheck.Run(
                ForAllPoints(points =>
                {
                    Vector2[] result = points.RemoveDuplicates();
                    bool noRepeats = result.Select((p, i) => result.Skip(i + 1).All(q => q != p)).All(unique => unique);
                    bool sameSet = points.All(p => result.Contains(p)) && result.All(p => points.Contains(p));
                    bool firstOccurrenceOrder = result.SequenceEqual(points.Where((p, i) => points.Take(i).All(q => q != p)));
                    return noRepeats.Label("no repeats").And(sameSet.Label("same set")).And(firstOccurrenceOrder.Label("first-occurrence order"));
                }),
                nameof(RemoveDuplicates_ResultHasNoRepeatsAndSameDistinctSet));

        #endregion

        #region EnsureOpenRing / EnsureClosedRing

        [TestMethod]
        public void EnsureClosedRing_AppendsFirstPointOnlyWhenOpen()
        {
            Vector2[] open = [P(0, 0), P(4, 0), P(4, 4)];
            Vector2[] closed = [P(0, 0), P(4, 0), P(4, 4), P(0, 0)];

            CollectionAssert.AreEqual(closed, open.EnsureClosedRing());
            CollectionAssert.AreEqual(closed, ((ICollection<Vector2>)open.ToList()).EnsureClosedRing().ToArray());
            CollectionAssert.AreEqual(closed, closed.EnsureClosedRing());
            CollectionAssert.AreEqual(closed, ((ICollection<Vector2>)closed.ToList()).EnsureClosedRing().ToArray());
            CollectionAssert.AreEqual(new[] { 1, 2, 3, 1 }, new List<int> { 1, 2, 3 }.EnsureClosedRing().ToArray());
            CollectionAssert.AreEqual(new[] { 1, 2, 1 }, new List<int> { 1, 2, 1 }.EnsureClosedRing().ToArray());
        }

        [TestMethod]
        public void EnsureClosedRing_DoesNotModifyItsInput()
        {
            Vector2[] open = [P(0, 0), P(4, 0), P(4, 4)];

            Vector2[] closed = open.EnsureClosedRing();

            Assert.AreEqual(3, open.Length);
            Assert.AreNotSame(open, closed);
            Assert.AreSame(closed, closed.EnsureClosedRing(), "a ring that is already closed is returned as is");
        }

        [TestMethod]
        public void EnsureClosedRing_EmptyInputThrows()
        {
            Assert.ThrowsException<InvalidOperationException>(() => Array.Empty<Vector2>().EnsureClosedRing());
            Assert.ThrowsException<InvalidOperationException>(() => ((ICollection<Vector2>)new List<Vector2>()).EnsureClosedRing());
        }

        [TestMethod]
        public void EnsureOpenRing_DropsClosingPointOnlyWhenClosed()
        {
            Vector2[] open = [P(0, 0), P(4, 0), P(4, 4)];
            Vector2[] closed = [P(0, 0), P(4, 0), P(4, 4), P(0, 0)];

            CollectionAssert.AreEqual(open, closed.EnsureOpenRing());
            CollectionAssert.AreEqual(open, ((ICollection<Vector2>)closed.ToList()).EnsureOpenRing().ToArray());
            CollectionAssert.AreEqual(open, open.EnsureOpenRing());
            CollectionAssert.AreEqual(open, ((ICollection<Vector2>)open.ToList()).EnsureOpenRing().ToArray());
        }

        [TestMethod]
        public void EnsureOpenRing_ZeroOrOnePointIsReturnedUnchanged()
        {
            Assert.AreEqual(0, Array.Empty<Vector2>().EnsureOpenRing().Length);
            Assert.AreEqual(0, ((ICollection<Vector2>)new List<Vector2>()).EnsureOpenRing().Count);
            CollectionAssert.AreEqual(new[] { P(3, 3) }, new[] { P(3, 3) }.EnsureOpenRing());
            CollectionAssert.AreEqual(new[] { P(3, 3) }, ((ICollection<Vector2>)new List<Vector2> { P(3, 3) }).EnsureOpenRing().ToArray());
        }

        [TestMethod]
        public void EnsureOpenRing_TwoCoincidentPointsLeaveOne() =>
            CollectionAssert.AreEqual(new[] { P(3, 3) }, new[] { P(3, 3), P(3, 3) }.EnsureOpenRing());

        [TestMethod]
        public void EnsureRing_ArrayAndCollectionOverloadsAgreeAndCloseWithMatchingEnds() =>
            CoreCheck.Run(
                ForAllPoints(points =>
                {
                    Vector2[] opened = points.EnsureOpenRing();
                    Vector2[] closed = points.EnsureClosedRing();
                    ICollection<Vector2> openedCollection = points.ToList().EnsureOpenRing();
                    ICollection<Vector2> closedCollection = ((ICollection<Vector2>)points.ToList()).EnsureClosedRing();

                    bool closedEnds = closed[0] == closed.Last();
                    bool closedAddsAtMostOne = closed.Length == points.Length || (closed.Length == points.Length + 1 && closed.Take(points.Length).SequenceEqual(points));
                    bool openDropsAtMostOne = opened.Length == points.Length || (points.Length >= 2 && opened.Length == points.Length - 1 && points[0] == points.Last());
                    bool arraysAgreeWithCollections = openedCollection.SequenceEqual(opened) && closedCollection.SequenceEqual(closed);
                    bool closedIsIdempotent = closed.EnsureClosedRing().SequenceEqual(closed);
                    return closedEnds.Label("closed ring ends match")
                        .And(closedAddsAtMostOne.Label("closing adds at most the first point"))
                        .And(openDropsAtMostOne.Label("opening drops at most the closing point"))
                        .And(arraysAgreeWithCollections.Label("array and collection overloads agree"))
                        .And(closedIsIdempotent.Label("closing twice changes nothing"));
                }),
                nameof(EnsureRing_ArrayAndCollectionOverloadsAgreeAndCloseWithMatchingEnds));

        [TestMethod]
        public void EnsureRing_CloseThenOpenRestoresAnyOpenRing() =>
            CoreCheck.Run(
                ForAllPoints(points =>
                {
                    Vector2[] open = points.ToList().RemoveAdjacentDuplicates().ToArray().EnsureOpenRing();
                    if (open.Length < 2)
                        return true.ToProperty();

                    Vector2[] roundTrip = open.EnsureClosedRing().EnsureOpenRing();
                    return roundTrip.SequenceEqual(open).Label($"open {open.Length} points, round trip {roundTrip.Length}");
                }, minCount: 2),
                nameof(EnsureRing_CloseThenOpenRestoresAnyOpenRing));

        #endregion

        #region PerimeterLength

        [TestMethod]
        public void PerimeterLength_UnitSquareIsFourWhetherOpenOrClosed()
        {
            Vector2[] open = [P(0, 0), P(1, 0), P(1, 1), P(0, 1)];

            Assert.AreEqual(4.0, open.PerimeterLength(), 1e-12);
            Assert.AreEqual(4.0, open.EnsureClosedRing().PerimeterLength(), 1e-12);
        }

        [TestMethod]
        public void PerimeterLength_ThreeFourFiveTriangleIsTwelve() =>
            Assert.AreEqual(12.0, new[] { P(0, 0), P(3, 0), P(3, 4) }.PerimeterLength(), 1e-12);

        [TestMethod]
        public void PerimeterLength_SinglePointIsZeroAndEmptyThrows()
        {
            Assert.AreEqual(0.0, new[] { P(9, 9) }.PerimeterLength());
            Assert.ThrowsException<InvalidOperationException>(() => Array.Empty<Vector2>().PerimeterLength());
        }

        [TestMethod]
        public void PerimeterLength_TwoPointsCountTheEdgeBothWays() =>
            Assert.AreEqual(10.0, new[] { P(0, 0), P(3, 4) }.PerimeterLength(), 1e-12);

        /// <summary>
        /// The perimeter of an axis-aligned volume-scale rectangle is exact in <c>double</c>: edge lengths are whole pixels, so the
        /// sum of four distances must equal 2(w+h) with no single-precision error.
        /// </summary>
        [TestMethod]
        public void PerimeterLength_RectangleAtVolumeScaleIsExact() =>
            CoreCheck.Run(
                Prop.ForAll(Arb.From(
                    from x in Gen.Choose(-500_000, 500_000)
                    from y in Gen.Choose(-500_000, 500_000)
                    from w in Gen.Choose(1, 100_000)
                    from h in Gen.Choose(1, 100_000)
                    select (x, y, w, h)),
                    r =>
                    {
                        Vector2[] open = [P(r.x, r.y), P(r.x + r.w, r.y), P(r.x + r.w, r.y + r.h), P(r.x, r.y + r.h)];
                        double expected = 2.0 * (r.w + r.h);
                        return (open.PerimeterLength() == expected).Label($"open {open.PerimeterLength()} vs {expected}")
                            .And((open.EnsureClosedRing().PerimeterLength() == expected).Label("closed"));
                    }),
                nameof(PerimeterLength_RectangleAtVolumeScaleIsExact));

        [TestMethod]
        public void PerimeterLength_OpenAndClosedRingsAgree() =>
            CoreCheck.Run(
                ForAllPoints(points =>
                {
                    Vector2[] open = points.EnsureOpenRing();
                    double reference = open.Length < 2 ? 0 : open.Zip(open.Skip(1).Append(open[0]), (a, b) => Vector2.Distance(a, b)).Sum();
                    double fromOpen = open.PerimeterLength();
                    double fromClosed = open.EnsureClosedRing().PerimeterLength();
                    return (fromOpen == fromClosed).Label("open equals closed")
                        .And((Math.Abs(fromOpen - reference) <= 1e-6).Label($"matches reference {reference}, got {fromOpen}"));
                }),
                nameof(PerimeterLength_OpenAndClosedRingsAgree));

        #endregion

        #region MinDistanceBetween*

        [TestMethod]
        public void MinDistanceBetweenAnyPoints_FindsClosestPairAnywhereInTheList()
        {
            Vector2[] points = [P(0, 0), P(10, 0), P(20, 0), P(10, 3)];

            Assert.AreEqual(3.0, points.MinDistanceBetweenAnyPoints(), 1e-12);
        }

        [TestMethod]
        public void MinDistanceBetweenAnyPoints_IgnoresCoincidentPoints() =>
            Assert.AreEqual(5.0, new[] { P(0, 0), P(0, 0), P(3, 4) }.MinDistanceBetweenAnyPoints(), 1e-12);

        [TestMethod]
        public void MinDistanceBetweenAnyPoints_NoDistinctPairReturnsMaxValue()
        {
            Assert.AreEqual(double.MaxValue, Array.Empty<Vector2>().MinDistanceBetweenAnyPoints());
            Assert.AreEqual(double.MaxValue, new[] { P(1, 1) }.MinDistanceBetweenAnyPoints());
            Assert.AreEqual(double.MaxValue, new[] { P(1, 1), P(1, 1) }.MinDistanceBetweenAnyPoints());
        }

        [TestMethod]
        public void MinDistanceBetweenSequentialPoints_ReportsSmallestStep()
        {
            Vector2[] points = [P(0, 0), P(10, 0), P(10, 2), P(30, 2)];

            Assert.AreEqual(2.0, points.MinDistanceBetweenSequentialPoints(out _), 1e-12);
        }

        /// <summary>
        /// The doc says FirstIndex locates the closest pair, but the method assigns it for every distinct step, so it ends
        /// as the last distinct step's index (2 here) rather than the smallest step's (1). The only call site is commented out,
        /// so the out parameter is unused; recorded in the improvement ledger as a deferred candidate, not fixed here.
        /// </summary>
        [TestMethod]
        [Ignore("Defect: FirstIndex is the last distinct step, not the smallest one (out parameter currently unused by any live caller).")]
        public void MinDistanceBetweenSequentialPoints_FirstIndexIsTheSmallestStep()
        {
            Vector2[] points = [P(0, 0), P(10, 0), P(10, 2), P(30, 2)];

            points.MinDistanceBetweenSequentialPoints(out int firstIndex);

            Assert.AreEqual(1, firstIndex);
        }

        [TestMethod]
        public void MinDistanceBetweenSequentialPoints_SkipsRepeatsAndIgnoresNonAdjacentCloseness()
        {
            //P(0,0) and P(1,0) are close but not adjacent; the repeated P(10,0) step has length zero and is skipped.
            Vector2[] points = [P(0, 0), P(10, 0), P(10, 0), P(1, 0)];

            Assert.AreEqual(9.0, points.MinDistanceBetweenSequentialPoints(out _), 1e-12);
        }

        [TestMethod]
        public void MinDistanceBetweenSequentialPoints_NoDistinctStepReturnsCountAndMaxValue()
        {
            Assert.AreEqual(double.MaxValue, new[] { P(1, 1), P(1, 1), P(1, 1) }.MinDistanceBetweenSequentialPoints(out int repeated));
            Assert.AreEqual(3, repeated);
            Assert.AreEqual(double.MaxValue, Array.Empty<Vector2>().MinDistanceBetweenSequentialPoints(out int empty));
            Assert.AreEqual(0, empty);
        }

        [TestMethod]
        public void MinDistanceBetweenPoints_MatchBruteForceReferences() =>
            CoreCheck.Run(
                ForAllPoints(points =>
                {
                    double any = points.MinDistanceBetweenAnyPoints();
                    double sequential = points.MinDistanceBetweenSequentialPoints(out _);
                    double referenceAny = points.SelectMany((a, i) => points.Skip(i + 1).Where(b => a != b).Select(b => Vector2.Distance(a, b)))
                        .DefaultIfEmpty(double.MaxValue).Min();
                    double[] steps = [.. points.Zip(points.Skip(1), (a, b) => a != b ? Vector2.Distance(a, b) : double.MaxValue)];
                    double referenceSequential = steps.Length == 0 ? double.MaxValue : steps.Min();
                    return (any == referenceAny).Label($"any {any} vs {referenceAny}")
                        .And((sequential == referenceSequential).Label($"sequential {sequential} vs {referenceSequential}"))
                        .And((sequential >= any).Label("sequential is never below the all-pairs minimum"));
                }),
                nameof(MinDistanceBetweenPoints_MatchBruteForceReferences));

        #endregion
    }
}
