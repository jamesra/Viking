using FsCheck;
using Geometry;
using GeometryTests.FSCheck;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using System;
using System.Collections.Generic;
using System.Linq;

namespace GeometryTests.Algorithms
{
    internal readonly struct BinaryMask(bool[] data, int width, int height)
    {
        public bool[] Data { get; } = data;
        public int Width { get; } = width;
        public int Height { get; } = height;

        public bool this[int x, int y] => Data[(y * Width) + x];
    }

    [TestClass]
    public class MarchingSquaresTests
    {
        [TestMethod]
        public void FindContours_SinglePixel_ReturnsClosedHalfPixelRing()
        {
            bool[] mask = new bool[9];
            mask[4] = true;

            IReadOnlyList<Vector2[]> contours = MarchingSquares.FindContours(mask, 3, 3);

            Assert.AreEqual(1, contours.Count);
            AssertContourInvariants(contours[0]);
            Assert.IsTrue(EvenOddContains(contours, new Vector2(1, 1)));
        }

        [TestMethod]
        public void FindContours_EmptyMask_ReturnsNoRings()
        {
            IReadOnlyList<Vector2[]> contours = MarchingSquares.FindContours(new bool[4], 2, 2);
            Assert.AreEqual(0, contours.Count);
        }

        [TestMethod]
        public void FindContours_FilledMask_ReturnsOneRingAroundEveryPixel()
        {
            bool[] mask = Enumerable.Repeat(true, 16).ToArray();
            IReadOnlyList<Vector2[]> contours = MarchingSquares.FindContours(mask, 4, 4);

            Assert.AreEqual(1, contours.Count);
            AssertContourInvariants(contours[0]);
            for (int y = 0; y < 4; y++)
            {
                for (int x = 0; x < 4; x++)
                    Assert.IsTrue(EvenOddContains(contours, new Vector2(x, y)), $"({x},{y})");
            }
        }

        [TestMethod]
        public void FindContours_TwoDisconnectedPixels_TwoRings()
        {
            bool[] mask = new bool[16];
            mask[0] = true;
            mask[15] = true;

            IReadOnlyList<Vector2[]> contours = MarchingSquares.FindContours(mask, 4, 4);

            Assert.AreEqual(2, contours.Count);
            foreach (Vector2[] ring in contours)
                AssertContourInvariants(ring);

            Assert.IsTrue(EvenOddContains(contours, new Vector2(0, 0)));
            Assert.IsTrue(EvenOddContains(contours, new Vector2(3, 3)));
            Assert.IsFalse(EvenOddContains(contours, new Vector2(1, 1)));
        }

        [TestMethod]
        public void FindContours_FourAdjacentPixels_OneRing()
        {
            bool[] mask = new bool[4];
            Array.Fill(mask, true);

            IReadOnlyList<Vector2[]> contours = MarchingSquares.FindContours(mask, 2, 2);

            Assert.AreEqual(1, contours.Count);
            AssertContourInvariants(contours[0]);
            Assert.IsTrue(EvenOddContains(contours, new Vector2(0, 0)));
            Assert.IsTrue(EvenOddContains(contours, new Vector2(1, 1)));
        }

        [TestMethod]
        public void FindContours_DiagonalSaddle_EvenOddKeepsBothPixels()
        {
            bool[] mask = new bool[4];
            mask[0] = true;
            mask[3] = true;

            IReadOnlyList<Vector2[]> contours = MarchingSquares.FindContours(mask, 2, 2);

            Assert.IsTrue(contours.Count >= 1);
            foreach (Vector2[] ring in contours)
                AssertContourInvariants(ring);

            Assert.IsTrue(EvenOddContains(contours, new Vector2(0, 0)));
            Assert.IsTrue(EvenOddContains(contours, new Vector2(1, 1)));
            Assert.IsFalse(EvenOddContains(contours, new Vector2(1, 0)));
            Assert.IsFalse(EvenOddContains(contours, new Vector2(0, 1)));
        }

        [TestMethod]
        public void FindContours_OppositeDiagonalSaddle_EvenOddKeepsBothPixels()
        {
            bool[] mask = new bool[4];
            mask[1] = true;
            mask[2] = true;

            IReadOnlyList<Vector2[]> contours = MarchingSquares.FindContours(mask, 2, 2);

            Assert.IsTrue(contours.Count >= 1);
            foreach (Vector2[] ring in contours)
                AssertContourInvariants(ring);

            Assert.IsTrue(EvenOddContains(contours, new Vector2(1, 0)));
            Assert.IsTrue(EvenOddContains(contours, new Vector2(0, 1)));
            Assert.IsFalse(EvenOddContains(contours, new Vector2(0, 0)));
            Assert.IsFalse(EvenOddContains(contours, new Vector2(1, 1)));
        }

