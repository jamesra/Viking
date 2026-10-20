using System;
using System.Globalization;
using System.Windows;
using System.Windows.Data;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using Viking.UI.WPF.Converters;

namespace VikingCoreTests
{
    /// <summary>Pins the small WPF value converters in Viking.UI.WPF.Converters.</summary>
    [TestClass]
    public class ConverterTests
    {
        private static readonly CultureInfo Inv = CultureInfo.InvariantCulture;
        private static readonly CultureInfo German = CultureInfo.GetCultureInfo("de-DE");

        /// <summary>An invariant-based culture whose negative sign is "~", so culture use is observable.</summary>
        private static CultureInfo TildeCulture()
        {
            var c = (CultureInfo)CultureInfo.InvariantCulture.Clone();
            c.NumberFormat.NegativeSign = "~";
            return c;
        }

        [TestMethod]
        public void BoolToVisibility_Convert_MapsBoolAndTreatsNonBoolAsCollapsed()
        {
            var c = new BoolToVisibilityConverter();

            Assert.AreEqual(Visibility.Visible, c.Convert(true, typeof(Visibility), null, Inv));
            Assert.AreEqual(Visibility.Collapsed, c.Convert(false, typeof(Visibility), null, Inv));
            Assert.AreEqual(Visibility.Collapsed, c.Convert(null, typeof(Visibility), null, Inv));
            Assert.AreEqual(Visibility.Collapsed, c.Convert("true", typeof(Visibility), null, Inv));
        }

        [TestMethod]
        public void BoolToVisibility_ConvertBack_OnlyVisibleIsTrue()
        {
            var c = new BoolToVisibilityConverter();

            Assert.AreEqual(true, c.ConvertBack(Visibility.Visible, typeof(bool), null, Inv));
            Assert.AreEqual(false, c.ConvertBack(Visibility.Collapsed, typeof(bool), null, Inv));
            Assert.AreEqual(false, c.ConvertBack(Visibility.Hidden, typeof(bool), null, Inv));
            Assert.AreEqual(false, c.ConvertBack(null, typeof(bool), null, Inv));
            Assert.AreEqual(false, c.ConvertBack(1, typeof(bool), null, Inv));
        }

        [TestMethod]
        public void BoolToFontWeight_Convert_TrueIsBoldEverythingElseNormal()
        {
            var c = new BoolToFontWeightConverter();

            Assert.AreEqual(FontWeights.Bold, c.Convert(true, typeof(FontWeight), null, Inv));
            Assert.AreEqual(FontWeights.Normal, c.Convert(false, typeof(FontWeight), null, Inv));
            Assert.AreEqual(FontWeights.Normal, c.Convert(null, typeof(FontWeight), null, Inv));
            Assert.AreEqual(FontWeights.Normal, c.Convert("true", typeof(FontWeight), null, Inv));
        }

        [TestMethod]
        public void BoolToFontWeight_ConvertBack_IsNotSupported()
        {
            Assert.ThrowsException<NotImplementedException>(
                () => new BoolToFontWeightConverter().ConvertBack(FontWeights.Bold, typeof(bool), null, Inv));
        }

        [TestMethod]
        public void StringToVisibility_Convert_VisibleOnlyForNonBlankStrings()
        {
            var c = new StringToVisibilityConverter();

            Assert.AreEqual(Visibility.Visible, c.Convert("x", typeof(Visibility), null, Inv));
            Assert.AreEqual(Visibility.Visible, c.Convert("  x ", typeof(Visibility), null, Inv));
            Assert.AreEqual(Visibility.Collapsed, c.Convert("", typeof(Visibility), null, Inv));
            Assert.AreEqual(Visibility.Collapsed, c.Convert("   \t", typeof(Visibility), null, Inv));
            Assert.AreEqual(Visibility.Collapsed, c.Convert(null, typeof(Visibility), null, Inv));
            Assert.AreEqual(Visibility.Collapsed, c.Convert(5, typeof(Visibility), null, Inv));
        }

        [TestMethod]
        public void StringToVisibility_ConvertBack_IsNotSupported()
        {
            Assert.ThrowsException<NotImplementedException>(
                () => new StringToVisibilityConverter().ConvertBack(Visibility.Visible, typeof(string), null, Inv));
        }

        [TestMethod]
        public void Int32Converter_Convert_FormatsWithSuppliedCulture()
        {
            var c = new Int32Converter();

            Assert.AreEqual("-1234567", c.Convert(-1234567, typeof(string), null, Inv));
            Assert.AreEqual("~5", c.Convert(-5, typeof(string), null, TildeCulture()));
        }

