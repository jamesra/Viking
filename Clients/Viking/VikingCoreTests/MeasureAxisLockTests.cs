using FsCheck;
using MeasurementExtension;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace VikingCoreTests
{
    /// <summary>
    /// Pins the SHIFT/CTRL axis lock of <see cref="MeasureAxisLock"/> at real volume magnitudes.
    /// </summary>
    [TestClass]
    public class MeasureAxisLockTests
    {
        [TestMethod]
        public void Apply_NoModifier_ReturnsCursor()
        {
            var cursor = new Geometry.Vector2(12.5, -7.25);
            Assert.AreEqual(cursor, MeasureAxisLock.Apply(new Geometry.Vector2(1, 2), cursor, false, false));
        }

        [TestMethod]
        public void Apply_Horizontal_KeepsOriginYAndCursorX()
        {
            var result = MeasureAxisLock.Apply(new Geometry.Vector2(1, 2), new Geometry.Vector2(10, 20), true, false);
            Assert.AreEqual(new Geometry.Vector2(10, 2), result);
        }

        [TestMethod]
        public void Apply_Vertical_KeepsOriginXAndCursorY()
        {
            var result = MeasureAxisLock.Apply(new Geometry.Vector2(1, 2), new Geometry.Vector2(10, 20), false, true);
            Assert.AreEqual(new Geometry.Vector2(1, 20), result);
        }

        [TestMethod]
        public void Apply_Horizontal_LargeVolumeCoordinates_DistanceIsExactlyDeltaX()
        {
            // 300000.123456789 is not representable as a float; the old float target left a non-zero dy.
            var origin = new Geometry.Vector2(250000.5, 300000.123456789);
            var cursor = new Geometry.Vector2(250000.75, 12.0);
            var target = MeasureAxisLock.Apply(origin, cursor, true, false);
            Assert.AreEqual(0.25, Geometry.Vector2.Distance(origin, target));
        }

        [TestMethod]
        public void Apply_LockedAxisAlwaysMatchesOrigin()
        {
            Prop.ForAll(Arb.From(Gen.Choose(-1_000_000, 1_000_000).Select(i => i / 7.0)), Arb.From(Gen.Choose(-1_000_000, 1_000_000).Select(i => i / 3.0)),
                (double ox, double oy) =>
                {
                    var origin = new Geometry.Vector2(ox, oy);
                    var cursor = new Geometry.Vector2(oy, ox);
                    return MeasureAxisLock.Apply(origin, cursor, true, false).Y == oy
                        && MeasureAxisLock.Apply(origin, cursor, false, true).X == ox;
                }).QuickCheckThrowOnFailure();
        }
    }
}
