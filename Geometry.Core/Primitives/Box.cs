using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Text;

namespace Geometry
{
    /// <summary>Axis-aligned 3D box. <see cref="MinVals"/> and <see cref="MaxVals"/> return copies.</summary>
    [Serializable]
    public readonly struct Box : IBox3D
    {
        readonly double[] _minVals;
        readonly double[] _maxVals;

        /// <summary>Copy of the inclusive minimum corner. Callers cannot mutate the box by writing this array. Null when the box is uninitialized.</summary>
        public double[] MinVals => _minVals is null ? null : (double[])_minVals.Clone();

        /// <summary>Copy of the inclusive maximum corner. Callers cannot mutate the box by writing this array. Null when the box is uninitialized.</summary>
        public double[] MaxVals => _maxVals is null ? null : (double[])_maxVals.Clone();


        public double[] dimensions
        {
            get
            {
                var dims = new double[_minVals.Length];
                for (int i = 0; i < _minVals.Length; i++)
                {
                    dims[i] = _maxVals[i] - _minVals[i];
                }

                return dims;
            }
        }


        public double[] Center
        {
            get
            {
                var result = new double[_minVals.Length];
                var dims = this.dimensions;
                for (int i = 0; i < _minVals.Length; i++)
                {
                    result[i] = _minVals[i] + (dims[i] / 2.0);
                }

                return result;
            }
        }

        public int DimensionCount => _minVals.Length;

        public override string ToString()
        {
            StringBuilder sb = new();

            sb.Append(_minVals.ToCSV());

            sb.Append(" Dims: ");

            sb.Append(dimensions.ToCSV());

            return sb.ToString();
        }

        public double Width =>
                //Debug.Assert(Right - Left >= 0); 
                _maxVals[(int)Axis.X] - _minVals[(int)Axis.X];

        public double Height =>
                //Debug.Assert(Top - Bottom >= 0); 
                _maxVals[(int)Axis.Y] - _minVals[(int)Axis.Y];

        public double Depth =>
                //Debug.Assert(Top - Bottom >= 0); 
                _maxVals[(int)Axis.Z] - _minVals[(int)Axis.Z];


        public Vector3 CenterPoint
        {
            get
            {
                double[] center = this.Center;
                return new Vector3(Center[0], Center[1], Center[2]);
            }
        }

        public Vector3 MinCorner => new(_minVals[0], _minVals[1], _minVals[2]);

        public Vector3 MaxCorner => new(_maxVals[0], _maxVals[1], _maxVals[2]);

        public double Volume => dimensions.Aggregate((accumulator, val) => accumulator * val);

        private void ThrowOnNegativeDimensions()
        {
            if (this.dimensions.Any(val => val < 0))
            {
                throw new ArgumentException("Box must have non-negative width and height");
            }
        }

        private void ThrowOnMinGreaterThanMax()
        {
            if (dimensions.Any(d => d < 0))
                throw new ArgumentException("Box minvals must be greater than maxvals");
        }

        public Box(double[] mins, double[] maxs)
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
            _minVals = new double[mins.Length];
            _maxVals = new double[maxs.Length];

            mins.CopyTo(_minVals, 0);
            maxs.CopyTo(_maxVals, 0);

            ThrowOnNegativeDimensions();
            ThrowOnMinGreaterThanMax();
        }


        public Box(Vector3 corner, Vector3 oppositeCorner)
        {
            this._minVals = [.. corner.Coords.Select((val, i) => Math.Min(val, oppositeCorner.Coords[i]))];
            this._maxVals = [.. corner.Coords.Select((val, i) => Math.Max(val, oppositeCorner.Coords[i]))];

            ThrowOnNegativeDimensions();
            ThrowOnMinGreaterThanMax();
        }

        public Box(Vector3 bottomleft, double[] dimensions)
        {
            _minVals = bottomleft.Coords;
            _maxVals = [.. _minVals.Select((val, i) => val + dimensions[i])];

            ThrowOnNegativeDimensions();
            ThrowOnMinGreaterThanMax();
        }

        public Box(Vector3 position, double radius)
        {
            _minVals = [.. position.Coords.Select(val => val - radius)];
            _maxVals = [.. position.Coords.Select(val => val + radius)];

            ThrowOnNegativeDimensions();
            ThrowOnMinGreaterThanMax();
        }

        public Box(IPoint3D position, double[] dimensions)
        {
            if (position is null)
                throw new ArgumentNullException(nameof(position));

            _minVals = [position.X, position.Y, position.Z];
            _maxVals = [.. _minVals.Select((val, i) => val + dimensions[i])];

            ThrowOnNegativeDimensions();
            ThrowOnMinGreaterThanMax();
        }

