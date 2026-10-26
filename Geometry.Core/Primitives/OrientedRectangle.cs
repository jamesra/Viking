using System;

namespace Geometry
{
    /// <summary>
    /// A rectangle rotated by <see cref="Angle"/> radians counter-clockwise about its centre. <see cref="Width"/> runs along
    /// the rotated X axis (<see cref="AxisU"/>) and <see cref="Height"/> along the rotated Y axis (<see cref="AxisV"/>).
    /// </summary>
    [Serializable]
    public readonly struct OrientedRectangle
    {
        public readonly Vector2 Center;
        public readonly double Width;
        public readonly double Height;
        public readonly double Angle;

        public OrientedRectangle(Vector2 center, double width, double height, double angle)
        {
            Center = center;
            Width = width;
            Height = height;
            Angle = angle;
        }

        /// <summary>Unit vector along <see cref="Width"/>.</summary>
        public Vector2 AxisU => new(Math.Cos(Angle), Math.Sin(Angle));

        /// <summary>Unit vector along <see cref="Height"/>, 90° counter-clockwise from <see cref="AxisU"/>.</summary>
        public Vector2 AxisV => new(-Math.Sin(Angle), Math.Cos(Angle));

        public double Area => Width * Height;

        /// <summary>World position of the point (<paramref name="u"/>, <paramref name="v"/>) in this rectangle's frame, origin at the centre.</summary>
        public Vector2 LocalToWorld(double u, double v) => Center + (AxisU * u) + (AxisV * v);

        /// <summary>Position of <paramref name="p"/> in this rectangle's frame, origin at the centre.</summary>
        public Vector2 WorldToLocal(Vector2 p)
        {
            Vector2 d = p - Center;
            return new Vector2(Vector2.Dot(d, AxisU), Vector2.Dot(d, AxisV));
        }

        /// <summary>Corners counter-clockwise from the lower left in the rectangle's frame.</summary>
        public Vector2[] Corners
        {
            get
            {
                double u = Width / 2.0, v = Height / 2.0;
                return [LocalToWorld(-u, -v), LocalToWorld(u, -v), LocalToWorld(u, v), LocalToWorld(-u, v)];
            }
        }

        public bool Contains(Vector2 p)
        {
            Vector2 local = WorldToLocal(p);
            return Math.Abs(local.X) < Width / 2.0 && Math.Abs(local.Y) < Height / 2.0;
        }

        /// <summary>Distance from <paramref name="p"/> to the rectangle's border, negative inside.</summary>
        public double SignedDistance(Vector2 p)
        {
            Vector2 local = WorldToLocal(p);
            double dx = Math.Abs(local.X) - (Width / 2.0);
            double dy = Math.Abs(local.Y) - (Height / 2.0);
            if (dx <= 0 && dy <= 0)
                return Math.Max(dx, dy);

            double ox = Math.Max(dx, 0), oy = Math.Max(dy, 0);
            return Math.Sqrt((ox * ox) + (oy * oy));
        }

        public Polygon ToPolygon()
        {
            Vector2[] corners = Corners;
            return new Polygon([.. corners, corners[0]]);
        }

        public override string ToString() => $"{Center} W: {Width:F2} H: {Height:F2} Angle: {Angle * 180.0 / Math.PI:F1}°";
    }
}
