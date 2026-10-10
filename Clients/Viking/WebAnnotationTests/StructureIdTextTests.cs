using Microsoft.VisualStudio.TestTools.UnitTesting;
using WebAnnotation.UI;

namespace WebAnnotationTests
{
    [TestClass]
    public class StructureIdTextTests
    {
        [TestMethod]
        [DataRow("0", 0L)]
        [DataRow("42", 42L)]
        [DataRow("  42 ", 42L)]
        [DataRow("-7", -7L)]
        [DataRow("2147483648", 2147483648L)]
        [DataRow("9223372036854775807", long.MaxValue)]
        public void ReadsIdsIncludingThoseBeyondInt32(string text, long expected)
        {
            Assert.IsTrue(StructureIdText.TryParse(text, out long id));
            Assert.AreEqual(expected, id);
        }

        [TestMethod]
        [DataRow(null)]
        [DataRow("")]
        [DataRow("  ")]
        [DataRow("12a")]
        [DataRow("1.5")]
        [DataRow("1,000")]
        [DataRow("9223372036854775808")]
        [DataRow("99999999999999999999")]
        [DataRow("٣")]
        public void UnreadableOrOutOfRangeTextIsReportedNotThrown(string? text)
        {
            Assert.IsFalse(StructureIdText.TryParse(text, out long id));
            Assert.AreEqual(0L, id);
        }
    }
}
