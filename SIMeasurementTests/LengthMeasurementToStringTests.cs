using Microsoft.VisualStudio.TestTools.UnitTesting;
using SIMeasurement;
using System.Globalization;
using System.Threading;

namespace SIMeasurementTests
{
    /// <summary>
    /// Pins the label text of <see cref="LengthMeasurement.ToString()"/> and
    /// <see cref="LengthMeasurement.ToString(uint, bool)"/> with <c>PreserveNonSignificant: true</c>,
    /// the form the measure tool uses (<c>ConvertToReadableUnits(...).ToString(3, true)</c>).
    /// Output follows the current culture, so every test runs under the invariant culture.
    /// </summary>
    [TestClass]
    public class LengthMeasurementToStringTests
    {
        private CultureInfo savedCulture = CultureInfo.InvariantCulture;

        [TestInitialize]
        public void PinInvariantCulture()
        {
            savedCulture = Thread.CurrentThread.CurrentCulture;
            Thread.CurrentThread.CurrentCulture = CultureInfo.InvariantCulture;
        }

        [TestCleanup]
        public void RestoreCulture() => Thread.CurrentThread.CurrentCulture = savedCulture;

        /// <summary>
        /// Trailing zeros are kept so measure labels keep a constant number of decimals.
        /// </summary>
        [TestMethod]
        public void ToStringScale_PadsTrailingZeros()
        {
            Assert.AreEqual("1.500 um", new LengthMeasurement(SILengthUnits.um, 1.5).ToString(3, true));
            Assert.AreEqual("2.000 nm", new LengthMeasurement(SILengthUnits.nm, 2).ToString(3, true));
        }

        /// <summary>
        /// Values with more digits than the scale round to the scale.
        /// </summary>
        [TestMethod]
        public void ToStringScale_RoundsToScale()
        {
            Assert.AreEqual("1.235 mm", new LengthMeasurement(SILengthUnits.mm, 1.23456).ToString(3, true));
            Assert.AreEqual("1.234 mm", new LengthMeasurement(SILengthUnits.mm, 1.2344).ToString(3, true));
        }

        /// <summary>
        /// Scale 0 prints no decimal point and rounds to a whole number.
        /// </summary>
        [TestMethod]
        public void ToStringScale_ScaleZero_HasNoDecimalPoint()
        {
            Assert.AreEqual("12 m", new LengthMeasurement(SILengthUnits.m, 12.3).ToString(0, true));
            Assert.AreEqual("13 m", new LengthMeasurement(SILengthUnits.m, 12.7).ToString(0, true));
        }

        /// <summary>
        /// A larger scale prints that many decimals.
        /// </summary>
        [TestMethod]
        public void ToStringScale_UsesRequestedDecimalCount()
        {
            Assert.AreEqual("0.50000 km", new LengthMeasurement(SILengthUnits.km, 0.5).ToString(5, true));
        }

        /// <summary>
        /// Zero prints as zeros at the scale, in its own unit.
        /// </summary>
        [TestMethod]
        public void ToStringScale_Zero_PrintsZeroWithUnit()
        {
            Assert.AreEqual("0.000 pm", new LengthMeasurement(SILengthUnits.pm, 0).ToString(3, true));
        }

        /// <summary>
        /// A negative length keeps its minus sign.
        /// </summary>
        [TestMethod]
        public void ToStringScale_Negative_KeepsSign()
        {
            Assert.AreEqual("-2.500 um", new LengthMeasurement(SILengthUnits.um, -2.5).ToString(3, true));
        }

        /// <summary>
        /// The unit suffix is the enum member name (the symbol), not its description.
        /// </summary>
        [TestMethod]
        public void ToStringScale_UnitSuffixIsSymbol()
        {
            Assert.AreEqual("1.000 Mm", new LengthMeasurement(SILengthUnits.Mm, 1).ToString(3, true));
            Assert.AreEqual("1.000 mm", new LengthMeasurement(SILengthUnits.mm, 1).ToString(3, true));
        }

        /// <summary>
        /// The measure tool path: a raw distance is made readable, then labeled with three decimals.
        /// </summary>
        [TestMethod]
        public void MeasureToolLabel_ReadableUnitsThenScale3()
        {
            LengthMeasurement readable = LengthMeasurement.ConvertToReadableUnits(SILengthUnits.nm, 1500);
            Assert.AreEqual("1.500 um", readable.ToString(3, true));
        }

        /// <summary>
        /// The decimal separator follows the current culture, so labels differ under a comma culture.
        /// </summary>
        [TestMethod]
        public void ToStringScale_FollowsCurrentCulture()
        {
            Thread.CurrentThread.CurrentCulture = new CultureInfo("de-DE");
            Assert.AreEqual("1,500 um", new LengthMeasurement(SILengthUnits.um, 1.5).ToString(3, true));
        }

        /// <summary>
        /// The parameterless form drops trailing zeros and shows at most three decimals.
        /// </summary>
        [TestMethod]
        public void ToStringDefault_TrimsTrailingZeros()
        {
            Assert.AreEqual("1.5 um", new LengthMeasurement(SILengthUnits.um, 1.5).ToString());
            Assert.AreEqual("2 nm", new LengthMeasurement(SILengthUnits.nm, 2).ToString());
            Assert.AreEqual("1.235 mm", new LengthMeasurement(SILengthUnits.mm, 1.23456).ToString());
        }

        /// <summary>
        /// The parameterless form prints zero as a single digit and keeps a negative sign.
        /// </summary>
        [TestMethod]
        public void ToStringDefault_ZeroAndNegative()
        {
            Assert.AreEqual("0 m", new LengthMeasurement(SILengthUnits.m, 0).ToString());
            Assert.AreEqual("-2.5 m", new LengthMeasurement(SILengthUnits.m, -2.5).ToString());
        }
    }
}
