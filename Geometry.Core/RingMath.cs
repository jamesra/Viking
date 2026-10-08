using System;

namespace Geometry
{
    /// <summary>
    /// Allocation-free ring arithmetic shared by <see cref="Vector2Extensions.PolygonArea(Vector2[])"/> and
    /// <see cref="Polygon.CalculateCentroid(Vector2[], bool)"/>.
    /// </summary>
    /// <remarks>
    /// Both callers used to copy the ring and subtract the ring average so large coordinates keep their precision.
    /// These helpers evaluate the same sums over the same shifted values on the fly, so results are bit-identical to
    /// the copying code. A ring whose first and last points differ is treated as if the first point were appended
    /// (index <c>ring.Length</c> and above map to point 0), matching <c>EnsureClosedRing</c>.
    /// Not thread-safe to call while another thread mutates the ring.
    /// </remarks>
    internal static class RingMath
    {
        /// <summary>
        /// Number of points in the closed form of <paramref name="ring"/>: <c>Length</c> when already closed within
        /// tolerance, otherwise <c>Length + 1</c>.
        /// </summary>
        /// <exception cref="ArgumentNullException"><paramref name="ring"/> is null.</exception>
        /// <exception cref="InvalidOperationException"><paramref name="ring"/> is empty.</exception>
        internal static int ClosedCount(Vector2[] ring)
        {
            if (ring is null)
                throw new ArgumentNullException(nameof(ring));
            if (ring.Length == 0)
                throw new InvalidOperationException("Sequence contains no elements");

            return ring[0] != ring[ring.Length - 1] ? ring.Length + 1 : ring.Length;
        }

        /// <summary>
        /// Point <paramref name="i"/> of the virtually closed ring with <paramref name="shift"/> subtracted.
        /// </summary>
        internal static Vector2 ShiftedAt(Vector2[] ring, int i, in Vector2 shift)
        {
            Vector2 p = i < ring.Length ? ring[i] : ring[0];
            return new Vector2(p.X - shift.X, p.Y - shift.Y);
        }

        /// <summary>
        /// Mean of the first <paramref name="count"/> ring points after subtracting <paramref name="shift"/>.
        /// When the first and last shifted points coincide the closing duplicate is removed from the sum but still
        /// counted in the divisor, exactly as <c>PrimitiveExtensions.Average</c> does.
        /// </summary>
        internal static Vector2 ShiftedAverage(Vector2[] ring, int count, in Vector2 shift)
        {
            double mX = 0;
            double mY = 0;

            for (int i = 0; i < count; i++)
            {
                Vector2 p = ShiftedAt(ring, i, shift);
                mX += p.X;
                mY += p.Y;
            }

            Vector2 first = ShiftedAt(ring, 0, shift);
            Vector2 last = ShiftedAt(ring, count - 1, shift);
            if (first == last)
            {
                mX -= first.X;
                mY -= first.Y;
            }

            return new Vector2(mX / (double)count, mY / (double)count);
        }

        /// <summary>
        /// Shoelace sum divided by two over the first <paramref name="count"/> ring points, each shifted by
        /// <paramref name="shift"/> and then by <paramref name="origin"/>. Positive for counter-clockwise rings.
        /// </summary>
        internal static double ShiftedArea(Vector2[] ring, int count, in Vector2 shift, in Vector2 origin)
        {
            double accumulator = 0;

            Vector2 p0 = ShiftedAt(ring, 0, shift);
            double x0 = p0.X - origin.X;
            double y0 = p0.Y - origin.Y;

            for (int i = 0; i < count - 1; i++)
            {
                Vector2 p1 = ShiftedAt(ring, i + 1, shift);
                double x1 = p1.X - origin.X;
                double y1 = p1.Y - origin.Y;
                accumulator += (x0 * y1) - (x1 * y0);
                x0 = x1;
                y0 = y1;
            }

            return accumulator / 2.0;
        }
    }
}
