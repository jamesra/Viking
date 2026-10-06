using Geometry;
using Geometry.Transforms;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using System;
using System.IO;
using System.Linq;
using System.Text;
using System.Text.Json;
using VolumeModel;
using Path = System.IO.Path;

namespace VolumeModelTests
{
    /// <summary>
    /// The transform cache must give back transforms that map exactly like the ones saved: same points bit for bit, same
    /// grid layout, bounds and info, and the same mapped results. Older cache formats must be rejected so they are rebuilt.
    /// </summary>
    [TestClass]
    public class JsonTransformSerializerTests
    {
        private static readonly DateTime Modified = new(2024, 3, 7, 12, 34, 56, 789, DateTimeKind.Utc);

        private static ITransform[] RoundTrip(params ITransform[] transforms)
        {
            using MemoryStream stream = new();
            JsonTransformSerializer.SerializeArray(stream, transforms);
            stream.Position = 0;
            return JsonTransformSerializer.DeserializeArray(stream);
        }

        /// <summary>A grid transform at volume scale with coordinates that use every digit of a double.</summary>
        private static GridTransform MakeGrid(int gx, int gy, int seed)
        {
            Random random = new(seed);
            const double cell = 1234.5678901234567;
            MappingVector2[] points = new MappingVector2[gx * gy];
            for (int y = 0; y < gy; y++)
            {
                for (int x = 0; x < gx; x++)
                {
                    Vector2 mapped = new(x * cell, y * cell);
                    Vector2 control = new(350000.123456789 + mapped.X + (random.NextDouble() * 40) - 20,
                                          420000.987654321 + mapped.Y + (random.NextDouble() * 40) - 20);
                    points[x + (y * gx)] = new MappingVector2(control, mapped);
                }
            }

            Rectangle bounds = new(0, (gx - 1) * cell, 0, (gy - 1) * cell);
            return new GridTransform(points, bounds, gx, gy, new TileTransformInfo("Leveled_042.png", 42, Modified, 4096.5, 4095.25));
        }

        private static MeshTransform MakeMesh(int count, int seed)
        {
            Random random = new(seed);
            MappingVector2[] points = new MappingVector2[count];
            for (int i = 0; i < count; i++)
            {
                Vector2 mapped = new(random.NextDouble() * 500000, random.NextDouble() * 500000);
                Vector2 control = mapped + new Vector2((random.NextDouble() * 300) - 150, (random.NextDouble() * 300) - 150);
                points[i] = new MappingVector2(control, mapped);
            }
            return new MeshTransform(points, new StosTransformInfo(645, 646, Modified));
        }

        private static void AssertSamePoints(MappingVector2[] expected, MappingVector2[] actual)
        {
            Assert.AreEqual(expected.Length, actual.Length);
            for (int i = 0; i < expected.Length; i++)
            {
                Assert.AreEqual(BitConverter.DoubleToInt64Bits(expected[i].ControlPoint.X), BitConverter.DoubleToInt64Bits(actual[i].ControlPoint.X), $"control X {i}");
                Assert.AreEqual(BitConverter.DoubleToInt64Bits(expected[i].ControlPoint.Y), BitConverter.DoubleToInt64Bits(actual[i].ControlPoint.Y), $"control Y {i}");
                Assert.AreEqual(BitConverter.DoubleToInt64Bits(expected[i].MappedPoint.X), BitConverter.DoubleToInt64Bits(actual[i].MappedPoint.X), $"mapped X {i}");
                Assert.AreEqual(BitConverter.DoubleToInt64Bits(expected[i].MappedPoint.Y), BitConverter.DoubleToInt64Bits(actual[i].MappedPoint.Y), $"mapped Y {i}");
            }
        }

