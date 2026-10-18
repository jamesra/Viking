using Microsoft.VisualStudio.TestTools.UnitTesting;
using System.Diagnostics;
using System.IO;
using System.Linq;
using SqlGeometryUtils;
using WebAnnotationModel;

namespace WebAnnotationTests.View
{
    /// <summary>
    /// The log written when a location is added to the spatial index twice: it has to say how the ID got there, so it keeps a bounded
    /// history of store and index events per location and writes it before the assertion can block the thread.
    /// </summary>
    [TestClass]
    public class LocationIndexDiagTests
    {
        [TestInitialize]
        public void Clear() => LocationIndexDiag.Reset();

        [TestMethod]
        public void HistoryIsOnlyThatLocationsEventsOldestFirst()
        {
            LocationIndexDiag.Record(1, "store add");
            LocationIndexDiag.Record(2, "store add");
            LocationIndexDiag.Record(1, "index add");
            LocationIndexDiag.Record(1, "store remove");

            var history = LocationIndexDiag.HistoryOf(1);

            CollectionAssert.AreEqual(new[] { "store add", "index add", "store remove" }, history.Select(entry => entry.Description).ToArray());
            Assert.IsTrue(history.All(entry => entry.Thread == System.Threading.Thread.CurrentThread.ManagedThreadId));
            Assert.IsTrue(history[0].Timestamp <= history[2].Timestamp);
            Assert.AreEqual(0, LocationIndexDiag.HistoryOf(99).Count);
        }

        [TestMethod]
        public void TheRingKeepsOnlyTheNewestEvents()
        {
            LocationIndexDiag.Record(7, "an old event");
            for (int i = 0; i < LocationIndexDiag.Capacity; i++)
                LocationIndexDiag.Record(1, "event " + i);

            Assert.AreEqual(0, LocationIndexDiag.HistoryOf(7).Count, "pushed out by Capacity newer events");
            var kept = LocationIndexDiag.HistoryOf(1);
            Assert.AreEqual(LocationIndexDiag.Capacity, kept.Count);
            Assert.AreEqual("event 0", kept[0].Description);
            Assert.AreEqual("event " + (LocationIndexDiag.Capacity - 1), kept[kept.Count - 1].Description);
        }

        [TestMethod]
        public void TheReportNamesTheLocationItsHistoryAndTheStack()
        {
            LocationIndexDiag.Record(55, "store add");
            LocationIndexDiag.Record(55, "index delete missed (not in the index)");
            long now = Stopwatch.GetTimestamp();

            string text = LocationIndexDiag.Describe(55, "Adding section=3.", LocationIndexDiag.HistoryOf(55), now, "at A()\nat B()\n", thread: 12);

            StringAssert.Contains(text, "Location 55 is already in the spatial index (thread 12)");
            StringAssert.Contains(text, "Adding section=3.");
            Assert.IsTrue(text.IndexOf("store add") < text.IndexOf("index delete missed"), "oldest first");
            StringAssert.Contains(text, "at A()");
            StringAssert.Contains(text, "at B()");
        }

        [TestMethod]
        public void AReportWithNoHistorySaysSo()
        {
            string text = LocationIndexDiag.Describe(8, "x", LocationIndexDiag.HistoryOf(8), Stopwatch.GetTimestamp(), "", thread: 1);

            StringAssert.Contains(text, "No earlier event for this location is kept");
        }

        [TestMethod]
        public void ADuplicateIsWrittenToTheLogFileAndReportsAreCapped()
        {
            long id = 9_000_000_001;
            string realLog = LocationIndexDiag.LogPath;
            string testLog = Path.Combine(Path.GetTempPath(), "viking-location-index-test-" + System.Guid.NewGuid().ToString("N") + ".log");
            LocationIndexDiag.LogPath = testLog;
            try
            {
                LocationIndexDiag.Record(id, "store add");

                for (int i = 0; i < LocationIndexDiag.MaxReports + 10; i++)
                    LocationIndexDiag.ReportDuplicate(id, "Adding test " + i);

                string log = File.ReadAllText(testLog);
                int written = log.Split(new[] { $"Location {id} is already in the spatial index" }, System.StringSplitOptions.None).Length - 1;
                Assert.AreEqual(LocationIndexDiag.MaxReports, written, "no more than the cap, so a loop cannot fill the disk");
                StringAssert.Contains(log, "store add");
            }
            finally
            {
                LocationIndexDiag.LogPath = realLog;
                File.Delete(testLog);
            }
        }

        private static LocationObj SquareLocation(long id)
        {
            var shape = Microsoft.SqlServer.Types.SqlGeometry.STGeomFromText(
                new System.Data.SqlTypes.SqlChars("POLYGON((0 0, 10 0, 10 10, 0 10, 0 0))"), 0);
            return new LocationObj(new AnnotationService.Types.Location
            {
                ID = id,
                TypeCode = (short)Viking.AnnotationServiceTypes.Interfaces.LocationType.POLYGON,
                Section = 1,
                MosaicShapeWKB = shape.AsBinary(),
                VolumeShapeWKB = shape.AsBinary()
            });
        }

        [TestMethod]
        public void AddingALocationTheIndexAlreadyHoldsReplacesItInsteadOfThrowing()
        {
            string realLog = LocationIndexDiag.LogPath;
            LocationIndexDiag.LogPath = Path.Combine(Path.GetTempPath(), "viking-location-index-test-" + System.Guid.NewGuid().ToString("N") + ".log");
            try
            {
                var index = new LocationRTree(null);
                var location = SquareLocation(42);

                index.AddObject(location);
                index.AddObject(location);

                Assert.AreEqual(1, index.SpatialSearch.Count);
                Assert.IsTrue(index.SpatialSearch.Contains(42));
            }
            finally
            {
                File.Delete(LocationIndexDiag.LogPath);
                LocationIndexDiag.LogPath = realLog;
            }
        }

        [TestMethod]
        public void ConcurrentAddsAndRemovesOfTheSameIdsNeverThrow()
        {
            string realLog = LocationIndexDiag.LogPath;
            LocationIndexDiag.LogPath = Path.Combine(Path.GetTempPath(), "viking-location-index-test-" + System.Guid.NewGuid().ToString("N") + ".log");
            try
            {
                var index = new LocationRTree(null);
                var locations = Enumerable.Range(1, 50).Select(i => SquareLocation(i)).ToArray();

                System.Threading.Tasks.Parallel.For(0, 8, new System.Threading.Tasks.ParallelOptions { MaxDegreeOfParallelism = 8 }, worker =>
                {
                    for (int round = 0; round < 400; round++)
                    {
                        foreach (var location in locations)
                        {
                            if ((round + worker) % 2 == 0)
                                index.AddObject(location);
                            else
                                index.RemoveObject(location.ID);
                        }
                    }
                });

                Assert.IsTrue(index.SpatialSearch.Count <= locations.Length, "an ID is never indexed twice");
            }
            finally
            {
                File.Delete(LocationIndexDiag.LogPath);
                LocationIndexDiag.LogPath = realLog;
            }
        }
    }
}
