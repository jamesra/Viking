using Microsoft.VisualStudio.TestTools.UnitTesting;
using System;
using System.Collections.Generic;
using System.IO;
using Viking.SectionCorrection;
using Path = System.IO.Path;

namespace SectionCorrection.Tests
{
    /// <summary>
    /// Pins how the flat <see cref="CorrectionCatalog"/> ({root}/{StosGroup}) reacts to the
    /// CorrectionPublisher swap: {group}.staging is written in place (manifest first, then section files),
    /// the live folder is moved to {group}.old, and staging is moved to the live name.
    /// </summary>
    [TestClass]
    public class CorrectionCatalogTests
    {
        static CorrectionProvenanceDto Provenance() => new()
        {
            BuiltUtc = DateTime.UtcNow,
            AnnotationWatermark = DateTime.UtcNow,
            PitchNm = 2000,
            KernelRadiusNm = 8000,
            MinAnnotationVotes = 3
        };

        static SectionVectorField Field(long z) =>
            new(z, 2000, 8000,
            [
                new LatticeNode(0, 0, 1, 0, 3),
                new LatticeNode(1, 0, 1, 0, 3),
                new LatticeNode(0, 1, 1, 0, 3),
                new LatticeNode(1, 1, 1, 0, 3)
            ]);

        static PublishedCorrectionSet Set(string stos, params long[] zs)
        {
            Dictionary<long, SectionVectorField> sections = [];
            foreach (long z in zs)
                sections[z] = Field(z);
            return new(new CorrectionManifestDto { StosGroup = stos, Provenance = Provenance() }, sections);
        }

        static string NewRoot(string tag) =>
            Path.Combine(Path.GetTempPath(), "corr-catalog-" + tag + "-" + Guid.NewGuid().ToString("N"));

        /// <summary>
        /// Writes a staging folder the way the publisher has it part way through WriteDirectory:
        /// the manifest lists every section but only the first section file exists yet.
        /// </summary>
        static void WritePartialStaging(string staging, string stos, long writtenZ, long pendingZ)
        {
            Set(stos, writtenZ, pendingZ).WriteDirectory(staging);
            File.Delete(Path.Combine(staging, PublishedCorrectionSet.SectionFileName(pendingZ)));
        }

        [TestMethod]
        public void Load_IgnoresStagingFolderBesideLiveSet()
        {
            string root = NewRoot("staging");
            string live = Path.Combine(root, "grid16");
            try
            {
                Directory.CreateDirectory(live);
                Set("grid16", 3).WriteDirectory(live);
                WritePartialStaging(live + ".staging", "grid16", 9, 10);

                using CorrectionCatalog catalog = new(root);
                catalog.Load();

                Assert.AreEqual(1, catalog.Sets.Count);
                Assert.IsTrue(catalog.TryGet("grid16", out PublishedCorrectionSet loaded));
                Assert.IsTrue(loaded.TryGetSection(3, out _));
                Assert.IsFalse(loaded.TryGetSection(9, out _));
            }
            finally
            {
                Directory.Delete(root, true);
            }
        }

        [TestMethod]
        public void Load_KeepsPreviousSetWhilePublishSwapsDirectories()
        {
            string root = NewRoot("swap");
            string live = Path.Combine(root, "grid16");
            try
            {
                Directory.CreateDirectory(live);
                Set("grid16", 3).WriteDirectory(live);

                using CorrectionCatalog catalog = new(root);
                catalog.Load();
                Assert.IsTrue(catalog.TryGet("grid16", out PublishedCorrectionSet before));

                Directory.Move(live, live + ".old");
                WritePartialStaging(live + ".staging", "grid16", 9, 10);
                Directory.Delete(live + ".old", true);
                catalog.Load();

                Assert.IsTrue(catalog.TryGet("grid16", out PublishedCorrectionSet during));
                Assert.AreSame(before, during);
            }
            finally
            {
                Directory.Delete(root, true);
            }
        }

