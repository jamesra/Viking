using System;

namespace VikingXNAGraphics
{
    /// <summary>
    /// How a viewer decides to draw again. There are three kinds of reason, and each has one route:
    /// <list type="number">
    /// <item><b>Animation</b>: something drawn from the clock (a shimmer, a fade, an animated line style, a pulsing label,
    /// the section number wheel gliding). While it draws a frame that is not its last, it calls
    /// <see cref="Request"/>. The viewer draws again at its animation rate (about 40 per second) while any frame asked for
    /// another, and stops when a frame asks for none.</item>
    /// <item><b>A change</b>: input, a camera move, a store update, a tile or texture arriving, a setting. Its handler
    /// invalidates the viewer once (from another thread through BeginInvoke). It never calls <see cref="Request"/>.</item>
    /// <item><b>A change due at a known time</b> (a hidden annotation's fade-in starting, a button's ? appearing): a
    /// timer or Task.Delay that invalidates when the time comes.</item>
    /// </list>
    /// A slow backstop in the viewer redraws now and then while idle, so a change that forgot to invalidate shows late
    /// rather than never.
    /// </summary>
    /// <remarks>
    /// The viewer brackets every paint with <see cref="BeginFrame"/> and <see cref="EndFrame"/>. Paints run one at a time on
    /// the UI thread, so one static flag serves every viewer. <see cref="Request"/> is only meaningful during a draw; a
    /// change outside a draw invalidates instead. Do not call Invalidate from inside a draw to animate: the window is dirty
    /// again before the paint ends, so the next paint follows at once, at whatever rate the message loop allows.
    /// </remarks>
    public static class FrameRequests
    {
        private static bool requested;
        private static string? firstReason;

        /// <summary>The first reason given during the last finished frame, or null when it asked for none. For diagnostics.</summary>
        public static string? LastFrameReason { get; private set; }

        /// <summary>Called by the viewer before it draws.</summary>
        public static void BeginFrame()
        {
            requested = false;
            firstReason = null;
        }

        /// <summary>Called by the viewer after it draws. True when something drawn in the frame asked for another.</summary>
        public static bool EndFrame()
        {
            LastFrameReason = requested ? firstReason : null;
            bool result = requested;
            requested = false;
            firstReason = null;
            return result;
        }

        /// <summary>
        /// An animation drawn in this frame has not finished: draw another frame soon. Call it every frame until the
        /// animation's last frame, and not on that one.
        /// </summary>
        /// <param name="reason">A short name for diagnostics, e.g. "proposal shimmer".</param>
        public static void Request(string reason)
        {
            if (!requested)
                firstReason = reason;
            requested = true;
        }

        /// <summary>
        /// Line and curve shader techniques that move with the time parameter, so a frame drawing them must be followed by
        /// another. Matches the techniques in RoundLine.fx, RoundCurve.fx and their HSV copies.
        /// </summary>
        public static bool IsAnimatedTechnique(string? techniqueName) =>
            techniqueName is not null &&
            (string.Equals(techniqueName, nameof(LineStyle.AnimatedLinear), StringComparison.Ordinal) ||
             string.Equals(techniqueName, nameof(LineStyle.AnimatedBidirectional), StringComparison.Ordinal) ||
             string.Equals(techniqueName, nameof(LineStyle.AnimatedRadial), StringComparison.Ordinal));

        /// <summary>Asks for another frame when <paramref name="techniqueName"/> is an animated line or curve style.</summary>
        public static void RequestIfAnimated(string? techniqueName)
        {
            if (IsAnimatedTechnique(techniqueName))
                Request("animated line style");
        }
    }
}
