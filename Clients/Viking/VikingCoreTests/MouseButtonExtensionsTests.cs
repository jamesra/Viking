using System;
using System.Windows.Forms;
using FsCheck;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using Viking.UI;
using VikingXNAGraphics.Controls;

namespace VikingCoreTests
{
    /// <summary>
    /// Pins WinForms ↔ VikingXNAGraphics mouse button mapping used for pen and touch input in NGVV.
    /// </summary>
    [TestClass]
    public class MouseButtonExtensionsTests
    {
        private static readonly (MouseButtons WinForms, MouseButton Viking)[] KnownPairs =
        {
            (MouseButtons.Left, MouseButton.LEFT),
            (MouseButtons.Right, MouseButton.RIGHT),
            (MouseButtons.Middle, MouseButton.MIDDLE),
            (MouseButtons.XButton1, MouseButton.X1),
            (MouseButtons.XButton2, MouseButton.X2),
            (MouseButtons.None, MouseButton.NONE),
        };

        [TestMethod]
        public void ToVikingButton_KnownWinFormsValues_MapToExpectedVikingButtons()
        {
            foreach (var (winForms, viking) in KnownPairs)
            {
                Assert.AreEqual(viking, winForms.ToVikingButton());
            }
        }

        [TestMethod]
        public void ToWinFormButton_KnownVikingValues_MapToExpectedWinFormsButtons()
        {
            foreach (var (winForms, viking) in KnownPairs)
            {
                Assert.AreEqual(winForms, viking.ToWinFormButton());
            }
        }

        [TestMethod]
        public void RoundTrip_WinFormsToVikingAndBack_PreservesButton()
        {
            foreach (var (winForms, _) in KnownPairs)
            {
                Assert.AreEqual(winForms, winForms.ToVikingButton().ToWinFormButton());
            }
        }

        [TestMethod]
        public void RoundTrip_VikingToWinFormsAndBack_PreservesButton()
        {
            foreach (var (_, viking) in KnownPairs)
            {
                Assert.AreEqual(viking, viking.ToWinFormButton().ToVikingButton());
            }
        }

        [TestMethod]
        public void ToVikingButton_UndefinedWinFormsValue_ThrowsArgumentException()
        {
            var undefined = (MouseButtons)1234567;
            var ex = Assert.ThrowsException<ArgumentException>(() => undefined.ToVikingButton());
            StringAssert.Contains(ex.Message, undefined.ToString());
        }

        [TestMethod]
        public void ToWinFormButton_UndefinedVikingValue_ThrowsArgumentException()
        {
            var undefined = (MouseButton)999;
            var ex = Assert.ThrowsException<ArgumentException>(() => undefined.ToWinFormButton());
            StringAssert.Contains(ex.Message, undefined.ToString());
        }

        [TestMethod]
        public void RoundTrip_AllKnownWinFormsButtons_PreservesValue()
        {
            var winFormsGen = Gen.Elements(
                MouseButtons.Left,
                MouseButtons.Right,
                MouseButtons.Middle,
                MouseButtons.XButton1,
                MouseButtons.XButton2,
                MouseButtons.None);

            Prop.ForAll(Arb.From(winFormsGen), button =>
            {
                Assert.AreEqual(button, button.ToVikingButton().ToWinFormButton());
            }).QuickCheckThrowOnFailure();
        }

        [TestMethod]
        public void RoundTrip_AllKnownVikingButtons_PreservesValue()
        {
            var vikingGen = Gen.Elements(
                MouseButton.LEFT,
                MouseButton.RIGHT,
                MouseButton.MIDDLE,
                MouseButton.X1,
                MouseButton.X2,
                MouseButton.NONE);

            Prop.ForAll(Arb.From(vikingGen), button =>
            {
                Assert.AreEqual(button, button.ToWinFormButton().ToVikingButton());
            }).QuickCheckThrowOnFailure();
        }
    }
}
