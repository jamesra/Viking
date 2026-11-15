using Microsoft.VisualStudio.TestTools.UnitTesting;
using System.Collections.Generic;
using System.Linq;
using System.Windows.Forms;
using Viking.Common;
using Viking.UI;

namespace VikingCoreTests
{
    /// <summary>
    /// Pins <see cref="KeyBinding"/> dispatch and the <see cref="HelpInput"/> to WinForms <see cref="Keys"/> match: each key in a
    /// table reaches its handler with exactly its modifiers, keys outside the table are left alone, and help lists the table.
    /// </summary>
    [TestClass]
    public class KeyBindingTests
    {
        private static KeyBinding[] Table(List<string> log, System.Func<bool>? when = null) =>
        [
            KeyBinding.Do(HelpEntry.For(HelpInput.Escape, "cancel"), _ => log.Add("esc")),
            KeyBinding.Do(new HelpEntry("step", Inputs: [HelpInput.Plus, HelpInput.Minus]), _ => log.Add("step")),
            KeyBinding.Do(new HelpEntry("step ten", HelpModifiers.Shift, [HelpInput.Plus, HelpInput.Minus]), _ => log.Add("step ten")),
            KeyBinding.Do(HelpEntry.For(HelpInput.G, "go to", HelpModifiers.Ctrl), _ => log.Add("goto")),
            KeyBinding.Do(HelpEntry.For(HelpInput.F5, "only sometimes"), _ => log.Add("f5"), when),
            new(HelpEntry.For(HelpInput.Delete, "declines"), _ => false),
        ];

        private static string? Press(KeyBinding[] table, List<string> log, Keys keyData)
        {
            log.Clear();
            KeyEventArgs e = new(keyData);
            bool handled = KeyBindings.TryDispatch(table, e);
            Assert.AreEqual(handled, e.Handled);
            Assert.AreEqual(handled, e.SuppressKeyPress);
            return log.SingleOrDefault();
        }

        [TestMethod]
        public void EachKeyReachesItsHandlerWithExactlyItsModifiers()
        {
            List<string> log = [];
            KeyBinding[] table = Table(log, () => true);

            Assert.AreEqual("esc", Press(table, log, Keys.Escape));
            Assert.AreEqual("step", Press(table, log, Keys.Oemplus));
            Assert.AreEqual("step", Press(table, log, Keys.Subtract));
            Assert.AreEqual("step ten", Press(table, log, Keys.Oemplus | Keys.Shift));
            Assert.AreEqual("goto", Press(table, log, Keys.G | Keys.Control));
            Assert.AreEqual("f5", Press(table, log, Keys.F5));
        }

        [TestMethod]
        public void KeysOutsideTheTableAreNotHandled()
        {
            List<string> log = [];
            KeyBinding[] table = Table(log, () => true);

            Assert.IsNull(Press(table, log, Keys.G), "Ctrl+G is bound, plain G is not");
            Assert.IsNull(Press(table, log, Keys.G | Keys.Control | Keys.Shift));
            Assert.IsNull(Press(table, log, Keys.Escape | Keys.Alt));
            Assert.IsNull(Press(table, log, Keys.Q));
            Assert.IsNull(Press(table, log, Keys.Delete), "a handler that returns false leaves the key unhandled");
        }

        [TestMethod]
        public void ABindingThatDoesNotApplyIsNeitherHandledNorListed()
        {
            List<string> log = [];
            bool applies = false;
            KeyBinding[] table = Table(log, () => applies);

            Assert.IsNull(Press(table, log, Keys.F5));
            Assert.IsFalse(KeyBindings.Help(table).Any(h => h.Description == "only sometimes"));

            applies = true;
            Assert.AreEqual("f5", Press(table, log, Keys.F5));
            Assert.IsTrue(KeyBindings.Help(table).Any(h => h.Description == "only sometimes"));
        }

        [TestMethod]
        public void HelpListsEveryBindingThatApplies()
        {
            KeyBinding[] table = Table([], () => true);
            CollectionAssert.AreEqual(table.Select(b => b.Help).ToArray(), KeyBindings.Help(table).ToArray());
        }

        [TestMethod]
        public void AnyModifiersMatchesTheKeyWithModifiersHeld()
        {
            int count = 0;
            KeyBinding[] table = [KeyBinding.Do(HelpEntry.For(HelpInput.Arrows, "pan"), _ => count++, anyModifiers: true)];

            Assert.IsTrue(KeyBindings.TryDispatch(table, new KeyEventArgs(Keys.Left)));
            Assert.IsTrue(KeyBindings.TryDispatch(table, new KeyEventArgs(Keys.Down | Keys.Shift)));
            Assert.AreEqual(2, count);
        }

        [TestMethod]
        public void KeypadAndMainKeyboardAreTheSameInput()
        {
            Assert.IsTrue(HelpInputKeys.Matches(HelpInput.Plus, Keys.Add));
            Assert.IsTrue(HelpInputKeys.Matches(HelpInput.Minus, Keys.OemMinus));
            Assert.IsTrue(HelpInputKeys.Matches(HelpInput.D3, Keys.NumPad3));
            Assert.IsTrue(HelpInputKeys.Matches(HelpInput.D3, Keys.D3));
            Assert.IsTrue(HelpInputKeys.Matches(HelpInput.Enter, Keys.Return));
            Assert.IsFalse(HelpInputKeys.Matches(HelpInput.LeftClick, Keys.LButton), "mouse inputs are not keys");
        }

        [TestMethod]
        public void EveryKeyInputRoundTripsThroughFromKey()
        {
            foreach (HelpInput input in System.Enum.GetValues(typeof(HelpInput)).Cast<HelpInput>()
                         .Where(i => HelpEntry.KindOf(i) == HelpInputKind.Key && i is not HelpInput.Other and not HelpInput.Arrows))
            {
                Keys key = Enumerable.Range(0, 256).Select(k => (Keys)k).First(k => HelpInputKeys.Matches(input, k));
                Assert.AreEqual(input, HelpInputKeys.FromKey(key).Input, $"{input} via {key}");
            }

            Assert.AreEqual(HelpInput.Arrows, HelpInputKeys.FromKey(Keys.Up).Input);
        }

        [TestMethod]
        public void AKeyWithNoMemberIsOtherWithItsNameAndStillMatches()
        {
            (HelpInput input, string? label) = HelpInputKeys.FromKey(Keys.OemPeriod | Keys.Control);
            Assert.AreEqual(HelpInput.Other, input);
            Assert.AreEqual("OemPeriod", label);
            Assert.IsTrue(HelpInputKeys.Matches(HelpInput.Other, Keys.OemPeriod, label));
            Assert.IsFalse(HelpInputKeys.Matches(HelpInput.Other, Keys.Oemcomma, label));
        }
    }
}
