using Geometry;
using System;
using System.Collections.Generic;

namespace WebAnnotation.UI.Commands.Segmentation
{
    /// <summary>
    /// The largest axis-aligned rectangle that lies inside a set of polygons.
    /// </summary>
    /// <remarks>
    /// Used for the box of a group prompt (several overlapping proposals re-segmented together). The server takes a
    /// client's box as inside the object, so the box has to be inside the polygons it came from. A square sized from a
    /// polygon's bounding box is only inside when the polygon is round; for a bent or elongated polygon it covers
    /// background, and the server then marks all of that as object.
    /// </remarks>
    internal static class InscribedRectangle
    {
        /// <summary>
        /// Cells along the longer side of the union's bounding box. The answer is accurate to one cell, so a
        /// polygon 1,500 pixels across is found to about 10 pixels, and the result is shrunk by that much.
        /// </summary>
        public const int GridSide = 160;

        /// <summary>
        /// The largest rectangle (by area) whose every sample lies inside at least one of <paramref name="polygons"/>,
        /// or null when there is none, for example when every polygon is thinner than a grid cell.
        /// </summary>
        /// <remarks>
        /// Scans a <see cref="GridSide"/> grid over the union's bounding box, marks each cell whose centre is inside
        /// a polygon, and finds the largest all-inside rectangle of cells with the histogram method in one pass over
        /// the rows. The result is then pulled in by one cell on every side, because only cell centres were tested and
        /// a polygon edge can cut a cell. The rectangle may span several polygons that overlap or touch.
        /// </remarks>
        /// <param name="polygons">Polygons in one coordinate space (world Y up). Null entries are ignored.</param>
        public static Rectangle? Largest(IReadOnlyList<Polygon> polygons)
        {
            if (polygons is null)
                return null;

            List<Polygon> usable = [];
            Rectangle? bounds = null;
            foreach (Polygon polygon in polygons)
            {
                if (polygon is null)
                    continue;

                usable.Add(polygon);
                bounds = bounds is null ? polygon.BoundingBox : Rectangle.Union(bounds.Value, polygon.BoundingBox);
            }

            if (usable.Count == 0 || bounds is null)
                return null;

            double width = bounds.Value.Width;
            double height = bounds.Value.Height;
            double cell = Math.Max(width, height) / GridSide;
            if (!(cell > 0))
                return null;

            int columns = Math.Max(1, (int)Math.Ceiling(width / cell));
            int rows = Math.Max(1, (int)Math.Ceiling(height / cell));
            bool[,] inside = new bool[rows, columns];
            for (int row = 0; row < rows; row++)
            {
                double y = bounds.Value.Bottom + ((row + 0.5) * cell);
                for (int column = 0; column < columns; column++)
                {
                    Vector2 centre = new(bounds.Value.Left + ((column + 0.5) * cell), y);
                    foreach (Polygon polygon in usable)
                    {
                        if (polygon.Contains(centre))
                        {
                            inside[row, column] = true;
                            break;
                        }
                    }
                }
            }

            if (!TryFindLargest(inside, out int firstRow, out int lastRow, out int firstColumn, out int lastColumn))
                return null;

            double left = bounds.Value.Left + ((firstColumn + 1) * cell);
            double right = bounds.Value.Left + (lastColumn * cell);
            double bottom = bounds.Value.Bottom + ((firstRow + 1) * cell);
            double top = bounds.Value.Bottom + (lastRow * cell);
            if (!(right > left) || !(top > bottom))
                return null;

            return new Rectangle(new Vector2(left, bottom), new Vector2(right, top));
        }

        /// <summary>
        /// Largest all-true rectangle of a boolean grid, as inclusive row and column ranges.
        /// </summary>
        /// <remarks>
        /// For each row, <c>heights[c]</c> is how many consecutive true cells end at that row in column <c>c</c>.
        /// A row of heights is a histogram, and the largest rectangle under it comes from a monotone stack, so the
        /// whole grid costs one pass. Returns false when no cell is true.
        /// </remarks>
        internal static bool TryFindLargest(
            bool[,] inside,
            out int firstRow,
            out int lastRow,
            out int firstColumn,
            out int lastColumn)
        {
            int rows = inside.GetLength(0);
            int columns = inside.GetLength(1);
            int[] heights = new int[columns];
            long bestArea = 0;
            firstRow = lastRow = firstColumn = lastColumn = 0;

            // Column indices whose heights are increasing from the bottom of the stack to the top.
            Stack<int> stack = new();
            for (int row = 0; row < rows; row++)
            {
                for (int column = 0; column < columns; column++)
                    heights[column] = inside[row, column] ? heights[column] + 1 : 0;

                stack.Clear();
                for (int column = 0; column <= columns; column++)
                {
                    int current = column == columns ? 0 : heights[column];
                    while (stack.Count > 0 && heights[stack.Peek()] >= current)
                    {
                        int rectangleHeight = heights[stack.Pop()];
                        int left = stack.Count == 0 ? 0 : stack.Peek() + 1;
                        int rectangleWidth = column - left;
                        long area = (long)rectangleHeight * rectangleWidth;
                        if (area > bestArea)
                        {
                            bestArea = area;
                            firstRow = row - rectangleHeight + 1;
                            lastRow = row;
                            firstColumn = left;
                            lastColumn = column - 1;
                        }
                    }

                    if (column < columns)
                        stack.Push(column);
                }
            }

            return bestArea > 0;
        }
    }
}