        [TestMethod]
        public void FindContours_CShape_ConcaveBayIsOutside()
        {
            bool[] mask = FilledRectangle(20, 20, 2, 2, 17, 17);
            ClearRectangle(mask, 20, 8, 7, 17, 12);

            IReadOnlyList<Vector2[]> contours = MarchingSquares.FindContours(mask, 20, 20);

            Assert.AreEqual(1, contours.Count);
            AssertContourInvariants(contours[0]);
            Assert.IsTrue(EvenOddContains(contours, new Vector2(5, 10)));
            Assert.IsFalse(EvenOddContains(contours, new Vector2(13, 10)));
        }

        [TestMethod]
        public void FindContours_RectangleWithHole_EvenOddKeepsHoleEmpty()
        {
            bool[] mask = FilledRectangle(20, 20, 2, 2, 17, 17);
            ClearRectangle(mask, 20, 7, 7, 12, 12);

            IReadOnlyList<Vector2[]> contours = MarchingSquares.FindContours(mask, 20, 20);

            Assert.AreEqual(2, contours.Count);
            foreach (Vector2[] ring in contours)
                AssertContourInvariants(ring);

            Assert.IsTrue(EvenOddContains(contours, new Vector2(4, 4)));
            Assert.IsFalse(EvenOddContains(contours, new Vector2(9, 9)));
            Assert.IsFalse(EvenOddContains(contours, new Vector2(0, 0)));
        }

        [TestMethod]
        public void FindContours_EveryTwoByTwoCellPattern_SatisfiesInvariants()
        {
            for (int cell = 0; cell < 16; cell++)
            {
                bool[] mask =
                [
                    (cell & 1) != 0,
                    (cell & 2) != 0,
                    (cell & 8) != 0,
                    (cell & 4) != 0
                ];

                IReadOnlyList<Vector2[]> contours = MarchingSquares.FindContours(mask, 2, 2);
                AssertContoursMatchMask(new BinaryMask(mask, 2, 2), contours);
            }
        }

        [TestMethod]
        public void FindContours_MismatchedDimensions_Throws()
        {
            Assert.ThrowsException<ArgumentException>(() => MarchingSquares.FindContours(new bool[3], 2, 2));
            Assert.ThrowsException<ArgumentException>(() => MarchingSquares.FindContours(new bool[4], 0, 2));
            Assert.ThrowsException<ArgumentException>(() => MarchingSquares.FindContours(new bool[4], 2, -1));
        }

        [TestMethod]
        public void FindContours_NullMask_Throws() =>
            Assert.ThrowsException<ArgumentNullException>(() => MarchingSquares.FindContours(null, 1, 1));

        [TestMethod]
        public void ContoursAreClosedHalfPixelRings() =>
            CoreCheck.Run(
                Prop.ForAll(ArbMask(), mask =>
                {
                    IReadOnlyList<Vector2[]> contours = MarchingSquares.FindContours(mask.Data, mask.Width, mask.Height);
                    return contours.All(HasClosedHalfPixelRing);
                }),
                nameof(ContoursAreClosedHalfPixelRings));

        [TestMethod]
        public void ConsecutiveVerticesAreCellEdgeSteps() =>
            CoreCheck.Run(
                Prop.ForAll(ArbMask(), mask =>
                {
                    IReadOnlyList<Vector2[]> contours = MarchingSquares.FindContours(mask.Data, mask.Width, mask.Height);
                    return contours.All(ring =>
                    {
                        for (int i = 1; i < ring.Length; i++)
                        {
                            if (!IsCellEdgeStep(ring[i - 1], ring[i]))
                                return false;
                        }

                        return true;
                    });
                }),
                nameof(ConsecutiveVerticesAreCellEdgeSteps));

        [TestMethod]
        public void EvenOddContainsAgreesWithEveryPixel() =>
            CoreCheck.Run(
                Prop.ForAll(ArbMask(), mask =>
                    AssertContoursMatchMask(mask, MarchingSquares.FindContours(mask.Data, mask.Width, mask.Height))),
                nameof(EvenOddContainsAgreesWithEveryPixel));

