using System.Globalization;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using Viking.UI.WPF.Controls;

namespace VikingCoreTests
{
    /// <summary>
    /// Pins <see cref="SliderValueText"/>, the text rules of the editable box beside every preference slider
    /// (decision D-SliderEditBox): typed numbers in range are kept exactly, out-of-range numbers clamp, text that is
    /// not a number keeps the current value, "." and the current culture's decimal separator both parse, and a
    /// percent format reads typed text in percent.
    /// </summary>
    [TestClass]
    public class SliderValueTextTests
    {
        private static void UnderCulture(string cultureName, System.Action body)
        {
            CultureInfo saved = CultureInfo.CurrentCulture;
            try
            {
                CultureInfo.CurrentCulture = new CultureInfo(cultureName);
                body();
            }
            finally
            {
                CultureInfo.CurrentCulture = saved;
            }
        }

        [TestMethod]
        public void ResolveTypedValue_InRange_IsKeptExactlyWithoutSnapOrRounding()
        {
            Assert.AreEqual(0.37, SliderValueText.ResolveTypedValue("0.37", 0.5, 0, 1, "F2"));
            Assert.AreEqual(0.333, SliderValueText.ResolveTypedValue("0.333", 0.5, 0, 1, "F2"));
            Assert.AreEqual(-2.5, SliderValueText.ResolveTypedValue(" -2.5 ", 0, -5, 5, "F1"));
        }

        [TestMethod]
        public void ResolveTypedValue_AtTheEnds_IsKept()
        {
            Assert.AreEqual(0.0, SliderValueText.ResolveTypedValue("0", 0.5, 0, 1, "F2"));
            Assert.AreEqual(1.0, SliderValueText.ResolveTypedValue("1", 0.5, 0, 1, "F2"));
        }

        [TestMethod]
        public void ResolveTypedValue_OutOfRange_ClampsToTheNearestEnd()
        {
            Assert.AreEqual(1.0, SliderValueText.ResolveTypedValue("7", 0.5, 0, 1, "F2"));
            Assert.AreEqual(250.0, SliderValueText.ResolveTypedValue("10", 500, 250, 1000, "F0"));
            Assert.AreEqual(32.0, SliderValueText.ResolveTypedValue("1e6", 4, 2, 32, "F0"));
        }

        [TestMethod]
        public void ResolveTypedValue_NotANumber_KeepsTheCurrentValue()
        {
            Assert.AreEqual(0.42, SliderValueText.ResolveTypedValue("abc", 0.42, 0, 1, "F2"));
            Assert.AreEqual(0.42, SliderValueText.ResolveTypedValue("", 0.42, 0, 1, "F2"));
            Assert.AreEqual(0.42, SliderValueText.ResolveTypedValue("   ", 0.42, 0, 1, "F2"));
            Assert.AreEqual(0.42, SliderValueText.ResolveTypedValue(null!, 0.42, 0, 1, "F2"));
            Assert.AreEqual(0.42, SliderValueText.ResolveTypedValue("NaN", 0.42, 0, 1, "F2"));
            Assert.AreEqual(0.42, SliderValueText.ResolveTypedValue("Infinity", 0.42, 0, 1, "F2"));
        }

        [TestMethod]
        public void TryParse_AcceptsPointUnderACommaCulture_AndTheCultureComma()
        {
            UnderCulture("de-DE", () =>
            {
                Assert.IsTrue(SliderValueText.TryParse("0.75", "F2", out double point));
                Assert.AreEqual(0.75, point);
                Assert.IsTrue(SliderValueText.TryParse("0,75", "F2", out double comma));
                Assert.AreEqual(0.75, comma);
            });
        }

        [TestMethod]
        public void TryParse_RejectsThousandsGrouping_SoPointAndCommaNeverDisagree()
        {
            UnderCulture("en-US", () => Assert.IsFalse(SliderValueText.TryParse("1,5", "F2", out _)));
            UnderCulture("de-DE", () =>
            {
                Assert.IsTrue(SliderValueText.TryParse("1.5", "F2", out double value));
                Assert.AreEqual(1.5, value);
            });
        }

        [TestMethod]
        public void PercentFormat_ReadsTypedTextInPercent()
        {
            Assert.IsTrue(SliderValueText.TryParse("5", "P0", out double plain));
            Assert.AreEqual(0.05, plain, 1e-12);
            Assert.IsTrue(SliderValueText.TryParse("12 %", "P0", out double spaced));
            Assert.AreEqual(0.12, spaced, 1e-12);
            Assert.IsTrue(SliderValueText.TryParse("3%", "P0", out double tight));
            Assert.AreEqual(0.03, tight, 1e-12);
            Assert.AreEqual(0.25, SliderValueText.ResolveTypedValue("40", 0.05, 0, 0.25, "P0"));
            Assert.IsFalse(SliderValueText.TryParse("%", "P0", out _));
        }

        [TestMethod]
        public void IsPercentFormat_OnlyForPercentFormats()
        {
            Assert.IsTrue(SliderValueText.IsPercentFormat("P"));
            Assert.IsTrue(SliderValueText.IsPercentFormat("p2"));
            Assert.IsTrue(SliderValueText.IsPercentFormat("0.#%"));
            Assert.IsFalse(SliderValueText.IsPercentFormat("F2"));
            Assert.IsFalse(SliderValueText.IsPercentFormat("0.##"));
            Assert.IsFalse(SliderValueText.IsPercentFormat(""));
            Assert.IsFalse(SliderValueText.IsPercentFormat(null!));
        }

        [TestMethod]
        public void NonPercentFormat_DoesNotDivideTypedText()
        {
            Assert.IsTrue(SliderValueText.TryParse("50", "F0", out double value));
            Assert.AreEqual(50.0, value);
        }

        [TestMethod]
        public void Format_UsesThePreferenceDialogFormatsWithAPoint()
        {
            UnderCulture("de-DE", () =>
            {
                Assert.AreEqual("0.50", SliderValueText.Format(0.5, "F2"));
                Assert.AreEqual("2.25", SliderValueText.Format(2.25, "0.##"));
                Assert.AreEqual("2", SliderValueText.Format(2.0, "0.##"));
                Assert.AreEqual("16", SliderValueText.Format(16, "F0"));
                Assert.AreEqual("0.5", SliderValueText.Format(0.5, null!));
            });
        }

        [TestMethod]
        public void Format_ThenParse_RoundTripsThePercentShown()
        {
            string shown = SliderValueText.Format(0.05, "P0");
            Assert.IsTrue(SliderValueText.TryParse(shown, "P0", out double value), shown);
            Assert.AreEqual(0.05, value, 1e-12);
        }

        [TestMethod]
        public void Clamp_InvertedRange_UsesTheMinimum()
        {
            Assert.AreEqual(5.0, SliderValueText.Clamp(3, 5, 1));
            Assert.AreEqual(5.0, SliderValueText.Clamp(9, 5, 1));
            Assert.AreEqual(0.5, SliderValueText.Clamp(0.5, 0, 1));
        }
    }
}
