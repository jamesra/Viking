using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Diagnostics; 
using Geometry; 

namespace Geometry
{
    [Serializable]
    public struct GridRectangle : IRectangle, ICloneable
    {
        public double Left { get; private set; }
        public double Right { get; private set; }
        /// <summary>
        /// Top has a larger value than bottom
        /// </summary>
        public double Top { get; private set; }

        /// <summary>
        /// Bottom has a smaller value than top
        /// </summary>
        public double Bottom { get; private set; }


        public override string ToString()
        {
            return string.Format("{0},{1} W: {2} H: {3} Center: {4}", Left, Bottom, Width, Height, Center.ToString());
        }

        public double Width
        {
            get{
                //Debug.Assert(Right - Left >= 0); 
                return Right - Left;
            }
        }

        public double Height 
        {
            get 
            { 
                //Debug.Assert(Top - Bottom >= 0); 
                return Top - Bottom;
            }
        }

        public GridVector2 Center
        {
            get
            {
                return new GridVector2(LowerLeft.X + (Width / 2), LowerLeft.Y + (Height / 2));
            }
        }

        public GridVector2 LowerLeft
        {
            get
            {
                return new GridVector2(Left, Bottom); 
            }
        }

        public GridVector2 UpperLeft
        {
            get
            {
                return new GridVector2(Left, Top); 
            }
        }

        public GridVector2 LowerRight
        {
            get
            {
                return new GridVector2(Right, Bottom); 
            }
        }

        public GridVector2 UpperRight
        {
            get
            {
                return new GridVector2(Right, Top); 
            }
        }

        public double Area
        {
            get
            {
                return Width * Height; 
            }

        }

        public GridLineSegment LeftEdge
        {
            get
            {
                return new GridLineSegment(new GridVector2(Left, Bottom),
                                           new GridVector2(Left, Top));
            }
        }

        public GridLineSegment RightEdge
        {
            get
            {
                return new GridLineSegment(new GridVector2(Right, Bottom),
                                           new GridVector2(Right, Top));
            }
        }

        public GridLineSegment TopEdge
        {
            get
            {
                return new GridLineSegment(new GridVector2(Left, Top),
                                           new GridVector2(Right, Top));
            }
        }

        public GridLineSegment BottomEdge
        {
            get
            {
                return new GridLineSegment(new GridVector2(Left, Bottom),
                                           new GridVector2(Right, Bottom));
            }
        }

        public GridRectangle BoundingBox
        {
            get
            {
                return this;
            }
        }

        public ShapeType2D ShapeType
        {
            get
            {
                return ShapeType2D.RECTANGLE;
            }
        }

        double IRectangle.Left
        {
            get
            {
                return Left;
            }
        }

        double IRectangle.Right
        {
            get
            {
                return Right;
            }
        }

        double IRectangle.Top
        {
            get
            {
                return Top;
            }
        }

        double IRectangle.Bottom
        {
            get
            {
                return Bottom;
            }
        }

        private GridVector2[] _Corners;


        public GridVector2[] Corners
        {
            get
            {
                if (_Corners == null)
                {
                    _Corners = new GridVector2[] { LowerLeft, UpperLeft, UpperRight, LowerRight };
                }

                return _Corners;
            }
        }
         
        private void ResetCache()
        {
            _Corners = null;
            _Segments = null;
        }

        public GridRectangle(GridVector2 corner, GridVector2 oppositeCorner)
        {
            _Corners = null;
            _Segments = null;
            GridVector2 RectOrigin = new GridVector2(Math.Min(corner.X, oppositeCorner.X), Math.Min(corner.Y, oppositeCorner.Y));
            double Width = Math.Abs(corner.X - oppositeCorner.X);
            double Height = Math.Abs(corner.Y - oppositeCorner.Y);
            if (Width == 0 || Height == 0)
            {
                throw new ArgumentException("Grid Rectangle must have non-negative width and height");
            }

            Left = RectOrigin.X;
            Bottom = RectOrigin.Y;
            Top = RectOrigin.Y + Height;
            Right = RectOrigin.X + Width;
            _HashCode = new int?();
        }


        public GridRectangle(double left, double right,  double bottom, double top)
        {
            _Corners = null;
            _Segments = null;
            Left = left;
            Bottom = bottom;
            Top = top;
            Right = right;
            _HashCode = new int?();

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
            _Corners = null;
            _Segments = null;
            Left = position.X;
            Bottom = position.Y;
            Top = Bottom + height;
            Right = Left + width;
            _HashCode = new int?();
            Debug.Assert(Left <= Right && Bottom <= Top, "Grid Rectable argument error");
            if (Left > Right || Bottom > Top)
            {
                throw new ArgumentException("Grid Rectangle must have non-negative width and height");
            }
        }

        public GridRectangle(GridVector2 position, double radius)
        {
            _Corners = null;
            _Segments = null;
            Left = position.X - radius;
            Bottom = position.Y - radius;
            Top = position.Y + radius;
            Right = position.X + radius;
            _HashCode = new int?();

            Debug.Assert(Left <= Right && Bottom <= Top, "Grid Rectable argument error");
        } 

