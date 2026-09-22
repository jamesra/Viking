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

        [TestMethod]
        public void PadContainsOriginal() =>
            CoreCheck.Run(
                Prop.ForAll(CoreArbitraries.ArbBox(), box =>
                    box.Pad(1).Contains(box) && box.Pad(1).Contains(box.MinCorner) && box.Pad(1).Contains(box.MaxCorner)),
                nameof(PadContainsOriginal));

        [TestMethod]
        public void ScaleAboutCenterLeavesCenterUnchanged() =>
            CoreCheck.Run(
                Prop.ForAll(CoreArbitraries.ArbBox(), box =>
                {
                    Box scaled = box.Scale(2);
                    return Vector3.Distance(scaled.CenterPoint, box.CenterPoint) < 0.01 &&
                           Tolerance.AreClose(scaled.Volume, box.Volume * 8);
                }),
                nameof(ScaleAboutCenterLeavesCenterUnchanged));

        [TestMethod]
        public void UnionContainsBothBoxes() =>
            CoreCheck.Run(
                Prop.ForAll(CoreArbitraries.ArbBox(), CoreArbitraries.ArbBox(), (a, b) =>
                {
                    Box u = a.Union(b, out _);
                    return u.Contains(a) && u.Contains(b) &&
                           u.Contains(a.MinCorner) && u.Contains(b.MaxCorner);
                }),
                nameof(UnionContainsBothBoxes));

        [TestMethod]
        public void ContainsNestedBox() =>
            CoreCheck.Run(
                Prop.ForAll(CoreArbitraries.ArbBox(), box =>
                    box.Contains(box) && box.Pad(1).Contains(box)),
                nameof(ContainsNestedBox));

        [TestMethod]
        public void EqualsIsReflexiveAndAgreesWithOperator() =>
            CoreCheck.Run(
                Prop.ForAll(CoreArbitraries.ArbBox(), box =>
                    box.Equals(box) && box == box.Clone() && box.GetHashCode() == box.Clone().GetHashCode()),
                nameof(EqualsIsReflexiveAndAgreesWithOperator));
    }
}
