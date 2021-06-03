using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace GraphLibTest
{
    [TestClass]
    public class AddRemoveTests
    {
        [TestMethod]
        public void TestEquality()
        {
            SimpleNode A1 = new SimpleNode(1);
            SimpleNode A2 = new SimpleNode(1);

            SimpleNode B = new SimpleNode(2);
            SimpleNode C = new SimpleNode(3);

            Assert.IsTrue(A1.Equals(A2));
            Assert.IsTrue(A1 == A2);

            Assert.IsFalse(A1.Equals(B));
            Assert.IsFalse(B.Equals(A1));

            Assert.IsFalse(B.Equals(null));


            SimpleEdge AB1_D = new SimpleEdge(1, 2, true);
            SimpleEdge AB1_U = new SimpleEdge(1, 2, false);
            SimpleEdge AB2_D = new SimpleEdge(1, 2, true);

            SimpleEdge AC1_U = new SimpleEdge(1, 3, false);
            SimpleEdge AC1_D = new SimpleEdge(1, 3, true);

            Assert.IsTrue(AB1_D == AB2_D);
            Assert.IsTrue(AB1_D.Equals(AB2_D));

            Assert.IsFalse(AB1_D.Equals(AB1_U));
            Assert.IsFalse(AB1_D.Equals(AC1_U));
            Assert.IsFalse(AB1_D.Equals(AC1_D));
        }
    }
}
