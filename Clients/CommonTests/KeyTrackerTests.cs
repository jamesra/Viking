using FsCheck;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Viking.Common;

namespace CommonTests
{
    /// <summary>
    /// Pins the add, remove, and rollback contracts of <see cref="KeyTracker{T}"/> and
    /// <see cref="RefCountingKeyTracker{T}"/>. WebAnnotation view models rely on the rollback
    /// when an action throws so a failed subscription can be retried.
    /// </summary>
    [TestClass]
    public class KeyTrackerTests
    {
        /// <summary>Exposes the protected reference count so tests can assert it directly.</summary>
        private class ExposedRefTracker : RefCountingKeyTracker<int>
        {
            public int Refs(int key) => RefCount(key);
        }

        [TestMethod]
        public void TryAdd_NewKey_AddsAndRunsActionOnce()
        {
            var tracker = new KeyTracker<int>();
            int calls = 0;

            Assert.IsTrue(tracker.TryAdd(5, () => { calls++; }));

            Assert.AreEqual(1, calls);
            Assert.IsTrue(tracker.Contains(5));
            Assert.AreEqual(1, tracker.Count);
        }

        [TestMethod]
        public void TryAdd_NoAction_AddsKey()
        {
            var tracker = new KeyTracker<int>();

            Assert.IsTrue(tracker.TryAdd(5));
            Assert.IsTrue(tracker.Contains(5));
        }

        [TestMethod]
        public void TryAdd_DuplicateKey_ReturnsFalseAndSkipsAction()
        {
            var tracker = new KeyTracker<int>();
            tracker.TryAdd(5);
            int calls = 0;

            Assert.IsFalse(tracker.TryAdd(5, () => { calls++; }));

            Assert.AreEqual(0, calls);
            Assert.AreEqual(1, tracker.Count);
        }

        [TestMethod]
        public void TryAdd_ActionThrows_RollsBackKeyAndRethrows()
        {
            var tracker = new KeyTracker<int>();

            Assert.ThrowsException<InvalidOperationException>(
                () => tracker.TryAdd(5, (Action)(() => throw new InvalidOperationException())));

            Assert.IsFalse(tracker.Contains(5));
            Assert.AreEqual(0, tracker.Count);
            Assert.IsTrue(tracker.TryAdd(5), "A failed add must leave the key available for a retry");
        }

        [TestMethod]
        public void TryAdd_CanAddTrue_AddsKey()
        {
            var tracker = new KeyTracker<int>();

            Assert.IsTrue(tracker.TryAdd(5, () => true));
            Assert.IsTrue(tracker.Contains(5));
        }

        [TestMethod]
        public void TryAdd_CanAddFalse_ReturnsFalseAndDoesNotAdd()
        {
            var tracker = new KeyTracker<int>();

            Assert.IsFalse(tracker.TryAdd(5, () => false));

            Assert.IsFalse(tracker.Contains(5));
            Assert.AreEqual(0, tracker.Count);
        }

        [TestMethod]
        public void TryAdd_CanAddForExistingKey_IsNotEvaluated()
        {
            var tracker = new KeyTracker<int>();
            tracker.TryAdd(5);
            int calls = 0;

            Assert.IsFalse(tracker.TryAdd(5, () => { calls++; return true; }));

            Assert.AreEqual(0, calls);
        }

        [TestMethod]
        public void TryAdd_CanAdd_ReleasesLockSoLaterCallsOnSameThreadSucceed()
        {
            var tracker = new KeyTracker<int>();

            Assert.IsTrue(tracker.TryAdd(1, () => true));
            Assert.IsFalse(tracker.TryAdd(2, () => false));
            Assert.IsTrue(tracker.TryAdd(3));
            Assert.IsTrue(tracker.TryRemove(1));
            Assert.AreEqual(1, tracker.Count);
        }

