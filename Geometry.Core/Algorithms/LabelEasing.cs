using System;

namespace Geometry
{
    /// <summary>Where one row of label text is drawn: its centre, its baseline angle and its font size, in volume space.</summary>
    public readonly struct LabelPose
    {
        public readonly Vector2 Center;

        /// <summary>Baseline angle in radians, counter-clockwise from +X, in (−π/2, π/2] like <see cref="LabelPlacement.Angle"/>.</summary>
        public readonly double Angle;

        public readonly double FontSize;

        public LabelPose(Vector2 center, double angle, double fontSize)
        {
            Center = center;
            Angle = angle;
            FontSize = fontSize;
        }

        public override string ToString() => $"{Center} {Angle * 180.0 / Math.PI:F1}° font {FontSize:F2}";
    }

    /// <summary>
    /// The maths of a label's animated switch from its old placement to its new one: an ease-out over
    /// <see cref="DurationSeconds"/>, the angle turned the short way within the range text reads in, the position moved in
    /// a straight line and the font size scaled geometrically. Pure, so the viewer only supplies the clock.
    /// </summary>
    public static class LabelEasing
    {
        /// <summary>How long a label takes to move to a new placement.</summary>
        public const double DurationSeconds = 0.15;

        /// <summary>Cubic ease-out, 1 − (1 − t)³, with <paramref name="t"/> clamped to [0, 1]: fast at first, settling gently.</summary>
        public static double EaseOut(double t)
        {
            if (!(t > 0))
                return 0;

            if (t >= 1)
                return 1;

            double rest = 1 - t;
            return 1 - (rest * rest * rest);
        }

        /// <summary>Eased progress, 0 to 1, <paramref name="elapsedSeconds"/> after a switch started.</summary>
        public static double Progress(double elapsedSeconds) => EaseOut(elapsedSeconds / DurationSeconds);

        /// <summary>
        /// The text angle a fraction <paramref name="t"/> of the way from <paramref name="from"/> to <paramref name="to"/>.
        /// Text at θ and θ + π lies along the same line, and <c>LabelView.DrawRotated</c> turns text that would read upside
        /// down by half a turn, so the angle turns by the difference taken modulo π, at most a quarter turn, and the result
        /// is kept in (−π/2, π/2]: the text always reads left to right (bottom to top when vertical) and never spins through
        /// upside down. A turn across vertical flips which end of the line the text starts from at the moment it crosses,
        /// as a level drag across vertical does. An exact quarter turn goes the direct way, so it does not cross vertical.
        /// </summary>
        public static double TextAngle(double from, double to, double t)
        {
            from = InscribedRuns.NormalizeAngle(from);
            to = InscribedRuns.NormalizeAngle(to);
            if (t >= 1)
                return to;

            double delta = to - from;
            double wrapped = delta > 0 ? delta - Math.PI : delta + Math.PI;
            if (Math.Abs(wrapped) < Math.Abs(delta))
                delta = wrapped;

            return InscribedRuns.NormalizeAngle(from + (delta * Math.Max(0, t)));
        }

        /// <summary>
        /// The pose a fraction <paramref name="t"/> (already eased) of the way from <paramref name="from"/> to
        /// <paramref name="to"/>; exactly <paramref name="to"/> at 1. The font scales geometrically, so a label doubling in
        /// size grows at an even rate, and linearly when either size is not positive.
        /// </summary>
        public static LabelPose Interpolate(LabelPose from, LabelPose to, double t)
        {
            if (t >= 1)
                return to;

            t = Math.Max(0, t);
            Vector2 center = new(from.Center.X + ((to.Center.X - from.Center.X) * t), from.Center.Y + ((to.Center.Y - from.Center.Y) * t));
            double font = from.FontSize > 0 && to.FontSize > 0
                ? from.FontSize * Math.Pow(to.FontSize / from.FontSize, t)
                : from.FontSize + ((to.FontSize - from.FontSize) * t);

            return new LabelPose(center, TextAngle(from.Angle, to.Angle, t), font);
        }
    }
}
