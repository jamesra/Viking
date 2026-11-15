using Microsoft.VisualStudio.TestTools.UnitTesting;
using System.Linq;
using Viking.UI.WPF;

namespace VikingCoreTests
{
    /// <summary>The help window (F1) adds a column of help rows for each <see cref="HelpWindow.ColumnWidth"/> of width.</summary>
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
        public void EntriesAreGroupedBySectionInSectionOrderLeavingOutEmptySections()
        {
            Viking.Common.HelpEntry always = Viking.Common.HelpEntry.For(Viking.Common.HelpInput.Escape, "Cancel command").In(Viking.Common.HelpSection.AlwaysActive);
            Viking.Common.HelpEntry command = Viking.Common.HelpEntry.For(Viking.Common.HelpInput.LeftClick, "Place a point");
            Viking.Common.HelpEntry shortcut = Viking.Common.HelpEntry.For(Viking.Common.HelpInput.S, "Synapse", Viking.Common.HelpModifiers.Ctrl).In(Viking.Common.HelpSection.AnnotationShortcuts);

            var sections = HelpWindow.GroupIntoSections([always, command, shortcut, always]);
            CollectionAssert.AreEqual(
                new[] { Viking.Common.HelpSection.AnnotationShortcuts, Viking.Common.HelpSection.Command, Viking.Common.HelpSection.AlwaysActive },
                sections.Select(s => s.Section).ToArray());
            Assert.AreEqual(1, sections[2].Rows.Count, "a repeated entry is listed once");

            Viking.Common.HelpEntry hover = Viking.Common.HelpEntry.For(Viking.Common.HelpInput.LeftDrag, "Move the annotation").In(Viking.Common.HelpSection.MouseOver);
            CollectionAssert.AreEqual(
                new[] { Viking.Common.HelpSection.Command, Viking.Common.HelpSection.MouseOver, Viking.Common.HelpSection.AlwaysActive },
                HelpWindow.GroupIntoSections([always, hover, command]).Select(s => s.Section).ToArray(),
                "mouse over sits between the command and the always-active keys");

            var onlyCommand = HelpWindow.GroupIntoSections([command]);
            Assert.AreEqual(1, onlyCommand.Count);
            Assert.AreEqual(Viking.Common.HelpSection.Command, onlyCommand[0].Section, "untagged entries are the command's");
        }

        [TestMethod]
        public void OneColumnHoldsEveryRow()
        {
            CollectionAssert.AreEqual(new[] { (0, 7) }, HelpWindow.SplitIntoColumns(7, 1).ToArray());
            CollectionAssert.AreEqual(new[] { (0, 0) }, HelpWindow.SplitIntoColumns(0, 4).ToArray());
        }
    }
}
