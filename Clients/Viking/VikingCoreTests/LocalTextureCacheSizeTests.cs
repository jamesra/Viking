using System.IO;
using System.Threading.Tasks;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using Viking;

namespace VikingCoreTests
{
    /// <summary>
    /// Pins the byte accounting of <see cref="LocalTextureCache"/>: after <c>AddAsync</c> the cache total must equal
    /// the length of the file written, because the 1 GB cap is enforced from that total.
    /// </summary>
    /// <remarks>
    /// Each test writes under its own temp folder, removed in cleanup. Sizes straddle the 4 KB
    /// <see cref="FileStream"/> buffer: a tile smaller than the buffer used to count as 0 bytes, and a larger one
    /// lost its last partial chunk.
    /// </remarks>
    [TestClass]
    public class LocalTextureCacheSizeTests
    {
        private string folder = "";

        /// <summary>Creates the per-test temp folder.</summary>
        [TestInitialize]
        public void Setup()
        {
            folder = Path.Combine(Path.GetTempPath(), "LocalTextureCacheSizeTests_" + Path.GetRandomFileName());
            Directory.CreateDirectory(folder);
        }

        /// <summary>Deletes the per-test temp folder.</summary>
        [TestCleanup]
        public void Cleanup()
        {
            try { Directory.Delete(folder, true); } catch (IOException) { }
        }

        /// <summary>
        /// A stream of any length, below, at and above the file buffer size, is counted at its full length.
        /// </summary>
        /// <param name="length">Number of bytes in the tile.</param>
        [DataTestMethod]
        [DataRow(1)]
        [DataRow(100)]
        [DataRow(4095)]
        [DataRow(4096)]
        [DataRow(4097)]
        [DataRow(10000)]
        [DataRow(100000)]
        public async Task AddAsync_CachedSize_EqualsFileLength(int length)
        {
            LocalTextureCache cache = new();
            string file = Path.Combine(folder, "sub", "tile.png");
            byte[] data = new byte[length];
            new System.Random(length).NextBytes(data);

            bool added = await cache.AddAsync(file, new MemoryStream(data));

            Assert.IsTrue(added);
            Assert.AreEqual(length, new FileInfo(file).Length);
            Assert.AreEqual((long)length, cache.CachedSize);
        }
    }
}