        public Box(IPoint3D position, double radius)
        {
            if (position is null)
                throw new ArgumentNullException(nameof(position));

            _minVals = [position.X - radius, position.Y - radius, position.Z - radius];
            _maxVals = [position.X + radius, position.Y + radius, position.Z + radius];

            ThrowOnNegativeDimensions();
            ThrowOnMinGreaterThanMax();
        }

        public Box(Rectangle bound_rect, double minZ, double maxZ)
        {
            _minVals = [bound_rect.Left, bound_rect.Bottom, minZ];
            _maxVals = [bound_rect.Right, bound_rect.Top, maxZ];

            ThrowOnNegativeDimensions();
            ThrowOnMinGreaterThanMax();
        }


        /// <summary>Scale outer dimensions without changing the center.</summary>
        public Box Scale(double scalar)
        {
            double[] scalars = [scalar, scalar, scalar];
            return this.Scale(scalars);
        }

        /// <summary>Scale outer dimensions without changing the center.</summary>
        public Box Scale(Vector3 scalar)
        {
            double[] scalars = [scalar.X, scalar.Y, scalar.Z];
            return this.Scale(scalars);
        }

        /// <summary>Scale outer dimensions without changing the center.</summary>
        private Box Scale(double[] scalars)
        {
            Debug.Assert(scalars.Length == this.dimensions.Length, "Scalar dimension and shape dimension do not match");
            //Have to cache center because it changes as we update points
            double[] center = this.Center;
            double[] dimensions = this.dimensions;
            double[] new_corner_distance = [.. dimensions.Select((dist, i) => ((dist / 2.0) * scalars[i]))];

            double[] new_mins = [.. center.Select((c, i) => c - new_corner_distance[i])];
            double[] new_maxs = [.. center.Select((c, i) => c + new_corner_distance[i])];

            return new Box(new_mins, new_maxs);
        }

        public Box Translate(Vector3 vector)
        {
            double[] translation = vector.Coords;
            Debug.Assert(translation.Length == this.DimensionCount, "Expecting 3D shape for translation with 3D vector");

            double[] translatedMins = [.. this._minVals.Select((min, i) => min + translation[i])];
            double[] translatedMaxs = [.. this._maxVals.Select((max, i) => max + translation[i])];

            return new Box(translatedMins, translatedMaxs);
        }

        /// <summary>
        /// Pad the requested amount onto the bounding box
        /// </summary>
        /// <param name="Radius"></param>
        /// <returns></returns>
        public Box Pad(double Radius)
        {
            double[] padded_minVals = [.. this._minVals.Select(val => val - Radius)];
            double[] padded_maxVals = [.. this._maxVals.Select(val => val + Radius)];

            return new Box(padded_minVals, padded_maxVals);
        }

        /// <summary>
        /// Returns true if the passed rectangle in inside or overlaps this rectangle
        /// </summary>
        /// <param name="rect"></param>
        /// <returns></returns>
        public bool Intersects(Box rect)
        {
            if (this._maxVals.Where((Val, i) => Val < rect._minVals[i]).Any())
                return false;

            if (this._minVals.Where((Val, i) => Val > rect._maxVals[i]).Any())
                return false;

            return true;
        }

        /// <summary>
        /// Expands the rectange to contain the specified point.
        /// Returns true if the rectangle expands, otherwise false.
        /// </summary>
        /// <param name="point"></param>
        /// <returns></returns>
        public Box Union(Vector3 point, out bool boundsChanged) => Union(point.Coords, out boundsChanged);

        /// <summary>
        /// Expands the rectange to contain the specified point.
        /// Returns true if the rectangle expands, otherwise false.
        /// </summary>
        /// <param name="point"></param>
        /// <returns></returns>
        public Box Union(IReadOnlyList<Vector3> points, out bool boundsChanged)
        {
            Box bbox = points.BoundingBox();
            return Union(bbox, out boundsChanged);
        }

        public Box Union(double[] coords, out bool boundsChanged)
        {
            bool updated_minVals = this._minVals.Where((val, i) => coords[i] < val).Any();
            bool updated_maxVals = this._maxVals.Where((val, i) => coords[i] > val).Any();

            boundsChanged = updated_minVals || updated_maxVals;
            if (boundsChanged)
            {
                return new Box([.. _minVals.Select((val, i) => Math.Min(val, coords[i]))],
                    [.. _maxVals.Select((val, i) => Math.Max(val, coords[i]))]);
            }
            else
            {
                return this.Clone();
            }
        }

