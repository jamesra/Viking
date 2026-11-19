using Microsoft.VisualStudio.TestTools.UnitTesting;
using System;
using System.IO;
using Viking.UI;

namespace VikingCoreTests
{
    /// <summary>
    /// Pins <see cref="State.ClearTextureCache"/>: the cache folder ends up empty but present, and nothing renamed aside is
    /// left behind, including folders left by an earlier clear that did not finish.
    /// </summary>
    [TestClass]
    public class ClearTextureCacheTests
    {
        private string _root = string.Empty;

        [TestInitialize]
        public void Setup()
        {
            _root = Path.Combine(Path.GetTempPath(), "ClearTextureCache-" + Guid.NewGuid().ToString("N"));
            Directory.CreateDirectory(_root);
        }

        [TestCleanup]
        public void Cleanup()
        {
            if (Directory.Exists(_root))
                Directory.Delete(_root, true);
        }

        [TestMethod]
        public void ClearEmptiesTheFolderAndLeavesNothingAside()
        {
            string cache = Path.Combine(_root, "Textures");
            Directory.CreateDirectory(Path.Combine(cache, "001", "TEM", "064"));
            File.WriteAllText(Path.Combine(cache, "001", "TEM", "064", "X001_Y001.png"), "tile");
            File.WriteAllText(Path.Combine(cache, "top.png"), "tile");

            State.ClearTextureCache(cache);

            Assert.IsTrue(Directory.Exists(cache));
            Assert.AreEqual(0, Directory.GetFileSystemEntries(cache).Length);
            CollectionAssert.AreEqual(new[] { cache }, Directory.GetDirectories(_root));
        }

        [TestMethod]
        public void ClearRemovesFoldersLeftByAnUnfinishedClear()
        {
            string cache = Path.Combine(_root, "Textures");
            Directory.CreateDirectory(cache);
            string leftover = cache + State.ClearingFolderSuffix + "old";
            Directory.CreateDirectory(Path.Combine(leftover, "sub"));
            File.WriteAllText(Path.Combine(leftover, "sub", "tile.png"), "tile");

            State.ClearTextureCache(cache);

            Assert.IsFalse(Directory.Exists(leftover));
            Assert.IsTrue(Directory.Exists(cache));
        }

        [TestMethod]
        public void ClearCreatesAMissingFolder()
        {
            string cache = Path.Combine(_root, "Textures");

            State.ClearTextureCache(cache + Path.DirectorySeparatorChar);

            Assert.IsTrue(Directory.Exists(cache));
        }
    }
}
