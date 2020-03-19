using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace Geometry
{
    /// <summary>
    /// A triangle class that uses indicies into a list of points to record it's node positions.
    /// </summary>
    [Serializable]
    public class GridIndexTriangle
    {
        /// <summary>
        /// THIS IS A REFERENCE.  DO NOT CHANGE ANY VALUES IN THIS ARRAY
        /// </summary>
        readonly GridVector2[] points;

        public readonly int i1;
        public readonly int i2;
        public readonly int i3;

        private GridCircle _Circle; 

        public GridCircle Circle
        {
            get{
                if(_Circle.Radius == 0)
                    GridCircle.CircleFromThreePoints(points[i1], points[i2], points[i3], ref _Circle);

                return _Circle; 
            }
        }

        public GridIndexTriangle(int index1, int index2, int index3, ref GridVector2[] pointArray)
        {
            i1 = index1;
            i2 = index2;
            i3 = index3;
            this.points = pointArray;
        }

        public int[] Indicies()
        {
            return new int[] { i1, i2, i3 }; 
        }

        public static implicit operator GridTriangle(GridIndexTriangle t)
        {
            if (t == null)
                throw new ArgumentNullException("t");


            return new GridTriangle(t.points[t.i1], t.points[t.i2], t.points[t.i3]); 
        }

        public GridVector2 P1
        {
            get { return points[i1]; }
        }

        public GridVector2 P2
        {
            get { return points[i2]; }
        }

        public GridVector2 P3
        {
            get { return points[i3]; }
        }
    }
}
