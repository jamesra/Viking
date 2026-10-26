using System;
using System.Collections.Generic;

namespace Geometry
{
    /// <summary>
    /// A rectangle inside a polygon at one of the angles <see cref="InscribedRuns"/> tries: a candidate place for a label
    /// that follows a long process. Found on a raster, so <see cref="TryVerify"/> checks it against the real outline
    /// before it is used.
    /// </summary>
    public sealed class RunCandidate
    {
        private readonly RingSegmentSet Rings;
        private readonly Vector2 Origin;

        /// <summary>Bounds in the frame rotated by −<see cref="Angle"/> about the polygon's bounding-box centre.</summary>
        private readonly double MinX, MinY, MaxX, MaxY;

        private bool Verified;
        private OrientedRectangle? VerifiedRegion;

        /// <summary>Angle of the run's long axis in radians, in (−π/2, π/2].</summary>
        public readonly double Angle;

        /// <summary>Length along the axis, from the raster.</summary>
        public double Width => MaxX - MinX;

        /// <summary>Thickness across the axis, from the raster.</summary>
        public double Height => MaxY - MinY;

        internal RunCandidate(RingSegmentSet rings, Vector2 origin, double angle, double minX, double minY, double maxX, double maxY)
        {
            Rings = rings;
            Origin = origin;
            Angle = angle;
            MinX = minX;
            MinY = minY;
            MaxX = maxX;
            MaxY = maxY;
        }

        /// <summary>
        /// The run as checked against the polygon's segments: no edge crosses it and its centre is inside. A raster run
        /// that fails is shrunk by 10% about its centre up to four times. False if it still fails. The result is cached.
        /// </summary>
        public bool TryVerify(out OrientedRectangle region)
        {
            if (!Verified)
            {
                VerifiedRegion = Verify();
                Verified = true;
            }

            region = VerifiedRegion.GetValueOrDefault();
            return VerifiedRegion.HasValue;
        }

        private OrientedRectangle? Verify()
        {
            double cx = (MinX + MaxX) / 2.0;
            double cy = (MinY + MaxY) / 2.0;
            if (!Rings.Contains(cx, cy))
                return null;

            double halfW = Width / 2.0;
            double halfH = Height / 2.0;
            for (int attempt = 0; attempt <= 4; attempt++)
            {
                if (!Rings.CrossesRectangle(cx - halfW, cy - halfH, cx + halfW, cy + halfH))
                {
                    double cos = Math.Cos(Angle), sin = Math.Sin(Angle);
                    Vector2 center = new(Origin.X + (cx * cos) - (cy * sin), Origin.Y + (cx * sin) + (cy * cos));
                    return new OrientedRectangle(center, halfW * 2.0, halfH * 2.0, Angle);
                }

                halfW *= 0.9;
                halfH *= 0.9;
            }

            return null;
        }

        public override string ToString() => $"W: {Width:F2} H: {Height:F2} Angle: {Angle * 180.0 / Math.PI:F1}°";
    }

    /// <summary>
    /// Largest rectangles that fit inside a polygon at a fixed set of angles, so a label can lie along a long thin process
    /// instead of shrinking into the process's small inscribed circle.
    /// </summary>
    /// <remarks>
    /// For each angle the polygon is rotated so the angle is +X, rasterised to a 128×128 grid stretched to its bounds, and
    /// the maximal all-inside rectangles are found row by row with the histogram-and-stack method. Only the Pareto front of
    /// (width, height) is kept, so any text aspect can be fitted later without rasterising again.
    /// </remarks>
    public static class InscribedRuns
    {
        /// <summary>Raster cells along each axis of the rotated bounds.</summary>
        public const int GridSize = 128;

        /// <summary>Angles tried besides the principal axis: −75° to 90° in 15° steps.</summary>
        private static readonly double[] StepAngles = CreateStepAngles();

        private static double[] CreateStepAngles()
        {
            double[] angles = new double[12];
            for (int i = 0; i < angles.Length; i++)
                angles[i] = (-75 + (15 * i)) * Math.PI / 180.0;

            return angles;
        }

