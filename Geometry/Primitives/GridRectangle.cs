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
         
        public override string ToString()
        {
            return $"{Left},{Bottom} W: {Width} H: {Height} Center:{Center}";
        }

        public double Width => Right - Left;

        public double Height => Top - Bottom;

        public GridVector2 Center => new GridVector2(LowerLeft.X + (Width / 2.0), LowerLeft.Y + (Height / 2.0));

        public GridVector2 LowerLeft => Corners[(int)Corner.LowerLeft];

        public GridVector2 UpperLeft => Corners[(int)Corner.UpperLeft];

        public GridVector2 LowerRight => Corners[(int)Corner.LowerRight];

        public GridVector2 UpperRight => Corners[(int)Corner.UpperRight];

        public double Area => Width * Height;

        public GridLineSegment LeftEdge => new GridLineSegment(Corners[(int)Corner.LowerLeft], Corners[(int)Corner.UpperLeft]);

        public GridLineSegment RightEdge => new GridLineSegment(Corners[(int)Corner.LowerRight],Corners[(int)Corner.UpperRight]);

        public GridLineSegment TopEdge => new GridLineSegment(Corners[(int)Corner.UpperLeft], Corners[(int)Corner.UpperRight]);

        public GridLineSegment BottomEdge => new GridLineSegment(Corners[(int)Corner.LowerLeft], Corners[(int)Corner.LowerRight]);

        public GridLineSegment[] Edges => new GridLineSegment[] { TopEdge, BottomEdge, LeftEdge, RightEdge };

        public GridRectangle BoundingBox => this;

        public ShapeType2D ShapeType => ShapeType2D.RECTANGLE;

        double IRectangle.Left => Left;

        double IRectangle.Right => Right;

        double IRectangle.Top => Top;

        double IRectangle.Bottom => Bottom;

        public readonly GridVector2[] Corners;
          
        public GridRectangle(GridVector2 corner, GridVector2 oppositeCorner)
        {  
            GridVector2 RectOrigin = new GridVector2(Math.Min(corner.X, oppositeCorner.X), Math.Min(corner.Y, oppositeCorner.Y));
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
        public GridRectangle(double[] borders)
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

        public GridRectangle(double left, double right, double bottom, double top)
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

        public GridRectangle(GridVector2 position, double width, double height)
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

        public GridRectangle(GridVector2 position, double radius)
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

        public GridRectangle(IPoint position, double width, double height)
        {  
            if (position == null)
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

        public GridRectangle(IPoint position, double radius)
        {  
            if (position == null)
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

        public OverlapType IntersectionType(in GridRectangle rect)
        {
            //Find out if the rectangles can't possibly intersect
            if (rect.Right < this.Left ||
               rect.Top < this.Bottom ||
               rect.Left > this.Right ||
               rect.Bottom > this.Top)
                return OverlapType.NONE;

            if (rect.Right > this.Left &&
               rect.Top > this.Bottom &&
               rect.Left < this.Right &&
               rect.Bottom < this.Top)
                return OverlapType.CONTAINED;

            GridRectangle? intersectionArea = this.Intersection(rect);

            if (intersectionArea.Value.Area > 0)
            {
                return OverlapType.INTERSECTING;
            }

            /*

            if (rect.Right > this.Left ||
               rect.Top > this.Bottom ||
               rect.Left < this.Right ||
               rect.Bottom < this.Top)
                return OverlapType.INTERSECTING;

            if (rect.Right == this.Left ||
               rect.Top == this.Bottom ||
               rect.Left == this.Right ||
               rect.Bottom == this.Top)
               */
            return OverlapType.TOUCHING;

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

        public OverlapType ContainsExt(in IPoint2D pos)
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
                    return OverlapType.TOUCHING;

                return OverlapType.CONTAINED;
            }

            return OverlapType.NONE;
        }

        public bool Contains(GridVector2 pos, double epsilon = Global.Epsilon)
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
            if (pos == null)
                throw new ArgumentNullException(nameof(pos));

            //Find out if the rectangles can't possibly intersect
            if (pos.X >= this.Left &&
               pos.Y >= this.Bottom &&
               pos.X <= this.Right &&
               pos.Y <= this.Top)
                return true;

            return false;
        }

        public OverlapType ContainsExt(in GridRectangle rect)
        {
            //Find out if the rectangles can't possibly intersect
            if (rect.Right < this.Left ||
               rect.Top < this.Bottom ||
               rect.Left > this.Right ||
               rect.Bottom > this.Top)
                return OverlapType.NONE;

            if (rect.Right <= this.Right &&
               rect.Top <= this.Top &&
               rect.Left >= this.Left &&
               rect.Bottom >= this.Bottom)
                return OverlapType.CONTAINED;

            bool LRIntersect = (this.Left < rect.Left && this.Right > rect.Left) ||
                               (this.Right > rect.Left && this.Right < rect.Right) ||
                               (this.Left > rect.Left && this.Right < rect.Right) ||
                               (this.Left > rect.Left && this.Left < rect.Right);

            bool UDIntersect = (this.Bottom < rect.Bottom && this.Top > rect.Bottom) ||
                               (this.Top > rect.Bottom && this.Top < rect.Top) ||
                               (this.Bottom > rect.Bottom && this.Top < rect.Top) ||
                               (this.Bottom > rect.Bottom && this.Bottom < rect.Top);

            if (LRIntersect && UDIntersect)
                return OverlapType.INTERSECTING;

            bool LRTouch = this.Left == rect.Right || this.Right == rect.Left;
            bool UDTouch = this.Bottom == rect.Top || this.Top == rect.Bottom;

            if ((LRTouch && UDIntersect) ||
                (UDTouch && LRIntersect) ||
                (LRTouch && UDTouch))
                return OverlapType.TOUCHING;

            
            if (rect.Width == 0 || rect.Height == 0 || this.Width == 0 || this.Height == 0)
            {
                //If we are dealing with a zero height rectangle then check some edge cases
                if (LRIntersect || UDIntersect)
                    return OverlapType.INTERSECTING;

                if (LRTouch || UDTouch)
                    return OverlapType.TOUCHING;
            }
            else
            {
                System.Diagnostics.Debug.Assert(false, "Every case should be handled at this point for a rectangle with non-zero width and height...");
            }

            return OverlapType.NONE;
        }

        private readonly int _HashCode;

        public override int GetHashCode() => _HashCode;

        private static int CalcHashCode(double left, double bottom, double right, double top)
        {
            return left.GetHashCode() ^ bottom.GetHashCode() ^ right.GetHashCode() ^ top.GetHashCode();
        }

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

        public static implicit operator RTree.Rectangle(GridRectangle rect)
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

        public static bool operator !=(in GridRectangle A, in GridRectangle B)
        {
            return !(A == B);
        }

        /// <summary>
        /// Pads the border by the specified amount
        /// </summary>
        /// <param name="A"></param>
        /// <param name="scalar"></param>
        /// <returns></returns>
        public static GridRectangle operator +(in GridRectangle A, double scalar)
        {
            return GridRectangle.Scale(A, scalar);
        }

        /// <summary>
        /// Performs a union of the rectangle and the point
        /// </summary>
        /// <param name="A"></param>
        /// <param name="scalar"></param>
        /// <returns></returns>
        public static GridRectangle operator +(in GridRectangle A, GridVector2 p)
        {
            return GridRectangle.Union(A, p);
        }

        /// <summary>
        /// Performs a union of the rectangle and the bounding box of the shape
        /// </summary>
        /// <param name="A"></param>
        /// <param name="scalar"></param>
        /// <returns></returns>
        public static GridRectangle operator +(in GridRectangle A, in IShape2D shape)
        {
            return GridRectangle.Union(A, shape.BoundingBox);
        }

        /// <summary>
        /// Performs a union of both rectangles and returns the bounding box of both
        /// </summary>
        /// <param name="A"></param>
        /// <param name="scalar"></param>
        /// <returns></returns>
        public static GridRectangle operator +(in GridRectangle A, in GridRectangle B)
        {
            return GridRectangle.Union(A, B);
        }

        public static GridRectangle operator *(in GridRectangle A, double scalar)
        {
            return GridRectangle.Scale(A, scalar);
        }

        public static GridRectangle operator /(in GridRectangle A, double scalar)
        {
            return GridRectangle.Scale(A, 1.0 / scalar);
        }

        /// <summary>
        /// Pad the requested amount onto the bounding box
        /// </summary>
        /// <param name="Radius"></param>
        /// <returns></returns>
        public static GridRectangle Pad(in GridRectangle rect, double radius)
        {
            return new GridRectangle(rect.Left - radius, rect.Right + radius, rect.Bottom - radius, rect.Top + radius);
        }

        public static GridRectangle Scale(in GridRectangle rect, double scalar)
        {
            //Have to cache center because it changes as we update points
            GridVector2 center = rect.Center;
            GridVector2 directionA = rect.UpperRight - center;

            directionA = directionA * scalar;

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
        public static GridRectangle Union(in IShape2D a, in IShape2D b)
        {
            return GridRectangle.Union(a.BoundingBox, b.BoundingBox);
        }

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
        public static GridRectangle Union(in GridRectangle rect, GridVector2 point)
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

        public static GridRectangle GetBoundingBox(GridVector2[] points)
        {
            double MinX = points.Min(v => v.X);
            double MinY = points.Min(v => v.Y);
            double MaxX = points.Max(v => v.X);
            double MaxY = points.Max(v => v.Y);

            return new GridRectangle(MinX, MaxX, MinY, MaxY);
        }

        public IShape2D Translate(in IPoint2D offset)
        {
            return this.Translate(offset.Convert());
        }

        public GridRectangle Translate(in GridVector2 offset)
        {
            return new GridRectangle(this.LowerLeft + offset, this.UpperRight + offset);
        }

        public object Clone()
        {
            return new GridRectangle(this.LowerLeft, this.Width, this.Height);
        }

        

        private static GridVector2[] CalculateCorners(double Left, double Bottom, double Right, double Top) => 
            new GridVector2[] { new GridVector2(Left, Bottom),
                new GridVector2(Left, Top),
                new GridVector2(Right, Top),
                new GridVector2(Right, Bottom) };
         
        private static GridLineSegment[] CalculateSegments(GridVector2[] corners)
        {
            var size = corners[(int)Corner.UpperRight] - corners[(int)Corner.LowerLeft];
            var width = size.X;
            var height = size.Y;

            if (width > Global.Epsilon && height > Global.Epsilon)
            {
                return new GridLineSegment[] {  new GridLineSegment(corners[(int)Corner.LowerLeft], corners[(int)Corner.UpperLeft]),
                                                new GridLineSegment(corners[(int)Corner.UpperLeft], corners[(int)Corner.UpperRight]),
                                                new GridLineSegment(corners[(int)Corner.UpperRight], corners[(int)Corner.LowerRight]),
                                                new GridLineSegment(corners[(int)Corner.LowerRight], corners[(int)Corner.LowerLeft])};
            }
            else if (width < Global.Epsilon && height < Global.Epsilon)
            {
                return Array.Empty<GridLineSegment>();
            }
            else
            {
                return new GridLineSegment[] { new GridLineSegment(corners[(int)Corner.LowerLeft], corners[(int)Corner.UpperRight]) };
            }
        }

        #endregion

        public IPoint2D Centroid => Center;

        GridVector2 IShape2D.Centroid => Center;
    }
}
