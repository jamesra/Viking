using System.Globalization;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using Viking.UI.Controls;

namespace VikingCoreTests
{
    /// <summary>
    /// Pins <see cref="NumericTextBox"/> value getters, which feed the Go To Location dialog: the key filter
    /// admits any run of digits, so text that overflows the target type must read as 0 (like other unparsable
    /// text) instead of throwing out of the OK handler.
    /// </summary>
    [TestClass]
    public class NumericTextBoxValueTests
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
        public void IntValue_ParsesValidAndNegativeText()
        {
            UnderCulture("en-US", () =>
            {
                using var box = new NumericTextBox { Text = "1234" };
                Assert.AreEqual(1234, box.IntValue);
                box.Text = "-17";
                Assert.AreEqual(-17, box.IntValue);
            });
        }

        [TestMethod]
        public void IntValue_UnparsableOrEmptyText_IsZero()
        {
            using var box = new NumericTextBox { Text = "-" };
            Assert.AreEqual(0, box.IntValue);
            box.Text = string.Empty;
            Assert.AreEqual(0, box.IntValue);
        }

        [TestMethod]
        public void IntValue_TextBeyondInt32Range_IsZeroInsteadOfThrowing()
        {
            using var box = new NumericTextBox { Text = "99999999999" };
            Assert.AreEqual(0, box.IntValue);
        }

        [TestMethod]
        public void DoubleValue_ParsesUsingCurrentCulture()
        {
            UnderCulture("de-DE", () =>
            {
                using var box = new NumericTextBox { Text = "2,5" };
                Assert.AreEqual(2.5, box.DoubleValue, 0);
            });
            UnderCulture("en-US", () =>
            {
                using var box = new NumericTextBox { Text = "2.5" };
                Assert.AreEqual(2.5, box.DoubleValue, 0);
            });
        }

        [TestMethod]
        public void DoubleValue_UnparsableText_IsZero()
        {
            Assert.AreEqual(0.0, box.DoubleValue, 0);
        }

        [TestMethod]
        public void DoubleValue_TextBeyondDoubleRange_IsZeroInsteadOfThrowing()
        {
            using var box = new NumericTextBox { Text = new string('9', 400) };
            Assert.AreEqual(0.0, box.DoubleValue, 0);
        }
    }
}
