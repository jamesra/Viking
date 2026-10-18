using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using FsCheck;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using Viking.UI.Forms;

namespace VikingCoreTests
{
    /// <summary>
    /// Pins <see cref="GoToLocationForm.ParseNumbers"/>, which turns text pasted into the Go To Location dialog
    /// into X, Y, Z, and downsample. Volume X and Y can be negative, so the sign must survive the paste.
    /// </summary>
    [TestClass]
    public class GoToLocationPasteParserTests
    {
        private static readonly NumberFormatInfo EnUs = new CultureInfo("en-US").NumberFormat;
        private static readonly NumberFormatInfo DeDe = new CultureInfo("de-DE").NumberFormat;

        private static string[] Parse(string text, NumberFormatInfo format) =>
            GoToLocationForm.ParseNumbers(text, format, 4).ToArray();

        [TestMethod]
        public void PlainCoordinates_AreSplitInOrder()
        {
            CollectionAssert.AreEqual(new[] { "1000.5", "200", "12", "2" }, Parse("1000.5 200 12 2", EnUs));
        }

        [TestMethod]
        public void LabeledCoordinates_SkipTheLabels()
        {
            CollectionAssert.AreEqual(new[] { "1000.5", "200", "12" }, Parse("X: 1000.5 Y: 200 Z: 12", EnUs));
        }

        [TestMethod]
        public void NegativeCoordinates_KeepTheirSign()
        {
            CollectionAssert.AreEqual(new[] { "-5000.5", "-200", "12" }, Parse("-5000.5 -200 12", EnUs));
            CollectionAssert.AreEqual(new[] { "-5000.5", "200" }, Parse("X: -5000.5 Y: 200", EnUs));
        }

        [TestMethod]
        public void LeadingDecimalSeparator_IsKept()
        {
            CollectionAssert.AreEqual(new[] { ".5", "-.25" }, Parse(".5 -.25", EnUs));
            CollectionAssert.AreEqual(new[] { ",5", "-,25" }, Parse(",5 -,25", DeDe));
        }

        [TestMethod]
        public void CommaSeparatedList_DoesNotLeaveTrailingSeparators()
        {
            // In en-US the comma is the group separator, so it was swallowed into the number: "34," then read as
            // Z = 0 because int parsing rejects it.
            CollectionAssert.AreEqual(new[] { "100", "200", "34", "1" }, Parse("100, 200, 34, 1", EnUs));
            CollectionAssert.AreEqual(new[] { "-100.5", "200", "34" }, Parse("(-100.5, 200, 34)", EnUs));
        }

        [TestMethod]
        public void GroupSeparatorInsideNumber_IsKept()
        {
            CollectionAssert.AreEqual(new[] { "123,456.5", "7" }, Parse("123,456.5 7", EnUs));
            CollectionAssert.AreEqual(new[] { "123.456,5", "7" }, Parse("123.456,5 7", DeDe));
        }

        [TestMethod]
        public void MoreThanMaxCountNumbers_StopsAtMaxCount()
        {
            CollectionAssert.AreEqual(new[] { "1", "2", "3", "4" }, Parse("1 2 3 4 5 6", EnUs));
        }

        [TestMethod]
        public void TextWithoutNumbers_IsEmpty()
        {
            Assert.AreEqual(0, Parse("", EnUs).Length);
            Assert.AreEqual(0, Parse("no numbers - here.", EnUs).Length);
            Assert.AreEqual(0, GoToLocationForm.ParseNumbers(null!, EnUs, 4).Count);
        }

        [TestMethod]
        public void SignSeparatedFromDigitsBySpace_IsNotPartOfTheNumber()
        {
            CollectionAssert.AreEqual(new[] { "12" }, Parse("Z - 12", EnUs));
            CollectionAssert.AreEqual(new[] { "1", "2" }, Parse("1 - 2", EnUs));
        }

        [TestMethod]
        public void TrailingText_EndsTheLastNumber()
        {
            CollectionAssert.AreEqual(new[] { "1", "2", "3" }, Parse("1 2 3 px", EnUs));
        }

        /// <summary>
        /// Any doubles written with the round-trip format and joined by ", " parse back to the same values, under
        /// both a dot-decimal and a comma-decimal culture.
        /// </summary>
        [TestMethod]
        public void FormattedDoubles_RoundTrip()
        {
            foreach (NumberFormatInfo format in new[] { EnUs, DeDe })
            {
                string separator = format == EnUs ? ", " : "; ";
                Prop.ForAll(
                    Arb.From(Gen.ArrayOf(4, Gen.Choose(-500000, 500000).Select(n => n / 8.0))),
                    values =>
                    {
                        string text = string.Join(separator, values.Select(v => v.ToString("R", format)));
                        double[] parsed = GoToLocationForm.ParseNumbers(text, format, 4)
                            .Select(s => double.Parse(s, NumberStyles.Float, format)).ToArray();
                        return parsed.SequenceEqual(values);
                    }).QuickCheckThrowOnFailure();
            }
        }
    }
}
