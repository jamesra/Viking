using Geometry;

namespace connectomes.utah.edu.XSD.BookmarkSchema.xsd
{
    partial class Position
    {
        public Position(Vector3 p)
        {
            this.X = p.X;
            this.Y = p.Y;
            this.Z = p.Z;
        }

        public Position(Vector2 p, double Z)
        {
            this.X = p.X;
            this.Y = p.Y;
            this.Z = Z;
        }

        public Vector2 ToVector2() => new Vector2(this.X, this.Y);
    }

}
