using FsCheck;
using Geometry;
using GeometryTests.FSCheck;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace GeometryTests
{
    [TestClass]
    public class RangeSpec
    {
        [TestMethod]
        public void NormalizeAndInterpolateAreInversesOnTheInterval() =>
            CoreCheck.Run(
                Prop.ForAll(CoreArbitraries.ArbRange(), Arb.From(Gen.Choose(0, 100).Select(i => i / 100.0)), (range, t) =>
                {
                    double value = range.Interpolate(t);
                    if (range.Span == 0)
                        return Tolerance.AreClose(range.Interpolate(range.Normalize(value)), range.Min);
                    return Tolerance.AreClose(range.Interpolate(range.Normalize(value)), value);
                }),
                nameof(NormalizeAndInterpolateAreInversesOnTheInterval));

        [TestMethod]
        public void ClipIsIdempotent() =>
            CoreCheck.Run(
                Prop.ForAll(CoreArbitraries.ArbRange(), CoreArbitraries.ArbScalar(), (range, value) =>
                {
                    double clipped = range.Clip(value);
                    return Tolerance.AreClose(range.Clip(clipped), clipped) &&
                           clipped >= range.Min && clipped <= range.Max;
                }),
                nameof(ClipIsIdempotent));

        [TestMethod]
        public void ContainsAgreesWithMinMax() =>
            CoreCheck.Run(
                Prop.ForAll(CoreArbitraries.ArbRange(), CoreArbitraries.ArbScalar(), (range, value) =>
                    range.Contains(value) == (value >= range.Min && value <= range.Max)),
                nameof(ContainsAgreesWithMinMax));
    }
}
