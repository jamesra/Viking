using System;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using WebAnnotation.UI.AutoPolygonize;

namespace WebAnnotationTests.Commands
{
    /// <summary>
    /// <see cref="ProposalButtonState"/>: press, release, a confirm delay that ignores clicks, "?", then the second click
    /// carries the button out; the timeout, Esc and a click elsewhere cancel; moving away does not.
    /// </summary>
    [TestClass]
    public class ProposalButtonStateTests
    {
        private static readonly TimeSpan Delay = TimeSpan.FromMilliseconds(500);
        private static readonly TimeSpan Timeout = TimeSpan.FromSeconds(4);

        private TimeSpan now;
        private ProposalButtonState state;
        private readonly object proposal = new();
        private readonly object other = new();

        [TestInitialize]
        public void Init()
        {
            now = TimeSpan.FromSeconds(100);
            state = new ProposalButtonState(() => now);
        }

        private ProposalButtonClick Press(object target = null, ProposalButtonKind kind = ProposalButtonKind.Accept) =>
            state.Press(target ?? proposal, kind, Delay, Timeout);

        private ProposalButtonPhase Phase(ProposalButtonKind kind = ProposalButtonKind.Accept) => state.PhaseOf(proposal, kind);

        private void Arm()
        {
            Press();
            state.Release();
            now += Delay;
        }

        [TestMethod]
        public void TwoClicks_WithTheDelayBetween_Execute()
        {
            Assert.AreEqual(ProposalButtonPhase.Idle, Phase());
            Assert.AreEqual(ProposalButtonClick.None, Press());
            Assert.AreEqual(ProposalButtonPhase.Pressed, Phase());
            Assert.AreEqual(ProposalButtonPhase.Idle, Phase(ProposalButtonKind.Hide), "the other button is untouched");

            Assert.IsTrue(state.Release());
            Assert.AreEqual(ProposalButtonPhase.Waiting, Phase());
            now += TimeSpan.FromMilliseconds(499);
            Assert.AreEqual(ProposalButtonPhase.Waiting, Phase());
            now += TimeSpan.FromMilliseconds(1);
            Assert.AreEqual(ProposalButtonPhase.Armed, Phase());

            Assert.AreEqual(ProposalButtonClick.Execute, Press());
            Assert.AreEqual(ProposalButtonPhase.Idle, Phase());
            Assert.IsNull(state.Target);
        }

        [TestMethod]
        public void ClickDuringTheDelay_IsIgnored()
        {
            Press();
            state.Release();
            now += TimeSpan.FromMilliseconds(100);

            Assert.AreEqual(ProposalButtonClick.None, Press(), "a double-click cannot carry it out");
            Assert.AreEqual(ProposalButtonPhase.Waiting, Phase(), "and does not restart it");
            now += TimeSpan.FromMilliseconds(400);
            Assert.AreEqual(ProposalButtonPhase.Armed, Phase());
        }

        [TestMethod]
        public void HeldButton_StaysPressed_AndAnotherPressIsIgnored()
        {
            Press();
            now += TimeSpan.FromSeconds(10);
            Assert.AreEqual(ProposalButtonPhase.Pressed, Phase(), "no delay or timeout runs while held");
            Assert.AreEqual(ProposalButtonClick.None, Press(), "the pen and mouse events of one tap");
            Assert.AreEqual(ProposalButtonPhase.Pressed, Phase());
        }

        [TestMethod]
        public void SecondReleaseChangesNothing()
        {
            Press();
            Assert.IsTrue(state.Release());
            now += TimeSpan.FromMilliseconds(300);
            Assert.IsFalse(state.Release(), "mouse-up after pen-leave");
            now += TimeSpan.FromMilliseconds(200);
            Assert.AreEqual(ProposalButtonPhase.Armed, Phase(), "the delay counts from the first release");
        }

        [TestMethod]
        public void Timeout_ReturnsToIdle()
        {
            Arm();
            now += Timeout - TimeSpan.FromMilliseconds(1);
            Assert.AreEqual(ProposalButtonPhase.Armed, Phase());
            now += TimeSpan.FromMilliseconds(1);
            Assert.AreEqual(ProposalButtonPhase.Idle, Phase());
            Assert.AreEqual(ProposalButtonClick.None, Press(), "a late click starts again");
            Assert.AreEqual(ProposalButtonPhase.Pressed, Phase());
        }

        [TestMethod]
        public void Reset_CancelsAnArmedButton()
        {
            Arm();
            Assert.IsTrue(state.Reset());
            Assert.AreEqual(ProposalButtonPhase.Idle, Phase());
            Assert.IsFalse(state.Reset(), "nothing left to cancel");
        }

        [TestMethod]
        public void PressingTheOtherButton_StartsItInstead()
        {
            Arm();
            Assert.AreEqual(ProposalButtonClick.None, Press(kind: ProposalButtonKind.Hide));
            Assert.AreEqual(ProposalButtonPhase.Idle, Phase(ProposalButtonKind.Accept));
            Assert.AreEqual(ProposalButtonPhase.Pressed, Phase(ProposalButtonKind.Hide));
        }

        [TestMethod]
        public void PressingAnotherProposal_StartsItInstead()
        {
            Arm();
            Press(other);
            Assert.AreEqual(ProposalButtonPhase.Idle, Phase());
            Assert.AreEqual(ProposalButtonPhase.Pressed, state.PhaseOf(other, ProposalButtonKind.Accept));
            Assert.AreSame(other, state.Target);
        }

        [TestMethod]
        public void TimeAloneDoesNotCancel_BeforeTheTimeout()
        {
            // Moving the cursor away does not reach the state at all; only time, Esc and clicks change it.
            Arm();
            now += TimeSpan.FromSeconds(3);
            Assert.AreEqual(ProposalButtonPhase.Armed, Phase());
        }

        [TestMethod]
        public void NextChange_TimesTheQuestionMarkAndTheTimeout()
        {
            Assert.IsNull(state.NextChange());
            Press();
            Assert.IsNull(state.NextChange(), "held: waits for the release");
            state.Release();
            Assert.AreEqual(Delay, state.NextChange());
            now += Delay;
            Assert.AreEqual(Timeout, state.NextChange());
            now += Timeout;
            Assert.IsNull(state.NextChange());
        }
    }
}
