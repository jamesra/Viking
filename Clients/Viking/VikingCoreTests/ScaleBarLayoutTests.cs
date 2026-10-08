using System;
using FsCheck;
using MeasurementExtension;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace VikingCoreTests
{
    /// <summary>
    /// Pins scale-bar rounding and zero-length guards used by <see cref="MeasureOverlay"/>.
    /// </summary>
    [TestClass]
    public class ScaleBarLayoutTests
    {
        private const double Epsilon = 1e-9;

        [TestMethod]
        public void TryRoundReadableLengthToBarDistance_ZeroOrNegative_ReturnsFalse()
        {
            Assert.IsFalse(ScaleBarLayout.TryRoundReadableLengthToBarDistance(0, out _));
            Assert.IsFalse(ScaleBarLayout.TryRoundReadableLengthToBarDistance(-1, out _));
            Assert.IsFalse(ScaleBarLayout.TryRoundReadableLengthToBarDistance(double.NaN, out _));
            Assert.IsFalse(ScaleBarLayout.TryRoundReadableLengthToBarDistance(double.PositiveInfinity, out _));
        }

        [TestMethod]
        public void TryRoundReadableLengthToBarDistance_KnownExamples_MatchPowerOfTenRounding()
        {
            Assert.IsTrue(ScaleBarLayout.TryRoundReadableLengthToBarDistance(123, out double d123));
            Assert.AreEqual(1000, d123, Epsilon);

            Assert.IsTrue(ScaleBarLayout.TryRoundReadableLengthToBarDistance(750, out double d750));
            Assert.AreEqual(5000, d750, Epsilon);

            Assert.IsTrue(ScaleBarLayout.TryRoundReadableLengthToBarDistance(450, out double d450));
            Assert.AreEqual(5000, d450, Epsilon);
        }

        [TestMethod]
        public void TryRoundReadableLengthToBarDistance_PositiveFinite_ReturnsAtLeastReadableLength()
        {
            var readableGen = Gen.Choose(1, 1_000_000_000).Select(i => i / 1000.0);
            Prop.ForAll(Arb.From(readableGen), readable =>
            {
                Assert.IsTrue(ScaleBarLayout.TryRoundReadableLengthToBarDistance(readable, out double barDistance));
                Assert.IsTrue(barDistance >= readable - Epsilon);
            }).QuickCheckThrowOnFailure();
        }
    }
}
