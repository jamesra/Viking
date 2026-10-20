using System;
using System.Collections;
using System.Windows.Forms;
using FsCheck;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using Viking.Common;

namespace VikingCoreTests
{
    /// <summary>
    /// Pins <see cref="ListViewColumnSorter"/> column ordering for ObjectListView: numeric columns
    /// compare <see cref="ListViewItem.ListViewSubItem.Tag"/> via <see cref="IConvertible.ToDecimal(System.IFormatProvider?)"/>,
    /// other columns use tag text or subitem text, with null rows sorted last.
    /// </summary>
    [TestClass]
    public class ListViewColumnSorterTests
    {
        private static ListViewItem Row(int sortIndex, string text, object? tag)
        {
            var item = new ListViewItem(sortIndex == 0 ? text : string.Empty);
            for (int i = 1; i <= sortIndex; i++)
            {
                if (item.SubItems.Count <= i)
                    item.SubItems.Add(i == sortIndex ? text : string.Empty);
            }

            if (sortIndex > 0)
                item.SubItems[sortIndex].Text = text;

            item.SubItems[sortIndex].Tag = tag;
            return item;
        }

        private static int Compare(ListViewColumnSorter sorter, object? a, object? b)
        {
            IComparer comparer = sorter;
            return comparer.Compare(a, b);
        }

        [TestMethod]
        public void Compare_BothNullListViewItems_ReturnsZero()
        {
            var sorter = new ListViewColumnSorter(0, typeof(int));
            Assert.AreEqual(0, Compare(sorter, null, null));
        }

        [TestMethod]
        public void Compare_NullListViewItem_SortsNullLast()
        {
            var sorter = new ListViewColumnSorter(0, typeof(int));
            var row = Row(0, "1", 1);
            Assert.AreEqual(1, Compare(sorter, null, row));
            Assert.AreEqual(-1, Compare(sorter, row, null));
        }

        [TestMethod]
        public void Compare_NumericColumn_Ascending_UsesTagDecimalOrder()
        {
            var sorter = new ListViewColumnSorter(0, typeof(int)) { AscendingSort = true };
            var low = Row(0, "2", 2);
            var high = Row(0, "10", 10);

            Assert.IsTrue(Compare(sorter, low, high) < 0);
            Assert.IsTrue(Compare(sorter, high, low) > 0);
            Assert.AreEqual(0, Compare(sorter, low, Row(0, "2", 2)));
        }

        [TestMethod]
        public void Compare_NumericColumn_Descending_ReversesDecimalOrder()
        {
            var sorter = new ListViewColumnSorter(0, typeof(int)) { AscendingSort = false };
            var low = Row(0, "2", 2);
            var high = Row(0, "10", 10);

            Assert.IsTrue(Compare(sorter, low, high) > 0);
            Assert.IsTrue(Compare(sorter, high, low) < 0);
        }

        [TestMethod]
        public void Compare_NumericColumn_WithoutConvertibleTags_FallsBackToText()
        {
            var sorter = new ListViewColumnSorter(0, typeof(int)) { AscendingSort = true };
            var a = Row(0, "apple", null);
            var b = Row(0, "zebra", null);

            Assert.IsTrue(Compare(sorter, a, b) < 0);
        }

        [TestMethod]
        public void Compare_NonNumericColumn_BothTagsPresent_UsesTagString()
        {
            var sorter = new ListViewColumnSorter(0, typeof(string)) { AscendingSort = true };
            var a = Row(0, "display-a", "m");
            var b = Row(0, "display-b", "a");

            Assert.IsTrue(Compare(sorter, a, b) > 0);
        }

        [TestMethod]
        public void Compare_NonNumericColumn_MissingTags_UsesSubItemText()
        {
            var sorter = new ListViewColumnSorter(0, typeof(string)) { AscendingSort = true };
            var a = Row(0, "beta", null);
            var b = Row(0, "alpha", null);

            Assert.IsTrue(Compare(sorter, a, b) > 0);
        }

        [TestMethod]
        public void Compare_NumericColumn_OnSecondarySubItem_UsesSortIndex()
        {
            var sorter = new ListViewColumnSorter(1, typeof(decimal)) { AscendingSort = true };
            var a = Row(1, "1.5", 1.5m);
            var b = Row(1, "9.0", 9.0m);

            Assert.IsTrue(Compare(sorter, a, b) < 0);
        }

        [TestMethod]
        public void Compare_NumericColumn_MatchesDecimalCompare_ForGeneratedValues()
        {
            var valueGen = Gen.Choose(-1000, 1000).Select(i => (decimal)i);

            Prop.ForAll(Arb.From(valueGen), Arb.From(valueGen), (decimal x, decimal y) =>
            {
                var sorter = new ListViewColumnSorter(0, typeof(int)) { AscendingSort = true };
                var left = Row(0, x.ToString(), x);
                var right = Row(0, y.ToString(), y);
                int actual = Compare(sorter, left, right);
                int expected = x.CompareTo(y);
                Assert.AreEqual(Math.Sign(expected), Math.Sign(actual));
            }).QuickCheckThrowOnFailure();
        }
    }
}
