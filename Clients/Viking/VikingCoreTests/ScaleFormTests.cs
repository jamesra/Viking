using System;
using System.Globalization;
using System.Linq;
using System.Windows.Forms;
using FsCheck;
using MeasurementExtension;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using SIMeasurement;

namespace VikingCoreTests
{
    /// <summary>
    /// Pins how Measurement > Set Scale (<see cref="ScaleForm"/>) shows and keeps the volume scale. The box used to be fixed at
    /// 0..100 with 3 decimals, so a scale over 100 units per pixel threw on Load, 0.0025 um was shown as 0.003, and OK sent the
    /// scale through a 15-digit decimal even when only the unit changed. Picking "cm" from the list threw on OK.
    /// </summary>
    [TestClass]
    public class ScaleFormTests
    {
        /// <summary>
        /// Runs the dialog's Load handler without showing it, as <see cref="Form.ShowDialog()"/> would.
        /// </summary>
        private sealed class LoadedScaleForm : ScaleForm
        {
            public LoadedScaleForm(SILengthUnits unit, double unitsPerPixel)
                : base(unit, unitsPerPixel)
            {
                OnLoad(EventArgs.Empty);
            }

            public NumericUpDown Number => (NumericUpDown)Controls.Find("numUnitsPerPixel", true)[0];

            public ComboBox Units => (ComboBox)Controls.Find("comboUnits", true)[0];
        }

        /// <summary>Real volume scales: TEM in nm (2.18 nm/px for RC1 and RC2), and the same in um, mm and coarse overviews.</summary>
        private static readonly (SILengthUnits Unit, double UnitsPerPixel)[] RealScales =
        {
            (SILengthUnits.nm, 2.18), (SILengthUnits.nm, 250), (SILengthUnits.nm, 2180), (SILengthUnits.um, 0.00218),
            (SILengthUnits.um, 0.0025), (SILengthUnits.um, 0.1), (SILengthUnits.mm, 0.00000218),
        };

        /// <summary>
        /// Regression: a scale over 100 threw ArgumentOutOfRangeException in Load, and 0.0025 was shown as 0.003. Every real
        /// scale loads, is shown with all its digits and is kept exactly by OK.
        /// </summary>
        [TestMethod]
        public void RealScales_LoadShowAllDigitsAndAreKept()
        {
            foreach ((SILengthUnits unit, double upp) in RealScales)
            {
                using LoadedScaleForm form = new(unit, upp);

                Assert.AreEqual((decimal)upp, decimal.Parse(form.Number.Text, CultureInfo.CurrentCulture), $"text shown for {upp} {unit}");
                Assert.AreEqual(unit.ToString(), form.Units.Text);
                Assert.IsTrue(form.TryAccept());
                Assert.AreEqual(upp, form.UnitsPerPixel, 0, $"{upp} {unit}");
                Assert.AreEqual(unit, form.UnitOfMeasure);
            }
        }

        /// <summary>
        /// The box shows exactly the scale's digits, and never fewer than the 3 decimals it always showed.
        /// </summary>
        [TestMethod]
        public void ShownText_HasTheScalesDigitsAndAtLeastThreeDecimals()
        {
            foreach ((double upp, string format) in new[] { (0.0025, "F4"), (2.18, "F3"), (250.0, "F3"), (0.00000218, "F8") })
            {
                using LoadedScaleForm form = new(SILengthUnits.um, upp);
                Assert.AreEqual(((decimal)upp).ToString(format, CultureInfo.CurrentCulture), form.Number.Text, $"{upp}");
            }
        }

        /// <summary>
        /// Regression: changing only the unit (0.0025 um to nm) must keep the scale's double, not its shown or decimal value.
        /// </summary>
        [TestMethod]
        public void ChangingOnlyTheUnit_KeepsTheScale()
        {
            using LoadedScaleForm form = new(SILengthUnits.um, 0.0025);
            form.Units.Text = "nm";

            Assert.IsTrue(form.TryAccept());
            Assert.AreEqual(0.0025, form.UnitsPerPixel, 0);
            Assert.AreEqual(SILengthUnits.nm, form.UnitOfMeasure);
        }

