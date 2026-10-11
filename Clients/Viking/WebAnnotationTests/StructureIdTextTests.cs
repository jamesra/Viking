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

        [TestMethod]
        [DataRow("0", 0UL)]
        [DataRow(" 42 ", 42UL)]
        [DataRow("9223372036854775807", (ulong)long.MaxValue)]
        public void ReadsPersistedUnsignedIds(string text, ulong expected)
        {
            Assert.IsTrue(StructureIdText.TryParseUnsigned(text, out ulong id));
            Assert.AreEqual(expected, id);
        }

        [TestMethod]
        [DataRow(null)]
        [DataRow("")]
        [DataRow("-1")]
        [DataRow("+1")]
        [DataRow("12a")]
        [DataRow("9223372036854775808")]
        [DataRow("18446744073709551615")]
        [DataRow("18446744073709551616")]
        public void DamagedPersistedIdsAreReportedNotThrown(string? text)
        {
            Assert.IsFalse(StructureIdText.TryParseUnsigned(text, out ulong id));
            Assert.AreEqual(0UL, id);
        }
    }
}