        [TestMethod]
        public void Int32Converter_Convert_NonIntIsEmptyString()
        {
            var c = new Int32Converter();

            Assert.AreEqual(string.Empty, c.Convert(null, typeof(string), null, Inv));
            Assert.AreEqual(string.Empty, c.Convert("5", typeof(string), null, Inv));
            Assert.AreEqual(string.Empty, c.Convert(5L, typeof(string), null, Inv));
        }

        [TestMethod]
        public void Int32Converter_Convert_NullCultureUsesCurrentCulture()
        {
            var saved = CultureInfo.CurrentCulture;
            try
            {
                CultureInfo.CurrentCulture = TildeCulture();

                Assert.AreEqual("~7", new Int32Converter().Convert(-7, typeof(string), null, null));
            }
            finally
            {
                CultureInfo.CurrentCulture = saved;
            }
        }

        [TestMethod]
        public void Int32Converter_ConvertBack_ParsesWithoutClampingByDefault()
        {
            var c = new Int32Converter();

            Assert.AreEqual(42, c.ConvertBack("42", typeof(int), null, Inv));
            Assert.AreEqual(-42, c.ConvertBack("-42", typeof(int), null, Inv));
            Assert.AreEqual(int.MaxValue, c.ConvertBack(int.MaxValue.ToString(Inv), typeof(int), null, Inv));
            Assert.AreEqual(int.MinValue, c.ConvertBack(int.MinValue.ToString(Inv), typeof(int), null, Inv));
        }

        [TestMethod]
        public void Int32Converter_ConvertBack_ClampsToMinimumAndMaximum()
        {
            var c = new Int32Converter { Minimum = 1, Maximum = 10 };

            Assert.AreEqual(1, c.ConvertBack("0", typeof(int), null, Inv));
            Assert.AreEqual(1, c.ConvertBack("-99", typeof(int), null, Inv));
            Assert.AreEqual(10, c.ConvertBack("11", typeof(int), null, Inv));
            Assert.AreEqual(10, c.ConvertBack("1000", typeof(int), null, Inv));
            Assert.AreEqual(1, c.ConvertBack("1", typeof(int), null, Inv));
            Assert.AreEqual(10, c.ConvertBack("10", typeof(int), null, Inv));
            Assert.AreEqual(5, c.ConvertBack("5", typeof(int), null, Inv));
        }

        [TestMethod]
        public void Int32Converter_ConvertBack_UsesSuppliedCultureSign()
        {
            var c = new Int32Converter();
            var tilde = TildeCulture();

            Assert.AreEqual(-5, c.ConvertBack("~5", typeof(int), null, tilde));
            Assert.AreSame(Binding.DoNothing, c.ConvertBack("-5", typeof(int), null, tilde));
        }

        [TestMethod]
        public void Int32Converter_ConvertBack_AllowsSurroundingWhitespaceButNotGroupingOrDecimals()
        {
            var c = new Int32Converter();

            Assert.AreEqual(12, c.ConvertBack(" 12 ", typeof(int), null, Inv));
            Assert.AreSame(Binding.DoNothing, c.ConvertBack("1,000", typeof(int), null, Inv));
            Assert.AreSame(Binding.DoNothing, c.ConvertBack("1.5", typeof(int), null, Inv));
            Assert.AreSame(Binding.DoNothing, c.ConvertBack("1.000", typeof(int), null, German));
        }

        [TestMethod]
        public void Int32Converter_ConvertBack_NullCultureParsesInvariantNotCurrent()
        {
            var saved = CultureInfo.CurrentCulture;
            try
            {
                CultureInfo.CurrentCulture = TildeCulture();

                Assert.AreEqual(-3, new Int32Converter().ConvertBack("-3", typeof(int), null, null));
            }
            finally
            {
                CultureInfo.CurrentCulture = saved;
            }
        }

        [TestMethod]
        public void Int32Converter_ConvertBack_ParseFailureOrNonStringReturnsDoNothing()
        {
            var c = new Int32Converter { Minimum = 1, Maximum = 10 };

            Assert.AreSame(Binding.DoNothing, c.ConvertBack("", typeof(int), null, Inv));
            Assert.AreSame(Binding.DoNothing, c.ConvertBack("abc", typeof(int), null, Inv));
            Assert.AreSame(Binding.DoNothing, c.ConvertBack("99999999999", typeof(int), null, Inv));
            Assert.AreSame(Binding.DoNothing, c.ConvertBack(null, typeof(int), null, Inv));
            Assert.AreSame(Binding.DoNothing, c.ConvertBack(5, typeof(int), null, Inv));
        }
    }
}
