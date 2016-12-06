using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Diagnostics;

namespace Geometry
{
    [Serializable]
    public class GridBox
    {
        public double[] minVals;
        public double[] maxVals;

        public double[] dimensions
        {
            get
            {
                return maxVals.Select((max, i) => maxVals[i] - minVals[i]).ToArray();
            }
        }

        public int numDims
        {
            get
            {
                return minVals.Count();
            }
        }

        public override string ToString()
        {
            StringBuilder sb = new StringBuilder();

            sb.Append(minVals.ToCSV());
            
            sb.Append(" Dims: ");

            sb.Append(dimensions.ToCSV());
            
            return sb.ToString();
        }

        public double Width
        {
            get
            {
                //Debug.Assert(Right - Left >= 0); 
                return maxVals[(int)AXIS.X] - minVals[(int)AXIS.X];
            }
        }

        public double Height
        {
            get
            {
                //Debug.Assert(Top - Bottom >= 0); 
                return maxVals[(int)AXIS.Y] - minVals[(int)AXIS.Y];
            }
        }

        public double Depth
        {
            get
            {
                //Debug.Assert(Top - Bottom >= 0); 
                return maxVals[(int)AXIS.Z] - minVals[(int)AXIS.Z];
            }
        }

        public double[] Center
        {
            get
            {
                return minVals.Select((val, i) => minVals[i] + (dimensions[i] / 2)).ToArray();
            }
        }

        public double Volume
        {
            get
            {
                return dimensions.Aggregate((accumulator, val) => accumulator * val);
            }

        }

        private void ThrowOnNegativeDimensions()
        {
            if (this.dimensions.Where(val => val < 0).Any())
            {
                throw new ArgumentException("GridBox must have non-negative width and height");
            }
        }

        private void ThrowOnMinGreaterThanMax()
        {
            if(this.maxVals.Where((val,i) => val < minVals[i]).Any())
            {
                throw new ArgumentException("GridBox minvals must be greater than maxvals");
            }
        }

        public GridBox(double[] mins, double[] maxs)
        {
            //Copy the array in case the caller tries to re-use the array somewhere else.  Required for how I implemented the Clone function
            minVals = new double[mins.Length];
            maxVals = new double[maxs.Length];
            _HashCode = new int?();

            mins.CopyTo(minVals, 0);
            maxs.CopyTo(maxVals, 0);

            ThrowOnNegativeDimensions();
            ThrowOnMinGreaterThanMax();
        }


        public GridBox(GridVector3 corner, GridVector3 oppositeCorner)
        {
            this.minVals = corner.coords.Select((val, i) => Math.Min(val, oppositeCorner.coords[i])).ToArray();
            this.maxVals = corner.coords.Select((val, i) => Math.Max(val, oppositeCorner.coords[i])).ToArray();

            ThrowOnNegativeDimensions();
            ThrowOnMinGreaterThanMax();

            _HashCode = new int?();
        }

        public GridBox(GridVector3 bottomleft, double[] dimensions)
        {
            minVals = bottomleft.coords;
            maxVals = minVals.Select((val, i) => val + dimensions[i]).ToArray();
            _HashCode = new int?();

            ThrowOnNegativeDimensions();
            ThrowOnMinGreaterThanMax();
        }

        public GridBox(GridVector3 position, double radius)
        {
            minVals = position.coords.Select(val => val - radius).ToArray();
            maxVals = position.coords.Select(val => val + radius).ToArray();
            _HashCode = new int?();

            ThrowOnNegativeDimensions();
            ThrowOnMinGreaterThanMax();
        }

        public GridBox(IPoint position, double[] dimensions)
        {
            if (position == null)
                throw new ArgumentNullException("points");

            minVals = new double[] { position.X, position.Y, position.Z };
            maxVals = minVals.Select((val, i) => val + dimensions[i]).ToArray();

            _HashCode = new int?();

            ThrowOnNegativeDimensions();
            ThrowOnMinGreaterThanMax();
        }

        public GridBox(IPoint position, double radius)
        {
            if (position == null)
                throw new ArgumentNullException("position");

            minVals = new double[] { position.X - radius, position.Y - radius, position.Z - radius };
            maxVals = new double[] { position.X + radius, position.Y + radius, position.Z + radius };

            _HashCode = new int?();

            ThrowOnNegativeDimensions();
            ThrowOnMinGreaterThanMax();
        }

        public GridBox(GridRectangle bound_rect, double minZ, double maxZ)
        {
            minVals = new double[] { bound_rect.Left, bound_rect.Bottom, minZ };
            maxVals = new double[] { bound_rect.Right, bound_rect.Top, minZ };

            _HashCode = new int?();

            ThrowOnNegativeDimensions();
            ThrowOnMinGreaterThanMax();
        }


        public void Scale(double scalar)
        {
            //Have to cache center because it changes as we update points
            double[] center = this.Center;
            double[] dimensions = this.dimensions;
            double[] new_corner_distance = dimensions.Select(dist => ((dist / 2.0) * scalar)).ToArray();

            double[] new_mins = center.Select((c, i) => c - new_corner_distance[i]).ToArray();
            double[] new_maxs = center.Select((c, i) => c + new_corner_distance[i]).ToArray();

            this.minVals = new_mins;
            this.maxVals = new_maxs;

            ThrowOnNegativeDimensions();
            ThrowOnMinGreaterThanMax();
        }