        [TestMethod]
        public void TryAdd_CanAddThrows_KeyIsNotAdded()
        {
            var tracker = new KeyTracker<int>();

            Assert.ThrowsException<InvalidOperationException>(
                () => tracker.TryAdd(5, (Func<bool>)(() => throw new InvalidOperationException())));

            Assert.IsFalse(tracker.Contains(5));
        }

        [TestMethod]
        public void TryRemove_PresentKey_RemovesAndRunsAction()
        {
            var tracker = new KeyTracker<int>();
            tracker.TryAdd(5);
            int calls = 0;

            Assert.IsTrue(tracker.TryRemove(5, () => { calls++; }));

            Assert.AreEqual(1, calls);
            Assert.IsFalse(tracker.Contains(5));
        }

        [TestMethod]
        public void TryRemove_MissingKey_ReturnsFalseAndSkipsAction()
        {
            var tracker = new KeyTracker<int>();
            int calls = 0;

            Assert.IsFalse(tracker.TryRemove(5, () => { calls++; }));

            Assert.AreEqual(0, calls);
        }

        [TestMethod]
        public void TryRemove_ActionThrows_KeyStaysRemoved()
        {
            var tracker = new KeyTracker<int>();
            tracker.TryAdd(5);

            Assert.ThrowsException<InvalidOperationException>(
                () => tracker.TryRemove(5, () => throw new InvalidOperationException()));

            Assert.IsFalse(tracker.Contains(5), "Unlike TryAdd, TryRemove does not restore the key when its action throws");
        }

        [TestMethod]
        public void ValuesCopy_Empty_ReturnsEmpty()
        {
            Assert.AreEqual(0, new KeyTracker<int>().ValuesCopy().Count());
        }

        [TestMethod]
        public void ValuesCopy_ReturnsSortedSnapshotUnaffectedByLaterChanges()
        {
            var tracker = new KeyTracker<int>();
            tracker.TryAdd(3);
            tracker.TryAdd(1);
            tracker.TryAdd(2);

            var snapshot = tracker.ValuesCopy().ToArray();
            tracker.TryRemove(2);
            tracker.TryAdd(9);

            CollectionAssert.AreEqual(new[] { 1, 2, 3 }, snapshot);
        }

        [TestMethod]
        public void TryAdd_ConcurrentSameKey_ExactlyOneWinnerRunsAction()
        {
            var tracker = new KeyTracker<int>();
            int actionCalls = 0;
            int winners = 0;

            Parallel.For(0, 64, new ParallelOptions { MaxDegreeOfParallelism = 16 }, _ =>
            {
                if (tracker.TryAdd(7, () => Interlocked.Increment(ref actionCalls)))
                    Interlocked.Increment(ref winners);
            });

            Assert.AreEqual(1, winners);
            Assert.AreEqual(1, actionCalls);
            Assert.AreEqual(1, tracker.Count);
        }

        [TestMethod]
        public void Property_AddRemoveSequence_MatchesHashSetModel()
        {
            Prop.ForAll(Arb.From<byte[]>(), ops =>
            {
                var tracker = new KeyTracker<int>();
                var model = new HashSet<int>();
                foreach (byte op in ops)
                {
                    bool add = op % 2 == 0;
                    int key = op / 2 % 16;
                    bool expected = add ? model.Add(key) : model.Remove(key);
                    bool actual = add ? tracker.TryAdd(key) : tracker.TryRemove(key);
                    if (expected != actual)
                        return false;
                }
                return tracker.Count == model.Count
                    && tracker.ValuesCopy().SequenceEqual(model.OrderBy(k => k));
            }).QuickCheckThrowOnFailure();
        }

        [TestMethod]
        public void AddRef_FirstReference_RunsActionOnceWithKey()
        {
            var tracker = new ExposedRefTracker();
            var seen = new List<int>();

            tracker.AddRef(4, seen.Add);
            tracker.AddRef(4, seen.Add);

            CollectionAssert.AreEqual(new[] { 4 }, seen);
            Assert.AreEqual(2, tracker.Refs(4));
            Assert.AreEqual(1, tracker.Count);
        }