        /// <summary>
        /// Pareto-optimal raster runs of <paramref name="polygon"/> at every 15° from −75° to 90°, and along its principal
        /// axis, normalised to (−90°, 90°].
        /// </summary>
        public static IReadOnlyList<RunCandidate> Find(Polygon polygon)
        {
            if (polygon is null)
                throw new ArgumentNullException(nameof(polygon));

            List<double> angles = [.. StepAngles];
            double principal = NormalizeAngle(PrincipalAxisAngle(polygon));
            if (!angles.Exists(a => Math.Abs(a - principal) < Math.PI / 180.0))
                angles.Add(principal);

            Vector2 origin = polygon.BoundingBox.Center;
            List<RunCandidate> candidates = [];
            foreach (double angle in angles)
                candidates.AddRange(FindAtAngle(polygon, origin, angle));

            return candidates;
        }

        /// <summary>Maps an angle in radians to (−π/2, π/2], the range text reads in without turning upside down.</summary>
        public static double NormalizeAngle(double angle)
        {
            angle %= Math.PI;
            if (angle <= -Math.PI / 2.0)
                angle += Math.PI;
            else if (angle > Math.PI / 2.0)
                angle -= Math.PI;

            return angle;
        }

        /// <summary>
        /// Direction of the major principal axis of the polygon's area, holes subtracted, from its second moments of area.
        /// </summary>
        public static double PrincipalAxisAngle(Polygon polygon)
        {
            Vector2 origin = polygon.BoundingBox.Center;
            double a = 0, sx = 0, sy = 0, sxx = 0, syy = 0, sxy = 0;
            AccumulateMoments(polygon.ExteriorRing, origin, 1, ref a, ref sx, ref sy, ref sxx, ref syy, ref sxy);
            foreach (Polygon hole in polygon.InteriorPolygons)
                AccumulateMoments(hole.ExteriorRing, origin, -1, ref a, ref sx, ref sy, ref sxx, ref syy, ref sxy);

            if (a <= 0)
                return 0;

            double cx = sx / a, cy = sy / a;
            double cxx = sxx - (a * cx * cx);
            double cyy = syy - (a * cy * cy);
            double cxy = sxy - (a * cx * cy);
            return 0.5 * Math.Atan2(2 * cxy, cxx - cyy);
        }

        /// <summary>
        /// Adds a ring's area, first moments and second moments (∫x², ∫y², ∫xy) about <paramref name="origin"/>, counted
        /// as positive area whatever the ring's winding, times <paramref name="sign"/>.
        /// </summary>
        private static void AccumulateMoments(Vector2[] ring, Vector2 origin, int sign,
            ref double a, ref double sx, ref double sy, ref double sxx, ref double syy, ref double sxy)
        {
            double ra = 0, rx = 0, ry = 0, rxx = 0, ryy = 0, rxy = 0;
            for (int i = 0; i < ring.Length - 1; i++)
            {
                double x0 = ring[i].X - origin.X, y0 = ring[i].Y - origin.Y;
                double x1 = ring[i + 1].X - origin.X, y1 = ring[i + 1].Y - origin.Y;
                double cross = (x0 * y1) - (x1 * y0);
                ra += cross;
                rx += (x0 + x1) * cross;
                ry += (y0 + y1) * cross;
                rxx += ((x0 * x0) + (x0 * x1) + (x1 * x1)) * cross;
                ryy += ((y0 * y0) + (y0 * y1) + (y1 * y1)) * cross;
                rxy += ((x0 * y1) + (2 * x0 * y0) + (2 * x1 * y1) + (x1 * y0)) * cross;
            }

            double orientation = ra < 0 ? -sign : sign;
            a += orientation * ra / 2.0;
            sx += orientation * rx / 6.0;
            sy += orientation * ry / 6.0;
            sxx += orientation * rxx / 12.0;
            syy += orientation * ryy / 12.0;
            sxy += orientation * rxy / 24.0;
        }

