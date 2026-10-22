using Microsoft.VisualStudio.TestTools.UnitTesting;
using WebAnnotation.UI.Commands.Segmentation;

namespace WebAnnotationTests
{
    /// <summary>
    /// The segmentation retry reads the missing tile out of a NotFound detail inside an exception filter, so a parse
    /// that throws silently turns the retry off.
    /// </summary>
    [TestClass]
    public class SegmentationMissingTileParseTests
    {
        [TestMethod]
        [DataRow("TILE_NOT_FOUND row=3 col=7", 3, 7)]
        [DataRow("tile missing: row=-2   col=-9 (evicted)", -2, -9)]
        [DataRow("row=0\tcol=0", 0, 0)]
        public void RowAndColumnAreRead(string detail, int row, int col)
        {
            Assert.IsTrue(SegmentationViewportSession.TryParseMissingTile(detail, out int parsedRow, out int parsedCol));
            Assert.AreEqual(row, parsedRow);
            Assert.AreEqual(col, parsedCol);
        }

        [TestMethod]
        [DataRow(null)]
        [DataRow("")]
        [DataRow("no tile here")]
        [DataRow("row=3")]
        [DataRow("row=99999999999 col=1")]
        [DataRow("row=1 col=99999999999")]
        [DataRow("row=٣ col=1")]
        public void UnreadableDetailReturnsFalseWithoutThrowing(string detail)
        {
            Assert.IsFalse(SegmentationViewportSession.TryParseMissingTile(detail, out int row, out int col));
            Assert.AreEqual(0, row);
            Assert.AreEqual(0, col);
        }

        [TestMethod]
        public void MinusSignIsInvariantUnderOtherCultures()
        {
            System.Globalization.CultureInfo saved = System.Threading.Thread.CurrentThread.CurrentCulture;
            try
            {
                System.Globalization.CultureInfo culture = (System.Globalization.CultureInfo)System.Globalization.CultureInfo.InvariantCulture.Clone();
                culture.NumberFormat.NegativeSign = "−";
                System.Threading.Thread.CurrentThread.CurrentCulture = culture;

                Assert.IsTrue(SegmentationViewportSession.TryParseMissingTile("row=-4 col=5", out int row, out int col));
                Assert.AreEqual(-4, row);
                Assert.AreEqual(5, col);
            }
            finally
            {
                System.Threading.Thread.CurrentThread.CurrentCulture = saved;
            }
        }
    }
}
