using System;
using System.Collections.Generic;

namespace Geometry
{
    /// <summary>
    /// Maximum inscribed circle of a polygon with holes (its pole of inaccessibility), and the general search behind it:
    /// the point that maximises any 1-Lipschitz function over a rectangle.
    /// </summary>
    /// <remarks>
    /// Branch and bound over square cells in a priority queue ordered by the best value a cell could hold, as in Mapbox
    /// polylabel (Agafonkin 2016) after Garcia-Castellanos and Lombardo, "Poles of inaccessibility: a calculation algorithm
    /// for the remotest places on Earth," Scottish Geographical Journal 123(3):227–233 (2007). A cell of half-size h
    /// centred at c can hold no more than f(c) + h√2 because f changes by at most the distance moved.
    /// </remarks>
    public static class PoleOfInaccessibility
    {
        /// <summary>Upper bound on cells examined, so a degenerate input cannot stall a background view build.</summary>
        private const int MaxCells = 100000;

        /// <summary>
        /// The largest circle inside <paramref name="polygon"/>, clear of its holes. The radius is within
        /// <paramref name="tolerance"/> of the true maximum; 0 or less uses a thousandth of the bounding-box diagonal.
        /// </summary>
        public static Circle MaximumInscribedCircle(this Polygon polygon, double tolerance = 0)
        {
            if (polygon is null)
                throw new ArgumentNullException(nameof(polygon));

            RingSegmentSet rings = new(polygon);
            Rectangle bounds = polygon.BoundingBox;
            if (tolerance <= 0)
                tolerance = DefaultTolerance(bounds);

            Vector2 center = Maximize(bounds, rings.SignedDistance, tolerance, polygon.Centroid, out double best);
            return new Circle(center, Math.Max(best, 0));
        }

        internal static double DefaultTolerance(Rectangle bounds) =>
            Math.Max(Math.Sqrt((bounds.Width * bounds.Width) + (bounds.Height * bounds.Height)) / 1000.0, 1e-9);

        /// <summary>
        /// The point in <paramref name="bounds"/> where the 1-Lipschitz <paramref name="f"/> is largest, to within
        /// <paramref name="tolerance"/>. <paramref name="seed"/> is evaluated first as a starting guess.
        /// </summary>
        internal static Vector2 Maximize(Rectangle bounds, Func<double, double, double> f, double tolerance, Vector2 seed, out double best)
        {
            double cellSize = Math.Min(bounds.Width, bounds.Height);
            Vector2 boundsCenter = bounds.Center;

            if (cellSize <= 0)
            {
                best = f(boundsCenter.X, boundsCenter.Y);
                return boundsCenter;
            }

            double h = cellSize / 2.0;
            MaxHeap<Cell> queue = new();
            for (double x = bounds.Left; x < bounds.Right; x += cellSize)
            {
                for (double y = bounds.Bottom; y < bounds.Top; y += cellSize)
                {
                    Cell cell = new(x + h, y + h, h, f);
                    queue.Push(cell.Max, cell);
                }
            }

            Cell bestCell = new(seed.X, seed.Y, 0, f);
            Cell centerCell = new(boundsCenter.X, boundsCenter.Y, 0, f);
            if (centerCell.D > bestCell.D)
                bestCell = centerCell;

            int examined = 0;
            while (queue.Count > 0 && examined < MaxCells)
            {
                Cell cell = queue.Pop();
                examined++;

                if (cell.D > bestCell.D)
                    bestCell = cell;

                if (cell.Max - bestCell.D <= tolerance)
                    continue;

                double half = cell.H / 2.0;
                foreach ((double dx, double dy) in Quadrants)
                {
                    Cell child = new(cell.X + (dx * half), cell.Y + (dy * half), half, f);
                    queue.Push(child.Max, child);
                }
            }

            best = bestCell.D;
            return new Vector2(bestCell.X, bestCell.Y);
        }

        private static readonly (double, double)[] Quadrants = [(-1, -1), (1, -1), (-1, 1), (1, 1)];

        private readonly struct Cell
        {
            public readonly double X, Y, H, D, Max;

            public Cell(double x, double y, double h, Func<double, double, double> f)
            {
                X = x;
                Y = y;
                H = h;
                D = f(x, y);
                Max = D + (h * Math.Sqrt(2));
            }
        }
    }

    /// <summary>Binary max-heap keyed by a double; netstandard2.0 has no PriorityQueue.</summary>
    internal sealed class MaxHeap<T>
    {
        private readonly List<(double Key, T Item)> Items = [];

        public int Count => Items.Count;

        public void Push(double key, T item)
        {
            Items.Add((key, item));
            int i = Items.Count - 1;
            while (i > 0)
            {
                int parent = (i - 1) / 2;
                if (Items[parent].Key >= Items[i].Key)
                    break;

                (Items[parent], Items[i]) = (Items[i], Items[parent]);
                i = parent;
            }
        }

        public T Pop()
        {
            T top = Items[0].Item;
            int last = Items.Count - 1;
            Items[0] = Items[last];
            Items.RemoveAt(last);

            int i = 0;
            while (true)
            {
                int left = (2 * i) + 1;
                int right = left + 1;
                int largest = i;
                if (left < Items.Count && Items[left].Key > Items[largest].Key)
                    largest = left;
                if (right < Items.Count && Items[right].Key > Items[largest].Key)
                    largest = right;
                if (largest == i)
                    break;

                (Items[largest], Items[i]) = (Items[i], Items[largest]);
                i = largest;
            }

            return top;
        }
    }
}