        private static List<RunCandidate> FindAtAngle(Polygon polygon, Vector2 origin, double angle)
        {
            RingSegmentSet rings = new(polygon, origin, angle);
            double cellW = (rings.MaxX - rings.MinX) / GridSize;
            double cellH = (rings.MaxY - rings.MinY) / GridSize;
            if (!(cellW > 0) || !(cellH > 0))
                return [];

            bool[,] inside = Rasterize(rings, cellW, cellH);
            return ParetoRuns(inside, cellW, cellH, rings, origin, angle);
        }

        /// <summary>
        /// Cell [row, column] is inside when the scanlines at its bottom, middle and top each cover the cell's whole X range
        /// within one inside interval. Row 0 is at the bottom.
        /// </summary>
        private static bool[,] Rasterize(RingSegmentSet rings, double cellW, double cellH)
        {
            int lines = (2 * GridSize) + 1;
            List<double>[] crossings = new List<double>[lines];
            for (int k = 0; k < lines; k++)
                crossings[k] = [];

            rings.AddScanlineCrossings(rings.MinY, cellH / 2.0, crossings);

            bool[,] covered = new bool[lines, GridSize];
            for (int k = 0; k < lines; k++)
            {
                List<double> xs = crossings[k];
                xs.Sort();
                for (int i = 0; i + 1 < xs.Count; i += 2)
                {
                    int first = Math.Max(0, (int)Math.Ceiling((xs[i] - rings.MinX) / cellW));
                    int last = Math.Min(GridSize - 1, (int)Math.Floor((xs[i + 1] - rings.MinX) / cellW) - 1);
                    for (int c = first; c <= last; c++)
                        covered[k, c] = true;
                }
            }

            bool[,] inside = new bool[GridSize, GridSize];
            for (int row = 0; row < GridSize; row++)
            {
                for (int c = 0; c < GridSize; c++)
                    inside[row, c] = covered[2 * row, c] && covered[(2 * row) + 1, c] && covered[(2 * row) + 2, c];
            }

            return inside;
        }

        /// <summary>
        /// Maximal rectangles of inside cells by the histogram-and-stack method, reduced to the widest one of each height
        /// and then to the (width, height) Pareto front.
        /// </summary>
        private static List<RunCandidate> ParetoRuns(bool[,] inside, double cellW, double cellH, RingSegmentSet rings, Vector2 origin, double angle)
        {
            // Widest rectangle seen for each height in cells
            (int Width, int Left, int Top)[] widest = new (int, int, int)[GridSize + 1];
            int[] heights = new int[GridSize];
            Stack<int> stack = new();

            for (int row = 0; row < GridSize; row++)
            {
                for (int c = 0; c < GridSize; c++)
                    heights[c] = inside[row, c] ? heights[c] + 1 : 0;

                stack.Clear();
                for (int c = 0; c <= GridSize; c++)
                {
                    int h = c < GridSize ? heights[c] : 0;
                    while (stack.Count > 0 && heights[stack.Peek()] >= h)
                    {
                        int barHeight = heights[stack.Pop()];
                        int left = stack.Count > 0 ? stack.Peek() + 1 : 0;
                        int width = c - left;
                        if (barHeight > 0 && width > widest[barHeight].Width)
                            widest[barHeight] = (width, left, row);
                    }

                    stack.Push(c);
                }
            }

            List<RunCandidate> front = [];
            int bestWidth = 0;
            for (int h = GridSize; h > 0; h--)
            {
                (int width, int left, int top) = widest[h];
                if (width <= bestWidth)
                    continue;

                bestWidth = width;
                int bottom = top - h + 1;
                front.Add(new RunCandidate(rings, origin, angle,
                    rings.MinX + (left * cellW), rings.MinY + (bottom * cellH),
                    rings.MinX + ((left + width) * cellW), rings.MinY + ((top + 1) * cellH)));
            }

            return front;
        }
    }
}