        public GridRectangle(IPoint position, double width, double height)
        {
            _Corners = null;
            _Segments = null;
            if (position == null)
                throw new ArgumentNullException("points");

            Left = position.X;
            Bottom = position.Y; 
            Top = Bottom + height; 
            Right = Left + width;
            _HashCode = new int?();

            Debug.Assert(Left <= Right && Bottom <= Top, "Grid Rectable argument error");
            if (Left > Right || Bottom > Top)
            {
                throw new ArgumentException("Grid Rectangle must have non-negative width and height");
            }
        }

        public GridRectangle(IPoint position, double radius)
        {
            _Corners = null;
            _Segments = null;
            if (position == null)
                throw new ArgumentNullException("position");

            Left = position.X - radius;
            Bottom = position.Y - radius;
            Top = position.Y + radius;
            Right = position.X + radius;
            _HashCode = new int?();

            Debug.Assert(Left <= Right && Bottom <= Top, "Grid Rectable argument error");
            if (Left > Right || Bottom > Top)
            {
                throw new ArgumentException("Grid Rectangle must have non-negative width and height");
            }
        }

        
        public void Scale(double scalar)
        {
            //Have to cache center because it changes as we update points
            GridVector2 center = this.Center;
            GridVector2 directionA = this.UpperRight - center;

            directionA = directionA * scalar;
            
            GridVector2 BottomLeft = center - directionA;
            GridVector2 TopRight = center + directionA;

            this.Left = BottomLeft.X;
            this.Bottom = BottomLeft.Y;
            this.Right = TopRight.X;
            this.Top = TopRight.Y;

            ResetCache();

            Debug.Assert(Left <= Right && Bottom <= Top, "Grid Rectable argument error"); 
        }

        /// <summary>
        /// Pad the requested amount onto the bounding box
        /// </summary>
        /// <param name="Radius"></param>
        /// <returns></returns>
        public GridRectangle Pad(double Radius)
        {
            return new GridRectangle(this.Left - Radius, this.Right + Radius, this.Bottom - Radius, this.Top + Radius);
        }

        /// <summary>
        /// Returns true if the passed rectangle in inside or overlaps this rectangle
        /// </summary>
        /// <param name="rect"></param>
        /// <returns></returns>
        public bool Intersects(GridRectangle rect)
        {
            //Find out if the rectangles can't possibly intersect
            if(rect.Right < this.Left ||
               rect.Top < this.Bottom ||
               rect.Left > this.Right ||
               rect.Bottom > this.Top)
                return false; 

            return true;
        }

        public bool Intersects(IShape2D shape)
        {
            return ShapeExtensions.RectangleIntersects(this, shape);
        }


        public bool Intersects(ICircle2D c)
        {
            GridCircle circle = c.Convert();
            return this.Intersects(circle);
        }

        public bool Intersects(GridCircle circle)
        {
            return RectangleIntersectionExtensions.Intersects(this, circle);
        }


        public bool Intersects(ILineSegment2D l)
        {
            GridLineSegment line = l.Convert();
            return this.Intersects(line);
        }

        public bool Intersects(GridLineSegment line)
        {
            return RectangleIntersectionExtensions.Intersects(this, line);
        }

        public bool Intersects(ITriangle2D t)
        {
            GridTriangle tri = t.Convert();
            return this.Intersects(tri);
        }

        public bool Intersects(GridTriangle tri)
        {
            return RectangleIntersectionExtensions.Intersects(this, tri);
        }

        public bool Intersects(IPolygon2D p)
        {
            GridPolygon poly = p.Convert();
            return this.Intersects(poly);
        }

        public bool Intersects(GridPolygon poly)
        {
            return RectangleIntersectionExtensions.Intersects(this, poly);
        }

        /// <summary>
        /// Returns the region of overlap between two rectangles
        /// </summary>
        /// <param name="other"></param>
        /// <returns></returns>
        public GridRectangle? Intersection(GridRectangle other)
        {
            if (!this.Intersects(other))
                return new GridRectangle?();

            double minx = Math.Max(this.Left, other.Left);
            double maxx = Math.Min(this.Right, other.Right);
            double miny = Math.Max(this.Bottom, other.Bottom);
            double maxy = Math.Min(this.Top, other.Top);

            return new GridRectangle(minx, maxx, miny, maxy);
        }

        /// <summary>
        /// Expands the rectange to contain the specified point.
        /// Returns true if the rectangle expands, otherwise false.
        /// </summary>
        /// <param name="point"></param>
        /// <returns></returns>
        public bool Union(GridVector2 point)
        {
            ResetCache();

            if (double.IsNaN(this.Left))
            {
                this.Left = point.X;
                this.Right = point.X;
                this.Bottom = point.Y;
                this.Top = point.Y;
                return true;
            }

            bool RetVal = false;
            if (point.Y < Bottom)
            {
                this.Bottom = point.Y;
                RetVal = true;
            }
            if (point.X < Left)
            {
                this.Left = point.X;
                RetVal = true;
            }
            if (point.Y > Top)
            {
                this.Top = point.Y;
                RetVal = true;
            }
            if (point.X > Right)
            {
                this.Right = point.X;
                RetVal = true;
            }

            return RetVal;
        }

