using System;
using System.Diagnostics;
using System.Linq;

namespace Geometry
{
    [Serializable]
    public readonly struct GridRectangle : IRectangle, ICloneable, IEquatable<IRectangle>, IEquatable<GridRectangle>
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

        public readonly GridLineSegment[] Segments;

        IPoint2D IRectangle.Center => Center;

        public override string ToString() => $"{Left},{Bottom} W: {Width} H: {Height} Center:{Center}";

        public double Width => Right - Left;

        public double Height => Top - Bottom;

        public GridVector2 Center => new(LowerLeft.X + (Width / 2.0), LowerLeft.Y + (Height / 2.0));

        public GridVector2 LowerLeft => Corners?[(int)Corner.LowerLeft] ?? default;

        public GridVector2 UpperLeft => Corners?[(int)Corner.UpperLeft] ?? default;

        public GridVector2 LowerRight => Corners?[(int)Corner.LowerRight] ?? default;

        public GridVector2 UpperRight => Corners?[(int)Corner.UpperRight] ?? default;

        public double Area => Width * Height;

        public GridLineSegment LeftEdge => new(Corners?[(int)Corner.LowerLeft] ?? default, Corners?[(int)Corner.UpperLeft] ?? default);

        public GridLineSegment RightEdge => new(Corners?[(int)Corner.LowerRight] ?? default, Corners?[(int)Corner.UpperRight] ?? default);

        public GridLineSegment TopEdge => new(Corners?[(int)Corner.UpperLeft] ?? default, Corners?[(int)Corner.UpperRight] ?? default);

        public GridLineSegment BottomEdge => new(Corners?[(int)Corner.LowerLeft] ?? default, Corners?[(int)Corner.LowerRight] ?? default);

        public GridLineSegment[] Edges => [TopEdge, BottomEdge, LeftEdge, RightEdge];

        public GridRectangle BoundingBox => this;

        public ShapeType2D ShapeType => ShapeType2D.RECTANGLE;

        double IRectangle.Left => Left;

        double IRectangle.Right => Right;

        double IRectangle.Top => Top;

        double IRectangle.Bottom => Bottom;

        public readonly GridVector2[] Corners;

        public GridRectangle(in GridVector2 corner, in GridVector2 oppositeCorner)
        {
            GridVector2 RectOrigin = new(Math.Min(corner.X, oppositeCorner.X), Math.Min(corner.Y, oppositeCorner.Y));
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

            Corners = CalculateCorners(Left, Bottom, Right, Top);
            _HashCode = CalcHashCode(Left, Bottom, Right, Top);
            Segments = CalculateSegments(Corners);

            _HashCode = Left.GetHashCode() ^ Bottom.GetHashCode();
        }

        /// <summary>
        /// 
        /// </summary>
        /// <param name="borders">[MinX, MaxX, MinY, MaxY]</param>
        public GridRectangle(in double[] borders)
        {
            Left = borders[0];
            Right = borders[1];
            Bottom = borders[2];
            Top = borders[3];

            Corners = CalculateCorners(Left, Bottom, Right, Top);
            _HashCode = CalcHashCode(Left, Bottom, Right, Top);
            Segments = CalculateSegments(Corners);

            if (!double.IsNaN(Left))
            {
                Debug.Assert(Left <= Right && Bottom <= Top, "Grid Rectangle argument error");
                if (Left > Right || Bottom > Top)
                {
                    throw new ArgumentException("Grid Rectangle must have non-negative width and height");
                }
            }
        }

        public GridRectangle(in double left, in double right, in double bottom, in double top)
        {
            Left = left;
            Bottom = bottom;
            Top = top;
            Right = right;

            Corners = CalculateCorners(Left, Bottom, Right, Top);
            _HashCode = CalcHashCode(Left, Bottom, Right, Top);
            Segments = CalculateSegments(Corners);

            if (!double.IsNaN(Left))
            {
                Debug.Assert(Left <= Right && Bottom <= Top, "Grid Rectangle argument error");
                if (Left > Right || Bottom > Top)
                {
                    throw new ArgumentException("Grid Rectangle must have non-negative width and height");
                }
            }
        }

