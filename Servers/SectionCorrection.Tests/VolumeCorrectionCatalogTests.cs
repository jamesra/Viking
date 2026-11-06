using Microsoft.VisualStudio.TestTools.UnitTesting;
using System;
using System.Collections.Generic;
using System.IO;
using System.Reflection;
using System.Threading;
using Viking.SectionCorrection;
using Path = System.IO.Path;

namespace SectionCorrection.Tests
{
    [TestClass]
    public class VolumeCorrectionCatalogTests
    {
        static CorrectionProvenanceDto Provenance() => new()
        {
            BuiltUtc = DateTime.UtcNow,
            AnnotationWatermark = DateTime.UtcNow,
            PitchNm = 2000,
            KernelRadiusNm = 8000,
            MinAnnotationVotes = 3
        };

        static PublishedCorrectionSet Set(string stos, long z) =>
            new(
                new CorrectionManifestDto { StosGroup = stos, Provenance = Provenance() },
                new Dictionary<long, SectionVectorField>
                {
                    [z] = new SectionVectorField(z, 2000, 8000,
                    [
                        new LatticeNode(0, 0, 1, 0, 3),
                        new LatticeNode(1, 0, 1, 0, 3),
                        new LatticeNode(0, 1, 1, 0, 3),
                        new LatticeNode(1, 1, 1, 0, 3)
                    ])
                });

        [TestMethod]
        public void Load_ReadsVolumeThenStosGroup()
        {
            string root = Path.Combine(Path.GetTempPath(), "vol-catalog-" + Guid.NewGuid().ToString("N"));
            string dest = Path.Combine(root, "RC1", "SliceToVolume");
            Directory.CreateDirectory(dest);
            try
            {
                Set("SliceToVolume", 3).WriteDirectory(dest);
                Directory.CreateDirectory(Path.Combine(root, "RC1", "SliceToVolume.staging"));
                Directory.CreateDirectory(Path.Combine(root, "RC1.old"));

                using VolumeCorrectionCatalog catalog = new(root);
                catalog.Load();
                Assert.AreEqual(1, catalog.Sets.Count);
                Assert.IsTrue(catalog.TryGet("RC1", "SliceToVolume", out PublishedCorrectionSet loaded));
                Assert.IsTrue(loaded.TryGetSection(3, out _));
                Assert.IsFalse(catalog.TryGet("RC1", "Missing", out _));
            }
            finally
            {
                Directory.Delete(root, true);
            }
        }

        [TestMethod]
        public void Load_KeepsPreviousSetWhilePublishSwapsDirectories()
        {
            string root = Path.Combine(Path.GetTempPath(), "vol-catalog-swap-" + Guid.NewGuid().ToString("N"));
            string live = Path.Combine(root, "RC1", "grid16");
            try
            {
                Directory.CreateDirectory(live);
                Set("grid16", 3).WriteDirectory(live);

                using VolumeCorrectionCatalog catalog = new(root);
                catalog.Load();
                Assert.IsTrue(catalog.TryGet("RC1", "grid16", out PublishedCorrectionSet before));
                Assert.IsTrue(before.TryGetSection(3, out _));

                Directory.Move(live, live + ".old");
                Directory.CreateDirectory(live + ".staging");
                catalog.Load();

                Assert.IsTrue(catalog.TryGet("RC1", "grid16", out PublishedCorrectionSet during));
                Assert.IsTrue(during.TryGetSection(3, out _));
            }
            finally
            {
                Directory.Delete(root, true);
            }
        }

        [TestMethod]
        public void Load_ReplacesSetWhenNewPublishIsInPlace()
        {
            string root = Path.Combine(Path.GetTempPath(), "vol-catalog-replace-" + Guid.NewGuid().ToString("N"));
            string live = Path.Combine(root, "RC1", "grid16");
            try
            {
                Directory.CreateDirectory(live);
                Set("grid16", 3).WriteDirectory(live);

                using VolumeCorrectionCatalog catalog = new(root);
                catalog.Load();

                Directory.Move(live, live + ".old");
                Directory.CreateDirectory(live);
                Set("grid16", 9).WriteDirectory(live);
                catalog.Load();

                Assert.IsTrue(catalog.TryGet("RC1", "grid16", out PublishedCorrectionSet loaded));
                Assert.IsFalse(loaded.TryGetSection(3, out _));
                Assert.IsTrue(loaded.TryGetSection(9, out _));
            }
            finally
            {
                Directory.Delete(root, true);
            }
        }

