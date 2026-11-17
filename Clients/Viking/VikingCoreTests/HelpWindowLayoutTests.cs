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
                new[] { Viking.Common.HelpSection.Command, Viking.Common.HelpSection.AnnotationShortcuts, Viking.Common.HelpSection.AlwaysActive },
                sections.Select(s => s.Section).ToArray(),
                "the active command is always first");
            Assert.AreEqual(Viking.Common.HelpSection.Command, HelpWindow.SectionOrder[0]);
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
        public void InASectionUngroupedRowsComeFirstThenEachGroupSortedInTheOrderItFirstAppears()
        {
            Viking.Common.HelpSection shortcuts = Viking.Common.HelpSection.AnnotationShortcuts;
            Viking.Common.HelpEntry tagB = Viking.Common.HelpEntry.For(Viking.Common.HelpInput.B, "Bipolar").In(shortcuts).InGroup("Structure tags");
            Viking.Common.HelpEntry tagA = Viking.Common.HelpEntry.For(Viking.Common.HelpInput.A, "Triad").In(shortcuts).InGroup("Structure tags");
            Viking.Common.HelpEntry annotate = Viking.Common.HelpEntry.For(Viking.Common.HelpInput.Z, "Gap Junction").In(shortcuts).InGroup("Annotate");
            Viking.Common.HelpEntry loose = Viking.Common.HelpEntry.For(Viking.Common.HelpInput.Y, "Something else").In(shortcuts);

            var rows = HelpWindow.GroupIntoSections([tagB, annotate, tagA, loose]).Single().Rows;
            CollectionAssert.AreEqual(new[] { "Something else", "Triad", "Bipolar", "Gap Junction" }, rows.Select(r => r.Description).ToArray());

            var groups = HelpWindow.SplitIntoGroups(rows);
            CollectionAssert.AreEqual(new[] { null, "Structure tags", "Annotate" }, groups.Select(g => g.Group).ToArray());
            CollectionAssert.AreEqual(new[] { 1, 2, 1 }, groups.Select(g => g.Rows.Count).ToArray());
            Assert.AreEqual(0, HelpWindow.SplitIntoGroups([]).Count);
        }

        [TestMethod]
        public void TheHelpStripShowsAsManyEntriesSideBySideAsFit()
        {
            Assert.AreEqual(1, HelpBar.ColumnCount(HelpBar.ColumnWidth - 1, 10));
            Assert.AreEqual(2, HelpBar.ColumnCount(700, 10));
            Assert.AreEqual(4, HelpBar.ColumnCount(1250, 10));
            Assert.AreEqual(3, HelpBar.ColumnCount(5000, 3), "never more columns than entries");
            Assert.AreEqual(1, HelpBar.ColumnCount(0, 10), "not laid out yet");
        }

        [TestMethod]
        public void TheStatusBarSectionComesLast()
        {
            Viking.Common.HelpEntry status = Viking.Common.HelpEntry.For(Viking.Common.HelpInput.LeftClick, "Turn Pen Mode on or off", where: "on Pen On/Off").In(Viking.Common.HelpSection.StatusBar);
            Viking.Common.HelpEntry always = Viking.Common.HelpEntry.For(Viking.Common.HelpInput.Escape, "Cancel command").In(Viking.Common.HelpSection.AlwaysActive);

            var sections = HelpWindow.GroupIntoSections([status, always]);
            CollectionAssert.AreEqual(
                new[] { Viking.Common.HelpSection.AlwaysActive, Viking.Common.HelpSection.StatusBar },
                sections.Select(s => s.Section).ToArray());
            Assert.AreEqual("Status bar", HelpWindow.TitleOf(Viking.Common.HelpSection.StatusBar));
            CollectionAssert.AreEquivalent(
                System.Enum.GetValues(typeof(Viking.Common.HelpSection)),
                HelpWindow.SectionOrder,
                "every section is shown somewhere");
        }

        /// <summary>Plain-text lines span the window above the columns, so only keyed rows are split and the divider never sits in a column.</summary>
        [TestMethod]
        public void TextLinesAndTheDividerStayOutOfTheColumns()
        {
            Viking.Common.HelpEntry note = Viking.Common.HelpEntry.Text("Move the mouse to set the circle's radius");
            Viking.Common.HelpEntry click = Viking.Common.HelpEntry.For(Viking.Common.HelpInput.LeftClick, "Release to set the radius");
            Viking.Common.HelpEntry escape = Viking.Common.HelpEntry.For(Viking.Common.HelpInput.Escape, "Cancel command");

            var rows = Viking.Common.HelpEntry.Arrange([escape, note, click]);
            Assert.IsTrue(rows.Any(r => r.IsDivider), "Arrange separates text from keyed entries");

            var (text, keyed) = HelpWindow.SplitAtDivider(rows);
            CollectionAssert.AreEqual(new[] { note }, text.ToArray());
            Assert.AreEqual(2, keyed.Count);
            Assert.IsFalse(keyed.Any(r => r.IsDivider || !r.IsKeyed));
        }

        [TestMethod]
        public void OneColumnHoldsEveryRow()
        {
            CollectionAssert.AreEqual(new[] { (0, 7) }, HelpWindow.SplitIntoColumns(7, 1).ToArray());
            CollectionAssert.AreEqual(new[] { (0, 0) }, HelpWindow.SplitIntoColumns(0, 4).ToArray());
        }
    }
}
