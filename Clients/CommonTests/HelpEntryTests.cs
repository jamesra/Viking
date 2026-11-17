using Microsoft.VisualStudio.TestTools.UnitTesting;
using System.Linq;
using Viking.Common;

namespace CommonTests
{
    /// <summary>
    /// Pins how the help bar orders command help (<see cref="HelpEntry.Arrange"/>): plain-text lines first in the order given,
    /// a divider, then keyed lines sorted by key, then modifiers; and the one-line text of an entry.
    /// </summary>
    [TestClass]
    public class HelpEntryTests
    {
        private static string[] Lines(params HelpEntry[] entries) => HelpEntry.Arrange(entries).Select(e => e.ToString()).ToArray();

        [TestMethod]
        public void PlainTextKeepsItsOrderThenADividerThenSortedKeys()
        {
            string[] lines = Lines(
                HelpEntry.For(HelpInput.Home, "Home"),
                HelpEntry.Text("Step two"),
                HelpEntry.For(HelpInput.A, "A"),
                HelpEntry.Text("Step one"));

            CollectionAssert.AreEqual(new[] { "Step two", "Step one", "—", "A: A", "Home: Home" }, lines);
        }

        [TestMethod]
        public void TheDividerAppearsOnlyBetweenTwoGroups()
        {
            Assert.IsFalse(HelpEntry.Arrange([HelpEntry.Text("Only text")]).Any(e => e.IsDivider));
            Assert.IsFalse(HelpEntry.Arrange([HelpEntry.For(HelpInput.A, "Only keys")]).Any(e => e.IsDivider));
            Assert.AreEqual(0, HelpEntry.Arrange([]).Count);
        }

        [TestMethod]
        public void KeysSortLettersDigitsFunctionKeysNamedKeysThenMouse()
        {
            string[] lines = Lines(
                HelpEntry.For(HelpInput.Wheel, "zoom"),
                HelpEntry.For(HelpInput.F11, "f11"),
                HelpEntry.For(HelpInput.Escape, "esc"),
                HelpEntry.For(HelpInput.F2, "f2"),
                HelpEntry.For(HelpInput.D1, "one"),
                HelpEntry.For(HelpInput.Z, "z"),
                HelpEntry.For(HelpInput.B, "b"));

            CollectionAssert.AreEqual(new[] { "b", "z", "one", "f2", "f11", "esc", "zoom" },
                lines.Select(l => l.Split(':')[1].Trim()).ToArray());
        }

        [TestMethod]
        public void OneKeySortsByModifiersNoneShiftCtrlCtrlShift()
        {
            string[] lines = Lines(
                HelpEntry.For(HelpInput.C, "ctrl+shift", HelpModifiers.Ctrl | HelpModifiers.Shift),
                HelpEntry.For(HelpInput.C, "ctrl", HelpModifiers.Ctrl),
                HelpEntry.For(HelpInput.C, "plain"),
                HelpEntry.For(HelpInput.C, "shift", HelpModifiers.Shift));

            CollectionAssert.AreEqual(new[] { "plain", "shift", "ctrl", "ctrl+shift" }, lines.Select(l => l.Split(':')[1].Trim()).ToArray());
        }

        [TestMethod]
        public void ModifierOnlyEntriesAreKeyedAndSortFirst()
        {
            HelpEntry hold = new("Hold to measure horizontally", HelpModifiers.Shift);
            Assert.IsTrue(hold.IsKeyed);

            string[] lines = Lines(HelpEntry.For(HelpInput.A, "a"), hold);
            CollectionAssert.AreEqual(new[] { "⇧ Shift: Hold to measure horizontally", "A: a" }, lines);
        }

        [TestMethod]
        public void RepeatedEntriesAreShownOnce()
        {
            HelpEntry rotate = HelpEntry.For(HelpInput.RightDrag, "Rotate");
            Assert.AreEqual(1, HelpEntry.Arrange([rotate, HelpEntry.For(HelpInput.RightDrag, "Rotate")]).Count);
        }

        [TestMethod]
        public void OtherKeysSortAfterNamedKeysByTheirLabel()
        {
            HelpEntry period = new("period", Inputs: [HelpInput.Other], Label: "OemPeriod");
            HelpEntry comma = new("comma", Inputs: [HelpInput.Other], Label: "Oemcomma");

            string[] lines = Lines(period, HelpEntry.For(HelpInput.Tab, "tab"), comma);
            CollectionAssert.AreEqual(new[] { "Tab: tab", "Oemcomma: comma", "OemPeriod: period" }, lines);
        }

        [TestMethod]
        public void TheTextOfAnEntryNamesItsKeysAlternativesAndPlace()
        {
            Assert.AreEqual("Ctrl+⇧ Shift++/−: Brighten/darken",
                new HelpEntry("Brighten/darken", HelpModifiers.Ctrl | HelpModifiers.Shift, [HelpInput.Plus, HelpInput.Minus]).ToString());
            Assert.AreEqual("Ctrl+click on a control point: Remove control point",
                HelpEntry.For(HelpInput.LeftClick, "Remove control point", HelpModifiers.Ctrl, "on a control point").ToString());
            Assert.AreEqual("F3/Enter: Continue", new HelpEntry("Continue", Inputs: [HelpInput.F3, HelpInput.Enter]).ToString());
        }

        [TestMethod]
        public void AToggleIsMarkedBeforeItsDescription()
        {
            HelpEntry toggle = HelpEntry.For(HelpInput.NumStar, "Pen Mode").AsToggle();
            Assert.IsTrue(toggle.IsToggle);
            Assert.AreEqual("Num *: ⇌ Pen Mode", toggle.ToString());
            Assert.AreEqual(2, HelpEntry.Arrange([toggle, HelpEntry.For(HelpInput.NumStar, "Pen Mode")]).Count,
                "a toggle and a plain entry with the same text are different lines");
        }

        [TestMethod]
        public void IndentedListItemsKeepTheirPlaceAfterTheLineThatIntroducesThem()
        {
            HelpEntry intro = HelpEntry.Text("Click a button to choose");
            HelpEntry first = HelpEntry.Text("Cut").Indented();
            HelpEntry second = HelpEntry.Text("Merge").Indented();
            Assert.AreEqual(1, first.Indent);
            Assert.AreEqual(2, first.Indented().Indent);

            var arranged = HelpEntry.Arrange([HelpEntry.For(HelpInput.Escape, "Cancel"), intro, first, second]);
            CollectionAssert.AreEqual(new[] { 0, 1, 1 }, arranged.Take(3).Select(e => e.Indent).ToArray());
            CollectionAssert.AreEqual(new[] { "Click a button to choose", "Cut", "Merge" }, arranged.Take(3).Select(e => e.Description).ToArray());
        }

        [TestMethod]
        public void MouseAndPenInputsAreNotKeys()
        {
            Assert.AreEqual(HelpInputKind.Mouse, HelpEntry.KindOf(HelpInput.LeftDrag));
            Assert.AreEqual(HelpInputKind.Mouse, HelpEntry.KindOf(HelpInput.BackForwardButtons));
            Assert.AreEqual(HelpInputKind.Pen, HelpEntry.KindOf(HelpInput.PenContact));
            Assert.AreEqual(HelpInputKind.Key, HelpEntry.KindOf(HelpInput.Other));
            Assert.AreEqual(HelpInputKind.Key, HelpEntry.KindOf(HelpInput.Tab));
        }
    }
}
