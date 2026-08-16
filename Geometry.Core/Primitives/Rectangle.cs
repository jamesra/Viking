using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;

namespace Geometry
{
    /// <summary>
    /// Double-precision axis-aligned rectangle. Distinct from <c>System.Drawing.Rectangle</c> (integer) and not a drop-in replacement.
    /// </summary>
    [Serializable]
    public readonly struct Rectangle : IRectangle2D, IHasControlPoints, ICloneable, IEquatable<IRectangle2D>, IEquatable<Rectangle>
    {
        private enum Corner
        {
            LowerLeft = 0,
            UpperLeft = 1,
            UpperRight = 2,
            LowerRight = 3
        }

        public readonly double Left;
        public readonly double Right;

        /// <summary>
        /// Top has a larger value than bottom
        /// </summary>
        public readonly double Top;

        /// <summary>
        /// Bottom has a smaller value than top
        /// </summary>
        public readonly double Bottom;

        IPoint2D IRectangle2D.Center => Center;

        public override string ToString() => $"{Left},{Bottom} W: {Width} H: {Height} Center:{Center}";

        public double Width => Right - Left;

        public double Height => Top - Bottom;

        public Vector2 Center => new(LowerLeft.X + (Width / 2.0), LowerLeft.Y + (Height / 2.0));

        public Vector2 LowerLeft => new(Left, Bottom);

        public Vector2 UpperLeft => new(Left, Top);

        public Vector2 LowerRight => new(Right, Bottom);

        public Vector2 UpperRight => new(Right, Top);

        public double Area => Width * Height;

        public LineSegment LeftEdge => new(LowerLeft, UpperLeft);

        public LineSegment RightEdge => new(LowerRight, UpperRight);

        public LineSegment TopEdge => new(UpperLeft, UpperRight);

        public LineSegment BottomEdge => new(LowerLeft, LowerRight);

        public LineSegment[] Edges => [TopEdge, BottomEdge, LeftEdge, RightEdge];

        public LineSegment[] Segments => CalculateSegments(Left, Right, Bottom, Top);

        public Vector2[] Corners => CalculateCorners(Left, Bottom, Right, Top);

        public Rectangle BoundingBox => this;

        public ShapeType2D ShapeType => ShapeType2D.Rectangle;

        IReadOnlyList<IPoint2D> IHasControlPoints.ControlPoints => [LowerLeft, UpperLeft, UpperRight, LowerRight];

        double IRectangle2D.Left => Left;

        double IRectangle2D.Right => Right;

        double IRectangle2D.Top => Top;

        double IRectangle2D.Bottom => Bottom;

        public Rectangle(in Vector2 corner, in Vector2 oppositeCorner)
        {
            Vector2 RectOrigin = new(Math.Min(corner.X, oppositeCorner.X), Math.Min(corner.Y, oppositeCorner.Y));
            double width = Math.Abs(corner.X - oppositeCorner.X);
            double height = Math.Abs(corner.Y - oppositeCorner.Y);
            if (width == 0 || height == 0)
            {
                throw new ArgumentException("Grid Rectangle must have non-negative width and height");
            }

            Left = RectOrigin.X;
            Bottom = RectOrigin.Y;
            Top = RectOrigin.Y + height;
            Right = RectOrigin.X + width;

            _HashCode = CalcHashCode(Left, Bottom, Right, Top);
        }

        /// <summary>
        /// 
        /// </summary>
        /// <param name="borders">[MinX, MaxX, MinY, MaxY]</param>
        public Rectangle(in double[] borders)
        {
            Left = borders[0];
            Right = borders[1];
            Bottom = borders[2];
            Top = borders[3];

            _HashCode = CalcHashCode(Left, Bottom, Right, Top);

            if (!double.IsNaN(Left))
            {
                Debug.Assert(Left <= Right && Bottom <= Top, "Grid Rectangle argument error");
                if (Left > Right || Bottom > Top)
                {
                    throw new ArgumentException("Grid Rectangle must have non-negative width and height");
                }
            }
        }

        public Rectangle(in double left, in double right, in double bottom, in double top)
        {
            Left = left;
            Bottom = bottom;
            Top = top;
            Right = right;

            _HashCode = CalcHashCode(Left, Bottom, Right, Top);

            if (!double.IsNaN(Left))
            {
                Debug.Assert(Left <= Right && Bottom <= Top, "Grid Rectangle argument error");
                if (Left > Right || Bottom > Top)
                {
                    throw new ArgumentException("Grid Rectangle must have non-negative width and height");
                }
            }
        }

        public Rectangle(in Vector2 position, double width, double height)
        {
            Left = position.X;
            Bottom = position.Y;
            Top = Bottom + height;
            Right = Left + width;

            _HashCode = CalcHashCode(Left, Bottom, Right, Top);

            Debug.Assert(Left <= Right && Bottom <= Top, "Grid Rectangle argument error");
            if (Left > Right || Bottom > Top)
            {
                throw new ArgumentException("Grid Rectangle must have non-negative width and height");
            }
        }

        public Rectangle(in Vector2 position, in double radius)
        {
            Left = position.X - radius;
            Bottom = position.Y - radius;
            Top = position.Y + radius;
            Right = position.X + radius;

            _HashCode = CalcHashCode(Left, Bottom, Right, Top);

            Debug.Assert(Left <= Right && Bottom <= Top, "Grid Rectangle argument error");
        }

        public Rectangle(in IPoint2D position, in double width, in double height)
        {
            if (position is null)
                throw new ArgumentNullException(nameof(position));

            Left = position.X;
            Bottom = position.Y;
            Top = Bottom + height;
            Right = Left + width;

            _HashCode = CalcHashCode(Left, Bottom, Right, Top);

            Debug.Assert(Left <= Right && Bottom <= Top, "Grid Rectable argument error");
            if (Left > Right || Bottom > Top)
            {
                throw new ArgumentException("Grid Rectangle must have non-negative width and height");
            }
        }

        public Rectangle(in IPoint2D position, in double radius)
        {
            if (position is null)
                throw new ArgumentNullException(nameof(position));

            Left = position.X - radius;
            Bottom = position.Y - radius;
            Top = position.Y + radius;
            Right = position.X + radius;

            _HashCode = CalcHashCode(Left, Bottom, Right, Top);

            Debug.Assert(Left <= Right && Bottom <= Top, "Grid Rectable argument error");
            if (Left > Right || Bottom > Top)
            {
                throw new ArgumentException("Grid Rectangle must have non-negative width and height");
            }
        }

        /// <summary>
        /// Returns true if the passed rectangle in inside or overlaps this rectangle
        /// </summary>
        /// <param name="rect"></param>
        /// <returns></returns>
        public bool Intersects(in Rectangle rect)
        {
            //Find out if the rectangles can't possibly intersect
            if (rect.Right < this.Left ||
               rect.Top < this.Bottom ||
               rect.Left > this.Right ||
               rect.Bottom > this.Top)
                return false;

            return true;
        }

        public ShapeRelation IntersectionType(in Rectangle rect)
        {
            //Find out if the rectangles can't possibly intersect
            if (rect.Right < this.Left ||
               rect.Top < this.Bottom ||
               rect.Left > this.Right ||
               rect.Bottom > this.Top)
                return ShapeRelation.None;

            if (rect.Right > this.Left &&
               rect.Top > this.Bottom &&
               rect.Left < this.Right &&
               rect.Bottom < this.Top)
                return ShapeRelation.Contained;

            Rectangle? intersectionArea = this.Intersection(rect);

            if (intersectionArea.Value.Area > 0)
            {
                return ShapeRelation.Intersecting;
            }

            /*

            if (rect.Right > this.Left ||
               rect.Top > this.Bottom ||
               rect.Left < this.Right ||
               rect.Bottom < this.Top)
                return ShapeRelation.Intersecting;

            if (rect.Right == this.Left ||
               rect.Top == this.Bottom ||
               rect.Left == this.Right ||
               rect.Bottom == this.Top)
               */
            return ShapeRelation.Touching;

            //throw new ArgumentException(string.Format("Unexpected rectangle intersection case {0} {1}", rect, this));
        }

        public bool Intersects(in IShape2D shape) => ShapeExtensions.RectangleIntersects(this, shape);

        public bool Intersects(in ICircle2D c) => Intersects(c.Convert());

        public bool Intersects(in Circle circle) => RectangleIntersectionExtensions.Intersects(this, circle);

        public bool Intersects(in ILineSegment2D l) => Intersects(l.Convert());

        public bool Intersects(in LineSegment line) => RectangleIntersectionExtensions.Intersects(this, line);

        public bool Intersects(in ITriangle2D t) => Intersects(t.Convert());

        public bool Intersects(in Triangle tri) => RectangleIntersectionExtensions.Intersects(this, tri);

        public bool Intersects(in IPolygon2D p) => Intersects(p.Convert());

        public bool Intersects(in Polygon poly) => RectangleIntersectionExtensions.Intersects(this, poly);

        /// <summary>
        /// Returns the region of overlap between two rectangles
        /// </summary>
        /// <param name="other"></param>
        /// <returns></returns>
        public Rectangle? Intersection(in Rectangle other)
        {
            if (false == this.Intersects(other))
                return new Rectangle?();

            double minx = Math.Max(this.Left, other.Left);
            double maxx = Math.Min(this.Right, other.Right);
            double miny = Math.Max(this.Bottom, other.Bottom);
            double maxy = Math.Min(this.Top, other.Top);

            return new Rectangle(minx, maxx, miny, maxy);
        }



        /// <summary>
        /// OGC Contains: <paramref name="rect"/> lies in this rectangle's interior (shared boundary is false).
        /// </summary>
        public bool Contains(in Rectangle rect) => GetRelation(rect).IsContains();

        public bool Covers(in Rectangle rect) => GetRelation(rect).IsCovers();

        public bool Contains(in IPoint2D pos) => GetRelation(pos).IsContains();

        public bool Covers(in IPoint2D pos) => GetRelation(pos).IsCovers();

        public ShapeRelation GetRelation(in IPoint2D pos)
        {
            if (pos is null)
                throw new ArgumentNullException(nameof(pos));

            const double eps = Tolerance.Epsilon;
            if (pos.X < Left - eps ||
                pos.Y < Bottom - eps ||
                pos.X > Right + eps ||
                pos.Y > Top + eps)
                return ShapeRelation.None;

            if (pos.X > Left + eps &&
                pos.Y > Bottom + eps &&
                pos.X < Right - eps &&
                pos.Y < Top - eps)
                return ShapeRelation.Contained;

            return ShapeRelation.Touching;
        }

        public ShapeRelation GetRelation(in ILineSegment2D line)
        {
            //This is very similar to the logic for Triangle
            ShapeRelation relA = this.GetRelation(line.A);
            ShapeRelation relB = this.GetRelation(line.B);

            ShapeRelation composite = relA | relB;

            bool containsA = relA == ShapeRelation.Contained;
            bool containsB = relB == ShapeRelation.Contained;

            if (containsA && containsB)
                return ShapeRelation.Contained;

            //Edge case where one end of the line is contained and the other is exactly on the edge
            if (composite.HasFlag(ShapeRelation.Touching | ShapeRelation.Contained))
                return ShapeRelation.Contained;

            //Edge case where the line is exactly along the edge... not sure if this should be touching or contained, if not the same edge it is contained, but if the same edge it is touching
            if (relA.HasFlag(ShapeRelation.Touching) && relB.HasFlag(ShapeRelation.Touching))
            {
                //Check if the line is touching the same segment in two places
                foreach (LineSegment e in this.Segments)
                    if (e.Intersects(line.A.Convert()) && e.Intersects(line.B.Convert()))
                        return ShapeRelation.Touching;

                return ShapeRelation.Contained;
            }

            //Check if line crosses the bounding box but both points are outside the box
            foreach (LineSegment e in this.Segments)
                if (e.Intersects(line))
                    return ShapeRelation.Intersecting;

            //OK, make sure one endpoint isn't touching and the rest of the line is outside the triangle
            if (composite.HasFlag(ShapeRelation.Touching))
                return ShapeRelation.Touching;

            return ShapeRelation.None;
        }

        public bool Covers(in Vector2 pos, in double epsilon = Tolerance.Epsilon)
        {
            if (pos.X >= Left - epsilon &&
               pos.Y >= Bottom - epsilon &&
               pos.X <= Right + epsilon &&
               pos.Y <= Top + epsilon)
                return true;

            return false;
        }

        public bool Contains(in Vector2 pos, in double epsilon) =>
            Covers(pos, epsilon) &&
            pos.X > Left + epsilon &&
            pos.Y > Bottom + epsilon &&
            pos.X < Right - epsilon &&
            pos.Y < Top - epsilon;

        public ShapeRelation GetRelation(in Rectangle rect)
        {
            //Find out if the rectangles can't possibly intersect
            if (rect.Right < this.Left ||
               rect.Top < this.Bottom ||
               rect.Left > this.Right ||
               rect.Bottom > this.Top)
                return ShapeRelation.None;

            if (rect.Right <= this.Right &&
               rect.Top <= this.Top &&
               rect.Left >= this.Left &&
               rect.Bottom >= this.Bottom)
                return ShapeRelation.Contained;

            bool LRIntersect = (this.Left < rect.Left && this.Right > rect.Left) ||
                               (this.Right > rect.Left && this.Right < rect.Right) ||
                               (this.Left > rect.Left && this.Right < rect.Right) ||
                               (this.Left > rect.Left && this.Left < rect.Right);

            bool UDIntersect = (this.Bottom < rect.Bottom && this.Top > rect.Bottom) ||
                               (this.Top > rect.Bottom && this.Top < rect.Top) ||
                               (this.Bottom > rect.Bottom && this.Top < rect.Top) ||
                               (this.Bottom > rect.Bottom && this.Bottom < rect.Top);

            if (LRIntersect && UDIntersect)
                return ShapeRelation.Intersecting;

            bool LRTouch = this.Left == rect.Right || this.Right == rect.Left;
            bool UDTouch = this.Bottom == rect.Top || this.Top == rect.Bottom;

            if ((LRTouch && UDIntersect) ||
                (UDTouch && LRIntersect) ||
                (LRTouch && UDTouch))
                return ShapeRelation.Touching;


            if (rect.Width == 0 || rect.Height == 0 || this.Width == 0 || this.Height == 0)
            {
                //If we are dealing with a zero height rectangle then check some edge cases
                if (LRIntersect || UDIntersect)
                    return ShapeRelation.Intersecting;

                if (LRTouch || UDTouch)
                    return ShapeRelation.Touching;
            }
            else
            {
                System.Diagnostics.Debug.Assert(false, "Every case should be handled at this point for a rectangle with non-zero width and height...");
            }

            return ShapeRelation.None;
        }

        private readonly int _HashCode;

        public override int GetHashCode() => _HashCode;

        private static int CalcHashCode(in double left, in double bottom, in double right, in double top) => left.GetHashCode() ^ bottom.GetHashCode() ^ right.GetHashCode() ^ top.GetHashCode();

        public override bool Equals(object obj)
        {
            if (obj is Rectangle other)
                return Equals(other);

            if (obj is IShape2D otherShape)
                return Equals(otherShape);

            return false;
        }

        public bool Equals(IShape2D other)
        {
            if (other is IRectangle2D otherRect)
                return Equals(otherRect);

            return false;
        }

        public bool Equals(IRectangle2D other)
        {
            return Left.Equals(other.Left) &&
                   Right.Equals(other.Right) &&
                   Top.Equals(other.Top) &&
                   Bottom.Equals(other.Bottom);
        }

        public bool Equals(Rectangle other)
        {
            return Left.Equals(other.Left) &&
                   Right.Equals(other.Right) &&
                   Top.Equals(other.Top) &&
                   Bottom.Equals(other.Bottom);
        }

        #region Static Methods

        public static bool operator ==(in Rectangle A, in Rectangle B)
        {
            return ((A.Left == B.Left) &&
                    (A.Right == B.Right) &&
                    (A.Top == B.Top) &&
                    (A.Bottom == B.Bottom));
        }

        public static bool operator !=(in Rectangle A, in Rectangle B) => !(A == B);

        /// <summary>
        /// Pads the border by the specified amount
        /// </summary>
        /// <param name="A"></param>
        /// <param name="scalar"></param>
        /// <returns></returns>
        public static Rectangle operator +(in Rectangle A, double scalar) => Rectangle.Scale(A, scalar);

        /// <summary>
        /// Performs a union of the rectangle and the point
        /// </summary>
        /// <param name="A"></param>
        /// <param name="scalar"></param>
        /// <returns></returns>
        public static Rectangle operator +(in Rectangle A, in Vector2 p) => Rectangle.Union(A, p);

        /// <summary>
        /// Performs a union of the rectangle and the bounding box of the shape
        /// </summary>
        /// <param name="A"></param>
        /// <param name="scalar"></param>
        /// <returns></returns>
        public static Rectangle operator +(in Rectangle A, in IShape2D shape) => Rectangle.Union(A, shape.BoundingBox);

        /// <summary>
        /// Performs a union of both rectangles and returns the bounding box of both
        /// </summary>
        /// <param name="A"></param>
        /// <param name="scalar"></param>
        /// <returns></returns>
        public static Rectangle operator +(in Rectangle A, in Rectangle B) => Rectangle.Union(A, B);

        public static Rectangle operator *(in Rectangle A, in double scalar) => Rectangle.Scale(A, scalar);

        public static Rectangle operator /(in Rectangle A, in double scalar) => Rectangle.Scale(A, 1.0 / scalar);

        /// <summary>
        /// Pad the requested amount onto the bounding box
        /// </summary>
        /// <param name="Radius"></param>
        /// <returns></returns>
        public static Rectangle Pad(in Rectangle rect, in double radius) => new Rectangle(rect.Left - radius, rect.Right + radius, rect.Bottom - radius, rect.Top + radius);

        public static Rectangle Scale(in Rectangle rect, in double scalar)
        {
            //Have to cache center because it changes as we update points
            Vector2 center = rect.Center;
            Vector2 directionA = rect.UpperRight - center;

            directionA *= scalar;

            Vector2 BottomLeft = center - directionA;
            Vector2 TopRight = center + directionA;

            var left = BottomLeft.X;
            var bottom = BottomLeft.Y;
            var right = TopRight.X;
            var top = TopRight.Y;

            Debug.Assert(left <= right && bottom <= top, "Grid Rectangle scale argument error");

            return new Rectangle(left: left, bottom: bottom,
                right: right, top: top);
        }


        /// <summary>
        /// Returns a rectangle bounding the passed rectangles
        /// </summary>
        /// <param name="A"></param>
        /// <param name="B"></param>
        /// <returns></returns>
        public static Rectangle Union(in IShape2D a, in IShape2D b) => Rectangle.Union(a.BoundingBox, b.BoundingBox);

        /// <summary>
        /// Returns a rectangle bounding the passed rectangles
        /// </summary>
        /// <param name="A"></param>
        /// <param name="B"></param>
        /// <returns></returns>
        public static Rectangle Union(in Rectangle A, in Rectangle B)
        {
            double left = A.Left < B.Left ? A.Left : B.Left;
            double right = A.Right > B.Right ? A.Right : B.Right;
            double top = A.Top > B.Top ? A.Top : B.Top;
            double bottom = A.Bottom < B.Bottom ? A.Bottom : B.Bottom;

            return new Rectangle(left, right, bottom, top);
        }

        /// <summary>
        /// Expands the rectange to contain the specified point.
        /// Returns true if the rectangle expands, otherwise false.
        /// </summary>
        /// <param name="point"></param>
        /// <returns></returns>
        public static Rectangle Union(in Rectangle rect, in Vector2 point)
        {
            if (double.IsNaN(rect.Left))
            {
                return new Rectangle(point, point);
            }

            double newBottom = rect.Bottom < point.Y ? rect.Bottom : point.Y;
            double newTop = rect.Top > point.Y ? rect.Top : point.Y;
            double newLeft = rect.Left < point.X ? rect.Left : point.X;
            double newRight = rect.Right > point.X ? rect.Right : point.X;

            return new Rectangle(newLeft, newRight, newBottom, newTop);
        }

        public static Rectangle GetBoundingBox(in Vector2[] points)
        {
            double MinX = points.Min(v => v.X);
            double MinY = points.Min(v => v.Y);
            double MaxX = points.Max(v => v.X);
            double MaxY = points.Max(v => v.Y);

            return new Rectangle(MinX, MaxX, MinY, MaxY);
        }

        public IShape2D Translate(in IPoint2D offset) => this.Translate(offset.Convert());

        public Rectangle Translate(in Vector2 offset) => new Rectangle(this.LowerLeft + offset, this.UpperRight + offset);

        public object Clone() => new Rectangle(this.LowerLeft, this.Width, this.Height);

        private static Vector2[] CalculateCorners(in double Left, in double Bottom, in double Right, in double Top) =>
            [ new(Left, Bottom),
                                new(Left, Top),
                                new(Right, Top),
                                new(Right, Bottom) ];

        private static LineSegment[] CalculateSegments(in Vector2[] corners)
        {
            var size = corners[(int)Corner.UpperRight] - corners[(int)Corner.LowerLeft];
            var width = size.X;
            var height = size.Y;

            if (width > Tolerance.Epsilon && height > Tolerance.Epsilon)
            {
                return [  new(corners[(int)Corner.LowerLeft], corners[(int)Corner.UpperLeft]),
                                                new(corners[(int)Corner.UpperLeft], corners[(int)Corner.UpperRight]),
                                                new(corners[(int)Corner.UpperRight], corners[(int)Corner.LowerRight]),
                                                new(corners[(int)Corner.LowerRight], corners[(int)Corner.LowerLeft])];
            }
            else if (width < Tolerance.Epsilon && height < Tolerance.Epsilon)
            {
                return [];
            }
            else
            {
                return [new(corners[(int)Corner.LowerLeft], corners[(int)Corner.UpperRight])];
            }
        }

        private static LineSegment[] CalculateSegments(in double left, in double right, in double bottom, in double top)
        {
            var width = right - left;
            var height = top - bottom;

            Vector2 LowerLeft = new(left, bottom);
            Vector2 UpperLeft = new(left, top);
            Vector2 LowerRight = new(right, bottom);
            Vector2 UpperRight = new(right, top);

            if (width > Tolerance.Epsilon && height > Tolerance.Epsilon)
            {
                return [  new(LowerLeft, UpperLeft),
                    new(UpperLeft, UpperRight),
                    new(UpperRight, LowerRight),
                    new(LowerRight, LowerLeft)];
            }
            else if (width < Tolerance.Epsilon && height < Tolerance.Epsilon)
            {
                return [];
            }
            else
            {
                return [new(LowerLeft, UpperRight)];
            }
        }

        #endregion

        public IPoint2D Centroid => Center;
    }
}