        [TestMethod]
        public void AddRef_ActionThrows_RollsBackAndRetryRunsActionAgain()
        {
            var tracker = new ExposedRefTracker();

            Assert.ThrowsException<InvalidOperationException>(
                () => tracker.AddRef(4, _ => throw new InvalidOperationException()));

            Assert.IsFalse(tracker.Contains(4));
            Assert.AreEqual(0, tracker.Refs(4));

            int calls = 0;
            tracker.AddRef(4, _ => calls++);
            Assert.AreEqual(1, calls);
            Assert.AreEqual(1, tracker.Refs(4));
        }

        [TestMethod]
        public void ReleaseRef_MissingKey_ReturnsFalseAndSkipsAction()
        {
            var tracker = new ExposedRefTracker();
            int calls = 0;

            Assert.IsFalse(tracker.ReleaseRef(4, _ => calls++));

            Assert.AreEqual(0, calls);
        }

        [TestMethod]
        public void ReleaseRef_NotLastReference_KeepsKeyAndSkipsAction()
        {
            var tracker = new ExposedRefTracker();
            tracker.AddRef(4);
            tracker.AddRef(4);
            int calls = 0;

            Assert.IsTrue(tracker.ReleaseRef(4, _ => calls++));

            Assert.AreEqual(0, calls);
            Assert.IsTrue(tracker.Contains(4));
            Assert.AreEqual(1, tracker.Refs(4));
        }

        [TestMethod]
        public void ReleaseRef_LastReference_RunsActionAndRemovesKey()
        {
            var tracker = new ExposedRefTracker();
            tracker.AddRef(4);
            var seen = new List<int>();

            Assert.IsTrue(tracker.ReleaseRef(4, seen.Add));

            CollectionAssert.AreEqual(new[] { 4 }, seen);
            Assert.IsFalse(tracker.Contains(4));
            Assert.AreEqual(0, tracker.Count);
        }

        [TestMethod]
        public void ReleaseRef_LastReferenceActionThrows_KeyStaysTracked()
        {
            var tracker = new ExposedRefTracker();
            tracker.AddRef(4);

            Assert.ThrowsException<InvalidOperationException>(
                () => tracker.ReleaseRef(4, _ => throw new InvalidOperationException()));

            Assert.IsTrue(tracker.Contains(4), "A failed teardown keeps the key so the release can be retried");
            Assert.AreEqual(1, tracker.Refs(4));
        }

        [TestMethod]
        public void Property_AddReleaseSequence_MatchesDictionaryModel()
        {
            Prop.ForAll(Arb.From<byte[]>(), ops =>
            {
                var tracker = new ExposedRefTracker();
                var model = new Dictionary<int, int>();
                int firstCalls = 0, lastCalls = 0, modelFirst = 0, modelLast = 0;
                foreach (byte op in ops)
                {
                    bool add = op % 2 == 0;
                    int key = op / 2 % 16;
                    if (add)
                    {
                        tracker.AddRef(key, _ => firstCalls++);
                        model.TryGetValue(key, out int n);
                        if (n == 0)
                            modelFirst++;
                        model[key] = n + 1;
                    }
                    else
                    {
                        bool present = model.TryGetValue(key, out int n);
                        bool released = tracker.ReleaseRef(key, _ => lastCalls++);
                        if (released != present)
                            return false;
                        if (present)
                        {
                            if (n == 1)
                            {
                                model.Remove(key);
                                modelLast++;
                            }
                            else
                            {
                                model[key] = n - 1;
                            }
                        }
                    }
                }
                return tracker.Count == model.Count
                    && firstCalls == modelFirst
                    && lastCalls == modelLast
                    && model.All(kv => tracker.Refs(kv.Key) == kv.Value);
            }).QuickCheckThrowOnFailure();
        }
    }
}
