using Geometry;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Viking.SectionCorrection;
using Path = System.IO.Path;

namespace SectionCorrection.Tests
{
    [TestClass]
    public class PublishedFieldTests
    {
        static List<LatticeNode> FourCorners(double dx = 10, double dy = -4) =>
        [
            new(0, 0, dx, dy, 3),
            new(1, 0, dx, dy, 3),
            new(0, 1, dx, dy, 3),
            new(1, 1, dx, dy, 3)
        ];

        static CorrectionProvenanceDto Provenance() => new()
        {
            BuiltUtc = DateTime.UtcNow,
            AnnotationWatermark = DateTime.UtcNow,
            PitchNm = 2000,
            KernelRadiusNm = 8000,
            MinAnnotationVotes = 3,
            ResidualWindow = new ResidualWindowDto { MinBoth = 2, MaxBoth = 3, MinOne = 5, MaxOne = 7 }
        };

        [TestMethod]
        public void NpzRoundtrip_PreservesNodesAndSample()
        {
            SectionVectorField original = new(42, 2000, 8000, FourCorners());
            string dir = Path.Combine(Path.GetTempPath(), "section-correction-tests-" + Guid.NewGuid().ToString("N"));
            Directory.CreateDirectory(dir);
            try
            {
                var set = new PublishedCorrectionSet(
                    new CorrectionManifestDto
                    {
                        StosGroup = "SliceToVolume1",
                        VolumeUrl = "",
                        Provenance = Provenance()
                    },
                    new Dictionary<long, SectionVectorField> { [42] = original });
                set.WriteDirectory(dir);

                PublishedCorrectionSet loaded = PublishedCorrectionSet.LoadDirectory(dir);
                Assert.AreEqual("SliceToVolume1", loaded.StosGroup);
                Assert.IsTrue(loaded.TryGetSection(42, out SectionVectorField roundtrip));
                Assert.AreEqual(4, roundtrip.NodeCount);

                (Vector2 a, bool trustedA) = original.Sample(new Vector2(1000, 1000));
                (Vector2 b, bool trustedB) = roundtrip.Sample(new Vector2(1000, 1000));
                Assert.IsTrue(trustedA);
                Assert.IsTrue(trustedB);
                Assert.AreEqual(a.X, b.X, 1e-3);
                Assert.AreEqual(a.Y, b.Y, 1e-3);
                Assert.IsTrue(File.Exists(Path.Combine(dir, "42.vfield.npz")));
                Assert.IsTrue(File.Exists(Path.Combine(dir, "volume.npz")));
            }
            finally
            {
                Directory.Delete(dir, true);
            }
        }

        [TestMethod]
        public void Sample_OutsideSupport_IsIdentityUntrusted()
        {
            SectionVectorField field = new(1, 2000, 8000, FourCorners());
            (Vector2 offset, bool trusted) = field.Sample(new Vector2(1e7, 1e7));
            Assert.IsFalse(trusted);
            Assert.AreEqual(0, offset.X, 1e-9);
            Assert.AreEqual(0, offset.Y, 1e-9);
        }

        [TestMethod]
        public void Catalog_LoadsStosGroupDirectory()
        {
            string root = Path.Combine(Path.GetTempPath(), "section-correction-catalog-" + Guid.NewGuid().ToString("N"));
            string groupDir = Path.Combine(root, "SliceToVolume1");
            Directory.CreateDirectory(groupDir);
            try
            {
                new PublishedCorrectionSet(
                    new CorrectionManifestDto
                    {
                        StosGroup = "SliceToVolume1",
                        Provenance = Provenance()
                    },
                    new Dictionary<long, SectionVectorField>
                    {
                        [7] = new SectionVectorField(7, 2000, 8000, FourCorners())
                    }).WriteDirectory(groupDir);

                using CorrectionCatalog catalog = new(root);
                catalog.Load();
                Assert.IsTrue(catalog.TryGet("SliceToVolume1", out PublishedCorrectionSet set));
                Assert.IsTrue(set.TryGetSection(7, out _));
                Assert.AreEqual(1, catalog.Sets.Count);
            }
            finally
            {
                Directory.Delete(root, true);
            }
        }

        [TestMethod]
        public void SampleParity_MatchesNeighborResidualField()
        {
            List<LatticeNode> nodes = FourCorners(12, -8);
            SectionVectorField published = new(5, 2000, 8000, nodes);
            AnnotationVizLib.NeighborResidualField neighbor = AnnotationVizLib.NeighborResidualField.FromLattice(
                nodes.Select(n => (5, n.Gx, n.Gy, n.Offset, n.VoteCount)),
                2000,
                8000);

            Vector2[] queries =
            [
                new(1000, 1000),
                new(0, 0),
                new(2500, 500),
                new(1e6, 1e6)
            ];
            foreach (Vector2 q in queries)
            {
                (Vector2 a, bool ta) = published.Sample(q);
                (Vector2 b, bool tb) = neighbor.SampleAlways(q, 5);
                Assert.AreEqual(tb, ta, $"trusted mismatch at {q}");
                Assert.AreEqual(b.X, a.X, 1e-6, $"dx mismatch at {q}");
                Assert.AreEqual(b.Y, a.Y, 1e-6, $"dy mismatch at {q}");
            }
        }
    }
}
