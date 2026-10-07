using FsCheck;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using Viking.Common;

namespace CommonTests
{
    /// <summary>
    /// Pins the inclusive-bounds behavior of <see cref="MathUtils.Clamp(int, int, int)"/> and its
    /// double and float overloads, which stand in for Math.Clamp on .NET Framework 4.8.
    /// </summary>
    [TestClass]
    public class MathUtilsTests
    {
        [TestMethod]
        public void ClampInt_Property_ResultIsInRangeAndMatchesMinMax()
        {
            Prop.ForAll(Arb.From<int>(), Arb.From<int>(), Arb.From<int>(), (value, a, b) =>
            {
                int min = System.Math.Min(a, b);
                int max = System.Math.Max(a, b);
                int result = MathUtils.Clamp(value, min, max);
                return result >= min && result <= max && result == System.Math.Min(System.Math.Max(value, min), max);
            }).QuickCheckThrowOnFailure();
        }

        [TestMethod]
        public void ClampDouble_Property_ResultIsInRangeAndIdempotent()
        {
            var finite = Arb.Default.Float().Filter(d => !double.IsNaN(d) && !double.IsInfinity(d));
            Prop.ForAll(finite, finite, finite, (value, a, b) =>
            {
                double min = System.Math.Min(a, b);
                double max = System.Math.Max(a, b);
                double result = MathUtils.Clamp(value, min, max);
                return result >= min && result <= max && MathUtils.Clamp(result, min, max) == result
                    && (value < min || value > max || result == value);
            }).QuickCheckThrowOnFailure();
        }

        [TestMethod]
        public void ClampInt_BelowMin_ReturnsMin()
        {
            Assert.AreEqual(2, MathUtils.Clamp(-5, 2, 10));
        }

        [TestMethod]
        public void ClampInt_AboveMax_ReturnsMax()
        {
            Assert.AreEqual(10, MathUtils.Clamp(50, 2, 10));
        }

        [TestMethod]
        public void ClampInt_Interior_ReturnsValue()
        {
            Assert.AreEqual(5, MathUtils.Clamp(5, 2, 10));
        }

        [TestMethod]
        public void ClampInt_OnBounds_ReturnsBound()
        {
            Assert.AreEqual(2, MathUtils.Clamp(2, 2, 10));
            Assert.AreEqual(10, MathUtils.Clamp(10, 2, 10));
        }

        [TestMethod]
        public void ClampInt_JustOutsideBounds_ReturnsBound()
        {
            Assert.AreEqual(2, MathUtils.Clamp(1, 2, 10));
            Assert.AreEqual(10, MathUtils.Clamp(11, 2, 10));
        }

        [TestMethod]
        public void ClampInt_DegenerateRange_ReturnsThatValue()
        {
            Assert.AreEqual(3, MathUtils.Clamp(-1, 3, 3));
            Assert.AreEqual(3, MathUtils.Clamp(3, 3, 3));
            Assert.AreEqual(3, MathUtils.Clamp(9, 3, 3));
        }

        [TestMethod]
        public void ClampInt_ExtremeValues()
        {
            Assert.AreEqual(int.MinValue, MathUtils.Clamp(int.MinValue, int.MinValue, int.MaxValue));
            Assert.AreEqual(int.MaxValue, MathUtils.Clamp(int.MaxValue, int.MinValue, int.MaxValue));
            Assert.AreEqual(0, MathUtils.Clamp(int.MinValue, 0, 1));
            Assert.AreEqual(1, MathUtils.Clamp(int.MaxValue, 0, 1));
        }

        [TestMethod]
        public void ClampDouble_BelowMin_ReturnsMin()
        {
            Assert.AreEqual(-1.5, MathUtils.Clamp(-9.0, -1.5, 2.5));
        }

        [TestMethod]
        public void ClampDouble_AboveMax_ReturnsMax()
        {
            Assert.AreEqual(2.5, MathUtils.Clamp(9.0, -1.5, 2.5));
        }

        [TestMethod]
        public void ClampDouble_Interior_ReturnsValueUnchanged()
        {
            Assert.AreEqual(0.1, MathUtils.Clamp(0.1, -1.5, 2.5));
        }

