using System;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using WebAnnotationModel;
using WebAnnotationModel.Objects;

namespace WebAnnotationTests
{
    /// <summary>
    /// Pins <see cref="LocationObj.RequestParentOnce"/>, the background parent fetch that <see cref="LocationObj.Parent"/>
    /// starts when the parent structure is not in the local store. Views read Parent two or three times each while
    /// they are built, and many locations share one parent, so without the in-flight check every read sent its own
    /// GetStructureByID call for the same ID.
    /// Each test uses its own parent ID because the in-flight set is shared by every location.
    /// </summary>
    [TestClass]
    public class LocationParentFetchTests
    {
        private static readonly TimeSpan Timeout = TimeSpan.FromSeconds(10);

        [TestMethod]
        public void RepeatedReadsWhileAFetchIsRunningStartOneFetch()
        {
            const long parentID = -1_000_001;
            int fetches = 0;
            using ManualResetEventSlim release = new(false);

            Task first = LocationObj.RequestParentOnce(parentID, _ => { Interlocked.Increment(ref fetches); release.Wait(Timeout); });
            Assert.IsNotNull(first);

            for (int i = 0; i < 50; i++)
            {
                Assert.IsNull(LocationObj.RequestParentOnce(parentID, _ => Interlocked.Increment(ref fetches)));
            }

            release.Set();
            Assert.IsTrue(first.Wait(Timeout));
            Assert.AreEqual(1, fetches);
        }

        /// <summary>
        /// A fetch that found nothing (deleted or inaccessible parent) releases the ID, so a later read asks again.
        /// </summary>
        [TestMethod]
        public void ReadAfterAFetchCompletesStartsANewFetch()
        {
            const long parentID = -1_000_002;
            int fetches = 0;

            Assert.IsTrue(LocationObj.RequestParentOnce(parentID, _ => Interlocked.Increment(ref fetches)).Wait(Timeout));
            Assert.IsTrue(LocationObj.RequestParentOnce(parentID, _ => Interlocked.Increment(ref fetches)).Wait(Timeout));

            Assert.AreEqual(2, fetches);
        }

        [TestMethod]
        public void FetchThatThrowsReleasesTheID()
        {
            const long parentID = -1_000_003;

            Task failed = LocationObj.RequestParentOnce(parentID, _ => throw new InvalidOperationException("server down"));
            Assert.ThrowsException<AggregateException>(() => failed.Wait(Timeout));

            int fetches = 0;
            Assert.IsTrue(LocationObj.RequestParentOnce(parentID, _ => Interlocked.Increment(ref fetches)).Wait(Timeout));
            Assert.AreEqual(1, fetches);
        }

        [TestMethod]
        public void FetchesForDifferentParentsRunIndependently()
        {
            const long blockedID = -1_000_004;
            const long otherID = -1_000_005;
            long fetchedOther = 0;
            using ManualResetEventSlim release = new(false);

            Task blocked = LocationObj.RequestParentOnce(blockedID, _ => release.Wait(Timeout));
            Task other = LocationObj.RequestParentOnce(otherID, id => Interlocked.Exchange(ref fetchedOther, id));

            Assert.IsNotNull(other);
            Assert.IsTrue(other.Wait(Timeout));
            Assert.AreEqual(otherID, Interlocked.Read(ref fetchedOther));

            release.Set();
            Assert.IsTrue(blocked.Wait(Timeout));
        }
    }
}