        public Box Union(Box bbox, out bool boundsChanged)
        {
            Box result = this.Union(bbox._minVals, out var minChanged);
            result = result.Union(bbox._maxVals, out var maxChanged);
            boundsChanged = minChanged || maxChanged;
            return result;
        }


        /// <summary>
        /// Returns true if the passed box is entirely inside this box
        /// </summary>
        /// <param name="rect"></param>
        /// <returns></returns>
        public bool Contains(Box rect)
        {
            bool MinOK = this._minVals.Select((val, i) => val <= rect._minVals[i]).All(b => b);
            bool MaxOK = this._maxVals.Select((val, i) => val >= rect._maxVals[i]).All(b => b);

            return MinOK && MaxOK;
        }

        public bool Contains(double[] coords, double epsilon = 0.0)
        {
            bool MinOK = this._minVals.Select((val, i) => val + epsilon <= coords[i]).All(b => b);
            bool MaxOK = this._maxVals.Select((val, i) => val - epsilon >= coords[i]).All(b => b);

            return MinOK && MaxOK;
        }

        public bool Contains(Vector3 pos, double epsilon = 0.0)
        {
            bool MinOK = this._minVals.Select((val, i) => val + epsilon <= pos.Coords[i]).All(b => b);
            bool MaxOK = this._maxVals.Select((val, i) => val - epsilon >= pos.Coords[i]).All(b => b);

            return MinOK && MaxOK;
        }

        public bool Contains(IPoint3D pos)
        {
            if (pos is null)
                throw new ArgumentNullException(nameof(pos));

            return this.Contains([pos.X, pos.Y, pos.Z]);
        }

        private int CalcHashcode()
        {
            int hashcode = 0;
            if (_minVals is null)
                return 0;

            foreach (var c in Center)
            {
                hashcode ^= c.GetHashCode();
            }

            return hashcode;
        }

        public override int GetHashCode() =>
            //Debug.Assert(!double.IsNaN(this._minVals[(int)Axis.X]));
            CalcHashcode();

        public override bool Equals(object obj)
        {
            if (obj is Box other)
                return this == other;

            return false;
        }

        public static bool operator ==(Box A, Box B)
        {
            //Check for a default bbox
            if (A._minVals is null && B._minVals is null)
                return true;

            if (A._minVals is null || B._minVals is null)
                return false;

            bool mins_match = A._minVals.Select((val, i) => val == B._minVals[i]).All(b => b);
            bool maxs_match = A._maxVals.Select((val, i) => val == B._maxVals[i]).All(b => b);

            return mins_match && maxs_match;
        }

        public static bool operator !=(Box A, Box B) => !(A == B);

        #region Static Methods

        /// <summary>
        /// Returns a rectangle bounding the passed rectangles
        /// </summary>
        /// <param name="A"></param>
        /// <param name="B"></param>
        /// <returns></returns>
        public static Box Union(Box A, Box B)
        {
            if (A._minVals is null && B._minVals is null)
                return default;

            if (A._minVals is null)
                return B.Clone();

            if (B._minVals is null)
                return A.Clone();

            double[] new_mins = [.. A._minVals.Select((val, i) => Math.Min(val, B._minVals[i]))];
            double[] new_maxs = [.. A._maxVals.Select((val, i) => Math.Max(val, B._maxVals[i]))];

            return new Box(new_mins, new_maxs);
        }

        public static Box GetBoundingBox(IEnumerable<Vector3> points)
        {
            if (points is null)
                throw new ArgumentException("Bounding box cannot be created for null points collection");

            if (!points.Any())
                throw new ArgumentException("Bounding box cannot be created for empty points collection");

            int DimensionCount = points.First().Coords.Length;
            double[] new_mins = new double[DimensionCount];
            double[] new_maxs = new double[DimensionCount];

            for (int iAxis = 0; iAxis < DimensionCount; iAxis++)
            {
                new_mins[iAxis] = points.Min(p => p.Coords[iAxis]);
                new_maxs[iAxis] = points.Max(p => p.Coords[iAxis]);
            }

            return new Box(new_mins, new_maxs);
        }

        public Box Clone() => new Box(this._minVals, this._maxVals);

        IPoint3D IBox3D.Min => MinCorner;

        IPoint3D IBox3D.Max => MaxCorner;

        bool IBox3D.Contains(in IPoint3D p) => Contains(p);

        IBox3D IBox3D.Translate(in IPoint3D offset) => Translate(new Vector3(offset.X, offset.Y, offset.Z));

        public bool Equals(IBox3D other)
        {
            if (other is null)
                return false;
            if (other is Box box)
                return this == box;
            return MinCorner.Equals(other.Min) && MaxCorner.Equals(other.Max);
        }

        #endregion
    }
}