        [TestMethod]
        public void IsolatedPixelsEachProduceOneRing() =>
            CoreCheck.Run(
                Prop.ForAll(ArbIsolatedPixels(), mask =>
                {
                    IReadOnlyList<Vector2[]> contours = MarchingSquares.FindContours(mask.Data, mask.Width, mask.Height);
                    int setCount = mask.Data.Count(set => set);
                    if (contours.Count != setCount)
                        return false;

                    return AssertContoursMatchMask(mask, contours);
                }),
                nameof(IsolatedPixelsEachProduceOneRing));

        static Arbitrary<BinaryMask> ArbMask() => Arb.From(GenMask(), ShrinkMask);

        static Arbitrary<BinaryMask> ArbIsolatedPixels() => Arb.From(GenIsolatedPixels(), ShrinkMask);

        static Gen<BinaryMask> GenMask() =>
            Gen.Frequency(
                Tuple.Create(4, GenRandomMask()),
                Tuple.Create(1, GenFilledRectangleMask()),
                Tuple.Create(1, GenRectangleWithHoleMask()),
                Tuple.Create(1, GenCheckerboardMask()),
                Tuple.Create(1, Gen.Constant(new BinaryMask(new bool[4], 2, 2))),
                Tuple.Create(1, Gen.Constant(new BinaryMask([true, true, true, true], 2, 2))));

        static Gen<BinaryMask> GenRandomMask() =>
            from width in Gen.Choose(1, 8)
            from height in Gen.Choose(1, 8)
            from data in Arb.Default.Bool().Generator.ArrayOf(width * height)
            select new BinaryMask(data, width, height);

        static Gen<BinaryMask> GenFilledRectangleMask() =>
            from width in Gen.Choose(3, 8)
            from height in Gen.Choose(3, 8)
            from left in Gen.Choose(0, width - 1)
            from top in Gen.Choose(0, height - 1)
            from right in Gen.Choose(left, width - 1)
            from bottom in Gen.Choose(top, height - 1)
            select new BinaryMask(FilledRectangle(width, height, left, top, right, bottom), width, height);

        static Gen<BinaryMask> GenRectangleWithHoleMask() =>
            from width in Gen.Choose(6, 10)
            from height in Gen.Choose(6, 10)
            let data = FilledRectangle(width, height, 1, 1, width - 2, height - 2)
            select ClearHole(data, width, 2, 2, width - 3, height - 3);

        static Gen<BinaryMask> GenCheckerboardMask() =>
            from width in Gen.Choose(2, 6)
            from height in Gen.Choose(2, 6)
            select Checkerboard(width, height);

        static Gen<BinaryMask> GenIsolatedPixels() =>
            from width in Gen.Choose(4, 8)
            from height in Gen.Choose(4, 8)
            from count in Gen.Choose(1, 4)
            from points in GenIsolatedPoints(width, height, count)
            select PlacePixels(width, height, points);

        static Gen<IReadOnlyList<(int X, int Y)>> GenIsolatedPoints(int width, int height, int count) =>
            Gen.Choose(0, width * height - 1).ArrayOf(count * 4).Select(raw =>
            {
                List<(int X, int Y)> placed = [];
                foreach (int index in raw)
                {
                    int x = index % width;
                    int y = index / width;
                    if (placed.All(p => Chebyshev(p.X, p.Y, x, y) >= 2))
                    {
                        placed.Add((x, y));
                        if (placed.Count == count)
                            break;
                    }
                }

                return placed.Count == 0 ? [(0, 0)] : (IReadOnlyList<(int X, int Y)>)placed;
            });

        static IEnumerable<BinaryMask> ShrinkMask(BinaryMask mask)
        {
            for (int i = 0; i < mask.Data.Length; i++)
            {
                if (!mask.Data[i])
                    continue;

                bool[] copy = (bool[])mask.Data.Clone();
                copy[i] = false;
                yield return new BinaryMask(copy, mask.Width, mask.Height);
            }
        }

        static bool AssertContoursMatchMask(BinaryMask mask, IReadOnlyList<Vector2[]> contours)
        {
            foreach (Vector2[] ring in contours)
            {
                if (!HasClosedHalfPixelRing(ring))
                    return false;
            }

            for (int y = 0; y < mask.Height; y++)
            {
                for (int x = 0; x < mask.Width; x++)
                {
                    bool inside = EvenOddContains(contours, new Vector2(x, y));
                    if (inside != mask[x, y])
                        return false;
                }
            }

            return !EvenOddContains(contours, new Vector2(-2, -2));
        }

