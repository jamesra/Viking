using Microsoft.VisualStudio.TestTools.UnitTesting;
using WebAnnotation.UI.Forms;

namespace WebAnnotationTests
{
    /// <summary>
    /// The Go To Location and Go To Structure dialogs parse their ID text box on every keystroke, so text too large
    /// for a <see cref="long"/> must report "not an ID" rather than throw out of the TextChanged handler.
    /// </summary>
    [TestClass]
    public class GoToActionFormIdTests
    {
        [TestMethod]
        [DataRow("0", 0L)]
        [DataRow("42", 42L)]
        [DataRow("  42 ", 42L)]
        [DataRow("+42", 42L)]
        [DataRow("-7", -7L)]
        [DataRow("2147483648", 2147483648L)]
        [DataRow("9223372036854775807", long.MaxValue)]
        [DataRow("-9223372036854775808", long.MinValue)]
        public void ReadsTheSameIdsConvertToInt64Did(string text, long expected)
        {
            Assert.IsTrue(GoToActionIDText.TryParse(text, out long id));
            Assert.AreEqual(expected, id);
        }

        [TestMethod]
        [DataRow("")]
        [DataRow("  ")]
        [DataRow("12a")]
        [DataRow("1.5")]
        [DataRow("1,000")]
        [DataRow("9223372036854775808")]
        [DataRow("99999999999999999999")]
        [DataRow("-9223372036854775809")]
        public void UnreadableOrOutOfRangeTextIsReportedNotThrown(string text)
        {
            Assert.IsFalse(GoToActionIDText.TryParse(text, out long id));
            Assert.AreEqual(0L, id);
        }
    }
}
