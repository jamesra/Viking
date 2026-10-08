using Geometry;
using Geometry.Transforms;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using System;
using System.Threading;
using System.Threading.Tasks;
using Viking.VolumeModel;

namespace VolumeModelTests
{
    /// <summary>
    /// The section mapping cache keeps sections until their estimated memory passes the budget, then evicts the least
    /// recently used sections that were not used since the previous budget pass, and only until it is back under budget.
    /// </summary>
    [TestClass]
    public class SectionTransformsCacheTests
    {
        /// <summary>A mapping with a settable memory estimate that records <see cref="FreeMemory"/> calls.</summary>
        private sealed class FakeMapping(string name, long bytes, ITransform volumeTransform = null) : MappingBase(null, name, "", "")
        {
            public long Bytes = bytes;
            public int FreeCount;

            public override long EstimatedMemoryBytes => Bytes;
            public override ITransform SharedVolumeTransform => volumeTransform;

            public override Task FreeMemory()
            {
                Interlocked.Increment(ref FreeCount);
                return Task.CompletedTask;
            }

            public override Rectangle ControlBounds => new(0, 0, 1, 1);
            public override int[] AvailableLevels => [1];
            public override bool Initialized => true;
            public override Rectangle? SectionBounds => ControlBounds;
            public override Rectangle? VolumeBounds => ControlBounds;
            public override Task Initialize(CancellationToken token) => Task.CompletedTask;
            public override TilePyramid VisibleTiles(Rectangle VisibleBounds, double DownSample) => new(VisibleBounds);
            public override Vector2[] SectionToVolume(Vector2[] P) => P;
            public override Vector2[] VolumeToSection(Vector2[] P) => P;
            public override bool TrySectionToVolume(Vector2 P, out Vector2 transformedP) { transformedP = P; return true; }
            public override bool TryVolumeToSection(Vector2 P, out Vector2 transformedP) { transformedP = P; return true; }
            public override bool[] TrySectionToVolume(in Vector2[] Points, out Vector2[] transformedP) { transformedP = Points; return new bool[Points.Length]; }
            public override bool[] TryVolumeToSection(in Vector2[] Points, out Vector2[] transformedP) { transformedP = Points; return new bool[Points.Length]; }
        }

        /// <summary>Exposes entries so a test can set access times and used marks.</summary>
        private sealed class TestCache : SectionTransformsCache
        {
            public SectionMappingsCacheEntry Entry(int section) => dictEntries[section];
        }

        private static readonly DateTime Start = new(2026, 1, 1, 0, 0, 0, DateTimeKind.Utc);

        /// <summary>
        /// Adds one 100-byte mapping per section, each accessed a minute after the previous, then runs a pass so none
        /// carries the "used" mark of a new entry.
        /// </summary>
        private static FakeMapping[] Fill(TestCache cache, int sections, long bytesEach = 100)
        {
            FakeMapping[] mappings = new FakeMapping[sections];
            for (int i = 0; i < sections; i++)
            {
                mappings[i] = new FakeMapping($"m{i}", bytesEach);
                SectionTransformsDictionary dict = new();
                dict[mappings[i].Name] = mappings[i];
                cache.GetOrAdd(i, dict);
                cache.Entry(i).LastAccessed = Start.AddMinutes(i);
            }

            cache.EnforceMemoryBudget();
            return mappings;
        }

        [TestMethod]
        public void DefaultBudgetIsOneGibibyte()
        {
            Assert.AreEqual(1L << 30, new SectionTransformsCache().MemoryBudgetBytes);
        }

        [TestMethod]
        public void UnderBudget_KeepsUnusedSections()
        {
            TestCache cache = new() { MemoryBudgetBytes = 1000 };
            FakeMapping[] mappings = Fill(cache, 5);

            Assert.AreEqual(0, cache.EnforceMemoryBudget());
            Assert.AreEqual(500, cache.CachedSize);
            for (int i = 0; i < 5; i++)
                Assert.IsTrue(cache.ContainsKey(i));
            Assert.IsTrue(Array.TrueForAll(mappings, m => m.FreeCount == 0));
        }

        [TestMethod]
        public void OverBudget_EvictsOldestUnusedUntilUnderBudget()
        {
            TestCache cache = new() { MemoryBudgetBytes = 300 };
            FakeMapping[] mappings = Fill(cache, 5);

            Assert.AreEqual(2, cache.EnforceMemoryBudget());

            Assert.IsFalse(cache.ContainsKey(0));
            Assert.IsFalse(cache.ContainsKey(1));
            Assert.IsTrue(cache.ContainsKey(2));
            Assert.IsTrue(cache.ContainsKey(3));
            Assert.IsTrue(cache.ContainsKey(4));
            Assert.AreEqual(300, cache.CachedSize);
            Assert.AreEqual(1, mappings[0].FreeCount);
            Assert.AreEqual(1, mappings[1].FreeCount);
            Assert.AreEqual(0, mappings[2].FreeCount);
        }

        [TestMethod]
        public void NewSections_AreNotEvictedBeforeTheNextPass()
        {
            TestCache cache = new() { MemoryBudgetBytes = 300 };
            for (int i = 0; i < 5; i++)
            {
                SectionTransformsDictionary dict = new();
                dict["m"] = new FakeMapping("m", 100);
                cache.GetOrAdd(i, dict);
            }

            Assert.AreEqual(0, cache.EnforceMemoryBudget(), "every section is new, so all count as used since the last pass");
            Assert.AreEqual(2, cache.EnforceMemoryBudget());
        }

