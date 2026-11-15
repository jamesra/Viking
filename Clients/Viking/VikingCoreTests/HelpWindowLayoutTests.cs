using Microsoft.VisualStudio.TestTools.UnitTesting;
using System.Linq;
using Viking.UI.WPF;

namespace VikingCoreTests
{
    /// <summary>The help window (Shift+F1) adds a column of help rows for each <see cref="HelpWindow.ColumnWidth"/> of width.</summary>
    [TestClass]
    public class HelpWindowLayoutTests
    {
        [TestMethod]
        public void ANarrowWindowHasOneColumn()
        {
            Assert.AreEqual(1, HelpWindow.ColumnCount(HelpWindow.ColumnWidth - 1, 30));
            Assert.AreEqual(1, HelpWindow.ColumnCount(0, 30), "not laid out yet");
            Assert.AreEqual(1, HelpWindow.ColumnCount(double.NaN, 30));
        }

        [TestMethod]
        public void EachColumnWidthAddsAColumn()
        {
            Assert.AreEqual(2, HelpWindow.ColumnCount(HelpWindow.ColumnWidth * 2, 30));
            Assert.AreEqual(3, HelpWindow.ColumnCount((HelpWindow.ColumnWidth * 3) + 10, 30));
        }

        [TestMethod]
        public void NeverMoreColumnsThanRows()
        {
            Assert.AreEqual(2, HelpWindow.ColumnCount(HelpWindow.ColumnWidth * 5, 2));
            Assert.AreEqual(1, HelpWindow.ColumnCount(HelpWindow.ColumnWidth * 5, 0));
        }

        [TestMethod]
        public void RowsSplitIntoConsecutiveEvenRunsWithTheExtraRowsFirst()
        {
            var runs = HelpWindow.SplitIntoColumns(10, 3);
            CollectionAssert.AreEqual(new[] { (0, 4), (4, 3), (7, 3) }, runs.ToArray());
            Assert.AreEqual(10, runs.Sum(r => r.Count));
        }

        [TestMethod]
        public void OneColumnHoldsEveryRow()
        {
            CollectionAssert.AreEqual(new[] { (0, 7) }, HelpWindow.SplitIntoColumns(7, 1).ToArray());
            CollectionAssert.AreEqual(new[] { (0, 0) }, HelpWindow.SplitIntoColumns(0, 4).ToArray());
        }
    }
}