        [TestMethod]
        public void ClampDouble_OnBounds_ReturnsBound()
        {
            Assert.AreEqual(-1.5, MathUtils.Clamp(-1.5, -1.5, 2.5));
            Assert.AreEqual(2.5, MathUtils.Clamp(2.5, -1.5, 2.5));
        }

        [TestMethod]
        public void ClampDouble_AdjacentToBounds_StaysExact()
        {
            double belowMin = System.BitConverter.Int64BitsToDouble(System.BitConverter.DoubleToInt64Bits(1.0) - 1);
            double aboveMax = System.BitConverter.Int64BitsToDouble(System.BitConverter.DoubleToInt64Bits(2.0) + 1);
            double insideMin = System.BitConverter.Int64BitsToDouble(System.BitConverter.DoubleToInt64Bits(1.0) + 1);
            double insideMax = System.BitConverter.Int64BitsToDouble(System.BitConverter.DoubleToInt64Bits(2.0) - 1);

            Assert.AreEqual(1.0, MathUtils.Clamp(belowMin, 1.0, 2.0));
            Assert.AreEqual(2.0, MathUtils.Clamp(aboveMax, 1.0, 2.0));
            Assert.AreEqual(insideMin, MathUtils.Clamp(insideMin, 1.0, 2.0));
            Assert.AreEqual(insideMax, MathUtils.Clamp(insideMax, 1.0, 2.0));
        }

        [TestMethod]
        public void ClampDouble_Infinities()
        {
            Assert.AreEqual(0.0, MathUtils.Clamp(double.NegativeInfinity, 0.0, 1.0));
            Assert.AreEqual(1.0, MathUtils.Clamp(double.PositiveInfinity, 0.0, 1.0));
            Assert.AreEqual(double.PositiveInfinity, MathUtils.Clamp(double.PositiveInfinity, 0.0, double.PositiveInfinity));
        }

        [TestMethod]
        public void ClampDouble_NaN_PassesThrough()
        {
            Assert.IsTrue(double.IsNaN(MathUtils.Clamp(double.NaN, 0.0, 1.0)));
        }

        [TestMethod]
        public void ClampFloat_BelowMin_ReturnsMin()
        {
            Assert.AreEqual(0.25f, MathUtils.Clamp(-3f, 0.25, 0.75));
        }

        [TestMethod]
        public void ClampFloat_AboveMax_ReturnsMax()
        {
            Assert.AreEqual(0.75f, MathUtils.Clamp(3f, 0.25, 0.75));
        }

        [TestMethod]
        public void ClampFloat_Interior_ReturnsValueUnchanged()
        {
            Assert.AreEqual(0.5f, MathUtils.Clamp(0.5f, 0.25, 0.75));
        }

        [TestMethod]
        public void ClampFloat_OnBounds_ReturnsBound()
        {
            Assert.AreEqual(0.25f, MathUtils.Clamp(0.25f, 0.25, 0.75));
            Assert.AreEqual(0.75f, MathUtils.Clamp(0.75f, 0.25, 0.75));
        }

        [TestMethod]
        public void ClampFloat_BoundsAreComparedInDouble()
        {
            // 0.1f widens to 0.10000000149..., which is above the double bound 0.1, so it clamps down to
            // (float)0.1 rather than passing through; the result must still round-trip to the same float.
            Assert.AreEqual(0.1f, MathUtils.Clamp(0.1f, 0.0, 0.1));
            Assert.AreEqual(0.1f, MathUtils.Clamp(0.5f, 0.0, 0.1));
        }

        [TestMethod]
        public void ClampFloat_Infinities()
        {
            Assert.AreEqual(0f, MathUtils.Clamp(float.NegativeInfinity, 0.0, 1.0));
            Assert.AreEqual(1f, MathUtils.Clamp(float.PositiveInfinity, 0.0, 1.0));
        }

        [TestMethod]
        public void ClampFloat_NaN_PassesThrough()
        {
            Assert.IsTrue(float.IsNaN(MathUtils.Clamp(float.NaN, 0.0, 1.0)));
        }
    }
}
