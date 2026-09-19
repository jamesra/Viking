using Microsoft.VisualStudio.TestTools.UnitTesting;
using Viking.VolumeModel;

namespace WebAnnotationTests
{
    [TestClass]
    public class VolumePathTests
    {
        [TestMethod]
        public void HttpHostUsesForwardSlash()
        {
            string path = VolumePath.Combine("http://tiles.example.com/volume", "Mosaic", "004", "tile.png");
            Assert.AreEqual("http://tiles.example.com/volume/Mosaic/004/tile.png", path);
            Assert.IsTrue(path.Contains("/"));
            Assert.IsFalse(path.Contains("\\"));
        }

        [TestMethod]
        public void JoinRelativeDoesNotPrependHost()
        {
            string path = VolumePath.JoinRelative("https://example.com", "Grid", "002", "X001_Y001.png");
            Assert.AreEqual("Grid/002/X001_Y001.png", path);
        }

        [TestMethod]
        public void LocalHostKeepsOsSeparator()
        {
            string path = VolumePath.JoinRelative(@"C:\volumes\rc2", "Grid", "001", "tile.png");
            char sep = System.IO.Path.DirectorySeparatorChar;
            Assert.AreEqual($"Grid{sep}001{sep}tile.png", path);
        }
    }
}