        /// <summary>Forward and inverse mapping of many points must give identical results and identical success flags.</summary>
        private static void AssertSameMapping(ITransform expected, ITransform actual, Rectangle mappedBounds, Rectangle controlBounds)
        {
            Random random = new(7);
            Vector2[] mappedQueries = [.. Enumerable.Range(0, 500).Select(_ => new Vector2(
                mappedBounds.Left + (random.NextDouble() * mappedBounds.Width), mappedBounds.Bottom + (random.NextDouble() * mappedBounds.Height)))];
            Vector2[] controlQueries = [.. Enumerable.Range(0, 500).Select(_ => new Vector2(
                controlBounds.Left + (random.NextDouble() * controlBounds.Width), controlBounds.Bottom + (random.NextDouble() * controlBounds.Height)))];

            bool[] okA = expected.TryTransform(mappedQueries, out Vector2[] a);
            bool[] okB = actual.TryTransform(mappedQueries, out Vector2[] b);
            CollectionAssert.AreEqual(okA, okB, "forward success flags");
            CollectionAssert.AreEqual(a, b, "forward results");

            okA = expected.TryInverseTransform(controlQueries, out a);
            okB = actual.TryInverseTransform(controlQueries, out b);
            CollectionAssert.AreEqual(okA, okB, "inverse success flags");
            CollectionAssert.AreEqual(a, b, "inverse results");
        }

        [TestMethod]
        public void GridTransformRoundTripsExactly()
        {
            GridTransform original = MakeGrid(9, 7, 1);
            GridTransform loaded = (GridTransform)RoundTrip(original).Single();

            AssertSamePoints(original.MapPoints, loaded.MapPoints);
            Assert.AreEqual(original.GridSizeX, loaded.GridSizeX);
            Assert.AreEqual(original.GridSizeY, loaded.GridSizeY);
            Assert.AreEqual(original.MappedBounds, loaded.MappedBounds);
            CollectionAssert.AreEqual(original.TriangleIndicies, loaded.TriangleIndicies);

            TileTransformInfo info = (TileTransformInfo)loaded.Info;
            Assert.AreEqual("Leveled_042.png", info.TileFileName);
            Assert.AreEqual(42, info.TileNumber);
            Assert.AreEqual(4096.5, info.ImageWidth);
            Assert.AreEqual(4095.25, info.ImageHeight);
            Assert.AreEqual(Modified, info.LastModified);
            Assert.AreEqual(DateTimeKind.Utc, info.LastModified.Kind);

            AssertSameMapping(original, loaded, original.MappedBounds, original.ControlBounds);
        }

        [TestMethod]
        public void MeshTransformRoundTripsExactly()
        {
            MeshTransform original = MakeMesh(300, 2);
            MeshTransform loaded = (MeshTransform)RoundTrip(original).Single();

            AssertSamePoints(original.MapPoints, loaded.MapPoints);
            CollectionAssert.AreEqual(original.TriangleIndicies, loaded.TriangleIndicies);

            StosTransformInfo info = (StosTransformInfo)loaded.Info;
            Assert.AreEqual(645, info.ControlSection);
            Assert.AreEqual(646, info.MappedSection);
            Assert.AreEqual(Modified, info.LastModified);

            AssertSameMapping(original, loaded, original.MappedBounds, original.ControlBounds);
        }

        [TestMethod]
        public void RbfTransformRoundTripsExactly()
        {
            MappingVector2[] points = MakeMesh(60, 3).MapPoints;
            RBFTransform original = new(points, new TransformBasicInfo(Modified));
            RBFTransform loaded = (RBFTransform)RoundTrip(original).Single();

            AssertSamePoints(original.MapPoints, loaded.MapPoints);
            Assert.AreEqual(Modified, loaded.Info.LastModified);

            Vector2 q = new(250000.5, 250000.25);
            Assert.AreEqual(original.Transform(q), loaded.Transform(q));
            Assert.AreEqual(original.InverseTransform(q), loaded.InverseTransform(q));
        }

