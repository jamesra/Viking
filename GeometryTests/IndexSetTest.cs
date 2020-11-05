using Geometry;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using System.Linq;

namespace GeometryTests
{
    [TestClass]
    public class IndexSetTest
    {
        [TestMethod]
        public void ContinuousIndexSetTests()
        {
            int startIndex = 5;
            int count = 10;
            ContinuousIndexSet set = new ContinuousIndexSet(startIndex, count);

            Assert.AreEqual(set[0], 5);
            Assert.AreEqual(set[set.Count - 1], 14);

            Assert.IsTrue(set.Max() == 14);
            Assert.IsTrue(set.Min() == 5);
        }

        [TestMethod]
        public void IndexSetTests()
        {
            int startIndex = 5;
            int count = 10;
            long[] indicies = new long[count];
            for (int i = 0; i < count; i++)
            {
                indicies[i] = startIndex + i;
            }

            IndexSet set = new IndexSet(indicies);

            Assert.AreEqual(set[0], 5);
            Assert.AreEqual(set[set.Count - 1], 14);

        }

        [TestMethod]
        public void ContinuousWrappedIndexSetTests()
        {
            int startIndex = 5;
            int minIndex = 1;
            int maxIndex = 10;

            FiniteWrappedIndexSet set = new FiniteWrappedIndexSet(minIndex, maxIndex, startIndex);

            Assert.AreEqual(set[0], 5);
            Assert.AreEqual(set[set.Count - 1], 4);

            Assert.IsTrue(set.Max() == maxIndex - 1);
            Assert.IsTrue(set.Min() == minIndex);
        }
    }
}
