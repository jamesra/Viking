using FsCheck;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using System;

namespace GraphLibTest
{
    /// <summary>
    /// Pins <see cref="GraphLib.Edge{NODEKEY}"/> ordering and equality used as keys in sorted edge
    /// collections: directional vs undirected endpoint swap, loop edges, null operators, and
    /// consistency between <see cref="GraphLib.Edge{NODEKEY}.CompareTo"/> and
    /// <see cref="GraphLib.Edge{NODEKEY}.Equals"/>.
    /// </summary>
    [TestClass]
    public class EdgeOrderingTests
    {
        [TestMethod]
        public void UndirectedEdgesTreatSwappedEndpointsAsEqual()
        {
            SimpleEdge forward = new(1, 2, false);
            SimpleEdge reverse = new(2, 1, false);

            Assert.IsTrue(forward.Equals(reverse));
            Assert.IsTrue(forward == reverse);
            Assert.AreEqual(0, forward.CompareTo(reverse));
            Assert.AreEqual(forward.GetHashCode(), reverse.GetHashCode());
        }

        [TestMethod]
        public void DirectionalEdgesDistinguishEndpointOrder()
        {
            SimpleEdge forward = new(1, 2, true);
            SimpleEdge reverse = new(2, 1, true);

            Assert.IsFalse(forward.Equals(reverse));
            Assert.IsTrue(forward != reverse);
            Assert.AreNotEqual(0, forward.CompareTo(reverse));
        }

        [TestMethod]
        public void LoopEdgesCompareEqualRegardlessOfDirectionalFlagSemantics()
        {
            SimpleEdge loopUndirected = new(5, 5, false);
            SimpleEdge loopDirectional = new(5, 5, true);

            Assert.IsTrue(loopUndirected.IsLoop);
            Assert.IsTrue(loopDirectional.IsLoop);
            Assert.AreEqual(0, loopUndirected.CompareTo(new SimpleEdge(5, 5, false)));
            Assert.AreEqual(0, loopDirectional.CompareTo(new SimpleEdge(5, 5, true)));
            Assert.IsFalse(loopUndirected.Equals(loopDirectional));
        }

        [TestMethod]
        public void NullOperatorsFollowReferenceEqualityRules()
        {
            SimpleEdge edge = new(1, 2, true);

            Assert.IsFalse(edge == null);
            Assert.IsTrue(edge != null);
            Assert.IsTrue(null == null);
            Assert.IsFalse(null != null);
            Assert.IsFalse(edge.Equals(null));
            Assert.IsFalse(edge.Equals((object)null));
        }

        [TestMethod]
        public void StaticComparerMatchesInstanceCompareTo()
        {
            SimpleEdge a = new(1, 3, false);
            SimpleEdge b = new(2, 4, false);
            SimpleEdge cmp = new(0, 0, false);

            Assert.AreEqual(a.CompareTo(b), cmp.Compare(a, b));
            Assert.AreEqual(0, cmp.Compare(a, a));
            Assert.AreEqual(0, cmp.Compare(null, null));
            Assert.AreEqual(-1, cmp.Compare(null, a));
            Assert.AreEqual(1, cmp.Compare(a, null));
        }

        /// <summary>Pair of generated edges for ordering and equality properties.</summary>
        public record EdgePair(int S1, int T1, bool D1, int S2, int T2, bool D2);

        private static Arbitrary<EdgePair> ArbEdgePair()
        {
            Gen<EdgePair> gen =
                from s1 in Gen.Choose(-20, 20)
                from t1 in Gen.Choose(-20, 20)
                from d1 in Arb.Default.Bool().Generator
                from s2 in Gen.Choose(-20, 20)
                from t2 in Gen.Choose(-20, 20)
                from d2 in Arb.Default.Bool().Generator
                select new EdgePair(s1, t1, d1, s2, t2, d2);
            return Arb.From(gen);
        }

        [TestMethod]
        public void CompareToIsAntisymmetricAndAgreesWithEquals()
        {
            Configuration config = Configuration.QuickThrowOnFailure;
            config.MaxNbOfTest = 500;

            Prop.ForAll(ArbEdgePair(), pair =>
                {
                    SimpleEdge left = new(pair.S1, pair.T1, pair.D1);
                    SimpleEdge right = new(pair.S2, pair.T2, pair.D2);

                    int cmp = left.CompareTo(right);
                    int reverse = right.CompareTo(left);

                    if (cmp == 0)
                    {
                        if (!left.Equals(right))
                            return false;
                        if (left.GetHashCode() != right.GetHashCode())
                            return false;
                    }
                    else if (cmp > 0)
                    {
                        if (reverse >= 0)
                            return false;
                    }
                    else if (reverse <= 0)
                        return false;

                    if (left.Equals(right) && cmp != 0)
                        return false;

                    return true;
                })
                .Check(config);
        }
    }
}

