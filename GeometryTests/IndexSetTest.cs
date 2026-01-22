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
            ContinuousIndexSet set = new(startIndex, count);

            Assert.AreEqual(5, set[0]);
            Assert.AreEqual(14, set[set.Count - 1]);

            Assert.AreEqual(14, set.Max());
            Assert.AreEqual(5, set.Min());
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

            IndexSet set = new(indicies);

            Assert.AreEqual(5, set[0]);
            Assert.AreEqual(14, set[set.Count - 1]);

        }

        [TestMethod]
        public void ContinuousWrappedIndexSetTests()
        {
            int startIndex = 5;
            int minIndex = 1;
            int maxIndex = 10;

            FiniteWrappedIndexSet set = new(minIndex, maxIndex, startIndex);

            Assert.AreEqual(5, set[0]);
            Assert.AreEqual(4, set[set.Count - 1]);

            Assert.AreEqual(maxIndex - 1, set.Max());
            Assert.AreEqual(minIndex, set.Min());
        }
    }
}
