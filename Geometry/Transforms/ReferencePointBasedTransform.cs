using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Runtime.Serialization;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace Geometry.Transforms
{
    [Serializable]
    public abstract class ReferencePointBasedTransform : IITKSerialization, ITransformInfo, ITransformControlPoints, ISerializable, IMemoryMinimization
    {
        public TransformBasicInfo Info { get; set; }

        public override string ToString()
        {
            if (Info != null)
                return Info.ToString();
            else
                return "Transform Base, No Info";
        }

        private Rectangle _ControlBounds = new();
        public Rectangle ControlBounds
        {
            get
            {
                if (_ControlBounds.Width <= 0)
                {
                    _ControlBounds = this.MapPoints.ControlBounds();
                }

                return _ControlBounds;
            }
            protected set => _ControlBounds = value;
        }

        private Rectangle _MappedBounds = new();
        public Rectangle MappedBounds
        {
            get
            {
                if (_MappedBounds.Width <= 0)
                {
                    _MappedBounds = this.MapPoints.MappedBounds();
                }

                return _MappedBounds;
            }
            protected set => _MappedBounds = value;
        }

        /// <summary>
        /// List of points that define transform.  Triangles are derived from these points.  They should be populated at creation.  They may
        /// be replaced during a transformation with a new list, which requires regenerating triangles and any other derived data.
        /// These points are sorted by control point x, lowest to highest
        /// </summary>
        private MappingVector2[] _mapPoints = [];
        public MappingVector2[] MapPoints
        {
            get { return _mapPoints; }
            protected set => AssignMapPoints(value, sortPoints: true);
        }

        protected void AssignMapPoints(MappingVector2[] value, bool sortPoints)
        {
            if (sortPoints)
                Array.Sort(value);
            _mapPoints = value;

            var _mapPointsList = _mapPoints.ToList();
            bool ArrayHadDuplicates = false;
            ArrayHadDuplicates = ArrayHadDuplicates || MappingVector2.RemoveControlSpaceDuplicates(_mapPointsList);
            ArrayHadDuplicates = ArrayHadDuplicates || MappingVector2.RemoveMappedSpaceDuplicates(_mapPointsList);

            //Replace our map points with the list that had duplicates removed if necessary
            if (ArrayHadDuplicates)
            {
                _mapPoints = [.. _mapPointsList];
                if (_mapPoints.Length < 3)
                    throw new ArgumentException("Not enough control points after duplicates removed");
            }

#if DEBUG
            DebugVerifyPointsAreUnique(_mapPoints);
#endif

            //Reset the bounds
            MappedBounds = new Rectangle();
            ControlBounds = new Rectangle();
        }

        private static bool DebugVerifyPointsAreUnique(MappingVector2[] listPoints)
        {
#if DEBUG
            //Check for duplicate points
            for (int i = 1; i < listPoints.Length; i++)
            {
                Debug.Assert(listPoints[i - 1].ControlPoint != listPoints[i].ControlPoint, $"Duplicate control space points found in transform.  This breaks Delaunay. Point #{i-1} and #{i}");
                Debug.Assert(listPoints[i - 1].MappedPoint != listPoints[i].MappedPoint, $"Duplicate mapped space points found in transform.  This breaks Delaunay. Point #{i - 1} and #{i}");
            }

            return true;
#else
            return true;
#endif
        }

        protected ReferencePointBasedTransform(MappingVector2[] points, TransformBasicInfo info)
        {
            AssignMapPoints(points, sortPoints: true);
            this.Info = info;
        }

        protected ReferencePointBasedTransform(MappingVector2[] points, TransformBasicInfo info, bool preserveMapPointOrder)
        {
            AssignMapPoints(points, sortPoints: !preserveMapPointOrder);
            this.Info = info;
        }

        protected ReferencePointBasedTransform(MappingVector2[] points, Rectangle mappedBounds, TransformBasicInfo info)
            : this(points, info)
        {
            this.MappedBounds = mappedBounds;
        }

        protected ReferencePointBasedTransform(MappingVector2[] points, Rectangle mappedBounds, TransformBasicInfo info, bool preserveMapPointOrder)
            : this(points, info, preserveMapPointOrder)
        {
            this.MappedBounds = mappedBounds;
        }

        protected ReferencePointBasedTransform(MappingVector2[] points, Rectangle mappedBounds, Rectangle controlBounds, TransformBasicInfo info)
        {
            this.MapPoints = points;
            this.MappedBounds = mappedBounds;
            this.ControlBounds = controlBounds;
            this.Info = info;
        }

        protected ReferencePointBasedTransform(SerializationInfo info, StreamingContext context)
        {
            if (info is null)
                throw new ArgumentNullException(nameof(info));

            _mapPoints = info.GetValue("_mapPoints", typeof(MappingVector2[])) as MappingVector2[];
            this.Info = info.GetValue("Info", typeof(TransformBasicInfo)) as TransformBasicInfo;
            MappedBounds = (Rectangle)info.GetValue("MappedBounds", typeof(Rectangle));
            ControlBounds = (Rectangle)info.GetValue("ControlBounds", typeof(Rectangle));
        }

        public virtual void GetObjectData(SerializationInfo info, StreamingContext context)
        {
            if (info is null)
                throw new ArgumentNullException(nameof(info));

            info.AddValue("_mapPoints", _mapPoints);
            info.AddValue("MappedBounds", MappedBounds);
            info.AddValue("ControlBounds", ControlBounds);
            info.AddValue("Info", this.Info);
        }

        /// <summary>
        /// Translates all verticies in the tile according to the vector
        /// </summary>
        /// <param name="vector"></param>
        public void Translate(Vector2 vector)
        {
            for (int i = 0; i < MapPoints.Length; i++)
            {
                var p = MapPoints[i];
                MapPoints[i] = new MappingVector2(p.ControlPoint + vector, p.MappedPoint);
            }

            //Remove any cached data structures
            //MinimizeMemory();

            ControlBounds = new Rectangle(ControlBounds.Left + vector.X,
                                              ControlBounds.Right + vector.X,
                                              ControlBounds.Bottom + vector.Y,
                                              ControlBounds.Top + vector.Y);
        }

        public static Rectangle CalculateControlBounds(ITransformControlPoints[] transforms)
        {
            if (transforms is null || transforms.Length == 0)
                return new Rectangle();

            double minX = double.MaxValue;
            double minY = double.MaxValue;
            double maxX = double.MinValue;
            double maxY = double.MinValue;

            foreach (ITransformControlPoints T in transforms)
            {
                Rectangle R = T.ControlBounds;

                if (R.Left < minX)
                    minX = R.Left;
                if (R.Right > maxX)
                    maxX = R.Right;
                if (R.Bottom < minY)
                    minY = R.Bottom;
                if (R.Top > maxY)
                    maxY = R.Top;
            }

            return new Rectangle(minX, maxX, minY, maxY);
        }

        public static Rectangle CalculateMappedBounds(ITransformControlPoints[] transforms)
        {
            if (transforms is null || transforms.Length == 0)
                return new Rectangle();

            double minX = double.MaxValue;
            double minY = double.MaxValue;
            double maxX = double.MinValue;
            double maxY = double.MinValue;

            foreach (ITransformControlPoints T in transforms)
            {
                Rectangle R = T.MappedBounds;

                if (R.Left < minX)
                    minX = R.Left;
                if (R.Right > maxX)
                    maxX = R.Right;
                if (R.Bottom < minY)
                    minY = R.Bottom;
                if (R.Top > maxY)
                    maxY = R.Top;
            }

            return new Rectangle(minX, maxX, minY, maxY);
        }

        /// <summary>
        /// Return Control points intersecting the rectangle
        /// </summary>
        /// <param name="gridRect"></param>
        /// <returns></returns>
        public List<MappingVector2> IntersectingControlRectangle(in Rectangle gridRect) => [.. this.controlPointsRTree.Intersects(gridRect.ToRTreeRect(0))];

        /// <summary>
        /// Return mapped control points intersecting the rectangle
        /// </summary>
        /// <param name="gridRect"></param>
        /// <returns></returns>
        public List<MappingVector2> IntersectingMappedRectangle(in Rectangle gridRect) => [.. this.mappedPointsRTree.Intersects(gridRect.ToRTreeRect(0))];


        /// <summary>
        /// You need to take this lock when building or changing the QuadTrees managing the triangles of the mesh
        /// </summary>
        [NonSerialized]
        readonly ReaderWriterLockSlim rwLockTriangles = new();

        private RTree.RTree<MappingVector2> _mappedPointsRTree = null;

        /// <summary>
        /// Quadtree mapping mapped points to triangles that contain the points
        /// </summary>
        public RTree.RTree<MappingVector2> mappedPointsRTree
        {
            get
            {
                //Try the read lock first since only one thread can be in upgradeable mode
                try
                {
                    rwLockTriangles.EnterReadLock();
                    if (_mappedPointsRTree != null)
                    {
                        return _mappedPointsRTree;
                    }
                }
                finally
                {
                    if (rwLockTriangles.IsReadLockHeld)
                        rwLockTriangles.ExitReadLock();
                }

                //_mapTriangles was null, so get in line to populate it
                try
                {
                    rwLockTriangles.EnterUpgradeableReadLock();
                    if (_mappedPointsRTree is null)
                        BuildPointRTree(); //Locks internally

                    Debug.Assert(_mappedPointsRTree != null);
                    return _mappedPointsRTree;
                }
                finally
                {
                    if (rwLockTriangles.IsUpgradeableReadLockHeld)
                        rwLockTriangles.ExitUpgradeableReadLock();
                }
            }
        }

        private RTree.RTree<MappingVector2> _controlPointsRTree = null;

        /// <summary>
        /// Quadtree mapping control points to triangles that contain the points
        /// </summary>
        public RTree.RTree<MappingVector2> controlPointsRTree
        {
            get
            {
                //Try the read lock first since only one thread can be in upgradeable mode
                try
                {
                    rwLockTriangles.EnterReadLock();
                    if (_controlPointsRTree != null)
                    {
                        return _controlPointsRTree;
                    }
                }
                finally
                {
                    if (rwLockTriangles.IsReadLockHeld)
                        rwLockTriangles.ExitReadLock();
                }

                //_mapTriangles was null, so get in line to populate it
                try
                {
                    rwLockTriangles.EnterUpgradeableReadLock();
                    if (_controlPointsRTree is null)
                        BuildPointRTree(); //Locks internally

                    Debug.Assert(_controlPointsRTree != null);
                    return _controlPointsRTree;
                }
                finally
                {
                    if (rwLockTriangles.IsUpgradeableReadLockHeld)
                        rwLockTriangles.ExitUpgradeableReadLock();
                }
            }
        }

        protected void BuildPointRTree()
        {
            try
            {
                rwLockTriangles.EnterWriteLock();

                this._mappedPointsRTree = new RTree.RTree<MappingVector2>();
                this._controlPointsRTree = new RTree.RTree<MappingVector2>();

                for (int i = 0; i < this.MapPoints.Length; i++)
                {
                    MappingVector2 mp = this._mapPoints[i];
                    this._mappedPointsRTree.Add(mp.MappedPoint.ToRTreeRect(0), mp);
                    this._controlPointsRTree.Add(mp.ControlPoint.ToRTreeRect(0), mp);
                }
            }
            finally
            {
                if (rwLockTriangles.IsWriteLockHeld)
                    rwLockTriangles.ExitWriteLock();
            }
        }


        /// <summary>
        /// Express the transform using the itk transform text format.  Any reference point transform can be a mesh, so we default to that representation
        /// </summary>
        /// <param name="stream"></param>
        public virtual string GetITKTransform()
        {
            double Downsample = 1.0;

            StringBuilder output = new();
            string transform = "meshtransform_double_2_2";

            output.Append("0\n0\n");
            //output += string.Format("{0:g} {1:g} {2:g} {3:g}\n", ControlBounds.Left, ControlBounds.Bottom, ControlBounds.Right, ControlBounds.Top);
            //output += string.Format("{0:g} {1:g} {2:g} {3:g}\n", MappedBounds.Left, MappedBounds.Bottom, MappedBounds.Right, MappedBounds.Top);
            output.AppendFormat("{0:g} {1:g} {2:g} {3:g}\n", 0, 0, ControlBounds.Width / Downsample, ControlBounds.Height / Downsample);
            //output += string.Format("{0:g} {1:g} {2:g} {3:g}\n", 0, 0, MappedBounds.Width, MappedBounds.Height);

            output.AppendFormat("{0:g} {1:g} {2:g} {3:g}\n", MappedBounds.Left / Downsample, MappedBounds.Bottom / Downsample, MappedBounds.Width / Downsample, MappedBounds.Height / Downsample);

            output.Append(transform + " vp ");
            output.AppendFormat("{0:d}", this.MapPoints.Length * 4);

            foreach (MappingVector2 p in this.MapPoints)
            {
                output.AppendFormat(" {0:g} {1:g} {2:g} {3:g}",
                                        (p.MappedPoint.X - MappedBounds.Left) / MappedBounds.Width,
                                        (p.MappedPoint.Y - MappedBounds.Bottom) / MappedBounds.Height,
                                        (p.ControlPoint.X) / Downsample,
                                        (p.ControlPoint.Y) / Downsample);
            }

            output.Append(" fp 8 0 0 0 ");
            output.AppendFormat("{0:g} {1:g} {2:g} {3:g}", MappedBounds.Left / Downsample, MappedBounds.Bottom / Downsample, MappedBounds.Width / Downsample, MappedBounds.Height / Downsample);
            //output += string.Format("{0:g} {1:g} {2:g} {3:g}", 0,0, MappedBounds.Width, MappedBounds.Height);

            output.AppendFormat(" {0:d}\n", this.MapPoints.Length);

            return output.ToString();
        }

        public virtual void MinimizeMemory()
        {
            try
            {
                rwLockTriangles.EnterWriteLock();
                this._controlPointsRTree = null;
                this._mappedPointsRTree = null;
            }
            finally
            {
                if (rwLockTriangles.IsWriteLockHeld)
                    rwLockTriangles.ExitWriteLock();
            }
        }

        public abstract Vector2 Transform(in Vector2 Point);
        public abstract Vector2[] Transform(in Vector2[] Points);
        public abstract Vector2 InverseTransform(in Vector2 Point);
        public abstract Vector2[] InverseTransform(in Vector2[] Points);
        public abstract bool CanTransform(in Vector2 Point);
        public abstract bool TryTransform(in Vector2 Point, out Vector2 v);
        public abstract bool[] TryTransform(in Vector2[] Points, out Vector2[] v);
        public abstract bool CanInverseTransform(in Vector2 Point);
        public abstract bool TryInverseTransform(in Vector2 Point, out Vector2 v);
        public abstract bool[] TryInverseTransform(in Vector2[] Points, out Vector2[] v);
    }
}
