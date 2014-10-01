using System;
using System.Collections.Generic;
using System.Text;
using System.Diagnostics;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;

namespace Util
{
    public class LineSegment
    {
        public Vector2 A;
        public Vector2 B;

        public LineSegment(Vector2 A, Vector2 B)
        {
            this.A = A;
            this.B = B;

            Debug.Assert(A != B, "Can't create line with two identical points"); 
        }

        public float Length
        {
            get
            {
                double d1 = (A.X - B.X);
                d1 = d1 * d1;
                double d2 = (A.Y - B.Y);
                d2 = d2 * d2; 

                return (float)Math.Sqrt(d1+d2); 
            }
        }

        /// <summary>
        /// Return true if either point at each end of the line matches an endpoint of the passed segment
        /// </summary>
        /// <param name="seg"></param>
        /// <param name="Endpoint"></param>
        /// <returns></returns>
        public bool SharedEndPoint(LineSegment seg, out Vector2 Endpoint)
        {
            Debug.Assert(false, "SharedEndPoint not implemented"); 
            Endpoint = new Vector2();
            return false; 
   //         if(seg.X + Global.epsilon < A.X 
        }

        public Vector2 Bisect()
        {
            float x = (A.X + B.X) / 2.0f;
            float y = (A.Y + B.Y) / 2.0f;

            return new Vector2(x, y); 
        }

        public bool Intersects(LineSegment seg, out Vector2 Intersection)
        {
            //Function for each line
            //Ax + By = C

            double A1 = B.Y - A.Y;
            double A2 = seg.B.Y - seg.A.Y;

            double B1 = A.X - B.X;
            double B2 = seg.A.X - seg.B.X;
            
            double C1 = A1*A.X + B1 * A.Y; 
            double C2 = A2*seg.A.X + B2 * seg.A.Y; 

            double det = A1 * B2 - A2 * B1;
            //Check if lines are parallel
            if (det == 0)
            {
                Intersection = new Vector2();
                return false;
            }
            else
            {
                double x = (B2 * C1 - B1 * C2) / det;
                double y = (A1 * C2 - A2 * C1) / det;

                Intersection.X = (float)x;
                Intersection.Y = (float)y;

                double minX = Math.Min(A.X, B.X) - Global.epsilon;
                double minSegX = Math.Min(seg.A.X, seg.B.X) - Global.epsilon;

                if (minX > x || minSegX > x)
                    return false;

                double maxX = Math.Max(A.X, B.X) + Global.epsilon; 
                double maxSegX = Math.Max(seg.A.X, seg.B.X) + Global.epsilon;

                if (maxX < x || maxSegX < x)
                    return false;

                double minY = Math.Min(A.Y, B.Y)- Global.epsilon;
                double minSegY = Math.Min(seg.A.Y, seg.B.Y) - Global.epsilon;

                if (minY > y || minSegY > y)
                    return false;

                double maxY = Math.Max(A.Y, B.Y) + Global.epsilon;
                double maxSegY = Math.Max(seg.A.Y, seg.B.Y) + Global.epsilon;

                if (maxY < y || maxSegY < y)
                    return false;

                return true;
            }
        }
    }
}
