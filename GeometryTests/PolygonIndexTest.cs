using System.Linq;
using Geometry;
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
            var A1 = new PolygonIndex(0, 0, 5);
            CheckPolygonIndexEquality(A1);
            var A2 = new PolygonIndex(3, 3, 5);
            CheckPolygonIndexEquality(A2);
            var indexWithInner = new PolygonIndex(3, 2, 3, 5);
            CheckPolygonIndexEquality(indexWithInner);
        }

        private void CheckPolygonIndexEquality(PolygonIndex input)
        {
            var clone = (PolygonIndex)input.Clone();
            Assert.AreEqual(input, clone);
            Assert.IsTrue(input == clone);
            Assert.IsFalse(input != clone);

            var differentRing =
                new PolygonIndex(input.iPoly, input.iInnerPoly, input.iVertex, input.NumUniqueInRing + 1);
            Assert.AreNotEqual(input, differentRing);

            var differentPolygon =
                new PolygonIndex(input.iPoly + 1, input.iInnerPoly, input.iVertex, input.NumUniqueInRing);
            Assert.AreNotEqual(input, differentPolygon);

            var differentInner =
                new PolygonIndex(input.iPoly, input.iInnerPoly.HasValue ? input.iInnerPoly.Value + 1 : 0, input.iVertex, input.NumUniqueInRing);
            Assert.AreNotEqual(input, differentInner);

            var differentInner2 =
                new PolygonIndex(input.iPoly, input.iInnerPoly.HasValue ? new int?() : 0, input.iVertex, input.NumUniqueInRing);
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
            GridPolygon box = Primitives.BoxPolygon(10);

            CheckVertexEnumerator(box);

            /////////////////////////////////
            //Outer poly with one inner poly
            GridPolygon OuterBox = Primitives.BoxPolygon(15);
            GridPolygon U = Primitives.UPolygon(10);

            //Add the U polygon as an interior polygon
            OuterBox.AddInteriorRing(U);

            CheckVertexEnumerator(OuterBox);

            /////////////////////////////////
            //Outer poly with two inner poly
            GridPolygon mini_box = Primitives.UPolygon(1);

            OuterBox.AddInteriorRing(mini_box);
            CheckVertexEnumerator(OuterBox);
        }

        private void CheckVertexEnumerator(GridPolygon polygon)
        {
            PolygonIndex[] forward = new PolygonVertexEnum(polygon).ToArray();
            PolygonIndex[] backward = new PolygonVertexEnum(polygon, reverse: true).Reverse().ToArray();

            //Check we got the expected number of indicies, one per vertex
            Assert.AreEqual(forward.Length, polygon.TotalUniqueVerticies);
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
                Assert.AreEqual(forward.Count(i => i.IsInner && i.iInnerPoly == iInner), innerPoly.ExteriorRing.Length - 1);
                Assert.AreEqual(backward.Count(i => i.IsInner && i.iInnerPoly == iInner), innerPoly.ExteriorRing.Length - 1);
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
            var polys = new GridPolygon[]
            {
                Primitives.BoxPolygon(1),
                Primitives.BoxPolygon(2),
                Primitives.BoxPolygon(3)
            };

            polys[1].AddInteriorRing(Primitives.ConcaveCheckPolygon(0.5));

            var enumeratorForward = new PolySetVertexEnum(polys);
            var forward = enumeratorForward.ToArray();

            var totalVerts = polys.Sum(p => p.TotalUniqueVerticies);
            Assert.AreEqual(totalVerts, forward.Length);
            Assert.AreEqual(forward.Length, forward.Distinct().Count());


        }
    }
}