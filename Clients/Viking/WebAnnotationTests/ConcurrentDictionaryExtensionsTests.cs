using System.Collections.Concurrent;
using System.Collections.Generic;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using WebAnnotation;

namespace WebAnnotationTests
{
    /// <summary>
    /// Pins <see cref="ConcurrentDictionaryExtensions.ValuesForKeys{TKey, TValue}"/>, which the section link view
    /// models use to map R-tree hits to views while a link removal on another thread may run between the two reads.
    /// </summary>
    [TestClass]
    public class ConcurrentDictionaryExtensionsTests
    {
        [TestMethod]
        public void ValuesForKeysReturnsValuesInKeyOrder()
        {
            ConcurrentDictionary<int, string> views = new() { [1] = "a", [2] = "b", [3] = "c" };
            CollectionAssert.AreEqual(new[] { "c", "a", "b" }, views.ValuesForKeys(new[] { 3, 1, 2 }));
        }

        /// <summary>
        /// The R-tree returned a key whose view was already removed (the link removal drops the dictionary
        /// entry before the R-tree entry). The old hit-test code used the indexer here and threw.
        /// </summary>
        [TestMethod]
        public void ValuesForKeysSkipsKeyRemovedAfterTheIndexWasRead()
        {
            ConcurrentDictionary<int, string> views = new() { [1] = "a", [2] = "b", [3] = "c" };
            List<int> keysFromIndex = [1, 2, 3];
            views.TryRemove(2, out _);

            CollectionAssert.AreEqual(new[] { "a", "c" }, views.ValuesForKeys(keysFromIndex));
        }

        /// <summary>
        /// A removal that lands while the keys are being mapped, as a background store event can.
        /// </summary>
        [TestMethod]
        public void ValuesForKeysSkipsKeyRemovedDuringTheLookup()
        {
            ConcurrentDictionary<int, string> views = new() { [1] = "a", [2] = "b", [3] = "c" };

            IEnumerable<int> KeysRemovingThird()
            {
                yield return 1;
                views.TryRemove(3, out _);
                yield return 3;
                yield return 2;
            }

            CollectionAssert.AreEqual(new[] { "a", "b" }, views.ValuesForKeys(KeysRemovingThird()));
        }

        [TestMethod]
        public void ValuesForKeysOfNoKeysIsEmpty()
        {
            ConcurrentDictionary<int, string> views = new() { [1] = "a" };
            Assert.AreEqual(0, views.ValuesForKeys(new int[0]).Count);
        }
    }
}
