using System;
using System.Collections.Generic;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace AnnotationUtilsTests
{
    [TestClass]
    public class UtilsTests
    {
        [TestMethod]
        public void TestSplitListIntoChunks()
        {
            List<int> list = new List<int>(new int[] { 1, 2, 3, 4, 5, 6, 7, 8, 9 });

            List<int>[] arrayOfLists = AnnotationUtils.Utils<int>.SplitListIntoChunks(list, 9);
            Assert.AreEqual(arrayOfLists.Length, 1);

            arrayOfLists = AnnotationUtils.Utils<int>.SplitListIntoChunks(list, 3);
            Assert.AreEqual(arrayOfLists.Length, 3);

            arrayOfLists = AnnotationUtils.Utils<int>.SplitListIntoChunks(list, 5);
            Assert.AreEqual(arrayOfLists.Length, 2);

            arrayOfLists = AnnotationUtils.Utils<int>.SplitListIntoChunks(list, 2);
            Assert.AreEqual(arrayOfLists.Length, 5);
        }
    }
}