        public GridRectangle(in GridVector2 position, double width, double height)
        {
            Left = position.X;
            Bottom = position.Y;
            Top = Bottom + height;
            Right = Left + width;

            Corners = CalculateCorners(Left, Bottom, Right, Top);
            _HashCode = CalcHashCode(Left, Bottom, Right, Top);
            Segments = CalculateSegments(Corners);

            Debug.Assert(Left <= Right && Bottom <= Top, "Grid Rectangle argument error");
            if (Left > Right || Bottom > Top)
            {
                throw new ArgumentException("Grid Rectangle must have non-negative width and height");
            }
        }

        public GridRectangle(in GridVector2 position, in double radius)
        {
            Left = position.X - radius;
            Bottom = position.Y - radius;
            Top = position.Y + radius;
            Right = position.X + radius;

            Corners = CalculateCorners(Left, Bottom, Right, Top);
            _HashCode = CalcHashCode(Left, Bottom, Right, Top);
            Segments = CalculateSegments(Corners);

            Debug.Assert(Left <= Right && Bottom <= Top, "Grid Rectangle argument error");
        }

        public GridRectangle(in IPoint position, in double width, in double height)
        {
            if (position is null)
                throw new ArgumentNullException(nameof(position));

            Left = position.X;
            Bottom = position.Y;
            Top = Bottom + height;
            Right = Left + width;

            Corners = CalculateCorners(Left, Bottom, Right, Top);
            _HashCode = CalcHashCode(Left, Bottom, Right, Top);
            Segments = CalculateSegments(Corners);

            Debug.Assert(Left <= Right && Bottom <= Top, "Grid Rectable argument error");
            if (Left > Right || Bottom > Top)
            {
                throw new ArgumentException("Grid Rectangle must have non-negative width and height");
            }
        }