        /// <summary>
        /// Any positive scale from 1e-6 to 1e6 (log-uniform, with random low mantissa bits so most need all 17 digits) loads,
        /// is shown to within the 15 significant digits a decimal keeps, and comes back bit for bit when the number is not
        /// edited.
        /// </summary>
        [TestMethod]
        public void AnyScale_UneditedOk_KeepsTheExactDouble()
        {
            Gen<double> scales = Gen.Choose(0, 1_000_000).SelectMany(i => Gen.Choose(0, 1023).Select(bits =>
                BitConverter.Int64BitsToDouble(BitConverter.DoubleToInt64Bits(Math.Pow(10, -6 + 12 * (i / 1_000_000.0))) ^ bits)));

            Prop.ForAll(scales.ToArbitrary(), upp =>
            {
                using LoadedScaleForm form = new(SILengthUnits.nm, upp);
                return form.TryAccept()
                    && form.UnitsPerPixel == upp
                    && decimal.Parse(form.Number.Text, CultureInfo.CurrentCulture) == (decimal)upp;
            }).QuickCheckThrowOnFailure();
        }

        /// <summary>An edited number is stored as entered.</summary>
        [TestMethod]
        public void EditedNumber_IsStored()
        {
            using LoadedScaleForm form = new(SILengthUnits.nm, 2.18);
            form.Number.Value = 3.5m;

            Assert.IsTrue(form.TryAccept());
            Assert.AreEqual(3.5, form.UnitsPerPixel, 0);
        }

        /// <summary>
        /// A scale beyond the box's range shows the box's nearest end instead of throwing, and OK still keeps the volume's own
        /// scale.
        /// </summary>
        [TestMethod]
        public void ScaleOutsideTheBoxRange_ShowsTheNearestEndAndIsKept()
        {
            foreach (double huge in new[] { 5e12, 1e30, double.PositiveInfinity })
            {
                using LoadedScaleForm above = new(SILengthUnits.nm, huge);
                Assert.AreEqual(above.Number.Maximum, above.Number.Value, $"shown for {huge}");
                Assert.IsTrue(above.TryAccept());
                Assert.AreEqual(huge, above.UnitsPerPixel, 0);
            }

            foreach (double bad in new[] { 0, -1, double.NaN, 1e-40 })
            {
                using LoadedScaleForm below = new(SILengthUnits.nm, bad);
                Assert.AreEqual(below.Number.Minimum, below.Number.Value, $"shown for {bad}");
                Assert.IsTrue(below.Number.Minimum > 0, $"minimum for {bad}");
            }
        }

        /// <summary>Every unit the list offers is one OK accepts; the list used to offer "cm", which is not an SI prefix unit.</summary>
        [TestMethod]
        public void EveryListedUnit_IsAccepted()
        {
            using LoadedScaleForm form = new(SILengthUnits.nm, 2.18);
            foreach (string item in form.Units.Items.Cast<string>())
            {
                form.Units.Text = item;
                Assert.IsTrue(form.TryAccept(), item);
                Assert.AreEqual(item, form.UnitOfMeasure.ToString());
            }
        }

        /// <summary>
        /// Every <see cref="SILengthUnits"/> name parses to itself, case-sensitively (mm is millimetre, Mm megametre), and
        /// typed text that names no unit is refused instead of throwing.
        /// </summary>
        [TestMethod]
        public void TryParseUnit_AcceptsExactlyTheUnitNames()
        {
            foreach (SILengthUnits unit in Enum.GetValues(typeof(SILengthUnits)))
            {
                Assert.IsTrue(ScaleForm.TryParseUnit(unit.ToString(), out SILengthUnits parsed), unit.ToString());
                Assert.AreEqual(unit, parsed);
            }

            Assert.IsTrue(ScaleForm.TryParseUnit(" um ", out SILengthUnits trimmed));
            Assert.AreEqual(SILengthUnits.um, trimmed);

            foreach (string text in new[] { "cm", "", " ", "MM", "nanometre", "5", "nm,um", "µm" })
                Assert.IsFalse(ScaleForm.TryParseUnit(text, out _), $"'{text}'");
        }

        /// <summary>Refused unit text leaves the stored scale and unit as they were.</summary>
        [TestMethod]
        public void UnknownUnit_IsRefusedAndNothingChanges()
        {
            using LoadedScaleForm form = new(SILengthUnits.um, 0.0025);
            form.Units.Text = "cm";
            form.Number.Value = 1m;

            Assert.IsFalse(form.TryAccept());
            Assert.AreEqual(SILengthUnits.um, form.UnitOfMeasure);
            Assert.AreEqual(0.0025, form.UnitsPerPixel, 0);
        }
    }
}
