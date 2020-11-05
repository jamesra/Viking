using Geometry;

namespace connectomes.utah.edu.XSD.BookmarkSchema.xsd
{
    partial class Position
    {
        public Position(GridVector3 p)
        {
            this.X = p.X;
            this.Y = p.Y;
            this.Z = p.Z;
        }

        public Position(GridVector2 p, double Z)
        {
            this.X = p.X;
            this.Y = p.Y;
            this.Z = Z;
        }

        public GridVector2 ToGridVector2()
        {
            return new GridVector2(this.X, this.Y);
        }
    }

}
