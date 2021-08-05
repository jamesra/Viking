using Geometry.Graphics;
using System;

namespace Geometry
{
    /// <summary>
    /// Two triangles that make a rectangle
    /// </summary>
    /// 
    [Serializable]
    public class GridQuad
    {
        GridTriangle T0;
        GridTriangle T1;

        public GridQuad(GridVector2 pos, double Width, double Height)
            : this(pos, new GridVector2(pos.X + Width, pos.Y), new GridVector2(pos.X, pos.Y + Height), new GridVector2(pos.X + Width, pos.Y + Height))
        { }

        /// <summary>
        /// 
        /// </summary>
        /// <param name="p1">BottomLeft</param>
        /// <param name="p2">BottomRight</param>
        /// <param name="p3">TopLeft</param>
        /// <param name="p4">TopRight</param>
        /// <param name="color"></param>
        public GridQuad(GridVector2 p1, GridVector2 p2, GridVector2 p3, GridVector2 p4)
        {
           T0 = new GridTriangle(p1,p2,p3); 
           T1 = new GridTriangle(p2,p4,p3); 
        }

        public GridQuad(GridRectangle rect)
        {
            T0 = new GridTriangle(rect.LowerLeft, rect.LowerRight, rect.UpperLeft);
            T1 = new GridTriangle(rect.LowerRight, rect.UpperRight, rect.UpperLeft); 
        }
        

        public GridVector2 Center => new GridLineSegment(T0.p2, T0.p3).Bisect();
         
        public GridVector2 BottomLeft
        {
            get => T0.p1;
            set {
                T0 = new GridTriangle(value, T0.p2, T0.p3); 
            }
        }

        public GridVector2 BottomRight
        {
            get => T0.p2;
            set {
                T0 = new GridTriangle(T0.p1, value, T0.p3);
                T1 = new GridTriangle(value, T1.p2, T1.p3); 
            }
        }

        public GridVector2 TopLeft
        {
            get => T0.p3;
            set {
                T0 = new GridTriangle(T0.p1, T0.p3, value);
                T1 = new GridTriangle(T1.p1, T1.p2, value); 
            }
        }

        public GridVector2 TopRight
        {
            get => T1.p2;
            set {
                T1 = new GridTriangle(T1.p1,  value, T1.p3); 
            }
        }

        public void Scale(double scalar)
        {
            //Have to cache center because it changes as we update points
            GridVector2 center = this.Center;
            GridVector2 directionA = this.TopRight - center;
            GridVector2 directionB = this.TopLeft - center; 

            directionA *= scalar;
            directionB *= scalar;

            this.BottomLeft = center - directionA;
            this.TopRight = center + directionA;

            this.BottomRight = center - directionB;
            this.TopLeft = center + directionB;
        }

        public bool Contains(GridVector2 p)
        {
            if(this.T0.Contains(p))
                return true;
            if(this.T1.Contains(p))
                return true;

            return false; 
        }

        public bool Contains(GridRectangle R)
        {
            return Contains(new GridQuad(new GridVector2(R.Left, R.Bottom), R.Width, R.Height) ); 
        }

        public bool Contains(GridQuad R)
        {
            if (R == null)
                throw new ArgumentNullException("R");

            GridVector2 v1 = R.BottomLeft;
            GridVector2 v2 = R.BottomRight;
            GridVector2 v3 = R.TopRight;
            GridVector2 v4 = R.TopLeft;

            //If any verticies are in the quad we return true 
            if (T0.Contains(v1) || T0.Contains(v2) || T0.Contains(v3) || T0.Contains(v4) ||
                T1.Contains(v1) || T1.Contains(v2) || T1.Contains(v3) || T1.Contains(v4))
                return true;

            if (R.T0.Contains(TopLeft) || R.T0.Contains(TopRight) || R.T0.Contains(BottomLeft) || R.T0.Contains(BottomRight) ||
                R.T1.Contains(TopLeft) || R.T1.Contains(TopRight) || R.T1.Contains(BottomLeft) || R.T1.Contains(BottomRight))
                return true; 

            GridLineSegment RL1 = new GridLineSegment(v1, v2);
            GridLineSegment RL2 = new GridLineSegment(v2, v3);
            GridLineSegment RL3 = new GridLineSegment(v3, v4);
            GridLineSegment RL4 = new GridLineSegment(v4, v1);

            GridLineSegment L1 = new GridLineSegment(this.BottomLeft, this.BottomRight);
            GridLineSegment L2 = new GridLineSegment(this.BottomRight, this.TopRight);
            GridLineSegment L3 = new GridLineSegment(this.TopRight, this.TopLeft);
            GridLineSegment L4 = new GridLineSegment(this.TopLeft, this.BottomLeft);

            GridLineSegment[] RA = new GridLineSegment[4] { RL1, RL2, RL3, RL4 };
            GridLineSegment[] A = new GridLineSegment[4] { L1, L2, L3, L4 };

            GridVector2 outparam; 
            foreach (GridLineSegment RL in RA)
            {
                foreach (GridLineSegment L in A)
                {
                    if (RL.Intersects(L, out outparam))
                        return true; 
                }
            }

            return false; 
        }

        public double Area => T0.Area + T1.Area; 
    }
}
