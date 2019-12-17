using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace Geometry
{
    /// <summary>
    /// Sorts points in clockwise order around a line from A to B, with A as the origin
    /// </summary>
    public class CompareAngle : IComparer<GridVector2>, IComparer<IPoint2D>
    {
        /// <summary>
        /// A line we are ordering points around by angle.  A is the origin.
        /// </summary>
        private GridLine Line;
        private GridVector2 ComparisonPoint;

        public readonly bool ClockwiseOrder = false;
        
        public CompareAngle(GridLineSegment line, bool clockwise = false)
        {
            Line = new GridLine(line.A, line.Direction);
            ComparisonPoint = Line.Origin + Line.Direction;
            ClockwiseOrder = clockwise;
        }

        public CompareAngle(GridLine line, bool clockwise = false)
        {
            Line = line;
            ComparisonPoint = Line.Origin + Line.Direction;
            ClockwiseOrder = clockwise;
        }

        public int Compare(GridVector2 A, GridVector2 B)
        {
            double angleA = GridVector2.ArcAngle(Line.Origin, A, ComparisonPoint);
            double angleB = GridVector2.ArcAngle(Line.Origin, B, ComparisonPoint);

            //We are measuring the angle from the line in one direction, so don't allow negative angles
            angleA = angleA < 0 ? angleA + (Math.PI * 2.0) : angleA;
            angleB = angleB < 0 ? angleB + (Math.PI * 2.0) : angleB;

            return ClockwiseOrder ? angleA.CompareTo(angleB) : angleB.CompareTo(angleA);
        }

        public int Compare(IPoint2D A, IPoint2D B)
        {
            double angleA = GridVector2.ArcAngle(Line.Origin, A, ComparisonPoint);
            double angleB = GridVector2.ArcAngle(Line.Origin, B, ComparisonPoint);

            //We are measuring the angle from the line in one direction, so don't allow negative angles
            angleA = angleA < 0 ? angleA + (Math.PI * 2.0) : angleA;
            angleB = angleB < 0 ? angleB + (Math.PI * 2.0) : angleB;

            return ClockwiseOrder ? angleA.CompareTo(angleB) : angleB.CompareTo(angleA);
        }
    }

    /// <summary>
    /// A line of infinite length
    /// </summary>
    /// 
    [Serializable]
    public struct GridLine
    {
        public readonly GridVector2 Origin;
        public readonly GridVector2 Direction;

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

            //if (seg == null)
            //    throw new ArgumentNullException("seg");

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