        [TestMethod]
        public void Load_KeepsPreviousSetWhenManifestBecomesUnreadable()
        {
            string root = NewRoot("corrupt");
            string live = Path.Combine(root, "grid16");
            try
            {
                Directory.CreateDirectory(live);
                Set("grid16", 3).WriteDirectory(live);

                using CorrectionCatalog catalog = new(root);
                catalog.Load();
                Assert.IsTrue(catalog.TryGet("grid16", out PublishedCorrectionSet before));

                File.WriteAllText(Path.Combine(live, PublishedCorrectionSet.ManifestFileName), "{ not json");
                catalog.Load();

                Assert.IsTrue(catalog.TryGet("grid16", out PublishedCorrectionSet after));
                Assert.AreSame(before, after);
            }
            finally
            {
                Directory.Delete(root, true);
            }
        }

        [TestMethod]
        public void Load_ReadsOldDirectoryWhenLiveFolderIsMissing()
        {
            string root = NewRoot("old");
            string parked = Path.Combine(root, "grid16.old");
            try
            {
                Directory.CreateDirectory(parked);
                Set("grid16", 3).WriteDirectory(parked);

                using CorrectionCatalog catalog = new(root);
                catalog.Load();

                Assert.IsTrue(catalog.TryGet("grid16", out PublishedCorrectionSet loaded));
                Assert.IsTrue(loaded.TryGetSection(3, out _));
            }
            finally
            {
                Directory.Delete(root, true);
            }
        }

        [TestMethod]
        public void Load_PrefersLiveFolderOverLeftoverOld()
        {
            string root = NewRoot("old-live");
            string live = Path.Combine(root, "grid16");
            try
            {
                Directory.CreateDirectory(live + ".old");
                Set("grid16", 3).WriteDirectory(live + ".old");
                Directory.CreateDirectory(live);
                Set("grid16", 9).WriteDirectory(live);

                using CorrectionCatalog catalog = new(root);
                catalog.Load();

                Assert.AreEqual(1, catalog.Sets.Count);
                Assert.IsTrue(catalog.TryGet("grid16", out PublishedCorrectionSet loaded));
                Assert.IsTrue(loaded.TryGetSection(9, out _));
                Assert.IsFalse(loaded.TryGetSection(3, out _));
            }
            finally
            {
                Directory.Delete(root, true);
            }
        }

        [TestMethod]
        public void Load_ReplacesSetWhenNewPublishIsInPlace()
        {
            string root = NewRoot("replace");
            string live = Path.Combine(root, "grid16");
            try
            {
                Directory.CreateDirectory(live);
                Set("grid16", 3).WriteDirectory(live);

                using CorrectionCatalog catalog = new(root);
                catalog.Load();

                Directory.Delete(live, true);
                Directory.CreateDirectory(live);
                Set("grid16", 9).WriteDirectory(live);
                catalog.Load();

                Assert.IsTrue(catalog.TryGet("grid16", out PublishedCorrectionSet loaded));
                Assert.IsFalse(loaded.TryGetSection(3, out _));
                Assert.IsTrue(loaded.TryGetSection(9, out _));
            }
            finally
            {
                Directory.Delete(root, true);
            }
        }

        [TestMethod]
        public void Load_DropsSetWhenDirectoryRemoved()
        {
            string root = NewRoot("drop");
            string live = Path.Combine(root, "grid16");
            try
            {
                Directory.CreateDirectory(live);
                Set("grid16", 3).WriteDirectory(live);

                using CorrectionCatalog catalog = new(root);
                catalog.Load();
                Directory.Delete(live, true);
                catalog.Load();

                Assert.IsFalse(catalog.TryGet("grid16", out _));
                Assert.AreEqual(0, catalog.Sets.Count);
            }
            finally
            {
                Directory.Delete(root, true);
            }
        }

        [TestMethod]
        public void Load_DropsSetWhenManifestRemovedFromLiveFolder()
        {
            string root = NewRoot("no-manifest");
            string live = Path.Combine(root, "grid16");
            try
            {
                Directory.CreateDirectory(live);
                Set("grid16", 3).WriteDirectory(live);

                using CorrectionCatalog catalog = new(root);
                catalog.Load();
                File.Delete(Path.Combine(live, PublishedCorrectionSet.ManifestFileName));
                catalog.Load();

                Assert.IsFalse(catalog.TryGet("grid16", out _));
            }
            finally
            {
                Directory.Delete(root, true);
            }
        }