        [TestMethod]
        public void ArrayKeepsOrderAndTypes()
        {
            ITransform[] originals = [MakeGrid(4, 4, 4), MakeMesh(40, 5), MakeGrid(3, 5, 6)];
            ITransform[] loaded = RoundTrip(originals);

            Assert.AreEqual(originals.Length, loaded.Length);
            for (int i = 0; i < originals.Length; i++)
            {
                Assert.AreEqual(originals[i].GetType(), loaded[i].GetType(), $"type {i}");
                AssertSamePoints(((ITransformControlPoints)originals[i]).MapPoints, ((ITransformControlPoints)loaded[i]).MapPoints);
            }
        }

        [TestMethod]
        public void SingleTransformApiRoundTrips()
        {
            GridTransform original = MakeGrid(5, 5, 8);
            using MemoryStream stream = new();
            JsonTransformSerializer.Serialize(stream, original);
            stream.Position = 0;
            AssertSamePoints(original.MapPoints, ((GridTransform)JsonTransformSerializer.Deserialize(stream)).MapPoints);
        }

        /// <summary>Version 1 caches (the reflection serializer's <c>{}</c> entries, and the property-per-point arrays) are stale.</summary>
        [DataTestMethod]
        [DataRow("{}")]
        [DataRow("[{}]")]
        [DataRow("[]")]
        [DataRow("[{\"mapPoints\":[{\"controlPoint\":{\"x\":1,\"y\":2},\"mappedPoint\":{\"x\":3,\"y\":4}}],\"info\":{\"infoType\":\"basic\"}}]")]
        [DataRow("{\"format\":\"viking-transform-cache\",\"version\":1,\"transforms\":[]}")]
        public void OlderFormatsAreRejected(string json)
        {
            using MemoryStream stream = new(Encoding.UTF8.GetBytes(json));
            Assert.ThrowsException<JsonException>(() => JsonTransformSerializer.DeserializeArray(stream));
        }

        [TestMethod]
        public void UnsupportedTransformTypeIsRejectedBeforeWriting()
        {
            DiscreteTransformWithContinuousFallback unsupported = new(MakeMesh(20, 9), new RBFTransform(MakeMesh(20, 9).MapPoints, new TransformBasicInfo()), new TransformBasicInfo());
            using MemoryStream stream = new();
            Assert.ThrowsException<NotSupportedException>(() => JsonTransformSerializer.SerializeArray(stream, [MakeGrid(3, 3, 1), unsupported]));
            Assert.AreEqual(0, stream.Length, "nothing may be written when a transform is unsupported");
        }

        /// <summary>
        /// The real RC2 section 646 mosaic, when VikingVolumeBench prime has saved it: every tile must round-trip exactly
        /// and map identically.
        /// </summary>
        [TestMethod]
        public void Rc2MosaicRoundTripsExactly()
        {
            string root = Environment.GetEnvironmentVariable("VIKINGBENCH_ROOT") ??
                          Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "VikingVolumeBench");
            string mosaic = Path.Combine(root, "mirror", "RC2", "TEM", "0646", "TEM", "Grid_Cel128_Mes8_Mes8_Thr0.25_it10_sp4.mosaic");
            if (!File.Exists(mosaic))
                Assert.Inconclusive($"RC2 mosaic not primed at {mosaic}");

            ITransform[] tiles = TransformFactory.LoadMosaic(Path.GetDirectoryName(mosaic), File.ReadAllLines(mosaic), File.GetLastWriteTimeUtc(mosaic));
            ITransform[] loaded = RoundTrip(tiles);

            Assert.AreEqual(tiles.Length, loaded.Length);
            for (int i = 0; i < tiles.Length; i++)
            {
                ITransformControlPoints a = (ITransformControlPoints)tiles[i];
                ITransformControlPoints b = (ITransformControlPoints)loaded[i];
                AssertSamePoints(a.MapPoints, b.MapPoints);
                Assert.AreEqual(((ITransformInfo)tiles[i]).Info.ToString(), ((ITransformInfo)loaded[i]).Info.ToString());
            }

            AssertSameMapping(tiles[0], loaded[0], ((ITransformControlPoints)tiles[0]).MappedBounds, ((ITransformControlPoints)tiles[0]).ControlBounds);
        }
    }
}