        public bool Union(GridRectangle rect)
        {
            bool llExpand = this.Union(rect.LowerLeft);
            bool urExpand = this.Union(rect.UpperRight);

            return llExpand || urExpand; //Cannot combine these or short-circuit execution will cancel one.
        }

        /// <summary>
        /// Expands the rectange to contain the specified point.
        /// Returns true if the rectangle expands, otherwise false.
        /// </summary>
        /// <param name="point"></param>
        /// <returns></returns>
        public static GridRectangle Union(GridRectangle rect, GridVector2 point)
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
          
        /// <summary>
        /// Returns true if the passed rectangle is entirely inside this rectangle
        /// </summary>
        /// <param name="rect"></param>
        /// <returns></returns>
        public bool Contains(GridRectangle rect)
        {
            //Find out if rect is inside this rectangle
            if (rect.Right <= this.Right &&
               rect.Top <= this.Top &&
               rect.Left >= this.Left &&
               rect.Bottom >= this.Bottom)
                return true;

            return false;
        }
        
        public bool Contains(IPoint2D pos)
        {
            //Find out if the rectangles can't possibly intersect
            if (pos.X >= this.Left &&
               pos.Y >= this.Bottom &&
               pos.X <= this.Right &&
               pos.Y <= this.Top)
                return true;

            return false;
        }

        public bool Contains(GridVector2 pos, double epsilon)
        {
            //Find out if the rectangles can't possibly intersect
            if (pos.X >= this.Left - epsilon &&
               pos.Y >= this.Bottom - epsilon &&
               pos.X <= this.Right + epsilon &&
               pos.Y <= this.Top + epsilon)
                return true;

            return false;
        }

        public bool Contains(IPoint pos)
        {
            if(pos == null)
                throw new ArgumentNullException("pos");

            //Find out if the rectangles can't possibly intersect
            if (pos.X >= this.Left &&
               pos.Y >= this.Bottom &&
               pos.X <= this.Right &&
               pos.Y <= this.Top)
                return true;

            return false;
        }

        int? _HashCode;

        public override int GetHashCode()
        {

            Debug.Assert(!double.IsNaN(this.Left));

            if (!_HashCode.HasValue)
            {
                _HashCode = this.Center.GetHashCode(); 
            }

            return _HashCode.Value;
        }

        public override bool Equals(object obj)
        {
            return (GridRectangle)obj == this; 
        }

        public static bool operator ==(GridRectangle A, GridRectangle B)
        {
            return ((A.Left == B.Left) &&
                   (A.Right == B.Right) &&
                   (A.Top == B.Top) &&
                   (A.Bottom == B.Bottom));
        }

        public static bool operator !=(GridRectangle A, GridRectangle B)
        {
            return !(A == B);
        }

        #region Static Methods

        /// <summary>
        /// Returns a rectangle bounding the passed rectangles
        /// </summary>
        /// <param name="A"></param>
        /// <param name="B"></param>
        /// <returns></returns>
        static public GridRectangle Union(GridRectangle A, GridRectangle B)
        {
            double left = A.Left < B.Left ? A.Left : B.Left;
            double right = A.Right > B.Right ? A.Right : B.Right;
            double top = A.Top > B.Top ? A.Top : B.Top;
            double bottom = A.Bottom < B.Bottom ? A.Bottom : B.Bottom;

            return new GridRectangle(left, right, bottom, top); 
        }

        static public GridRectangle GetBoundingBox(GridVector2[] points)
        {
            double MinX = points.Min(v => v.X);
            double MinY = points.Min(v => v.Y);
            double MaxX = points.Max(v => v.X);
            double MaxY = points.Max(v => v.Y);

            return new GridRectangle(MinX, MaxX, MinY, MaxY);
        }

        public IShape2D Translate(IPoint2D offset)
        {
            return this.Translate(offset.Convert());
        }

        public GridRectangle Translate(GridVector2 offset)
        {
            return new GridRectangle(this.LowerLeft + offset, this.UpperRight + offset);
        }

        public object Clone()
        {
            return new GridRectangle(this.LowerLeft, this.Width, this.Height);
        }

        private GridLineSegment[] _Segments;
        public GridLineSegment[] Segments
        {
            get
            {
                if (_Segments == null)
                {
                    _Segments = new GridLineSegment[] { new GridLineSegment(LowerLeft, UpperLeft),
                                                        new GridLineSegment(UpperLeft, UpperRight),
                                                        new GridLineSegment(UpperRight, LowerRight),
                                                        new GridLineSegment(LowerRight, LowerLeft)};
                }
                return _Segments;
            }
        }

        #endregion
    }
}
