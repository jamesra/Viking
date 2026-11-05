namespace Viking.SegmentationPrompts.Geometry
{
    /// <summary>A mosaic position, X right, Y up, in full-resolution pixels.</summary>
    internal readonly struct Point
    {
        public readonly double X;
        public readonly double Y;

        public Point(double x, double y)
        {
            X = x;
            Y = y;
        }

        public override string ToString() => $"({X}, {Y})";
    }
}