        public GridRectangle(in IPoint position, in double radius)
        {
            if (position is null)
                throw new ArgumentNullException(nameof(position));

            Left = position.X - radius;
            Bottom = position.Y - radius;
            Top = position.Y + radius;
            Right = position.X + radius;

            Corners = CalculateCorners(Left, Bottom, Right, Top);
            _HashCode = CalcHashCode(Left, Bottom, Right, Top);
            Segments = CalculateSegments(Corners);

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
        public bool Intersects(in GridRectangle rect)
        {
            //Find out if the rectangles can't possibly intersect
            if (rect.Right < this.Left ||
               rect.Top < this.Bottom ||
               rect.Left > this.Right ||
               rect.Bottom > this.Top)
                return false;

            return true;
        }

        public ShapeRelation IntersectionType(in GridRectangle rect)
        {
            //Find out if the rectangles can't possibly intersect
            if (rect.Right < this.Left ||
               rect.Top < this.Bottom ||
               rect.Left > this.Right ||
               rect.Bottom > this.Top)
                return ShapeRelation.NONE;

            if (rect.Right > this.Left &&
               rect.Top > this.Bottom &&
               rect.Left < this.Right &&
               rect.Bottom < this.Top)
                return ShapeRelation.CONTAINED;

            GridRectangle? intersectionArea = this.Intersection(rect);

            if (intersectionArea.Value.Area > 0)
            {
                return ShapeRelation.INTERSECTING;
            }

            /*

            if (rect.Right > this.Left ||
               rect.Top > this.Bottom ||
               rect.Left < this.Right ||
               rect.Bottom < this.Top)
                return ShapeRelation.INTERSECTING;

            if (rect.Right == this.Left ||
               rect.Top == this.Bottom ||
               rect.Left == this.Right ||
               rect.Bottom == this.Top)
               */
            return ShapeRelation.TOUCHING;

            //throw new ArgumentException(string.Format("Unexpected rectangle intersection case {0} {1}", rect, this));
        }

        public bool Intersects(in IShape2D shape) => Equals(ShapeExtensions.RectangleIntersects(this, shape));

        public bool Intersects(in ICircle2D c) => Intersects(c.Convert());

        public bool Intersects(in GridCircle circle) => RectangleIntersectionExtensions.Intersects(this, circle);

        public bool Intersects(in ILineSegment2D l) => Intersects(l.Convert());

        public bool Intersects(in GridLineSegment line) => RectangleIntersectionExtensions.Intersects(this, line);

        public bool Intersects(in ITriangle2D t) => Intersects(t.Convert());

        public bool Intersects(in GridTriangle tri) => RectangleIntersectionExtensions.Intersects(this, tri);

        public bool Intersects(in IPolygon2D p) => Intersects(p.Convert());

        public bool Intersects(in GridPolygon poly) => RectangleIntersectionExtensions.Intersects(this, poly);

        /// <summary>
        /// Returns the region of overlap between two rectangles
        /// </summary>
        /// <param name="other"></param>
        /// <returns></returns>
        public GridRectangle? Intersection(in GridRectangle other)
        {
            if (false == this.Intersects(other))
                return new GridRectangle?();

            double minx = Math.Max(this.Left, other.Left);
            double maxx = Math.Min(this.Right, other.Right);
            double miny = Math.Max(this.Bottom, other.Bottom);
            double maxy = Math.Min(this.Top, other.Top);

            return new GridRectangle(minx, maxx, miny, maxy);
        }



        /// <summary>
        /// Returns true if the passed rectangle is entirely inside this rectangle
        /// </summary>
        /// <param name="rect"></param>
        /// <returns></returns>
        public bool Contains(in GridRectangle rect)
        {
            //Find out if rect is inside this rectangle
            if (rect.Right <= this.Right &&
               rect.Top <= this.Top &&
               rect.Left >= this.Left &&
               rect.Bottom >= this.Bottom)
                return true;

            return false;
        }

        public bool Contains(in IPoint2D pos)
        {
            //Find out if the rectangles can't possibly intersect
            if (pos.X >= this.Left &&
               pos.Y >= this.Bottom &&
               pos.X <= this.Right &&
               pos.Y <= this.Top)
                return true;

            return false;
        }

        public ShapeRelation GetRelation(in IPoint2D pos)
        {
            //Find out if the rectangles can't possibly intersect
            if (pos.X >= this.Left &&
               pos.Y >= this.Bottom &&
               pos.X <= this.Right &&
               pos.Y <= this.Top)
            {
                if (pos.X == this.Left ||
                    pos.Y == this.Bottom ||
                    pos.X == this.Right ||
                    pos.Y == this.Top)
                    return ShapeRelation.TOUCHING;

                return ShapeRelation.CONTAINED;
            }

            return ShapeRelation.NONE;
        }

        public ShapeRelation GetRelation(in ILineSegment2D line)
        {
            //This is very similar to the logic for GridTriangle
            ShapeRelation relA = this.GetRelation(line.A);
            ShapeRelation relB = this.GetRelation(line.B);

            ShapeRelation composite = relA | relB;

            bool containsA = relA == ShapeRelation.CONTAINED;
            bool containsB = relB == ShapeRelation.CONTAINED;

            if (containsA && containsB)
                return ShapeRelation.CONTAINED;

            //Edge case where one end of the line is contained and the other is exactly on the edge
            if (composite.HasFlag(ShapeRelation.TOUCHING | ShapeRelation.CONTAINED))
                return ShapeRelation.CONTAINED;

            //Edge case where the line is exactly along the edge... not sure if this should be touching or contained, if not the same edge it is contained, but if the same edge it is touching
            if (relA.HasFlag(ShapeRelation.TOUCHING) && relB.HasFlag(ShapeRelation.TOUCHING))
            {
                //Check if the line is touching the same segment in two places
                foreach (GridLineSegment e in this.Segments)
                    if (e.Intersects(line.A.Convert()) && e.Intersects(line.B.Convert()))
                        return ShapeRelation.TOUCHING;

                return ShapeRelation.CONTAINED;
            }

            //Check if line crosses the bounding box but both points are outside the box
            foreach (GridLineSegment e in this.Segments)
                if (e.Intersects(line))
                    return ShapeRelation.INTERSECTING;

            //OK, make sure one endpoint isn't touching and the rest of the line is outside the triangle
            if (composite.HasFlag(ShapeRelation.TOUCHING))
                return ShapeRelation.TOUCHING;

            return ShapeRelation.NONE;
        }

        public bool Contains(in GridVector2 pos, in double epsilon = Global.Epsilon)
        {
            //Find out if the rectangles can't possibly intersect
            if (pos.X >= this.Left - epsilon &&
               pos.Y >= this.Bottom - epsilon &&
               pos.X <= this.Right + epsilon &&
               pos.Y <= this.Top + epsilon)
                return true;

            return false;
        }

        public bool Contains(in IPoint pos)
        {
            if (pos is null)
                throw new ArgumentNullException(nameof(pos));

            //Find out if the rectangles can't possibly intersect
            if (pos.X >= this.Left &&
               pos.Y >= this.Bottom &&
               pos.X <= this.Right &&
               pos.Y <= this.Top)
                return true;

            return false;
        }

        public ShapeRelation GetRelation(in GridRectangle rect)
        {
            //Find out if the rectangles can't possibly intersect
            if (rect.Right < this.Left ||
               rect.Top < this.Bottom ||
               rect.Left > this.Right ||
               rect.Bottom > this.Top)
                return ShapeRelation.NONE;

            if (rect.Right <= this.Right &&
               rect.Top <= this.Top &&
               rect.Left >= this.Left &&
               rect.Bottom >= this.Bottom)
                return ShapeRelation.CONTAINED;

            bool LRIntersect = (this.Left < rect.Left && this.Right > rect.Left) ||
                               (this.Right > rect.Left && this.Right < rect.Right) ||
                               (this.Left > rect.Left && this.Right < rect.Right) ||
                               (this.Left > rect.Left && this.Left < rect.Right);

            bool UDIntersect = (this.Bottom < rect.Bottom && this.Top > rect.Bottom) ||
                               (this.Top > rect.Bottom && this.Top < rect.Top) ||
                               (this.Bottom > rect.Bottom && this.Top < rect.Top) ||
                               (this.Bottom > rect.Bottom && this.Bottom < rect.Top);

            if (LRIntersect && UDIntersect)
                return ShapeRelation.INTERSECTING;

            bool LRTouch = this.Left == rect.Right || this.Right == rect.Left;
            bool UDTouch = this.Bottom == rect.Top || this.Top == rect.Bottom;

            if ((LRTouch && UDIntersect) ||
                (UDTouch && LRIntersect) ||
                (LRTouch && UDTouch))
                return ShapeRelation.TOUCHING;


            if (rect.Width == 0 || rect.Height == 0 || this.Width == 0 || this.Height == 0)
            {
                //If we are dealing with a zero height rectangle then check some edge cases
                if (LRIntersect || UDIntersect)
                    return ShapeRelation.INTERSECTING;

                if (LRTouch || UDTouch)
                    return ShapeRelation.TOUCHING;
            }
            else
            {
                System.Diagnostics.Debug.Assert(false, "Every case should be handled at this point for a rectangle with non-zero width and height...");
            }

            return ShapeRelation.NONE;
        }

        private readonly int _HashCode;

        public override int GetHashCode() => _HashCode;

        private static int CalcHashCode(in double left, in double bottom, in double right, in double top) => left.GetHashCode() ^ bottom.GetHashCode() ^ right.GetHashCode() ^ top.GetHashCode();

        public override bool Equals(object obj)
        {
            if (obj is GridRectangle other)
                return Equals(other);

            if (obj is IShape2D otherShape)
                return Equals(otherShape);

            return false;
        }

        public bool Equals(IShape2D other)
        {
            if (other is IRectangle otherRect)
                return Equals(otherRect);

            return false;
        }

        public bool Equals(IRectangle other)
        {
            return Left.Equals(other.Left) &&
                   Right.Equals(other.Right) &&
                   Top.Equals(other.Top) &&
                   Bottom.Equals(other.Bottom);
        }

        public bool Equals(GridRectangle other)
        {
            return Left.Equals(other.Left) &&
                   Right.Equals(other.Right) &&
                   Top.Equals(other.Top) &&
                   Bottom.Equals(other.Bottom);
        }

        #region Static Methods

        public static implicit operator RTree.Rectangle(in GridRectangle rect)
        {
            return new RTree.Rectangle(rect.Left, rect.Bottom, rect.Right, rect.Top, 0, 0);
        }

        public static bool operator ==(in GridRectangle A, in GridRectangle B)
        {
            return ((A.Left == B.Left) &&
                    (A.Right == B.Right) &&
                    (A.Top == B.Top) &&
                    (A.Bottom == B.Bottom));
        }

        public static bool operator !=(in GridRectangle A, in GridRectangle B) => !(A == B);

        /// <summary>
        /// Pads the border by the specified amount
        /// </summary>
        /// <param name="A"></param>
        /// <param name="scalar"></param>
        /// <returns></returns>
        public static GridRectangle operator +(in GridRectangle A, double scalar) => GridRectangle.Scale(A, scalar);

        /// <summary>
        /// Performs a union of the rectangle and the point
        /// </summary>
        /// <param name="A"></param>
        /// <param name="scalar"></param>
        /// <returns></returns>
        public static GridRectangle operator +(in GridRectangle A, in GridVector2 p) => GridRectangle.Union(A, p);

        /// <summary>
        /// Performs a union of the rectangle and the bounding box of the shape
        /// </summary>
        /// <param name="A"></param>
        /// <param name="scalar"></param>
        /// <returns></returns>
        public static GridRectangle operator +(in GridRectangle A, in IShape2D shape) => GridRectangle.Union(A, shape.BoundingBox);

        /// <summary>
        /// Performs a union of both rectangles and returns the bounding box of both
        /// </summary>
        /// <param name="A"></param>
        /// <param name="scalar"></param>
        /// <returns></returns>
        public static GridRectangle operator +(in GridRectangle A, in GridRectangle B) => GridRectangle.Union(A, B);

        public static GridRectangle operator *(in GridRectangle A, in double scalar) => GridRectangle.Scale(A, scalar);

        public static GridRectangle operator /(in GridRectangle A, in double scalar) => GridRectangle.Scale(A, 1.0 / scalar);

        /// <summary>
        /// Pad the requested amount onto the bounding box
        /// </summary>
        /// <param name="Radius"></param>
        /// <returns></returns>
        public static GridRectangle Pad(in GridRectangle rect, in double radius) => new GridRectangle(rect.Left - radius, rect.Right + radius, rect.Bottom - radius, rect.Top + radius);

        public static GridRectangle Scale(in GridRectangle rect, in double scalar)
        {
            //Have to cache center because it changes as we update points
            GridVector2 center = rect.Center;
            GridVector2 directionA = rect.UpperRight - center;

            directionA *= scalar;

            GridVector2 BottomLeft = center - directionA;
            GridVector2 TopRight = center + directionA;

            var left = BottomLeft.X;
            var bottom = BottomLeft.Y;
            var right = TopRight.X;
            var top = TopRight.Y;

            Debug.Assert(left <= right && bottom <= top, "Grid Rectangle scale argument error");

            return new GridRectangle(left: left, bottom: bottom,
                right: right, top: top);
        }


        /// <summary>
        /// Returns a rectangle bounding the passed rectangles
        /// </summary>
        /// <param name="A"></param>
        /// <param name="B"></param>
        /// <returns></returns>
        public static GridRectangle Union(in IShape2D a, in IShape2D b) => GridRectangle.Union(a.BoundingBox, b.BoundingBox);

        /// <summary>
        /// Returns a rectangle bounding the passed rectangles
        /// </summary>
        /// <param name="A"></param>
        /// <param name="B"></param>
        /// <returns></returns>
        public static GridRectangle Union(in GridRectangle A, in GridRectangle B)
        {
            double left = A.Left < B.Left ? A.Left : B.Left;
            double right = A.Right > B.Right ? A.Right : B.Right;
            double top = A.Top > B.Top ? A.Top : B.Top;
            double bottom = A.Bottom < B.Bottom ? A.Bottom : B.Bottom;

            return new GridRectangle(left, right, bottom, top);
        }

        /// <summary>
        /// Expands the rectange to contain the specified point.
        /// Returns true if the rectangle expands, otherwise false.
        /// </summary>
        /// <param name="point"></param>
        /// <returns></returns>
        public static GridRectangle Union(in GridRectangle rect, in GridVector2 point)
        {
            if (double.IsNaN(rect.Left))
            {
                return new GridRectangle(point, point);
            }

            double newBottom = rect.Bottom < point.Y ? rect.Bottom : point.Y;
            double newTop = rect.Top > point.Y ? rect.Top : point.Y;
            double newLeft = rect.Left < point.X ? rect.Left : point.X;
            double newRight = rect.Right > point.X ? rect.Right : point.X;

            return new GridRectangle(newLeft, newRight, newBottom, newTop);
        }

        public static GridRectangle GetBoundingBox(in GridVector2[] points)
        {
            double MinX = points.Min(v => v.X);
            double MinY = points.Min(v => v.Y);
            double MaxX = points.Max(v => v.X);
            double MaxY = points.Max(v => v.Y);

            return new GridRectangle(MinX, MaxX, MinY, MaxY);
        }

        public IShape2D Translate(in IPoint2D offset) => this.Translate(offset.Convert());

        public GridRectangle Translate(in GridVector2 offset) => new GridRectangle(this.LowerLeft + offset, this.UpperRight + offset);

        public object Clone() => new GridRectangle(this.LowerLeft, this.Width, this.Height);

        private static GridVector2[] CalculateCorners(in double Left, in double Bottom, in double Right, in double Top) =>
            [ new(Left, Bottom),
                                new(Left, Top),
                                new(Right, Top),
                                new(Right, Bottom) ];

        private static GridLineSegment[] CalculateSegments(in GridVector2[] corners)
        {
            var size = corners[(int)Corner.UpperRight] - corners[(int)Corner.LowerLeft];
            var width = size.X;
            var height = size.Y;

            if (width > Global.Epsilon && height > Global.Epsilon)
            {
                return [  new(corners[(int)Corner.LowerLeft], corners[(int)Corner.UpperLeft]),
                                                new(corners[(int)Corner.UpperLeft], corners[(int)Corner.UpperRight]),
                                                new(corners[(int)Corner.UpperRight], corners[(int)Corner.LowerRight]),
                                                new(corners[(int)Corner.LowerRight], corners[(int)Corner.LowerLeft])];
            }
            else if (width < Global.Epsilon && height < Global.Epsilon)
            {
                return [];
            }
            else
            {
                return [new(corners[(int)Corner.LowerLeft], corners[(int)Corner.UpperRight])];
            }
        }

        private static GridLineSegment[] CalculateSegments(in double left, in double right, in double bottom, in double top)
        {
            var width = right - left;
            var height = top - bottom;

            GridVector2 LowerLeft = new(left, bottom);
            GridVector2 UpperLeft = new(left, top);
            GridVector2 LowerRight = new(right, bottom);
            GridVector2 UpperRight = new(right, top);

            if (width > Global.Epsilon && height > Global.Epsilon)
            {
                return [  new(LowerLeft, UpperLeft),
                    new(UpperLeft, UpperRight),
                    new(UpperRight, LowerRight),
                    new(LowerRight, LowerLeft)];
            }
            else if (width < Global.Epsilon && height < Global.Epsilon)
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
