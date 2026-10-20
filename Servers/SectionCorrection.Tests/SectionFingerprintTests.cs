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
    public class SectionFingerprintTests
    {
        static readonly DateTime T0 = new(2026, 1, 1, 0, 0, 0, DateTimeKind.Utc);

        static List<(long Id, long ParentId, long Z, DateTime Modified)> Locations() =>
        [
            (1, 10, 5, T0), (2, 10, 6, T0), (3, 10, 7, T0), (4, 11, 6, T0)
        ];

        static List<(long A, long B, DateTime Created)> Links() => [(1, 2, T0), (2, 3, T0)];

        [TestMethod]
        public void Compute_SummarisesEachSection()
        {
            Dictionary<long, SectionFingerprintDto> fp = SectionFingerprintBuilder.Compute(Locations(), Links());

            CollectionAssert.AreEquivalent(new long[] { 5, 6, 7 }, fp.Keys.ToList());
            Assert.AreEqual(2, fp[6].Rows);
            Assert.AreEqual(2 + 4, fp[6].SumId);
            Assert.AreEqual(10 + 11, fp[6].SumParent);
            Assert.AreEqual(T0.Ticks, fp[6].MaxModifiedTicks);
            Assert.AreEqual(2, fp[6].LinkRows, "section 6 holds the end of link 1-2 and the start of link 2-3");
            Assert.AreEqual(1, fp[5].LinkRows);
        }

        [TestMethod]
        public void Compute_IgnoresLinksToUnknownLocations()
        {
            Dictionary<long, SectionFingerprintDto> fp = SectionFingerprintBuilder.Compute(Locations(), [(3, 999, T0)]);

            Assert.AreEqual(1, fp[7].LinkRows);
            Assert.AreEqual(0, fp[5].LinkRows);
        }

        [TestMethod]
        public void DirtySections_UnchangedData_IsEmpty()
        {
            var a = SectionFingerprintBuilder.Compute(Locations(), Links());
            var b = SectionFingerprintBuilder.Compute(Locations(), Links());

            Assert.AreEqual(0, SectionFingerprintDiff.DirtySections(a, b).Count);
        }

        [TestMethod]
        public void DirtySections_EditedLocation_MarksItsSection()
        {
            var before = SectionFingerprintBuilder.Compute(Locations(), Links());
            List<(long Id, long ParentId, long Z, DateTime Modified)> edited = Locations();
            edited[1] = (2, 10, 6, T0.AddHours(1));

            var dirty = SectionFingerprintDiff.DirtySections(before, SectionFingerprintBuilder.Compute(edited, Links()));

            CollectionAssert.AreEqual(new long[] { 6 }, dirty.ToList());
        }

        [TestMethod]
        public void DirtySections_DeletedLocation_IsDetected_WhichTheWatermarkCouldNot()
        {
            var before = SectionFingerprintBuilder.Compute(Locations(), Links());
            List<(long Id, long ParentId, long Z, DateTime Modified)> fewer = Locations();
            fewer.RemoveAt(3);

            var dirty = SectionFingerprintDiff.DirtySections(before, SectionFingerprintBuilder.Compute(fewer, Links()));

            CollectionAssert.AreEqual(new long[] { 6 }, dirty.ToList());
            Assert.AreEqual(
                SectionFingerprintDiff.Newest(before.Values),
                SectionFingerprintDiff.Newest(SectionFingerprintBuilder.Compute(fewer, Links()).Values),
                "the newest modification is unchanged by a deletion, so a watermark gate would skip it");
        }

        [TestMethod]
        public void DirtySections_SectionThatEmptiedOrAppeared_IsDirty()
        {
            var before = SectionFingerprintBuilder.Compute(Locations(), Links());
            List<(long Id, long ParentId, long Z, DateTime Modified)> moved = Locations();
            moved[2] = (3, 10, 9, T0);

            var dirty = SectionFingerprintDiff.DirtySections(before, SectionFingerprintBuilder.Compute(moved, Links()));

            CollectionAssert.AreEqual(new long[] { 7, 9 }, dirty.ToList());
        }

        [TestMethod]
        public void DirtySections_MovedBetweenStructures_IsDetected()
        {
            var before = SectionFingerprintBuilder.Compute(Locations(), Links());
            List<(long Id, long ParentId, long Z, DateTime Modified)> reparented = Locations();
            reparented[3] = (4, 10, 6, T0);

            var dirty = SectionFingerprintDiff.DirtySections(before, SectionFingerprintBuilder.Compute(reparented, Links()));

            CollectionAssert.AreEqual(new long[] { 6 }, dirty.ToList());
        }

        [TestMethod]
        public void DirtySections_LinkChange_MarksBothEnds()
        {
            var before = SectionFingerprintBuilder.Compute(Locations(), Links());

            var dirty = SectionFingerprintDiff.DirtySections(before, SectionFingerprintBuilder.Compute(Locations(), [(1, 2, T0)]));

            CollectionAssert.AreEqual(new long[] { 6, 7 }, dirty.ToList(), "link 2-3 spans sections 6 and 7");
        }

        [TestMethod]
        public void DirtySections_ChangedStos_MarksTheSection()
        {
            var before = SectionFingerprintBuilder.Compute(Locations(), Links());
            var after = SectionFingerprintBuilder.Compute(Locations(), Links());
            before[6].Stos = "Rigid:1";
            after[6].Stos = "Rigid:2";

            CollectionAssert.AreEqual(new long[] { 6 }, SectionFingerprintDiff.DirtySections(before, after).ToList());
        }

        [TestMethod]
        public void Manifest_RoundtripsFingerprintsAndVersion()
        {
            string dir = Path.Combine(Path.GetTempPath(), "fingerprint-manifest-" + Guid.NewGuid().ToString("N"));
            try
            {
                var fingerprints = SectionFingerprintBuilder.Compute(Locations(), Links());
                fingerprints[5].Stos = "Rigid:7";
                var manifest = new CorrectionManifestDto
                {
                    StosGroup = "SliceToVolume1",
                    BuilderVersion = 3,
                    SectionFingerprints = fingerprints,
                    Provenance = new CorrectionProvenanceDto { MinLocations = 4, BuiltUtc = T0 }
                };
                new PublishedCorrectionSet(manifest, new Dictionary<long, SectionVectorField>()).WriteManifest(dir);

                CorrectionManifestDto loaded = PublishedCorrectionSet.LoadManifest(dir);

                Assert.AreEqual(3, loaded.BuilderVersion);
                Assert.AreEqual(4, loaded.Provenance.MinLocations);
                Assert.AreEqual(0, SectionFingerprintDiff.DirtySections(fingerprints, loaded.SectionFingerprints).Count);
                Assert.AreEqual("Rigid:7", loaded.SectionFingerprints[5].Stos);
            }
            finally
            {
                if (Directory.Exists(dir))
                    Directory.Delete(dir, true);
            }
        }

        [TestMethod]
        public void Manifest_WithoutFingerprints_LoadsWithTheDefaults()
        {
            string dir = Path.Combine(Path.GetTempPath(), "legacy-manifest-" + Guid.NewGuid().ToString("N"));
            Directory.CreateDirectory(dir);
            try
            {
                File.WriteAllText(Path.Combine(dir, "manifest.json"), "{\"stos_group\":\"G\",\"z\":[1,2]}");

                CorrectionManifestDto loaded = PublishedCorrectionSet.LoadManifest(dir);

                Assert.AreEqual(0, loaded.BuilderVersion);
                Assert.IsNull(loaded.SectionFingerprints);
            }
            finally
            {
                Directory.Delete(dir, true);
            }
        }
    }
}