        [TestMethod]
        public void SectionUsedSinceLastPass_IsKept_EvenWhenOldest()
        {
            TestCache cache = new() { MemoryBudgetBytes = 300 };
            Fill(cache, 5);

            Assert.IsNotNull(cache.Fetch(0));
            cache.Entry(0).LastAccessed = Start.AddMinutes(-10);

            Assert.AreEqual(2, cache.EnforceMemoryBudget());
            Assert.IsTrue(cache.ContainsKey(0));
            Assert.IsFalse(cache.ContainsKey(1));
            Assert.IsFalse(cache.ContainsKey(2));
            Assert.IsTrue(cache.ContainsKey(3));
        }

        [TestMethod]
        public void EveryPass_StartsANewWindow()
        {
            TestCache cache = new() { MemoryBudgetBytes = 300 };
            Fill(cache, 5);
            Assert.IsNotNull(cache.Fetch(0));
            cache.Entry(0).LastAccessed = Start.AddMinutes(-10);
            Assert.AreEqual(2, cache.EnforceMemoryBudget());

            ((FakeMapping)cache.Fetch(3)["m3"]).Bytes = 150;

            Assert.AreEqual(1, cache.EnforceMemoryBudget(), "section 0 was kept by the previous pass but not used since");
            Assert.IsFalse(cache.ContainsKey(0));
            Assert.IsTrue(cache.ContainsKey(3));
            Assert.IsTrue(cache.ContainsKey(4));
            Assert.AreEqual(250, cache.CachedSize);
        }

        [TestMethod]
        public void GrowingSections_AreMeasuredAtEachPass()
        {
            TestCache cache = new() { MemoryBudgetBytes = 1000 };
            FakeMapping[] mappings = Fill(cache, 5);
            Assert.AreEqual(0, cache.EnforceMemoryBudget());

            mappings[4].Bytes = 700;

            Assert.AreEqual(1, cache.EnforceMemoryBudget());
            Assert.AreEqual(1000, cache.CachedSize);
            Assert.IsFalse(cache.ContainsKey(0));
            Assert.IsTrue(cache.ContainsKey(1));
        }

        [TestMethod]
        public void SharedVolumeTransform_IsCountedOncePerSection()
        {
            MeshTransform volume = MakeMesh(10);
            long volumeBytes = MappingBase.EstimateTransformBytes(volume);
            Assert.IsTrue(volumeBytes > 0);

            SectionTransformsDictionary dict = new();
            dict["mosaic"] = new FakeMapping("mosaic", 100, volume);
            dict["tileset"] = new FakeMapping("tileset", 50, volume);

            Assert.AreEqual(150 + volumeBytes, new SectionMappingsCacheEntry(1, dict).EstimatedMemoryBytes());
        }

        [TestMethod]
        public void TransformEstimate_FollowsBuiltCaches()
        {
            MeshTransform mesh = MakeMesh(20);
            long unbuilt = mesh.EstimatedMemoryBytes;
            Assert.IsTrue(unbuilt >= mesh.MapPoints.Length * 32);

            _ = mesh.TriangleIndicies;
            long triangulated = mesh.EstimatedMemoryBytes;
            Assert.IsTrue(triangulated > unbuilt);

            _ = mesh.mapTrianglesRTree;
            _ = mesh.controlTrianglesRTree;
            _ = mesh.mappedPointsRTree;
            long indexed = mesh.EstimatedMemoryBytes;
            Assert.IsTrue(indexed > triangulated + (mesh.MapPoints.Length * 1000));

            mesh.MinimizeMemory();
            Assert.AreEqual(triangulated, mesh.EstimatedMemoryBytes, "MinimizeMemory drops the RTrees and keeps the triangles");
        }

        [TestMethod]
        public void GridTransform_DoesNotCountSharedGridTopology()
        {
            const int g = 6;
            MappingVector2[] points = new MappingVector2[g * g];
            for (int y = 0; y < g; y++)
                for (int x = 0; x < g; x++)
                    points[x + (y * g)] = new MappingVector2(new Vector2(x * 10.0, y * 10.0), new Vector2(x * 10.0, y * 10.0));

            GridTransform grid = new(points, new Rectangle(0, 50, 0, 50), g, g, new TransformBasicInfo(Start));
            long before = grid.EstimatedMemoryBytes;
            _ = grid.TriangleIndicies;
            _ = grid.Edges;
            Assert.AreEqual(before, grid.EstimatedMemoryBytes);
        }

        private static MeshTransform MakeMesh(int g)
        {
            Random random = new(g);
            MappingVector2[] points = new MappingVector2[g * g];
            for (int y = 0; y < g; y++)
                for (int x = 0; x < g; x++)
                {
                    Vector2 mapped = new(x * 100.0, y * 100.0);
                    points[x + (y * g)] = new MappingVector2(new Vector2(mapped.X + (random.NextDouble() * 20), mapped.Y + (random.NextDouble() * 20)), mapped);
                }

            return new MeshTransform(points, new TransformBasicInfo(Start));
        }
    }
}