        /// <summary>
        /// Pad the requested amount onto the bounding box
        /// </summary>
        /// <param name="Radius"></param>
        /// <returns></returns>
        public GridBox Pad(double Radius)
        {
            double[] padded_minVals = this.minVals.Select(val => val - Radius).ToArray();
            double[] padded_maxVals = this.maxVals.Select(val => val + Radius).ToArray();

            return new GridBox(padded_minVals, padded_maxVals);
        }

        /// <summary>
        /// Returns true if the passed rectangle in inside or overlaps this rectangle
        /// </summary>
        /// <param name="rect"></param>
        /// <returns></returns>
        public bool Intersects(GridBox rect)
        {
            if (this.maxVals.Where((Val, i) => Val < rect.minVals[i]).Any())
                return false;

            if (this.minVals.Where((Val, i) => Val > rect.maxVals[i]).Any())
                return false;

            return true;
        }

        /// <summary>
        /// Expands the rectange to contain the specified point.
        /// Returns true if the rectangle expands, otherwise false.
        /// </summary>
        /// <param name="point"></param>
        /// <returns></returns>
        public bool Union(GridVector3 point)
        {
            return Union(point.coords);
        }

        public bool Union(double[] coords)
        {
            bool updated_minVals = this.minVals.Where((val, i) => coords[i] < val).Any();
            bool updated_maxVals = this.maxVals.Where((val, i) => coords[i] > val).Any();

            if (updated_minVals)
            {
                this.minVals = this.minVals.Select((val, i) => Math.Min(val, coords[i])).ToArray();
            }

            if (updated_maxVals)
            {
                this.maxVals = this.maxVals.Select((val, i) => Math.Max(val, coords[i])).ToArray();
            }

            return updated_maxVals || updated_minVals;
        }

        public bool Union(GridBox bbox)
        {
            bool llExpand = this.Union(bbox.minVals);
            bool urExpand = this.Union(bbox.maxVals);

            return llExpand || urExpand; //Cannot combine these or short-circuit execution will cancel one.
        }


        /// <summary>
        /// Returns true if the passed box is entirely inside this box
        /// </summary>
        /// <param name="rect"></param>
        /// <returns></returns>
        public bool Contains(GridBox rect)
        {
            bool MinOK = this.minVals.Select((val, i) => val <= rect.minVals[i]).All(b => b);
            bool MaxOK = this.maxVals.Select((val, i) => val >= rect.maxVals[i]).All(b => b);

            return MinOK && MaxOK;
        }

        public bool Contains(double[] coords, double epsilon = 0.0)
        {
            bool MinOK = this.minVals.Select((val, i) => val + epsilon <= coords[i]).All(b => b);
            bool MaxOK = this.maxVals.Select((val, i) => val - epsilon >= coords[i]).All(b => b);

            return MinOK && MaxOK;
        }

        public bool Contains(GridVector3 pos, double epsilon = 0.0)
        {
            bool MinOK = this.minVals.Select((val, i) => val + epsilon <= pos.coords[i]).All(b => b);
            bool MaxOK = this.maxVals.Select((val, i) => val - epsilon >= pos.coords[i]).All(b => b);

            return MinOK && MaxOK;
        }

        public bool Contains(IPoint pos)
        {
            if (pos == null)
                throw new ArgumentNullException("pos");

            return this.Contains(new double[] { pos.X, pos.Y, pos.Z });
        }

        int? _HashCode;

        public override int GetHashCode()
        {
            Debug.Assert(!double.IsNaN(this.minVals[(int)AXIS.X]));

            if (!_HashCode.HasValue)
            {
                _HashCode = (int)this.Center.Sum();
            }

            return _HashCode.Value;
        }

        public override bool Equals(object obj)
        {
            return (GridBox)obj == this;
        }

        public static bool operator ==(GridBox A, GridBox B)
        {
            if (System.Object.ReferenceEquals(A, B))
            {
                return true;
            }

            if ((object)A == null)
                return false;
            if ((object)B == null)
                return false;

            bool mins_match = A.minVals.Select((val, i) => val == B.minVals[i]).All(b => b);
            bool maxs_match = A.maxVals.Select((val, i) => val == B.maxVals[i]).All(b => b);

            return mins_match && maxs_match;
        }

        public static bool operator !=(GridBox A, GridBox B)
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
        static public GridBox Union(GridBox A, GridBox B)
        {
            double[] new_mins = A.minVals.Select((val, i) => Math.Min(val, B.minVals[i])).ToArray();
            double[] new_maxs = A.maxVals.Select((val, i) => Math.Max(val, B.maxVals[i])).ToArray();

            return new GridBox(new_mins, new_maxs);
        }

        static public GridBox GetBoundingBox(GridVector3[] points)
        {
            int numDims = points[0].coords.Count();
            double[] new_mins = new double[numDims];
            double[] new_maxs = new double[numDims];

            for (int iAxis = 0; iAxis < numDims; iAxis++)
            {
                new_mins[iAxis] = points.Min(p => p.coords[iAxis]);
                new_maxs[iAxis] = points.Max(p => p.coords[iAxis]);
            }

            return new GridBox(new_mins, new_maxs);
        }

        public GridBox Clone()
        {
            return new GridBox(this.minVals, this.maxVals);
        }

        #endregion
    }
}
