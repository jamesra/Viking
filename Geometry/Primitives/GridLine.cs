using System;
using System.Collections.Generic;

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
        private readonly GridLine Line;
        private readonly GridVector2 ComparisonPoint;

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
            //We are measuring the angle from the line in one direction, so don't allow negative angles
            double angleA = GridVector2.AbsArcAngle(Line.Origin, A, ComparisonPoint, ClockwiseOrder);
            double angleB = GridVector2.AbsArcAngle(Line.Origin, B, ComparisonPoint, ClockwiseOrder);

            //return ClockwiseOrder ? angleA.CompareTo(angleB) : angleB.CompareTo(angleA);
            return angleA.CompareTo(angleB);
        }

        public int Compare(IPoint2D A, IPoint2D B)
        {
            //We are measuring the angle from the line in one direction, so don't allow negative angles

            double angleA = GridVector2.AbsArcAngle(Line.Origin, A, ComparisonPoint, ClockwiseOrder);
            double angleB = GridVector2.AbsArcAngle(Line.Origin, B, ComparisonPoint, ClockwiseOrder);

            //return ClockwiseOrder ? angleA.CompareTo(angleB) : angleB.CompareTo(angleA);
            return angleA.CompareTo(angleB);
        }
    }

    /// <summary>
    /// A line of infinite length
    /// </summary>
    /// 
    [Serializable]
    public readonly struct GridLine
    {
        public readonly GridVector2 Origin;
        public readonly GridVector2 Direction;

        public GridLine(GridVector2 O, GridVector2 dir)
        {
            Origin = O;
            this.Direction = GridVector2.Normalize(dir);
        }

        public GridLine(IPoint2D O, IPoint2D dir)
        {
            Origin = new GridVector2(O.X, O.Y);
            this.Direction = GridVector2.Normalize(new GridVector2(dir.X, dir.Y));
        }

        public override string ToString()
        {
            return $"Line Origin {Origin} Direction {Direction}";
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
                throw new ArgumentNullException(nameof(seg));

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

        /// <summary>
        /// Returns a line starting at origin of the specified length
        /// </summary>
        /// <param name="Length"></param>
        /// <returns></returns>
        public GridLineSegment ToLine(double Length)
        {
            GridVector2 endpoint = this.Direction * Length;
            endpoint += this.Origin;
            GridLineSegment output = new GridLineSegment(this.Origin, endpoint);
            System.Diagnostics.Debug.Assert(Math.Abs(output.Length - Length) < Global.Epsilon, "Created line does not match requested length");
            return output;
        }

        /// <summary>
        /// Return true if point p is to left when standing at A looking towards B
        /// </summary>
        /// <param name="p"></param>
        /// <returns> 1 for left
        ///           0 for on the line
        ///           -1 for right
        /// </returns>
        public int IsLeft(GridVector2 p)
        {
            double result = (Direction.X * (p.Y - Origin.Y)) - (Direction.Y * (p.X - Origin.X));
            if (result == 0)
                return 0;

            if (Math.Abs(result) < Global.EpsilonSquared)
            {
                //                if (GridVector2.Distance(p, A) < Global.Epsilon || GridVector2.Distance(p, B) < Global.Epsilon)
                //                  return 0; 
                GridTriangle tri;
                try
                {
                    tri = new GridTriangle(Origin, Origin + Direction, p);
                }
                catch (ArgumentException)
                {
                    return 0; //This means the points are on a line
                }

                if (double.IsNaN(tri.Area) || tri.Area == 0)
                    return 0;
            }

            return Math.Sign(result);
        }
    }
}
