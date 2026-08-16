using FsCheck;
using Geometry;
using GeometryTests.FSCheck;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace GeometryTests
{
    [TestClass]
    public class BoxSpec
    {
        [TestMethod]
        public void TranslateIsInvertible() =>
            CoreCheck.Run(
                Prop.ForAll(CoreArbitraries.ArbBox(), CoreArbitraries.ArbVector3(), (box, offset) =>
                {
                    Box back = box.Translate(offset).Translate(-offset);
                    return back.MinCorner == box.MinCorner && back.MaxCorner == box.MaxCorner;
                }),
                nameof(TranslateIsInvertible));

        [TestMethod]
        public void ContainsCornersAndVolumeIsNonNegative() =>
            CoreCheck.Run(
                Prop.ForAll(CoreArbitraries.ArbBox(), box =>
                    box.Contains(box.MinCorner) &&
                    box.Contains(box.MaxCorner) &&
                    box.Volume >= 0),
                nameof(ContainsCornersAndVolumeIsNonNegative));

        [TestMethod]
        public void IntersectsIsSymmetric() =>
            CoreCheck.Run(
                Prop.ForAll(CoreArbitraries.ArbBox(), CoreArbitraries.ArbBox(),
                    (a, b) => a.Intersects(b) == b.Intersects(a)),
                nameof(IntersectsIsSymmetric));
    }
}
