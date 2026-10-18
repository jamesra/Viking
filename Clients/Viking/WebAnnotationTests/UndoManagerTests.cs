using Microsoft.VisualStudio.TestTools.UnitTesting;
using System;
using System.Collections.Generic;
using WebAnnotation;

namespace WebAnnotationTests
{
    /// <summary>
    /// Pins the bounded undo stack in <c>WebAnnotation/UndoManager.cs</c>: newest-first order, the 50-entry cap
    /// that drops the oldest entry, <c>CanUndo</c>/<c>NextUndoDescription</c>, and that an entry is removed even
    /// when its undo throws. The FaultException path is not covered because it shows a MessageBox.
    /// </summary>
    [TestClass]
    public class UndoManagerTests
    {
        /// <summary>The stack is static, so empty it before and after each test.</summary>
        private static void Drain()
        {
            while (UndoManager.CanUndo)
            {
                UndoManager.UndoLast();
            }
        }

        [TestInitialize]
        public void SetUp() => Drain();

        [TestCleanup]
        public void TearDown() => Drain();

        [TestMethod]
        public void EmptyStackCannotUndoAndHasNoDescription()
        {
            Assert.IsFalse(UndoManager.CanUndo);
            Assert.IsNull(UndoManager.NextUndoDescription);
            Assert.IsFalse(UndoManager.UndoLast());
        }

        [TestMethod]
        public void UndoRunsNewestEntryFirst()
        {
            List<string> ran = new();
            UndoManager.Push("a", () => ran.Add("a"));
            UndoManager.Push("b", () => ran.Add("b"));
            UndoManager.Push("c", () => ran.Add("c"));

            Assert.IsTrue(UndoManager.CanUndo);
            Assert.AreEqual("c", UndoManager.NextUndoDescription);

            Assert.IsTrue(UndoManager.UndoLast());
            Assert.AreEqual("b", UndoManager.NextUndoDescription);
            Assert.IsTrue(UndoManager.UndoLast());
            Assert.AreEqual("a", UndoManager.NextUndoDescription);
            Assert.IsTrue(UndoManager.UndoLast());

            CollectionAssert.AreEqual(new[] { "c", "b", "a" }, ran);
            Assert.IsFalse(UndoManager.CanUndo);
            Assert.IsNull(UndoManager.NextUndoDescription);
            Assert.IsFalse(UndoManager.UndoLast());
        }

        [TestMethod]
        public void StackKeepsOnlyTheNewestFiftyEntries()
        {
            const int cap = 50;
            List<int> ran = new();
            for (int i = 0; i < cap + 5; i++)
            {
                int n = i;
                UndoManager.Push(n.ToString(), () => ran.Add(n));
            }

            while (UndoManager.UndoLast())
            {
            }

            Assert.AreEqual(cap, ran.Count);
            Assert.AreEqual(cap + 4, ran[0], "newest entry undoes first");
            Assert.AreEqual(5, ran[cap - 1], "entries 0-4 were dropped, so 5 is the oldest kept");
        }

        [TestMethod]
        public void StackAtExactlyFiftyEntriesDropsNothing()
        {
            List<int> ran = new();
            for (int i = 0; i < 50; i++)
            {
                int n = i;
                UndoManager.Push(n.ToString(), () => ran.Add(n));
            }

            while (UndoManager.UndoLast())
            {
            }

            Assert.AreEqual(50, ran.Count);
            Assert.AreEqual(0, ran[49]);
        }

        [TestMethod]
        public void EntryIsRemovedEvenWhenItsUndoThrows()
        {
            bool olderRan = false;
            UndoManager.Push("older", () => olderRan = true);
            UndoManager.Push("bad", () => throw new InvalidOperationException("boom"));

            Assert.ThrowsException<InvalidOperationException>(() => UndoManager.UndoLast());

            Assert.AreEqual("older", UndoManager.NextUndoDescription);
            Assert.IsTrue(UndoManager.UndoLast());
            Assert.IsTrue(olderRan);
        }
    }
}
