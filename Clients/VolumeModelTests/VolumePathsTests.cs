using Microsoft.VisualStudio.TestTools.UnitTesting;
using System;
using System.IO;
using Viking.VolumeModel;

namespace VolumeModelTests
{
    /// <summary>
    /// Pins <see cref="VolumePaths"/>: stos cache path composition, stos group name sanitizing,
    /// cache file name formats, and lazy directory creation. Uses a unique temp directory per test.
    /// </summary>
    [TestClass]
    public class VolumePathsTests
    {
        private string _root;
        private char _sep = Path.DirectorySeparatorChar;

        [TestInitialize]
        public void Setup()
        {
            _root = Path.Combine(Path.GetTempPath(), "VolumePathsTests_" + Guid.NewGuid().ToString("N"));
        }

        [TestCleanup]
        public void Cleanup()
        {
            if (Directory.Exists(_root))
                Directory.Delete(_root, true);
        }

        [TestMethod]
        public void ServerStosCachePath_IsCacheNameStosZip_AndCreatesNothing()
        {
            var paths = new VolumePaths(_root, "RC2");
            Assert.AreEqual(_root + _sep + "RC2" + _sep + "StosZip", paths.ServerStosCachePath);
            Assert.IsFalse(Directory.Exists(_root));
        }

        [TestMethod]
        public void GetServerStosCachePath_NullOrEmptyGroup_ReturnsUngroupedPath()
        {
            var paths = new VolumePaths(_root, "RC2");
            Assert.AreEqual(paths.ServerStosCachePath, paths.GetServerStosCachePath(null));
            Assert.AreEqual(paths.ServerStosCachePath, paths.GetServerStosCachePath(string.Empty));
        }

        [TestMethod]
        public void GetServerStosCachePath_PlainGroup_IsSubfolderOfUngroupedPath()
        {
            var paths = new VolumePaths(_root, "RC2");
            Assert.AreEqual(paths.ServerStosCachePath + _sep + "SliceToVolumeLinear1",
                paths.GetServerStosCachePath("SliceToVolumeLinear1"));
        }

        [TestMethod]
        public void GetServerStosCachePath_DistinctGroups_GetDistinctFolders()
        {
            var paths = new VolumePaths(_root, "RC2");
            Assert.AreNotEqual(paths.GetServerStosCachePath("A"), paths.GetServerStosCachePath("B"));
        }

        [TestMethod]
        public void GetServerStosCachePath_InvalidFileNameChars_AreReplacedWithUnderscore()
        {
            var paths = new VolumePaths(_root, "RC2");
            foreach (char c in Path.GetInvalidFileNameChars())
            {
                string result = paths.GetServerStosCachePath("a" + c + "b");
                Assert.AreEqual(paths.ServerStosCachePath + _sep + "a_b", result, "char code " + (int)c);
            }
        }

        [TestMethod]
        public void GetServerStosCachePath_SeparatorsInGroup_CannotEscapeStosZipFolder()
        {
            var paths = new VolumePaths(_root, "RC2");
            string result = paths.GetServerStosCachePath(@"..\..\evil/x");
            Assert.AreEqual(paths.ServerStosCachePath + _sep + ".._.._evil_x", result);
        }

        [TestMethod]
        public void LocalVolumeDir_CreatesDirectory_WithTrailingSeparator()
        {
            var paths = new VolumePaths(_root, "RC2");
            string dir = paths.LocalVolumeDir;
            Assert.AreEqual(Path.Combine(_root, "RC2") + _sep, dir);
            Assert.IsTrue(Directory.Exists(dir));
        }

        [TestMethod]
        public void LocalVolumeDir_SecondRead_ReturnsCachedValueEvenIfDirectoryRemoved()
        {
            var paths = new VolumePaths(_root, "RC2");
            string first = paths.LocalVolumeDir;
            Directory.Delete(_root, true);
            Assert.AreEqual(first, paths.LocalVolumeDir);
            Assert.IsFalse(Directory.Exists(first), "getter caches and does not recreate");
        }

        [TestMethod]
        public void StosCacheDir_CreatesStosFolderUnderVolumeDir()
        {
            var paths = new VolumePaths(_root, "RC2");
            string dir = paths.StosCacheDir;
            Assert.AreEqual(Path.Combine(_root, "RC2", "Stos"), dir);
            Assert.IsTrue(Directory.Exists(dir));
            Assert.IsTrue(Directory.Exists(paths.LocalVolumeDir));
        }

        [TestMethod]
        public void GetStosCacheName_FormatsMappedDashControlWithExtension()
        {
            var paths = new VolumePaths(_root, "RC2");
            Assert.AreEqual(Path.Combine(_root, "RC2", "Stos", "12-7.xyz"), paths.GetStosCacheName(12, 7, ".xyz"));
        }

        [TestMethod]
        public void GetStosCacheName_LargeAndNegativeSections_UseFullLongText()
        {
            var paths = new VolumePaths(_root, "RC2");
            string name = Path.GetFileName(paths.GetStosCacheName(5000000000L, -3, ".s"));
            Assert.AreEqual("5000000000--3.s", name);
        }

        [TestMethod]
        public void GetITKSCacheName_UsesStosExtension()
        {
            var paths = new VolumePaths(_root, "RC2");
            Assert.AreEqual("100-99.stos", Path.GetFileName(paths.GetITKSCacheName(100, 99)));
        }

        [TestMethod]
        public void GetSerializerCacheName_UsesStosBinExtension()
        {
            var paths = new VolumePaths(_root, "RC2");
            Assert.AreEqual("100-99.stos_bin", Path.GetFileName(paths.GetSerializerCacheName(100, 99)));
        }

        [TestMethod]
        public void GetITKSAndSerializerNames_ShareDirectory_AndDifferOnlyByExtension()
        {
            var paths = new VolumePaths(_root, "RC2");
            Assert.AreEqual(Path.GetDirectoryName(paths.GetITKSCacheName(1, 2)), Path.GetDirectoryName(paths.GetSerializerCacheName(1, 2)));
            Assert.AreNotEqual(paths.GetITKSCacheName(1, 2), paths.GetSerializerCacheName(1, 2));
        }

        [TestMethod]
        public void CreateDirectories_CreatesVolumeFolder()
        {
            var paths = new VolumePaths(_root, "RC2");
            VolumePaths.CreateDirectories(paths);
            Assert.IsTrue(Directory.Exists(Path.Combine(_root, "RC2")));
        }

        [TestMethod]
        public void CreateDirectories_CalledTwice_DoesNotThrow()
        {
            var paths = new VolumePaths(_root, "RC2");
            VolumePaths.CreateDirectories(paths);
            VolumePaths.CreateDirectories(paths);
            Assert.IsTrue(Directory.Exists(paths.LocalVolumeDir));
        }
    }
}
