using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Text;

namespace Geometry
{
    [Serializable]
    public readonly struct GridBox
    {
        public readonly double[] minVals;
        public readonly double[] maxVals;

        public double[] dimensions
        {
            get
            {
                var dims = new double[minVals.Length];
                for (int i = 0; i < minVals.Length; i++)
                {
                    dims[i] = maxVals[i] - minVals[i];
                }

                return dims;
            }
        }


        public double[] Center
        {
            get
            {
                var result = new double[minVals.Length];
                var dims = this.dimensions;
                for (int i = 0; i < minVals.Length; i++)
                {
                    result[i] = minVals[i] + (dims[i] / 2.0);
                }

                return result;
            }
        }

        public int numDims => minVals.Length;

        public override string ToString()
        {
            StringBuilder sb = new StringBuilder();

            sb.Append(minVals.ToCSV());

            sb.Append(" Dims: ");

            sb.Append(dimensions.ToCSV());

            return sb.ToString();
        }

        public double Width =>
                //Debug.Assert(Right - Left >= 0); 
                maxVals[(int)AXIS.X] - minVals[(int)AXIS.X];

        public double Height =>
                //Debug.Assert(Top - Bottom >= 0); 
                maxVals[(int)AXIS.Y] - minVals[(int)AXIS.Y];

        public double Depth =>
                //Debug.Assert(Top - Bottom >= 0); 
                maxVals[(int)AXIS.Z] - minVals[(int)AXIS.Z];


        public GridVector3 CenterPoint
        {
            get
            {
                double[] center = this.Center;
                return new GridVector3(Center[0], Center[1], Center[2]);
            }
        }

        public GridVector3 MinCorner => new GridVector3(minVals[0], minVals[1], minVals[2]);

        public GridVector3 MaxCorner => new GridVector3(maxVals[0], maxVals[1], maxVals[2]);

        public double Volume => dimensions.Aggregate((accumulator, val) => accumulator * val);

        private void ThrowOnNegativeDimensions()
        {
            if (this.dimensions.Any(val => val < 0))
            {
                throw new ArgumentException("GridBox must have non-negative width and height");
            }
        }

        private void ThrowOnMinGreaterThanMax()
        {
            if(dimensions.Any(d => d < 0))
                throw new ArgumentException("GridBox minvals must be greater than maxvals");
        }

        public GridBox(double[] mins, double[] maxs)
        {
            if (mins is null)
                throw new ArgumentNullException(nameof(mins));

            if (maxs is null)
                throw new ArgumentNullException(nameof(maxs));

            if (mins.Length != maxs.Length)
                throw new ArgumentException("mins and maxs parameters must have same array length");

            if (mins.Length < 1)
                throw new ArgumentException("mins and maxs parameters must have non-zero array length");

            //Copy the array in case the caller tries to re-use the array somewhere else.  Required for how I implemented the Clone function
            minVals = new double[mins.Length];
            maxVals = new double[maxs.Length];

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
        }

        public GridBox(GridVector3 bottomleft, double[] dimensions)
        {
            minVals = bottomleft.coords;
            maxVals = minVals.Select((val, i) => val + dimensions[i]).ToArray();

            ThrowOnNegativeDimensions();
            ThrowOnMinGreaterThanMax();
        }

        public GridBox(GridVector3 position, double radius)
        {
            minVals = position.coords.Select(val => val - radius).ToArray();
            maxVals = position.coords.Select(val => val + radius).ToArray();

            ThrowOnNegativeDimensions();
            ThrowOnMinGreaterThanMax();
        }

        public GridBox(IPoint position, double[] dimensions)
        {
            if (position == null)
                throw new ArgumentNullException(nameof(position));

            minVals = new double[] { position.X, position.Y, position.Z };
            maxVals = minVals.Select((val, i) => val + dimensions[i]).ToArray();

            ThrowOnNegativeDimensions();
            ThrowOnMinGreaterThanMax();
        }

        public GridBox(IPoint position, double radius)
        {
            if (position == null)
                throw new ArgumentNullException(nameof(position));

            minVals = new double[] { position.X - radius, position.Y - radius, position.Z - radius };
            maxVals = new double[] { position.X + radius, position.Y + radius, position.Z + radius };

            ThrowOnNegativeDimensions();
            ThrowOnMinGreaterThanMax();
        }

        public GridBox(GridRectangle bound_rect, double minZ, double maxZ)
        {
            minVals = new double[] { bound_rect.Left, bound_rect.Bottom, minZ };
            maxVals = new double[] { bound_rect.Right, bound_rect.Top, maxZ };

            ThrowOnNegativeDimensions();
            ThrowOnMinGreaterThanMax();
        }


        /// <summary>
        /// Scale outer dimensions without changing center point
        /// </summary>
        /// <param name="scalar"></param>
        public GridBox Scale(double scalar)
        {
            double[] scalars = new double[] { scalar, scalar, scalar };
            return this.Scale(scalars);
        }

        /// <summary>
        /// Scale outer dimensions without changing center point
        /// </summary>
        /// <param name="scalar"></param>
        public GridBox Scale(GridVector3 scalar)
        {
            double[] scalars = new double[] { scalar.X, scalar.Y, scalar.Z };
            return this.Scale(scalars);
        }

        /// <summary>
        /// Scale outer dimensions without changing center point
        /// </summary>
        /// <param name="scalar"></param>
        private GridBox Scale(double[] scalars)
        {
            Debug.Assert(scalars.Length == this.dimensions.Length, "Scalar dimension and shape dimension do not match");
            //Have to cache center because it changes as we update points
            double[] center = this.Center;
            double[] dimensions = this.dimensions;
            double[] new_corner_distance = dimensions.Select((dist, i) => ((dist / 2.0) * scalars[i])).ToArray();

            double[] new_mins = center.Select((c, i) => c - new_corner_distance[i]).ToArray();
            double[] new_maxs = center.Select((c, i) => c + new_corner_distance[i]).ToArray();

            return new GridBox(new_mins, new_maxs); 
        }

        public GridBox Translate(GridVector3 vector)
        {
            double[] translation = vector.coords;
            Debug.Assert(translation.Length == this.numDims, "Expecting 3D shape for translation with 3D vector");

            double[] translatedMins = this.minVals.Select((min, i) => min + translation[i]).ToArray();
            double[] translatedMaxs = this.maxVals.Select((max, i) => max + translation[i]).ToArray();

            return new GridBox(translatedMins, translatedMaxs);
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
        public GridBox Union(GridVector3 point, out bool boundsChanged)
        {
            return Union(point.coords, out boundsChanged);
        }

        /// <summary>
        /// Expands the rectange to contain the specified point.
        /// Returns true if the rectangle expands, otherwise false.
        /// </summary>
        /// <param name="point"></param>
        /// <returns></returns>
        public GridBox Union(IReadOnlyList<GridVector3> points, out bool boundsChanged)
        {
            GridBox bbox = points.BoundingBox();
            return Union(bbox, out boundsChanged);
        }

        public GridBox Union(double[] coords, out bool boundsChanged)
        {
            bool updated_minVals = this.minVals.Where((val, i) => coords[i] < val).Any();
            bool updated_maxVals = this.maxVals.Where((val, i) => coords[i] > val).Any();

            boundsChanged = updated_minVals || updated_maxVals;
            if(boundsChanged)
            {
                return new GridBox(minVals.Select((val, i) => Math.Min(val, coords[i])).ToArray(),
                    maxVals.Select((val, i) => Math.Max(val, coords[i])).ToArray());
            }
            else
            {
                return this.Clone();
            }
        }

        public GridBox Union(GridBox bbox, out bool boundsChanged)
        {
            GridBox result = this.Union(bbox.minVals, out var minChanged);
            result = result.Union(bbox.maxVals, out var maxChanged);
            boundsChanged = minChanged || maxChanged;
            return result;
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
                throw new ArgumentNullException(nameof(pos));

            return this.Contains(new double[] { pos.X, pos.Y, pos.Z });
        }

        private int CalcHashcode()
        {
            int hashcode = 0;
            if (minVals is null)
                return 0;

            foreach (var c in Center)
            {
                hashcode ^= c.GetHashCode();
            }

            return hashcode;
        }

        public override int GetHashCode()
        {
            //Debug.Assert(!double.IsNaN(this.minVals[(int)AXIS.X]));
            return CalcHashcode();
        }

        public override bool Equals(object obj)
        {
            if (obj is GridBox other)
                return this == other;

            return false;
        }

        public static bool operator ==(GridBox A, GridBox B)
        { 
            //Check for a default bbox
            if (A.minVals is null && B.minVals is null)
                return true;

            if (A.minVals is null || B.minVals is null)
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
        public static GridBox Union(GridBox A, GridBox B)
        {
            if (A.minVals is null && B.minVals is null)
                return default;

            if (A.minVals is null)
                return B.Clone();

            if (B.minVals is null)
                return A.Clone();

            double[] new_mins = A.minVals.Select((val, i) => Math.Min(val, B.minVals[i])).ToArray();
            double[] new_maxs = A.maxVals.Select((val, i) => Math.Max(val, B.maxVals[i])).ToArray();

            return new GridBox(new_mins, new_maxs);
        }

        public static GridBox GetBoundingBox(IEnumerable<GridVector3> points)
        {
            if (points == null)
                throw new ArgumentException("Bounding box cannot be created for null points collection");

            if (points.Any() == false || points.First() == null)
                throw new ArgumentException("Bounding box cannot be created for empty points collection");

            int numDims = points.First().coords.Length;
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
