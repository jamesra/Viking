using Microsoft.VisualStudio.TestTools.UnitTesting;
using Viking.VolumeModel;

namespace WebAnnotationTests
{
    [TestClass]
    public class TileGridMetricsTests
    {
        [TestMethod]
        public void ScaledExtentUsesEachAxisIndependently()
        {
            Assert.AreEqual(256, TileGridMappingBase.ScaledTileExtent(64, 4));
            Assert.AreEqual(128, TileGridMappingBase.ScaledTileExtent(32, 4));
            Assert.AreNotEqual(
                TileGridMappingBase.ScaledTileExtent(64, 4),
                TileGridMappingBase.ScaledTileExtent(32, 4));
        }
    }
}
