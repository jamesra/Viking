using System.Linq;
using FsCheck;
using Geometry;
using GeometryTests.FSCheck;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace GeometryTests
{
    [TestClass]
    public class PolygonIndexTest
    {

        [TestMethod]
        public void PolygonIndexBasics()
        {
            //Check that equality works for identical indicies
            PolygonIndex A1 = new(0, 0, 5);
            PolygonIndexTest.CheckPolygonIndexEquality(A1);
            PolygonIndex A2 = new(3, 3, 5);
            PolygonIndexTest.CheckPolygonIndexEquality(A2);
            PolygonIndex indexWithInner = new(3, 2, 3, 5);
            PolygonIndexTest.CheckPolygonIndexEquality(indexWithInner);
        }

        private static void CheckPolygonIndexEquality(PolygonIndex input)
        {
            PolygonIndex clone = (PolygonIndex)input.Clone();
            Assert.AreEqual(input, clone);
            Assert.IsTrue(input == clone);
            Assert.IsFalse(input != clone);

            PolygonIndex differentRing =
                new(input.ShapeIndex, input.InnerShapeIndex, input.VertexIndex, input.NumUniqueInRing + 1);
            Assert.AreNotEqual(input, differentRing);

            PolygonIndex differentPolygon =
                new(input.ShapeIndex + 1, input.InnerShapeIndex, input.VertexIndex, input.NumUniqueInRing);
            Assert.AreNotEqual(input, differentPolygon);

            PolygonIndex differentInner =
                new(input.ShapeIndex, input.InnerShapeIndex.HasValue ? input.InnerShapeIndex.Value + 1 : 0, input.VertexIndex, input.NumUniqueInRing);
            Assert.AreNotEqual(input, differentInner);

            PolygonIndex differentInner2 =
                new(input.ShapeIndex, input.InnerShapeIndex.HasValue ? new int?() : 0, input.VertexIndex, input.NumUniqueInRing);
            Assert.AreNotEqual(input, differentInner2);

            var stepForward = input.Next;
            Assert.AreNotEqual(input, stepForward);

            var stepBackward = input.Previous;
            Assert.AreNotEqual(input, stepBackward);

            var stepAround = input.FirstInRing;
            Assert.AreEqual(input.FirstInRing, input.LastInRing.Next);

            var stepAround2 = input.FirstInRing;
            Assert.AreEqual(input.FirstInRing.Previous, input.LastInRing);
        }

        [TestMethod]
        public void PolygonVertexEnumeratorTest()
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
            Polygon box = Primitives.BoxPolygon(10);

            PolygonIndexTest.CheckVertexEnumerator(box);

            /////////////////////////////////
            //Outer poly with one inner poly
            Polygon OuterBox = Primitives.BoxPolygon(15);
            Polygon U = Primitives.UPolygon(10);

            //Add the U polygon as an interior polygon
            OuterBox.AddInteriorRing(U);

            PolygonIndexTest.CheckVertexEnumerator(OuterBox);

            /////////////////////////////////
            //Outer poly with two inner poly
            Polygon mini_box = Primitives.UPolygon(1);

            OuterBox.AddInteriorRing(mini_box);
            PolygonIndexTest.CheckVertexEnumerator(OuterBox);
        }

        private static void CheckVertexEnumerator(Polygon polygon)
        {
            PolygonIndex[] forward = new PolygonVertexEnum(polygon).ToArray();
            PolygonIndex[] backward = [.. new PolygonVertexEnum(polygon, reverse: true).Reverse()];

            //Check we got the expected number of indicies, one per vertex
            Assert.AreEqual(forward.Length, polygon.TotalUniqueVertices);
            Assert.AreEqual(forward.Length, backward.Length);

            //Check that all indicies returned by the enumerator are unique
            Assert.AreEqual(forward.Distinct().Count(), forward.Length);
            Assert.AreEqual(backward.Distinct().Count(), forward.Length);

            //Check that all indicies can apply comparison operators correctly
            for (int i = 0; i < forward.Length; i++)
            {
                var f = forward[i];
                if (i > 0)
                {
                    var fprev = forward[i - 1];
                    Assert.IsTrue(f.CompareTo(fprev) > 0);
                }

                if (i < forward.Length - 1)
                {
                    var fNext = forward[i + 1];
                    Assert.IsTrue(f.CompareTo(fNext) < 0);
                }
            }

            //Ensure we get the correct number of verticies for both external and internal polygons
            Assert.AreEqual(forward.Count(i => i.IsInner == false), polygon.ExteriorRing.Length - 1);
            Assert.AreEqual(backward.Count(i => i.IsInner == false), polygon.ExteriorRing.Length - 1);

            for (int iInner = 0; iInner < polygon.InteriorPolygons.Count; iInner++)
            {
                var innerPoly = polygon.InteriorPolygons[iInner];
                Assert.AreEqual(forward.Count(i => i.IsInner && i.InnerShapeIndex == iInner), innerPoly.ExteriorRing.Length - 1);
                Assert.AreEqual(backward.Count(i => i.IsInner && i.InnerShapeIndex == iInner), innerPoly.ExteriorRing.Length - 1);
            }

            //Ensure the forward and reversed backward arrays are equal
            for (int i = 0; i < forward.Length; i++)
            {
                Assert.AreEqual(forward[i], backward[i]);
            }
        }


        [TestMethod]
        public void PolySetVertexEnumTests()
        {
            Polygon[] polys =
            [
                Primitives.BoxPolygon(1),
                Primitives.BoxPolygon(2),
                Primitives.BoxPolygon(3)
            ];

            polys[1].AddInteriorRing(Primitives.ConcaveCheckPolygon(0.5));

            PolySetVertexEnum enumeratorForward = new(polys);
            var forward = enumeratorForward.ToArray();

            var totalVerts = polys.Sum(p => p.TotalUniqueVertices);
            Assert.AreEqual(totalVerts, forward.Length);
            Assert.AreEqual(forward.Length, forward.Distinct().Count());


        }

        [TestMethod]
        public void PolygonIndexWalksRingAndRoundTrips() =>
            CoreCheck.Run(
                Prop.ForAll(Arb.From(Gen.Choose(3, 24)), n =>
                {
                    PolygonIndex start = new(0, 0, n);
                    PolygonIndex cur = start;
                    for (int i = 0; i < n; i++)
                        cur = cur.Next;
                    PolygonIndex inner = new(0, 1, 0, n);
                    PolygonIndex innerWalk = inner;
                    for (int i = 0; i < n; i++)
                        innerWalk = innerWalk.Next;
                    return cur == start &&
                           start.Previous.Next == start &&
                           start.FirstInRing == start.LastInRing.Next &&
                           innerWalk == inner &&
                           inner.Clone().Equals(inner);
                }),
                nameof(PolygonIndexWalksRingAndRoundTrips));

        [TestMethod]
        public void PolylineIndexWalksAndOrders() =>
            CoreCheck.Run(
                Prop.ForAll(Arb.From(Gen.Choose(2, 24)), n =>
                {
                    IShapeIndex idx = new PolylineIndex(0, 0, n);
                    int count = 0;
                    PolylineIndex? prev = null;
                    while (idx != null)
                    {
                        PolylineIndex current = (PolylineIndex)idx;
                        if (prev.HasValue)
                        {
                            if (current.CompareTo(prev.Value) <= 0)
                                return false;
                        }

                        count++;
                        prev = current;
                        idx = idx.Next;
                    }

                    PolylineIndex first = new(0, 0, n);
                    PolylineIndex last = new(0, n - 1, n);
                    return count == n && first.Clone().Equals(first) && last.PreviousVertex == n - 2;
                }),
                nameof(PolylineIndexWalksAndOrders));

        [TestMethod]
        public void NextPreviousRoundTripAndReindexToInner() =>
            CoreCheck.Run(
                Prop.ForAll(CoreArbitraries.ArbPolygonIndex(), idx =>
                {
                    PolygonIndex inner = idx.ReindexToInner(1);
                    return idx.Next.Previous == idx &&
                           idx.Previous.Next == idx &&
                           ((PolygonIndex)idx.Clone()).Equals(idx) &&
                           inner.IsInner &&
                           inner.InnerShapeIndex == 1 &&
                           inner.VertexIndex == idx.VertexIndex &&
                           inner.NumUniqueInRing == idx.NumUniqueInRing;
                }),
                nameof(NextPreviousRoundTripAndReindexToInner));
    }
}