        [TestMethod]
        public void ResolveInsideRoot_ReturnsFullPathOfFileInGroupFolder()
        {
            string root = NewRoot("resolve");
            string expected = Path.Combine(Path.GetFullPath(root), "grid16", PublishedCorrectionSet.ManifestFileName);
            Assert.AreEqual(expected, CorrectionCatalog.ResolveInsideRoot(root, "grid16", PublishedCorrectionSet.ManifestFileName));
        }

        [TestMethod]
        public void ResolveInsideRoot_AcceptsRootWithTrailingSeparator()
        {
            string root = NewRoot("resolve-slash");
            string expected = Path.Combine(Path.GetFullPath(root), "grid16", "a.npz");
            Assert.AreEqual(expected, CorrectionCatalog.ResolveInsideRoot(root + Path.DirectorySeparatorChar, "grid16", "a.npz"));
        }

        /// <summary>Regression: root "corr" must not admit the sibling folder "corr-private" that shares its name prefix.</summary>
        [TestMethod]
        public void ResolveInsideRoot_RejectsSiblingFolderSharingRootPrefix()
        {
            string root = NewRoot("resolve-sibling");
            string sibling = Path.GetFileName(root) + "-private";
            Assert.IsNull(CorrectionCatalog.ResolveInsideRoot(root, "..", sibling, "secret.json"));
        }

        [TestMethod]
        public void ResolveInsideRoot_RejectsParentTraversalInGroup()
        {
            string root = NewRoot("resolve-dotdot");
            Assert.IsNull(CorrectionCatalog.ResolveInsideRoot(root, "..", "manifest.json"));
            Assert.IsNull(CorrectionCatalog.ResolveInsideRoot(root, "grid16", "..", "..", "manifest.json"));
        }

        [TestMethod]
        public void ResolveInsideRoot_RejectsRootItself()
        {
            string root = NewRoot("resolve-self");
            Assert.IsNull(CorrectionCatalog.ResolveInsideRoot(root, "grid16", ".."));
            Assert.IsNull(CorrectionCatalog.ResolveInsideRoot(root));
            Assert.IsNull(CorrectionCatalog.ResolveInsideRoot(root, "grid16" + Path.DirectorySeparatorChar + ".." + Path.DirectorySeparatorChar));
        }

        [TestMethod]
        public void ResolveInsideRoot_RejectsRootedPart()
        {
            string root = NewRoot("resolve-rooted");
            string outside = Path.Combine(Path.GetTempPath(), "elsewhere", "manifest.json");
            Assert.IsNull(CorrectionCatalog.ResolveInsideRoot(root, outside));
        }

        [TestMethod]
        public void ResolveInsideRoot_RejectsBlankRootAndNullPart()
        {
            Assert.IsNull(CorrectionCatalog.ResolveInsideRoot(null, "grid16", "manifest.json"));
            Assert.IsNull(CorrectionCatalog.ResolveInsideRoot("  ", "grid16", "manifest.json"));
            Assert.IsNull(CorrectionCatalog.ResolveInsideRoot(NewRoot("resolve-null"), "grid16", null));
            Assert.IsNull(CorrectionCatalog.ResolveInsideRoot(NewRoot("resolve-null-array"), null));
        }

        [TestMethod]
        public void ResolveInsideRoot_RejectsDifferentCaseOnlyOnCaseSensitivePlatforms()
        {
            string root = NewRoot("resolve-case");
            string upper = Path.Combine(Path.GetDirectoryName(Path.GetFullPath(root)), Path.GetFileName(root).ToUpperInvariant());
            string resolved = CorrectionCatalog.ResolveInsideRoot(root, "..", Path.GetFileName(upper), "manifest.json");
            if (Path.DirectorySeparatorChar == '\\')
                Assert.IsNotNull(resolved);
            else
                Assert.IsNull(resolved);
        }

        [TestMethod]
        public void Load_MissingRootGivesEmptyCatalog()
        {
            using CorrectionCatalog catalog = new(NewRoot("missing"));
            catalog.Load();
            Assert.AreEqual(0, catalog.Sets.Count);
        }
    }
}
