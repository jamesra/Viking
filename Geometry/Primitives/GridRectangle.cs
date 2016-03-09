using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Diagnostics; 
using Geometry; 

namespace Geometry
{
    [Serializable]
    public struct GridRectangle
    {
        public double Left;
        public double Right;
        /// <summary>
        /// Top has a larger value than bottom
        /// </summary>
        public double Top;

        /// <summary>
        /// Bottom has a smaller value than top
        /// </summary>
        public double Bottom;

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

         

        public GridRectangle(double left, double right,  double bottom, double top)
        {
            
            Left = left;
            Bottom = bottom;
            Top = top;
            Right = right;
            _HashCode = new int?();

            if (!double.IsNaN(Left))
            {
                Debug.Assert(Left <= Right && Bottom <= Top, "Grid Rectable argument error");
            }
        }

        public GridRectangle(GridVector2 position, double width, double height)
        {
            Left = position.X;
            Bottom = position.Y;
            Top = Bottom + height;
            Right = Left + width;
            _HashCode = new int?();
            Debug.Assert(Left <= Right && Bottom <= Top, "Grid Rectable argument error"); 
        }

        public GridRectangle(GridVector2 position, double radius)
        {
            Left = position.X - radius;
            Bottom = position.Y - radius;
            Top = position.Y + radius;
            Right = position.X + radius;
            _HashCode = new int?();

            Debug.Assert(Left <= Right && Bottom <= Top, "Grid Rectable argument error"); 
        } 

        public GridRectangle(IPoint position, double width, double height)
        {
            if(position == null)
                throw new ArgumentNullException("points");

            Left = position.X;
            Bottom = position.Y; 
            Top = Bottom + height; 
            Right = Left + width;
            _HashCode = new int?();

            Debug.Assert(Left <= Right && Bottom <= Top, "Grid Rectable argument error"); 
        }

        public GridRectangle(IPoint position, double radius)
        {
            if(position == null)
                throw new ArgumentNullException("position");

            Left = position.X - radius;
            Bottom = position.Y - radius;
            Top = position.Y + radius;
            Right = position.X + radius;
            _HashCode = new int?();

            Debug.Assert(Left <= Right && Bottom <= Top, "Grid Rectable argument error"); 
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

            Debug.Assert(Left < Right && Bottom < Top, "Grid Rectable argument error"); 
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

        /// <summary>
        /// Expands the rectange to contain the specified point.
        /// Returns true if the rectangle expands, otherwise false.
        /// </summary>
        /// <param name="point"></param>
        /// <returns></returns>
        public bool Union(GridVector2 point)
        {
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
            return this.Union(rect.LowerLeft) || this.Union(rect.UpperRight); 
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
        
        public bool Contains(GridVector2 pos)
        {
            //Find out if the rectangles can't possibly intersect
            if (pos.X >= this.Left &&
               pos.Y >= this.Bottom &&
               pos.X <= this.Right &&
               pos.Y <= this.Top)
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

        #endregion
    }
}
