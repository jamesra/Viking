using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace Geometry
{
    /// <summary>
    /// A line of infinite length
    /// </summary>
    /// 
    [Serializable]
    public class GridLine
    {
        GridVector2 Origin;
        GridVector2 Direction;

        public GridLine(GridVector2 O, GridVector2 dir)
        {
            Origin = O;
            this.Direction = GridVector2.Normalize(dir);
        }

        public bool Intersects(GridLine seg, out GridVector2 Intersection)
        { 
            //Function for each line
            //Ax + By = C
            Intersection = new GridVector2();

            if (seg == null)
                throw new ArgumentNullException("seg");

            if (this.Direction == seg.Direction)
                return false; 

            GridVector2 A = Origin;
            GridVector2 B = Origin + Direction;

            GridVector2 segA = seg.Origin;
            GridVector2 segB = seg.Origin + seg.Direction; 

            double A1 = B.Y - A.Y;
            double A2 = segB.Y - segA.Y;

            double B1 = A.X - B.X;
            double B2 = segA.X - segB.X;

            double C1 = A1 * A.X + B1 * A.Y;
            double C2 = A2 * segA.X + B2 * segA.Y;

            double det = A1 * B2 - A2 * B1;
            //Check if lines are parallel
            if (det == 0)
            { 
                return false;
            }
            else
            {
                double x = (B2 * C1 - B1 * C2) / det;
                double y = (A1 * C2 - A2 * C1) / det;

                Intersection = new GridVector2(x, y);
                return true;
            }
        }

        public GridLine Perpendicular()
        {
            return new GridLine(this.Origin, GridVector2.Rotate90(Direction));
        }

        public bool Intersects(GridLineSegment seg, out GridVector2 Intersection)
        { 
            //Function for each line
            //Ax + By = C
            Intersection = new GridVector2();

            if (seg == null)
                throw new ArgumentNullException("seg");

            if (this.Direction == seg.Direction)
                return false;

            GridVector2 A = Origin;
            GridVector2 B = Origin + Direction;

            GridVector2 segA = seg.A;
            GridVector2 segB = seg.B;

            double A1 = B.Y - A.Y;
            double A2 = segB.Y - segA.Y;

            double B1 = A.X - B.X;
            double B2 = segA.X - segB.X;

            double C1 = A1 * A.X + B1 * A.Y;
            double C2 = A2 * segA.X + B2 * segA.Y;

            double det = A1 * B2 - A2 * B1;
            //Check if lines are parallel
            if (det == 0)
            { 
                return false;
            }
            else
            {
                double x = (B2 * C1 - B1 * C2) / det;
                double y = (A1 * C2 - A2 * C1) / det;

                Intersection = new GridVector2(x, y);

                return seg.BoundingBox.Contains(Intersection);
            }
        }
         


    }
}
