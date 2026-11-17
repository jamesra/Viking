#nullable enable

using System;
using System.Diagnostics;

namespace WebAnnotation.UI.AutoPolygonize
{
    /// <summary>How a proposal button looks in its two-click cycle.</summary>
    internal enum ProposalButtonPhase
    {
        /// <summary>Shows its check or X; a click presses it.</summary>
        Idle,

        /// <summary>Held down after the first click: drawn pressed, without its symbol.</summary>
        Pressed,

        /// <summary>Released after the first click; the confirm delay is running and clicks are ignored.</summary>
        Waiting,

        /// <summary>Shows "?"; the second click carries out the button.</summary>
        Armed,
    }

    /// <summary>What a click on a proposal button should do.</summary>
    internal enum ProposalButtonClick
    {
        /// <summary>Nothing beyond redrawing: the click pressed the button, or came too soon after the first.</summary>
        None,

        /// <summary>The second click on an armed button: accept or hide the proposal now.</summary>
        Execute,
    }

    /// <summary>
    /// The two-click confirm shared by every proposal's accept and hide buttons. Only one button is in its cycle at a time.
    /// </summary>
    /// <remarks>
    /// The first click presses the button (<see cref="ProposalButtonPhase.Pressed"/>). Releasing it starts the confirm delay
    /// (<see cref="ProposalButtonPhase.Waiting"/>), during which clicks are ignored, so a double-click or a bounce cannot
    /// carry it out. After the delay the button shows "?" (<see cref="ProposalButtonPhase.Armed"/>) and the next click on it
    /// carries it out. It returns to idle when the timeout passes without that click, on Esc, or on a click anywhere else.
    /// Moving the cursor away does not cancel it. Phases that depend on time are worked out when read, from
    /// <see cref="Clock"/>, so nothing needs a timer; the viewer only has to redraw at <see cref="NextChange"/>.
    /// </remarks>
    internal sealed class ProposalButtonState
    {
        private object? target;
        private ProposalButtonKind kind;
        private bool held;
        private TimeSpan releasedAt;
        private TimeSpan confirmDelay;
        private TimeSpan confirmTimeout;

        /// <param name="clock">Time since an arbitrary start; tests pass a fake. Null uses a <see cref="Stopwatch"/>.</param>
        public ProposalButtonState(Func<TimeSpan>? clock = null)
        {
            if (clock is null)
            {
                Stopwatch stopwatch = Stopwatch.StartNew();
                clock = () => stopwatch.Elapsed;
            }

            Clock = clock;
        }

        /// <summary>The time source the phases are worked out from.</summary>
        public Func<TimeSpan> Clock { get; }

        /// <summary>The proposal whose button is in its cycle, or null when none is.</summary>
        public object? Target
        {
            get
            {
                Expire();
                return target;
            }
        }

        /// <summary>The phase of <paramref name="buttonKind"/> on <paramref name="proposal"/>.</summary>
        public ProposalButtonPhase PhaseOf(object proposal, ProposalButtonKind buttonKind)
        {
            Expire();
            if (target is null || !ReferenceEquals(target, proposal) || kind != buttonKind)
                return ProposalButtonPhase.Idle;

            if (held)
                return ProposalButtonPhase.Pressed;

            return Clock() - releasedAt < confirmDelay ? ProposalButtonPhase.Waiting : ProposalButtonPhase.Armed;
        }

        /// <summary>
        /// A click (mouse down or pen contact) on <paramref name="buttonKind"/> of <paramref name="proposal"/>.
        /// <paramref name="delay"/> and <paramref name="timeout"/> are the confirm delay and timeout to use if this click
        /// starts a new cycle.
        /// </summary>
        public ProposalButtonClick Press(object proposal, ProposalButtonKind buttonKind, TimeSpan delay, TimeSpan timeout)
        {
            if (proposal is null)
                throw new ArgumentNullException(nameof(proposal));

            switch (PhaseOf(proposal, buttonKind))
            {
                case ProposalButtonPhase.Armed:
                    Reset();
                    return ProposalButtonClick.Execute;
                case ProposalButtonPhase.Pressed:
                case ProposalButtonPhase.Waiting:
                    return ProposalButtonClick.None;
            }

            // Idle for this button: any other button's cycle ends and this one starts.
            target = proposal;
            kind = buttonKind;
            held = true;
            confirmDelay = delay < TimeSpan.Zero ? TimeSpan.Zero : delay;
            confirmTimeout = timeout < TimeSpan.Zero ? TimeSpan.Zero : timeout;
            return ProposalButtonClick.None;
        }

        /// <summary>The mouse button or pen was lifted. Starts the confirm delay when a button is held.</summary>
        /// <returns>True when this changed what is drawn.</returns>
        public bool Release()
        {
            if (target is null || !held)
                return false;

            held = false;
            releasedAt = Clock();
            return true;
        }

        /// <summary>Ends any cycle: Esc, a click anywhere else, or the proposal going away.</summary>
        /// <returns>True when a button was in its cycle.</returns>
        public bool Reset()
        {
            bool active = target is not null;
            target = null;
            held = false;
            return active;
        }

        /// <summary>
        /// How long until a button's look changes by itself ("?" appearing or timing out), or null when nothing will change
        /// without input. The viewer redraws then.
        /// </summary>
        public TimeSpan? NextChange()
        {
            Expire();
            if (target is null || held)
                return null;

            TimeSpan since = Clock() - releasedAt;
            if (since < confirmDelay)
                return confirmDelay - since;

            return confirmDelay + confirmTimeout - since;
        }

        /// <summary>Drops a cycle whose "?" has waited longer than the timeout.</summary>
        private void Expire()
        {
            if (target is not null && !held && Clock() - releasedAt >= confirmDelay + confirmTimeout)
                Reset();
        }
    }
}
