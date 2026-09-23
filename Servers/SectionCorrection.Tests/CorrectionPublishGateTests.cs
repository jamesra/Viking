using Microsoft.VisualStudio.TestTools.UnitTesting;
using System;
using Viking.SectionCorrection;

namespace SectionCorrection.Tests
{
    [TestClass]
    public class CorrectionPublishGateTests
    {
        [TestMethod]
        public void ShouldSkip_WhenWatermarkUnchanged()
        {
            DateTime watermark = new(2026, 1, 1, 0, 0, 0, DateTimeKind.Utc);
            Assert.IsTrue(CorrectionPublishGate.ShouldSkip(watermark, watermark, force: false));
            Assert.IsTrue(CorrectionPublishGate.ShouldSkip(watermark.AddHours(1), watermark, force: false));
        }

        [TestMethod]
        public void ShouldSkip_FalseWhenForceOrMissingOrStale()
        {
            DateTime watermark = new(2026, 1, 1, 0, 0, 0, DateTimeKind.Utc);
            Assert.IsFalse(CorrectionPublishGate.ShouldSkip(watermark, watermark, force: true));
            Assert.IsFalse(CorrectionPublishGate.ShouldSkip(null, watermark, force: false));
            Assert.IsFalse(CorrectionPublishGate.ShouldSkip(watermark.AddHours(-1), watermark, force: false));
        }
    }
}
