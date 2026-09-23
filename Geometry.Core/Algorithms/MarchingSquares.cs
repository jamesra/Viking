using System;
using System.Collections.Generic;

namespace Geometry
{
    /// <summary>
    /// Traces closed iso-contours on a raster whose samples sit on integer pixel coordinates.
    /// Binary masks are the 0/1 field at iso 0.5, which places vertices on pixel-edge midpoints.
    /// Soft fields (SAM2 probabilities) place each vertex where the edge crosses the iso level.
    /// </summary>
    public static class MarchingSquares
    {
        private readonly struct Segment(Vector2 a, Vector2 b)
        {
            public Vector2 A { get; } = a;
            public Vector2 B { get; } = b;
        }

        /// <summary>
        /// Closed rings for a binary mask. Equivalent to <see cref="FindContours(float[], int, int, float)"/>
        /// with true = 1, false = 0, and iso level 0.5.
        /// </summary>
        public static IReadOnlyList<Vector2[]> FindContours(bool[] mask, int width, int height)
        {
            if (mask is null)
                throw new ArgumentNullException(nameof(mask));
            if (width <= 0 || height <= 0 || mask.Length != width * height)
                throw new ArgumentException("Mask dimensions do not match its data.", nameof(mask));

            float[] field = new float[mask.Length];
            for (int i = 0; i < mask.Length; i++)
                field[i] = mask[i] ? 1f : 0f;

            return FindContours(field, width, height, 0.5f);
        }

        /// <summary>
        /// Closed rings where each vertex is the linear crossing of <paramref name="isoLevel"/> on a cell edge.
        /// Samples outside the array are 0, so a probability field should use an iso level above 0.
        /// Shared edges are interpolated in a fixed direction so adjacent cells meet at one vertex.
        /// </summary>
        public static IReadOnlyList<Vector2[]> FindContours(float[] field, int width, int height, float isoLevel)
        {
            if (field is null)
                throw new ArgumentNullException(nameof(field));
            if (width <= 0 || height <= 0 || field.Length != width * height)
                throw new ArgumentException("Field dimensions do not match its data.", nameof(field));

            List<Segment> segments = [];
            for (int y = -1; y < height; y++)
            {
                for (int x = -1; x < width; x++)
                {
                    float v00 = Sample(field, width, height, x, y);
                    float v10 = Sample(field, width, height, x + 1, y);
                    float v11 = Sample(field, width, height, x + 1, y + 1);
                    float v01 = Sample(field, width, height, x, y + 1);
                    int cell = (Inside(v00, isoLevel) ? 1 : 0)
                             | (Inside(v10, isoLevel) ? 2 : 0)
                             | (Inside(v11, isoLevel) ? 4 : 0)
                             | (Inside(v01, isoLevel) ? 8 : 0);

                    AddCellSegments(segments, cell, x, y, v00, v10, v11, v01, isoLevel);
                }
            }

            return JoinSegments(segments);
        }

        private static float Sample(float[] field, int width, int height, int x, int y) =>
            x >= 0 && x < width && y >= 0 && y < height ? field[(y * width) + x] : 0f;

        private static bool Inside(float value, float isoLevel) => value >= isoLevel;

        private static void AddCellSegments(
            List<Segment> segments,
            int cell,
            int x,
            int y,
            float v00,
            float v10,
            float v11,
            float v01,
            float isoLevel)
        {
            Vector2 top = Interpolate(x, y, v00, x + 1, y, v10, isoLevel);
            Vector2 right = Interpolate(x + 1, y, v10, x + 1, y + 1, v11, isoLevel);
            Vector2 bottom = Interpolate(x, y + 1, v01, x + 1, y + 1, v11, isoLevel);
            Vector2 left = Interpolate(x, y, v00, x, y + 1, v01, isoLevel);

            switch (cell)
            {
                case 0:
                case 15:
                    return;
                case 1: Add(segments, left, top); return;
                case 2: Add(segments, top, right); return;
                case 3: Add(segments, left, right); return;
                case 4: Add(segments, right, bottom); return;
                case 5:
                    Add(segments, left, bottom);
                    Add(segments, top, right);
                    return;
                case 6: Add(segments, top, bottom); return;
                case 7: Add(segments, left, bottom); return;
                case 8: Add(segments, bottom, left); return;
                case 9: Add(segments, bottom, top); return;
                case 10:
                    Add(segments, top, left);
                    Add(segments, right, bottom);
                    return;
                case 11: Add(segments, bottom, right); return;
                case 12: Add(segments, right, left); return;
                case 13: Add(segments, right, top); return;
                case 14: Add(segments, top, left); return;
            }
        }

        /// <summary>
        /// Linear iso crossing. Endpoints are ordered so both cells that share the edge
        /// produce the same rounded coordinate.
        /// </summary>
        private static Vector2 Interpolate(
            int ax,
            int ay,
            float av,
            int bx,
            int by,
            float bv,
            float isoLevel)
        {
            if (bx < ax || (bx == ax && by < ay))
                return Interpolate(bx, by, bv, ax, ay, av, isoLevel);

            float a = av == isoLevel ? isoLevel + 1e-4f : av;
            float b = bv == isoLevel ? isoLevel + 1e-4f : bv;
            double denom = b - a;
            double t = Math.Abs(denom) < 1e-20 ? 0.5 : (isoLevel - a) / denom;
            if (t < 0)
                t = 0;
            else if (t > 1)
                t = 1;

            double x = ax + ((bx - ax) * t);
            double y = ay + ((by - ay) * t);
            return new Vector2(Tolerance.Round(x), Tolerance.Round(y));
        }

        private static void Add(List<Segment> segments, Vector2 a, Vector2 b) =>
            segments.Add(new Segment(a, b));

        private static IReadOnlyList<Vector2[]> JoinSegments(IReadOnlyList<Segment> segments)
        {
            Dictionary<Vector2, List<int>> adjacency = [];
            for (int i = 0; i < segments.Count; i++)
            {
                AddAdjacent(adjacency, segments[i].A, i);
                AddAdjacent(adjacency, segments[i].B, i);
            }

            bool[] used = new bool[segments.Count];
            List<Vector2[]> contours = [];
            for (int i = 0; i < segments.Count; i++)
            {
                if (used[i])
                    continue;

                List<Vector2> contour = [segments[i].A, segments[i].B];
                used[i] = true;
                Vector2 current = segments[i].B;

                while (current != contour[0])
                {
                    int nextIndex = -1;
                    foreach (int index in adjacency[current])
                    {
                        if (!used[index])
                        {
                            nextIndex = index;
                            break;
                        }
                    }
                    if (nextIndex < 0)
                        break;

                    used[nextIndex] = true;
                    Segment next = segments[nextIndex];
                    current = next.A == current ? next.B : next.A;
                    contour.Add(current);
                }

                if (contour.Count >= 4 && contour[0] == contour[contour.Count - 1])
                    contours.Add([.. contour]);
            }

            return contours;
        }

        private static void AddAdjacent(Dictionary<Vector2, List<int>> adjacency, Vector2 point, int segmentIndex)
        {
            if (!adjacency.TryGetValue(point, out List<int> indices))
            {
                indices = [];
                adjacency.Add(point, indices);
            }

            indices.Add(segmentIndex);
        }
    }
}
