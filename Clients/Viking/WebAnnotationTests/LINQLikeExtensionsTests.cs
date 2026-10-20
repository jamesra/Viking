using System;
using System.Collections.Generic;
using System.Linq;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using WebAnnotation;

namespace WebAnnotationTests
{
    /// <summary>
    /// Pins the contract of <see cref="LINQLikeExtensions.ForEach{T}(IEnumerable{T}, Action{T})"/> and its indexed overloads
    /// for arrays and other sequences: visit order, zero-based indices, and argument validation.
    /// </summary>
    [TestClass]
    public class LINQLikeExtensionsTests
    {
        [TestMethod]
        public void ForEachVisitsSequenceItemsInOrder()
        {
            var seen = new List<int>();
            Enumerable.Range(3, 4).ForEach(seen.Add);
            CollectionAssert.AreEqual(new[] { 3, 4, 5, 6 }, seen);
        }

        [TestMethod]
        public void IndexedForEachOverSequencePassesZeroBasedIndex()
        {
            var seen = new List<(string, int)>();
            IEnumerable<string> source = new List<string> { "a", "b", "c" };
            source.ForEach((item, i) => seen.Add((item, i)));
            CollectionAssert.AreEqual(new[] { ("a", 0), ("b", 1), ("c", 2) }, seen);
        }

        [TestMethod]
        public void IndexedForEachOverArrayPassesZeroBasedIndex()
        {
            var seen = new List<(string, int)>();
            new[] { "x", "y" }.ForEach((item, i) => seen.Add((item, i)));
            CollectionAssert.AreEqual(new[] { ("x", 0), ("y", 1) }, seen);
        }

        [TestMethod]
        public void ForEachOnEmptySourceNeverInvokesAction()
        {
            int calls = 0;
            new int[0].ForEach((_, __) => calls++);
            Enumerable.Empty<int>().ForEach(_ => calls++);
            Enumerable.Empty<int>().ForEach((_, __) => calls++);
            Assert.AreEqual(0, calls);
        }

        [TestMethod]
        public void NullSourceThrowsArgumentNullExceptionNamedSource()
        {
            Assert.AreEqual("source", Assert.ThrowsException<ArgumentNullException>(() => ((IEnumerable<int>)null).ForEach(_ => { })).ParamName);
            Assert.AreEqual("source", Assert.ThrowsException<ArgumentNullException>(() => ((IEnumerable<int>)null).ForEach((_, __) => { })).ParamName);
            Assert.AreEqual("source", Assert.ThrowsException<ArgumentNullException>(() => ((int[])null).ForEach((_, __) => { })).ParamName);
        }

        [TestMethod]
        public void NullActionThrowsArgumentNullExceptionNamedAction()
        {
            var array = new[] { 1 };
            Assert.AreEqual("action", Assert.ThrowsException<ArgumentNullException>(() => array.ForEach((Action<int>)null)).ParamName);
            Assert.AreEqual("action", Assert.ThrowsException<ArgumentNullException>(() => array.ForEach((Action<int, int>)null)).ParamName);
            Assert.AreEqual("action", Assert.ThrowsException<ArgumentNullException>(() => array.Select(x => x).ForEach((Action<int, int>)null)).ParamName);
        }
    }
}
