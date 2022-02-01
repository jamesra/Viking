using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Runtime.Serialization;
using System.Text;
using System.Threading;

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

        private GridRectangle _ControlBounds = new GridRectangle();
        public GridRectangle ControlBounds
        {
            get
            {
                if (_ControlBounds.Width <= 0)
                {
                    _ControlBounds = this.MapPoints.ControlBounds();
                }

                return _ControlBounds;
            }
            protected set
            {
                _ControlBounds = value;
            }
        }

        private GridRectangle _MappedBounds = new GridRectangle();
        public GridRectangle MappedBounds
        {
            get
            {
                if (_MappedBounds.Width <= 0)
                {
                    _MappedBounds = this.MapPoints.MappedBounds();
                }

                return _MappedBounds;
            }
            protected set
            {
                _MappedBounds = value;
            }
        }

        /// <summary>
        /// List of points that define transform.  Triangles are derived from these points.  They should be populated at creation.  They may
        /// be replaced during a transformation with a new list, which requires regenerating triangles and any other derived data.
        /// These points are sorted by control point x, lowest to highest
        /// </summary>
        private MappingGridVector2[] _mapPoints = Array.Empty<MappingGridVector2>();
        public MappingGridVector2[] MapPoints
        {
            get { return _mapPoints; }
            protected set
            {
                //SortedSet<MappingGridVector2> listPoints = new SortedSet<MappingGridVector2>(value);
                Array.Sort(value);
                _mapPoints = value;
#if DEBUG
                DebugVerifyPointsAreUnique(_mapPoints);
#endif

                //Reset the bounds
                MappedBounds = new GridRectangle();
                ControlBounds = new GridRectangle();
            }
        }

        private static void DebugVerifyPointsAreUnique(MappingGridVector2[] listPoints)
        {
#if DEBUG
            //Check for duplicate points
            for (int i = 1; i < listPoints.Length; i++)
            {
                Debug.Assert(listPoints[i - 1].ControlPoint != listPoints[i].ControlPoint, "Duplicate Points found in transform.  This breaks Delaunay.");
                Debug.Assert(listPoints[i - 1].MappedPoint != listPoints[i].MappedPoint, "Duplicate Points found in transform.  This breaks Delaunay.");
            }
#endif
        }

        protected ReferencePointBasedTransform(MappingGridVector2[] points, TransformBasicInfo info)
        {
            //List<MappingGridVector2> listPoints = new List<MappingGridVector2>(points);
            //MappingGridVector2.RemoveDuplicates(listPoints);

            //this.MapPoints = listPoints.ToArray();
            this.MapPoints = points;
            this.Info = info;
        }

        protected ReferencePointBasedTransform(MappingGridVector2[] points, GridRectangle mappedBounds, TransformBasicInfo info)
            : this(points, info)
        {
            this.MappedBounds = mappedBounds;
        }

        protected ReferencePointBasedTransform(MappingGridVector2[] points, GridRectangle mappedBounds, GridRectangle controlBounds, TransformBasicInfo info)
        {
            this.MapPoints = points;
            this.MappedBounds = mappedBounds;
            this.ControlBounds = controlBounds;
            this.Info = info;
        }

        protected ReferencePointBasedTransform(SerializationInfo info, StreamingContext context)
        {
            if (info == null)
                throw new ArgumentNullException(nameof(info));

            _mapPoints = info.GetValue("_mapPoints", typeof(MappingGridVector2[])) as MappingGridVector2[];
            this.Info = info.GetValue("Info", typeof(TransformBasicInfo)) as TransformBasicInfo;
            MappedBounds = (GridRectangle)info.GetValue("MappedBounds", typeof(GridRectangle));
            ControlBounds = (GridRectangle)info.GetValue("ControlBounds", typeof(GridRectangle));
        }

        public virtual void GetObjectData(SerializationInfo info, StreamingContext context)
        {
            if (info == null)
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
        public void Translate(GridVector2 vector)
        {
            for (int i = 0; i < MapPoints.Length; i++)
            {
                MapPoints[i].ControlPoint += vector;
            }

            //Remove any cached data structures
            //MinimizeMemory();

            ControlBounds = new GridRectangle(ControlBounds.Left + vector.X,
                                              ControlBounds.Right + vector.X,
                                              ControlBounds.Bottom + vector.Y,
                                              ControlBounds.Top + vector.Y);
        }

        public static GridRectangle CalculateControlBounds(ITransformControlPoints[] transforms)
        {
            if (transforms == null || transforms.Length == 0)
                return new GridRectangle();

            double minX = double.MaxValue;
            double minY = double.MaxValue;
            double maxX = double.MinValue;
            double maxY = double.MinValue;

            foreach (ITransformControlPoints T in transforms)
            {
                GridRectangle R = T.ControlBounds;

                if (R.Left < minX)
                    minX = R.Left;
                if (R.Right > maxX)
                    maxX = R.Right;
                if (R.Bottom < minY)
                    minY = R.Bottom;
                if (R.Top > maxY)
                    maxY = R.Top;
            }

            return new GridRectangle(minX, maxX, minY, maxY);
        }

        public static GridRectangle CalculateMappedBounds(ITransformControlPoints[] transforms)
        {
            if (transforms == null || transforms.Length == 0)
                return new GridRectangle();

            double minX = double.MaxValue;
            double minY = double.MaxValue;
            double maxX = double.MinValue;
            double maxY = double.MinValue;

            foreach (ITransformControlPoints T in transforms)
            {
                GridRectangle R = T.MappedBounds;

                if (R.Left < minX)
                    minX = R.Left;
                if (R.Right > maxX)
                    maxX = R.Right;
                if (R.Bottom < minY)
                    minY = R.Bottom;
                if (R.Top > maxY)
                    maxY = R.Top;
            }

            return new GridRectangle(minX, maxX, minY, maxY);
        }

        /// <summary>
        /// Return Control points intersecting the rectangle
        /// </summary>
        /// <param name="gridRect"></param>
        /// <returns></returns>
        public List<MappingGridVector2> IntersectingControlRectangle(in GridRectangle gridRect) => this.controlPointsRTree.Intersects(gridRect).ToList();

        /// <summary>
        /// Return mapped control points intersecting the rectangle
        /// </summary>
        /// <param name="gridRect"></param>
        /// <returns></returns>
        public List<MappingGridVector2> IntersectingMappedRectangle(in GridRectangle gridRect) => this.mappedPointsRTree.Intersects(gridRect).ToList();


        /// <summary>
        /// You need to take this lock when building or changing the QuadTrees managing the triangles of the mesh
        /// </summary>
        [NonSerialized]
        readonly ReaderWriterLockSlim rwLockTriangles = new ReaderWriterLockSlim();

        private RTree.RTree<MappingGridVector2> _mappedPointsRTree = null;

        /// <summary>
        /// Quadtree mapping mapped points to triangles that contain the points
        /// </summary>
        public RTree.RTree<MappingGridVector2> mappedPointsRTree
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
                    if (_mappedPointsRTree == null)
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

        private RTree.RTree<MappingGridVector2> _controlPointsRTree = null;

        /// <summary>
        /// Quadtree mapping control points to triangles that contain the points
        /// </summary>
        public RTree.RTree<MappingGridVector2> controlPointsRTree
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
                    if (_controlPointsRTree == null)
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

                this._mappedPointsRTree = new RTree.RTree<MappingGridVector2>();
                this._controlPointsRTree = new RTree.RTree<MappingGridVector2>();

                for (int i = 0; i < this.MapPoints.Length; i++)
                {
                    MappingGridVector2 mp = this._mapPoints[i];
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
        /// Save a transform using the itk transform text format.  Any reference point transform can be a mesh, so we default to that representation
        /// </summary>
        /// <param name="stream"></param>
        public virtual void WriteITKTransform(System.IO.StreamWriter stream)
        {
            if (stream == null)
                throw new ArgumentNullException(nameof(stream));

            double Downsample = 1.0;

            StringBuilder output = new StringBuilder();
            string transform = "meshtransform_double_2_2";

            output.Append("0\n0\n");
            //output += string.Format("{0:g} {1:g} {2:g} {3:g}\n", ControlBounds.Left, ControlBounds.Bottom, ControlBounds.Right, ControlBounds.Top);
            //output += string.Format("{0:g} {1:g} {2:g} {3:g}\n", MappedBounds.Left, MappedBounds.Bottom, MappedBounds.Right, MappedBounds.Top);
            output.AppendFormat("{0:g} {1:g} {2:g} {3:g}\n", 0, 0, ControlBounds.Width / Downsample, ControlBounds.Height / Downsample);
            //output += string.Format("{0:g} {1:g} {2:g} {3:g}\n", 0, 0, MappedBounds.Width, MappedBounds.Height);

            output.AppendFormat("{0:g} {1:g} {2:g} {3:g}\n", MappedBounds.Left / Downsample, MappedBounds.Bottom / Downsample, MappedBounds.Width / Downsample, MappedBounds.Height / Downsample);

            output.Append(transform + " vp ");
            output.AppendFormat("{0:d}", this.MapPoints.Length * 4);

            foreach (MappingGridVector2 p in this.MapPoints)
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

            stream.Write(output.ToString());
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
    }
}