        static bool HasClosedHalfPixelRing(Vector2[] ring)
        {
            if (ring.Length < 4 || ring[0] != ring[ring.Length - 1])
                return false;

            for (int i = 0; i < ring.Length; i++)
            {
                if (!IsHalfPixelVertex(ring[i]))
                    return false;
            }

            return true;
        }

        static void AssertContourInvariants(Vector2[] ring)
        {
            Assert.IsTrue(HasClosedHalfPixelRing(ring));
            for (int i = 1; i < ring.Length; i++)
                Assert.IsTrue(IsCellEdgeStep(ring[i - 1], ring[i]), $"{ring[i - 1]} -> {ring[i]}");
        }

        static bool IsHalfPixelVertex(Vector2 p)
        {
            bool xInteger = Tolerance.AreClose(p.X, Math.Round(p.X));
            bool yInteger = Tolerance.AreClose(p.Y, Math.Round(p.Y));
            bool xHalf = Tolerance.AreClose(p.X * 2.0, Math.Round(p.X * 2.0));
            bool yHalf = Tolerance.AreClose(p.Y * 2.0, Math.Round(p.Y * 2.0));
            return xHalf && yHalf && (xInteger != yInteger);
        }

        static bool IsCellEdgeStep(Vector2 a, Vector2 b)
        {
            double dx = Math.Abs(a.X - b.X);
            double dy = Math.Abs(a.Y - b.Y);
            bool diagonal = Tolerance.AreClose(dx, 0.5) && Tolerance.AreClose(dy, 0.5);
            bool horizontal = Tolerance.AreClose(dx, 1) && Tolerance.AreClose(dy, 0);
            bool vertical = Tolerance.AreClose(dy, 1) && Tolerance.AreClose(dx, 0);
            return diagonal || horizontal || vertical;
        }

        static bool EvenOddContains(IReadOnlyList<Vector2[]> contours, Vector2 p)
        {
            bool inside = false;
            foreach (Vector2[] ring in contours)
            {
                if (EvenOddContains(ring, p))
                    inside = !inside;
            }

            return inside;
        }

        static bool EvenOddContains(Vector2[] ring, Vector2 p)
        {
            int n = ring.Length;
            if (n >= 2 && ring[0] == ring[n - 1])
                n--;

            bool inside = false;
            for (int i = 0, j = n - 1; i < n; j = i++)
            {
                double yi = ring[i].Y;
                double yj = ring[j].Y;
                if ((yi > p.Y) == (yj > p.Y))
                    continue;

                double xi = ring[i].X;
                double xj = ring[j].X;
                double xIntersect = ((xj - xi) * (p.Y - yi) / (yj - yi)) + xi;
                if (p.X < xIntersect)
                    inside = !inside;
            }

            return inside;
        }

        static bool[] FilledRectangle(int width, int height, int left, int top, int right, int bottom)
        {
            bool[] mask = new bool[width * height];
            for (int y = top; y <= bottom; y++)
            {
                for (int x = left; x <= right; x++)
                    mask[(y * width) + x] = true;
            }

            return mask;
        }

        static void ClearRectangle(bool[] mask, int width, int left, int top, int right, int bottom)
        {
            for (int y = top; y <= bottom; y++)
            {
                for (int x = left; x <= right; x++)
                    mask[(y * width) + x] = false;
            }
        }

        static BinaryMask ClearHole(bool[] data, int width, int left, int top, int right, int bottom)
        {
            ClearRectangle(data, width, left, top, right, bottom);
            return new BinaryMask(data, width, data.Length / width);
        }

        static BinaryMask Checkerboard(int width, int height)
        {
            bool[] data = new bool[width * height];
            for (int y = 0; y < height; y++)
            {
                for (int x = 0; x < width; x++)
                    data[(y * width) + x] = ((x + y) & 1) == 0;
            }

            return new BinaryMask(data, width, height);
        }

        static BinaryMask PlacePixels(int width, int height, IReadOnlyList<(int X, int Y)> points)
        {
            bool[] data = new bool[width * height];
            foreach ((int x, int y) in points)
                data[(y * width) + x] = true;

            return new BinaryMask(data, width, height);
        }

        static int Chebyshev(int ax, int ay, int bx, int by) =>
            Math.Max(Math.Abs(ax - bx), Math.Abs(ay - by));
    }
}
