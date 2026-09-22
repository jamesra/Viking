using Geometry;

namespace connectomes.utah.edu.XSD.BookmarkSchemaV2.xsd
{

    partial class Point2D
    {

        public Point2D(Vector2 p)
        {
            this.X = p.X;
            this.Y = p.Y;
        }

        public Point2D(double X, double Y)
        {
            this.X = X;
            this.Y = Y;
        }

        public Vector2 ToVector2() => new Vector2(this.X, this.Y);
    }
}