        [TestMethod]
        public void Load_ReadsOldDirectoryWhenLiveFolderIsMissing()
        {
            string root = Path.Combine(Path.GetTempPath(), "vol-catalog-old-" + Guid.NewGuid().ToString("N"));
            string parked = Path.Combine(root, "RC1", "grid16.old");
            try
            {
                Directory.CreateDirectory(parked);
                Set("grid16", 3).WriteDirectory(parked);
                Directory.CreateDirectory(Path.Combine(root, "RC1", "grid16.staging"));

                using VolumeCorrectionCatalog catalog = new(root);
                catalog.Load();

                Assert.IsTrue(catalog.TryGet("RC1", "grid16", out PublishedCorrectionSet loaded));
                Assert.IsTrue(loaded.TryGetSection(3, out _));
            }
            finally
            {
                Directory.Delete(root, true);
            }
        }

        [TestMethod]
        public void Load_DropsSetWhenDirectoryRemoved()
        {
            string root = Path.Combine(Path.GetTempPath(), "vol-catalog-drop-" + Guid.NewGuid().ToString("N"));
            string live = Path.Combine(root, "RC1", "grid16");
            try
            {
                Directory.CreateDirectory(live);
                Set("grid16", 3).WriteDirectory(live);

                using VolumeCorrectionCatalog catalog = new(root);
                catalog.Load();
                Directory.Delete(live, true);
                catalog.Load();

                Assert.IsFalse(catalog.TryGet("RC1", "grid16", out _));
            }
            finally
            {
                Directory.Delete(root, true);
            }
        }

        static readonly MethodInfo OnChanged = typeof(VolumeCorrectionCatalog).GetMethod(
            "OnChanged", BindingFlags.Instance | BindingFlags.NonPublic);

        static void RaiseChangedConcurrently(VolumeCorrectionCatalog catalog, string root)
        {
            FileSystemEventArgs args = new(WatcherChangeTypes.Changed, root, "manifest.json");
            const int threads = 16;
            using Barrier start = new(threads);
            Thread[] workers = new Thread[threads];
            for (int t = 0; t < threads; t++)
            {
                workers[t] = new Thread(() =>
                {
                    start.SignalAndWait();
                    for (int i = 0; i < 500; i++)
                        OnChanged.Invoke(catalog, [null, args]);
                });
                workers[t].Start();
            }

            foreach (Thread worker in workers)
                worker.Join();
        }

        [TestMethod]
        public void ConcurrentChangeEvents_ReloadOnceDebounceElapses()
        {
            string root = Path.Combine(Path.GetTempPath(), "vol-catalog-reload-" + Guid.NewGuid().ToString("N"));
            string live = Path.Combine(root, "RC1", "grid16");
            Directory.CreateDirectory(live);
            try
            {
                using VolumeCorrectionCatalog catalog = new(root);
                Set("grid16", 3).WriteDirectory(live);
                RaiseChangedConcurrently(catalog, root);

                Thread.Sleep(200);
                Assert.IsFalse(catalog.TryGet("RC1", "grid16", out _), "Reloaded before the debounce window.");
                Thread.Sleep(1500);
                Assert.IsTrue(catalog.TryGet("RC1", "grid16", out _));

                Directory.Delete(live, true);
                Thread.Sleep(1500);
                Assert.IsTrue(catalog.TryGet("RC1", "grid16", out _), "Reloaded again with no new change event.");
            }
            finally
            {
                Directory.Delete(root, true);
            }
        }

        /// <summary>
        /// FileSystemWatcher raises events on pooled threads. A burst of concurrent events must leave one pending
        /// reload that Dispose cancels, not stray timers that reload the catalog after it was disposed.
        /// </summary>
        [TestMethod]
        public void ConcurrentChangeEvents_NoReloadAfterDispose()
        {
            string root = Path.Combine(Path.GetTempPath(), "vol-catalog-events-" + Guid.NewGuid().ToString("N"));
            Directory.CreateDirectory(root);
            try
            {
                VolumeCorrectionCatalog catalog = new(root);
                RaiseChangedConcurrently(catalog, root);

                catalog.Dispose();
                OnChanged.Invoke(catalog, [null, new FileSystemEventArgs(WatcherChangeTypes.Changed, root, "manifest.json")]);

                string live = Path.Combine(root, "RC1", "grid16");
                Directory.CreateDirectory(live);
                Set("grid16", 3).WriteDirectory(live);
                Thread.Sleep(1500);

                Assert.IsFalse(catalog.TryGet("RC1", "grid16", out _));
            }
            finally
            {
                Directory.Delete(root, true);
            }
        }

        [TestMethod]
        public void SanitizeVolumeName_ReplacesInvalidChars()
        {
            Assert.AreEqual("_", VolumeCorrectionCatalog.SanitizeVolumeName(""));
            Assert.AreEqual("RC_1", VolumeCorrectionCatalog.SanitizeVolumeName("RC/1"));
        }
    }
}
