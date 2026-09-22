using System;
using System.Collections.Generic;

namespace Geometry
{
    /// <summary>
    /// Traces closed half-pixel contours around a binary raster.
    /// Used by auto-polygonize to turn a segmentation mask into rings.
    /// </summary>
    public static class MarchingSquares
    {
        private readonly struct Segment(Vector2 a, Vector2 b)
        {
            public Vector2 A { get; } = a;
            public Vector2 B { get; } = b;
        }

        /// <summary>
        /// Closed rings in half-pixel coordinates. Vertices sit on cell edges so adjacent pixels share endpoints.
        /// </summary>
        public static IReadOnlyList<Vector2[]> FindContours(bool[] mask, int width, int height)
        {
            if (mask is null)
                throw new ArgumentNullException(nameof(mask));
            if (width <= 0 || height <= 0 || mask.Length != width * height)
                throw new ArgumentException("Mask dimensions do not match its data.", nameof(mask));

            List<Segment> segments = [];
            for (int y = -1; y < height; y++)
            {
                for (int x = -1; x < width; x++)
                {
                    int cell = (IsSet(mask, width, height, x, y) ? 1 : 0)
                             | (IsSet(mask, width, height, x + 1, y) ? 2 : 0)
                             | (IsSet(mask, width, height, x + 1, y + 1) ? 4 : 0)
                             | (IsSet(mask, width, height, x, y + 1) ? 8 : 0);

                    AddCellSegments(segments, cell, x, y);
                }
            }

            return JoinSegments(segments);
        }

        private static bool IsSet(bool[] mask, int width, int height, int x, int y) =>
            x >= 0 && x < width && y >= 0 && y < height && mask[(y * width) + x];

        private static void AddCellSegments(List<Segment> segments, int cell, int x, int y)
        {
            Vector2 top = new(x + 0.5, y);
            Vector2 right = new(x + 1, y + 0.5);
            Vector2 bottom = new(x + 0.5, y + 1);
            Vector2 left = new(x, y + 0.5);

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
