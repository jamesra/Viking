using System;
using System.Collections.Generic;
using System.Text;
using System.Diagnostics;
using System.Threading;
using System.Runtime.Serialization; 

namespace Geometry
{
    /// <summary>
    /// This is the parent class of all transforms.  All transforms, even continuos, are implemented by placing a grid 
    /// of control points on the mapped and control section.  
    /// </summary>
    [Serializable]
    public class GridTransform : TransformBase, ICloneable, ISerializable
    {
        /// <summary>
        /// Section the transform maps to
        /// </summary>
        public int ControlSection = 0;
        protected GridRectangle ControlBounds = new GridRectangle();

        /// <summary>
        /// Section the transform maps from
        /// </summary>
        public int MappedSection = 1;
        protected GridRectangle MappedBounds = new GridRectangle();

        /// <summary>
        /// List of points that define transform.  Triangles are derived from these points.  They should be populated at creation.  They may
        /// be replaced during a transformation with a new list, which requires regenerating triangles and any other derived data.
        /// These points are sorted by control point x, lowest to highest
        /// </summary>
        private MappingGridVector2[] _mapPoints = new MappingGridVector2[0];

        public MappingGridVector2[] mapPoints
        {
            get
            {
                return _mapPoints; 
            }
            set
            {
                List<MappingGridVector2> listPoints = new List<MappingGridVector2>(value);
                listPoints.Sort();

#if DEBUG
                //Check for duplicate points
                for (int i = 1; i < listPoints.Count; i++)
                {
                    Debug.Assert(listPoints[i - 1].ControlPoint != listPoints[i].ControlPoint, "Duplicate Points found in transform.  This breaks Delaunay.");
                }
#endif 
                _mapPoints = listPoints.ToArray();

                if (_mapPoints.Length > 0)
                {
                    this._CachedMappedBounds = MappingGridVector2.CalculateMappedBounds(this.mapPoints);
                    this._CachedControlBounds = MappingGridVector2.CalculateControlBounds(this.mapPoints);
                    CalculateTriangles();
                    CalculateEdges();
                }
            }
        }

        #region Triangles

        protected void CalculateTriangles()
        {
            //Use Delaunay to find the triangles
            GridVector2[] points = new GridVector2[mapPoints.Length];
            for (int i = 0; i < mapPoints.Length; i++)
            {
                points[i] = mapPoints[i].ControlPoint;
            }

            int[] triangles = Delaunay.Triangulate(points, CachedControlBounds);
            _TriangleIndiciesCache = triangles;
        }

        /// <summary>
        /// Use this to set points and triangles so we don't have to run the delaunay algorithm.
        /// </summary>
        /// <param name="points"></param>
        /// <param name="triangleIndicies"></param>
        public void SetPointsAndTriangles(MappingGridVector2[] points, int[] triangleIndicies)
        {
#if DEBUG
            //Check for duplicate points
            for (int i = 1; i < points.Length; i++)
            {
                Debug.Assert(points[i - 1].ControlPoint != points[i].ControlPoint, "Duplicate Points found in transform.  This breaks Delaunay.");
            }
#endif 
            _mapPoints = points;
            _TriangleIndiciesCache = triangleIndicies;

            this._CachedMappedBounds = MappingGridVector2.CalculateMappedBounds(this.mapPoints);
            this._CachedControlBounds = MappingGridVector2.CalculateControlBounds(this.mapPoints);
        }
        
        /// <summary>
        /// This stores the output of the Delaunay triangulation.  Every group of three integers represents a triangle
        /// </summary>
        private int[] _TriangleIndiciesCache = null;

        public int[] TriangleIndicies
        {
            get
            {
                if (_TriangleIndiciesCache == null)
                    CalculateTriangles(); 

                return _TriangleIndiciesCache; 
            }
        }

        /// <summary>
        /// You need to take this lock when building or changing the QuadTrees managing the triangles of the mesh
        /// </summary>
        ReaderWriterLockSlim rwLockTriangles = new ReaderWriterLockSlim(); 
        private QuadTree<List<MappingGridTriangle>> _mapTriangles = null; 

        /// <summary>
        /// Quadtree mapping mapped points to triangles that contain the points
        /// </summary>
        public QuadTree<List<MappingGridTriangle>> mapTriangles
        {
            get
            {
                //Try the read lock first since only one thread can be in upgradeable mode
                try
                {
                    rwLockTriangles.EnterReadLock();
                    if (_mapTriangles != null)
                    {
                        return _mapTriangles; 
                    }
                }
                finally
                {
                    if(rwLockTriangles.IsReadLockHeld)
                        rwLockTriangles.ExitReadLock(); 
                }

                //_mapTriangles was null, so get in line to populate it
                try
                {
                    rwLockTriangles.EnterUpgradeableReadLock();
                    if(_mapTriangles == null)
                        BuildTriangleQuadTree(); //Locks internally

                    Debug.Assert(_mapTriangles != null);
                    return _mapTriangles;
                }
                finally
                {
                    if(rwLockTriangles.IsUpgradeableReadLockHeld)
                        rwLockTriangles.ExitUpgradeableReadLock(); 
                }
            }
        }

        private QuadTree<List<MappingGridTriangle>> _controlTriangles = null;

        /// <summary>
        /// Quadtree mapping control points to triangles that contain the points
        /// </summary>
        public QuadTree<List<MappingGridTriangle>> controlTriangles
        {
            get
            {
                //Try the read lock first since only one thread can be in upgradeable mode
                try
                {
                    rwLockTriangles.EnterReadLock();
                    if (_controlTriangles != null)
                    {
                        return _controlTriangles;
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
                    if (_controlTriangles == null)
                        BuildTriangleQuadTree(); //Locks internally

                    Debug.Assert(_controlTriangles != null);
                    return _controlTriangles;
                }
                finally
                {
                    if (rwLockTriangles.IsUpgradeableReadLockHeld)
                        rwLockTriangles.ExitUpgradeableReadLock();
                }
            }
        }

        List<MappingGridTriangle>[] _TriangleList;
        List<MappingGridTriangle>[] TriangleList
        {
            get
            {
                if (_TriangleList == null)
                {
                    BuildTriangleList();
                }
                Debug.Assert(_TriangleList != null);
                return _TriangleList;
            }
        }

        protected void BuildTriangleList()
        {
            if (_TriangleList != null)
                return;

            _TriangleList = new List<MappingGridTriangle>[this._mapPoints.Length];

            for (int i = 0; i < _TriangleIndiciesCache.Length; i += 3)
            {
                int iOne = _TriangleIndiciesCache[i];
                int iTwo = _TriangleIndiciesCache[i + 1];
                int iThree = _TriangleIndiciesCache[i + 2];

                //Safe to go straight into the cache since we looked at TriangleIndicies to initialize list
                MappingGridTriangle newTri = new MappingGridTriangle(mapPoints,
                                                     _TriangleIndiciesCache[i],
                                                     _TriangleIndiciesCache[i + 1],
                                                     _TriangleIndiciesCache[i + 2]);

                //Get the list for each point and add a reference to the triangle

                if (_TriangleList[iOne] == null)
                {
                    _TriangleList[iOne] = new List<MappingGridTriangle>(6);
                }
                _TriangleList[iOne].Add(newTri);

                if (_TriangleList[iTwo] == null)
                {
                    _TriangleList[iTwo] = new List<MappingGridTriangle>(6);
                }
                _TriangleList[iTwo].Add(newTri);

                if (_TriangleList[iThree] == null)
                {
                    _TriangleList[iThree] = new List<MappingGridTriangle>(6);
                }
                _TriangleList[iThree].Add(newTri);
            }
        }

        //
        /// <summary>
        /// Build a quad tree for both mapping and control triangles, takes the rwLockTriangles write lock
        /// </summary>
        protected void BuildTriangleQuadTree()
        {
            try
            {
                rwLockTriangles.EnterWriteLock();

                //Don't rebuild if they already exist, someone probably calculated them while we waited for the lock
                if(_mapTriangles != null)
                    return; 

                GridVector2[] quadMapPoints = new GridVector2[mapPoints.Length];
                GridVector2[] quadControlPoints = new GridVector2[mapPoints.Length];
                //Build the list map points
                for(int i = 0; i < mapPoints.Length; i++)
                {
                    quadMapPoints[i] = mapPoints[i].MappedPoint;
                    quadControlPoints[i] = mapPoints[i].ControlPoint;
                }

                //Build the quad tree for mapping points
                _mapTriangles = new QuadTree<List<MappingGridTriangle>>(quadMapPoints, TriangleList, CachedMappedBounds);

                //Build the quad tree for control points
                _controlTriangles = new QuadTree<List<MappingGridTriangle>>(quadControlPoints, TriangleList, CachedControlBounds);
            }
            finally 
            {
                if(rwLockTriangles.IsWriteLockHeld)
                    rwLockTriangles.ExitWriteLock();
            }
        }

        public List<MappingGridVector2> IntersectingControlRectangle(GridRectangle gridRect,  bool IncludeAdjacent)
        {
            List<MappingGridVector2> foundPoints = IntersectingRectangle(gridRect, this.controlTriangles);
            if (!IncludeAdjacent)
            {
                for (int i = 0; i < foundPoints.Count; i++)
                {
                    if (!gridRect.Contains(foundPoints[i].ControlPoint))
                    {
                        foundPoints.RemoveAt(i);
                        i--;
                    }
                }
            }

            return foundPoints;
        }

        public List<MappingGridVector2> IntersectingMappedRectangle(GridRectangle gridRect, bool IncludeAdjacent)
        {
            List<MappingGridVector2> foundPoints = IntersectingRectangle(gridRect, this.mapTriangles);
            if (!IncludeAdjacent)
            {
                for (int i = 0; i < foundPoints.Count; i++)
                {
                    if (!gridRect.Contains(foundPoints[i].MappedPoint))
                    {
                        foundPoints.RemoveAt(i);
                        i--;
                    }
                }
            }

            return foundPoints;
        }

        /// <summary>
        /// Returns all points inside the requested region.  
        /// If include adjacent is set to true we include points with an edge that crosses the border of the requested rectangle
        /// </summary>
        /// <param name="gridRect"></param>
        /// <returns></returns>
        private List<MappingGridVector2> IntersectingRectangle(GridRectangle gridRect,
                                                               QuadTree<List<MappingGridTriangle>> PointTree)
        {
            List<GridVector2> Points; 
            List<List<MappingGridTriangle>> ListofListTriangles;

            List<MappingGridVector2> MappingPointList= null;

            if (gridRect.Contains(PointTree.Border))
            {
                MappingPointList = new List<MappingGridVector2>(_mapPoints);
                return MappingPointList; 
            }

            PointTree.Intersect(gridRect, out Points, out ListofListTriangles);

            bool[] Added = new bool[_mapPoints.Length];
            MappingPointList = new List<MappingGridVector2>(Points.Count * 2);
            List<List<MappingGridTriangle>> MappingTriangleList = new List<List<MappingGridTriangle>>(Points.Count * 2);

            //Add all the unique points bordering the requested rectangle
            for (int iPoint = 0; iPoint < Points.Count; iPoint++)
            {
                List<MappingGridTriangle> FoundTriangleList = ListofListTriangles[iPoint];
                for (int iTri = 0; iTri < FoundTriangleList.Count; iTri++)
                {
                    MappingGridTriangle Triangle = FoundTriangleList[iTri];
                    if (!Added[Triangle.N1])
                    {
                        Added[Triangle.N1] = true;
                        MappingPointList.Add(this._mapPoints[Triangle.N1]);
                        MappingTriangleList.Add(this._TriangleList[Triangle.N1]);
                    }
                    if (!Added[Triangle.N2])
                    {
                        Added[Triangle.N2] = true;
                        MappingPointList.Add(this._mapPoints[Triangle.N2]);
                        MappingTriangleList.Add(this._TriangleList[Triangle.N1]);
                    }
                    if (!Added[Triangle.N3])
                    {
                        Added[Triangle.N3] = true;
                        MappingPointList.Add(this._mapPoints[Triangle.N3]);
                        MappingTriangleList.Add(this._TriangleList[Triangle.N1]);
                    }
                }
            }

            return MappingPointList; 
            

           
        }

#endregion

        #region Edges

        ReaderWriterLockSlim rwLockEdges = new ReaderWriterLockSlim();
        /// <summary>
        /// Each key value contains a list of indicies which are map points connected to this node
        /// </summary>
        Dictionary<int, List<int>> _edges = null;

        /// <summary>
        /// This is called from multiple threads so this data needs to be read-only
        /// </summary>
        /// <param name="iNode"></param>
        /// <returns></returns>
        public List<int> GetEdges(int iNode)
        {
            //Try read lock first since only one thread can be in upgradeable mode
            try
            {
                rwLockEdges.EnterReadLock();
                if (_edges != null)
                    return _edges[iNode];
            }
            finally
            {
                if (rwLockEdges.IsReadLockHeld)
                    rwLockEdges.ExitReadLock();
            }

            //Get in line to populate _edges
            try
            {
                rwLockEdges.EnterUpgradeableReadLock();
                if (_edges == null)
                {
                    CalculateEdges();
                }

                return _edges[iNode];
            }
            finally
            {
                if (rwLockEdges.IsUpgradeableReadLockHeld)
                    rwLockEdges.ExitUpgradeableReadLock();
            }
        }

        public Dictionary<int, List<int>> Edges
        {
            get
            {
                 //Try read lock first since only one thread can be in upgradeable mode
                 try
                 {
                    rwLockEdges.EnterReadLock();
                    if (_edges != null)
                        return _edges; 
                 }
                 finally
                 {
                     if (rwLockEdges.IsReadLockHeld)
                         rwLockEdges.ExitReadLock();
                 }

                 //Get in line to populate _edges
                 try
                 {
                    rwLockEdges.EnterUpgradeableReadLock();
                    if (_edges == null)
                        CalculateEdges();

                    return _edges;
                 }
                 finally
                 {
                    if (rwLockEdges.IsUpgradeableReadLockHeld)
                        rwLockEdges.ExitUpgradeableReadLock();
                 }
            }
        }

        private PairedLineSearchGrid _LineSegmentGrid; 

        protected void CalculateEdges()
        {
            try
            {
                rwLockEdges.EnterWriteLock();

                //In this case someone went through this routine ahead of us exit if the data structure is built
                if (_edges != null)
                    return;

                _edges = new Dictionary<int, List<int>>();

                List<int> EdgeIndicies = new List<int>(8);
                for (int i = 0; i < this.TriangleIndicies.Length; i += 3)
                {
                    int iOne = TriangleIndicies[i];
                    int iTwo = TriangleIndicies[i + 1];
                    int iThree = TriangleIndicies[i + 2];

                    //Add edges to the lists for each node
                    if (!_edges.ContainsKey(iOne))
                        _edges.Add(iOne, new List<int>(new int[] { iTwo, iThree }));
                    else
                    {
                        //Doing this the long way to avoid the new operator which slows down threads
                        List<int> listOne = _edges[iOne];
                        listOne.Add(iTwo);
                        listOne.Add(iThree); 
           //             _edges[iOne].AddRange(new int[] { iTwo, iThree });
                    }

                    //Add edges to the lists for each node
                    if (!_edges.ContainsKey(iTwo))
                        _edges.Add(iTwo, new List<int>(new int[] { iOne, iThree }));
                    else
                    {
                        //Doing this the long way to avoid the new operator which slows down threads
                        List<int> listOne = _edges[iTwo];
                        listOne.Add(iOne);
                        listOne.Add(iThree); 
           //             _edges[iTwo].AddRange(new int[] { iOne, iThree });
                    }

                    //Add edges to the lists for each node
                    if (!_edges.ContainsKey(iThree))
                        _edges.Add(iThree, new List<int>(new int[] { iOne, iTwo }));
                    else
                    {
                        //Doing this the long way to avoid the new operator which slows down threads
                        List<int> listOne = _edges[iThree];
                        listOne.Add(iOne);
                        listOne.Add(iTwo); 
           //             _edges[iThree].AddRange(new int[] { iOne, iTwo });
                    }
                }

                //Remove duplicates from edge list
                foreach (List<int> indexList in _edges.Values)
                {
                    //Sort indicies and remove duplicates
                    indexList.Sort();
                    for (int iTest = 1; iTest < indexList.Count; iTest++)
                    {
                        if (indexList[iTest - 1] == indexList[iTest])
                        {
                            indexList.RemoveAt(iTest);
                            iTest--;
                        }
                    }
                }

                _LineSegmentGrid = new PairedLineSearchGrid(this._mapPoints, CachedMappedBounds, _edges);
            }
            finally 
            {
                if(rwLockEdges.IsWriteLockHeld)
                    rwLockEdges.ExitWriteLock();
            }
        }

        #endregion

        public void BuildDataStructures()
        {
            CalculateEdges();
            BuildTriangleQuadTree();
        }

        /// <summary>
        /// This call removes cached data from the transform to reduce memory footprint
        /// </summary>
        public void MinimizeMemory()
        {
            //if(_mapTriangles != null)
                //_mapTriangles.Clear();

            try
            {
                rwLockTriangles.EnterWriteLock();

                _mapTriangles = null;
                _controlTriangles = null;
            }
            finally
            {
                if (rwLockTriangles.IsWriteLockHeld)
                    rwLockTriangles.ExitWriteLock(); 
            }

            try
            {
                rwLockEdges.EnterWriteLock();
                if (_edges != null)
                    _edges.Clear();
                _edges = null;
            }
            finally
            {
                if (rwLockEdges.IsWriteLockHeld)
                    rwLockEdges.ExitWriteLock();
            }

            this._LineSegmentGrid = null; 
        }

            
        public GridTransform() : base() 
        {
        }

        #region ISerializable Members

        public GridTransform(SerializationInfo info, StreamingContext context)
        {
            _mapPoints = info.GetValue("_mapPoints", typeof(MappingGridVector2[])) as MappingGridVector2[];
            _TriangleIndiciesCache = info.GetValue("_TriangleIndiciesCache", typeof(int[])) as int[];
            _CachedMappedBounds = (GridRectangle)info.GetValue("_CachedMappedBounds", typeof(GridRectangle)); 
            _CachedControlBounds = (GridRectangle)info.GetValue("_CachedControlBounds", typeof(GridRectangle));
            LastModified = info.GetDateTime("LastModified");
        }
        
        public virtual void GetObjectData(SerializationInfo info, StreamingContext context)
        {
            info.AddValue("_mapPoints", _mapPoints);
            info.AddValue("_TriangleIndiciesCache", _TriangleIndiciesCache);
            info.AddValue("_CachedMappedBounds", _CachedMappedBounds);
            info.AddValue("_CachedControlBounds", _CachedControlBounds);
            info.AddValue("LastModified", this.LastModified); 
        }


        #endregion

        public override string ToString()
        {
           return "GridTransform " + MappedSection.ToString() + " to " + ControlSection.ToString();
        }

        public GridTransform Copy()
        {
            return ((ICloneable)this).Clone() as GridTransform; 
        }

        object ICloneable.Clone()
        {
            GridTransform newObj = new GridTransform();
            newObj = this.MemberwiseClone() as GridTransform;

            List<MappingGridVector2> TempList = new List<MappingGridVector2>();
            
            foreach (MappingGridVector2 pt in mapPoints)
            {
                TempList.Add((MappingGridVector2)pt.Copy());
            }

            //Setting the mapPoints will sort and recalculate triangles
            newObj.mapPoints = TempList.ToArray(); 

            return newObj;
        }

        public static GridRectangle CalculateBounds(GridTransform[] transforms)
        {
            double minX = double.MaxValue;
            double minY = double.MaxValue;
            double maxX = double.MinValue;
            double maxY = double.MinValue;

            foreach (GridTransform T in transforms)
            {
                GridRectangle R = T.CachedControlBounds;

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

        #region Transform

        /// <summary>
        /// Return the mapping triangle which can map the point
        /// </summary>
        /// <param name="Point"></param>
        /// <returns></returns>
        public override bool CanTransform(GridVector2 Point)
        {
            if (GetTransform(Point) == null)
                return false;

            return true;
        }

        /// <summary>
        /// Return the mapping triangle which can map the point
        /// </summary>
        /// <param name="Point"></param>
        /// <returns></returns>
        public MappingGridTriangle GetTransform(GridVector2 Point)
        {
            //TODO: Optimize the search

            //Having a smaller epsilon caused false positives.  
            //We just want to know if we are close enough to check with the more time consuming math
            double epsilon = 5;

            if (Point.X < CachedMappedBounds.Left - epsilon)
                return null;
            if (Point.X > CachedMappedBounds.Right + epsilon)
                return null;
            if (Point.Y > CachedMappedBounds.Top + epsilon)
                return null;
            if (Point.Y < CachedMappedBounds.Bottom - epsilon)
                return null;

            //Fetch a list of triangles from the nearest point
            double distance;
            List<MappingGridTriangle> triangles = mapTriangles.FindNearest(Point, out distance);

            if (triangles == null)
                return null;

            
            foreach (MappingGridTriangle t in triangles)
            {
                if (t.MinMapX > Point.X)
                    continue;
                if (t.MaxMapX < Point.X)
                    continue;
                if (t.MinMapY > Point.Y)
                    continue;
                if (t.MaxMapY < Point.Y)
                    continue; 
                
                if (t.IntersectsMapped(Point))
                    return t;

            }

            //You can't just accept that these triangles are the closest triangles.  It's possible there is a point
            //which is the closest to the test point, but isn't a vertex of the bounding tiangle.
            //As a hack I expand the search to include all verticies of the bounding triangles if the first search failss
            List<MappingGridTriangle> fallbackTriangles = new List<MappingGridTriangle>();

            foreach (MappingGridTriangle t in triangles)
            {
                fallbackTriangles.AddRange(mapTriangles.FindNearest(t.Mapped.p1, out distance));
                fallbackTriangles.AddRange(mapTriangles.FindNearest(t.Mapped.p2, out distance));
                fallbackTriangles.AddRange(mapTriangles.FindNearest(t.Mapped.p3, out distance));
            }

            //Check the fallback triangles
            foreach (MappingGridTriangle t in fallbackTriangles)
            {
                if (t.MinMapX > Point.X)
                    continue;
                if (t.MaxMapX < Point.X)
                    continue;
                if (t.MinMapY > Point.Y)
                    continue;
                if (t.MaxMapY < Point.Y)
                    continue;

                if (t.IntersectsMapped(Point))
                    return t;
            }
            
            return null;
        }

        /// <summary>
        /// Transform point from mapped space to control space
        /// </summary>
        /// <param name="Point"></param>
        /// <returns></returns>
        public override GridVector2 Transform(GridVector2 Point)
        {
            MappingGridTriangle t = GetTransform(Point);
            if (t == null)
            {
                throw new ArgumentOutOfRangeException("Point", "Transform: Point could not be mapped"); 
            }

            return t.Transform(Point);
        }

        /// <summary>
        /// Transform point from mapped space to control space
        /// </summary>
        /// <param name="Point"></param>
        /// <returns></returns>
        public override bool TryTransform(GridVector2 Point, out GridVector2 v)
        {
            v = new GridVector2();
            MappingGridTriangle t = GetTransform(Point);
            if (t == null)
                return false; 

            v = t.Transform(Point);
            return true; 
        }

        #endregion

        #region InverseTransform

        /// <summary>
        /// Return the mapping triangle which can map the point
        /// </summary>
        /// <param name="Point"></param>
        /// <returns></returns>
        public override bool CanInverseTransform(GridVector2 Point)
        {
            if (GetInverseTransform(Point) == null)
                return false;

            return true;
        }

        /// <summary>
        /// Return the mapping triangle which can map the point
        /// </summary>
        /// <param name="Point"></param>
        /// <returns></returns>
        public MappingGridTriangle GetInverseTransform(GridVector2 Point)
        {
            //TODO: Optimize the search

            //Having a smaller epsilon caused false positives.  
            //We just want to know if we are close enough to check with the more time consuming math
            double epsilon = 5;

            if (Point.X < CachedControlBounds.Left - epsilon)
                return null;
            if (Point.X > CachedControlBounds.Right + epsilon)
                return null;
            if (Point.Y > CachedControlBounds.Top + epsilon)
                return null;
            if (Point.Y < CachedControlBounds.Bottom - epsilon)
                return null;

            //Fetch a list of triangles from the nearest point
            double distance;
            List<MappingGridTriangle> triangles = controlTriangles.FindNearest(Point, out distance);

            if (triangles == null)
                return null; 

            
            foreach (MappingGridTriangle t in triangles)
            {
                if (t.MinCtrlX > Point.X)
                    continue;
                if (t.MaxCtrlX < Point.X)
                    continue;
                if (t.MinCtrlY > Point.Y)
                    continue;
                if (t.MaxCtrlY < Point.Y)
                    continue;

                if (t.IntersectsControl(Point))
                    return t;
            }

            //You can't just accept that these triangles are the closest triangles.  It's possible there is a point
            //which is the closest to the test point, but isn't a vertex of the bounding tiangle.
            //As a hack I expand the search to include all verticies of the bounding triangles if the first search failss
            List<MappingGridTriangle> fallbackTriangles = new List<MappingGridTriangle>(); 

            foreach(MappingGridTriangle t in triangles)
            {
                fallbackTriangles.AddRange(controlTriangles.FindNearest(t.Control.p1, out distance));
                fallbackTriangles.AddRange(controlTriangles.FindNearest(t.Control.p2, out distance));
                fallbackTriangles.AddRange(controlTriangles.FindNearest(t.Control.p3, out distance));
            }

            //Check the fallback triangles
            foreach (MappingGridTriangle t in fallbackTriangles)
            {
                if (t.MinCtrlX > Point.X)
                    continue;
                if (t.MaxCtrlX < Point.X)
                    continue;
                if (t.MinCtrlY > Point.Y)
                    continue;
                if (t.MaxCtrlY < Point.Y)
                    continue;

                if (t.IntersectsControl(Point))
                    return t;
            }
            

            return null;
        }

        /// <summary>
        /// Transform point from mapped space to control space
        /// </summary>
        /// <param name="Point"></param>
        /// <returns></returns>
        public override GridVector2 InverseTransform(GridVector2 Point)
        {
            MappingGridTriangle t = GetInverseTransform(Point);
            if (t == null)
            {
                throw new ArgumentOutOfRangeException("Point", "InverseTransform: Point could not be mapped");
            }

            return t.InverseTransform(Point);
        }

        /// <summary>
        /// Transform point from mapped space to control space
        /// </summary>
        /// <param name="Point"></param>
        /// <returns></returns>
        public override bool TryInverseTransform(GridVector2 Point, out GridVector2 v)
        {
            v = new GridVector2();

            MappingGridTriangle t = GetInverseTransform(Point);
            if (t == null)
                return false; 

            v = t.InverseTransform(Point);

            return true;
        }

        #endregion

        /// <summary>
        /// Translates all verticies in the tile according to the vector
        /// </summary>
        /// <param name="vector"></param>
        public void Translate(GridVector2 vector)
        {
            for (int i = 0; i < mapPoints.Length; i++)
            {
                mapPoints[i].ControlPoint += vector;
            }

            //Remove any cached data structures
            MinimizeMemory(); 

            _CachedControlBounds.Left += vector.X;
            _CachedControlBounds.Right += vector.X;
            _CachedControlBounds.Bottom += vector.Y;
            _CachedControlBounds.Top += vector.Y;
        }

        protected GridRectangle _CachedMappedBounds = new GridRectangle(double.MinValue, double.MinValue, double.MinValue, double.MinValue);
        public GridRectangle CachedMappedBounds
        {
            get
            {
                if (_CachedMappedBounds.Left == double.MaxValue ||
                    _CachedMappedBounds.Left == double.MinValue)
                {
                    this._CachedMappedBounds = MappingGridVector2.CalculateMappedBounds(this.mapPoints);
                }

                return _CachedMappedBounds;
            }
        }



        protected GridRectangle _CachedControlBounds = new GridRectangle(double.MinValue, double.MinValue, double.MinValue, double.MinValue );
        public GridRectangle CachedControlBounds
        {
            get
            {
                if (_CachedControlBounds.Left == double.MinValue)
                {
                    this._CachedControlBounds = MappingGridVector2.CalculateControlBounds(this.mapPoints);
                }

                return _CachedControlBounds;
            }
        }

        

        /// <summary>
        /// Returns the coordinate on the section to be mapped given a grid coordinate from reading the transform
        /// </summary>
        /// <param name="x"></param>
        /// <param name="y"></param>
        /// <returns></returns>
        protected static GridVector2 CoordinateFromGridPos(int x, int y, double gridWidth, double gridHeight, double MappedWidth, double MappedHeight)
        {
            return new GridVector2(((x) / (gridWidth - 1)) * MappedWidth, (y / (gridHeight - 1)) * MappedHeight);
        }

        /// <summary>
        /// Returns the coordinate on the section to be mapped given a grid coordinate from reading the transform
        /// </summary>
        /// <param name="x"></param>
        /// <param name="y"></param>
        /// <returns></returns>
        protected GridVector2 CoordinateFromGridPos(int x, int y, double gridWidth, double gridHeight)
        {
            return new GridVector2(((x) / (gridWidth - 1)) * (double)MappedBounds.Width, (y / (gridHeight - 1)) * (double)MappedBounds.Height);
        }

        /// <summary>
        /// Takes two transforms and transforms the control grid of this section into the control grid space of the passed transfrom. Requires control section
        /// of this transform to match mapped section of adding transform
        /// </summary>
        public void Add(GridTransform transform)
        {
            //We can't map if we don't have a triangle
            if (transform.mapPoints.Length < 3)
                return; 

            //Temp: Ensure we know the edges
            this.CalculateEdges();
            transform.BuildDataStructures(); 

            //Reset boundaries since they will be changed
            _CachedControlBounds = new GridRectangle(double.MinValue, double.MinValue, 0, 0);
            _CachedMappedBounds = new GridRectangle(double.MinValue, double.MinValue, 0, 0);

            List<AddTransformThreadObj> threadObjList = new List<AddTransformThreadObj>();
            List<ManualResetEvent> doneEvents = new List<ManualResetEvent>();

            List<MappingGridVector2> newPoints = new List<MappingGridVector2>(mapPoints.Length);

#if DEBUG
            List<GridVector2> mapPointList = new List<GridVector2>(newPoints.Count);
#endif

            //            Trace.WriteLine("Starting with " + mapPoints.Length + " points", "Geometry"); 

        //    List<MappingGridVector2> newPoints = new List<MappingGridVector2>(); 

            //           Trace.WriteLine("Started GridTransform.Add with " + mapPoints.Length.ToString() + " points", "Geometry"); 

            //Search all mapping triangles and update control points, if they fall outside the grid then discard the triangle
            const int PointsPerThread = 4; 
            for (int iPoint = 0; iPoint < this.mapPoints.Length; iPoint += PointsPerThread )
            {
                //Create a series of points for the thread to process so they aren't constantly hitting the queue lock looking for new work. 
                List<int> listPoints = new List<int>(PointsPerThread);
                for (int iAddPoint = iPoint; iAddPoint < iPoint + PointsPerThread; iAddPoint++)
                {
                    //Don't add if the point is out of range
                    if (iAddPoint >= this.mapPoints.Length)
                        break; 

                    listPoints.Add(iAddPoint);
                }

                //MappingGridVector2 mapPoint = mapPoints[iPoint];
                AddTransformThreadObj AddThreadObj = new AddTransformThreadObj(listPoints.ToArray(), this, transform);
                doneEvents.Add(AddThreadObj.DoneEvent);
                threadObjList.Add(AddThreadObj);
                
                /*
                MappingGridVector2 UnmappedPoint = mapPoints[iPoint];
                MappingGridTriangle mapTri = transform.GetTransform(mapPoints[iPoint].ControlPoint);

                if (mapTri != null)
                {
                    GridVector2 newControl = mapTri.Transform(UnmappedPoint.ControlPoint);
                    newPoints.AddRange(new MappingGridVector2[] { new MappingGridVector2(newControl, UnmappedPoint.MappedPoint) });

                    List<MappingGridVector2> sortPoints = new List<MappingGridVector2>(newPoints);
                    sortPoints.Sort();
                    for (int i = 1; i < sortPoints.Count; i++)
                    {
                        Debug.Assert(sortPoints[i - 1].ControlPoint != sortPoints[i].ControlPoint);
                    }
                }
                else
                {
                    //In this case we need to test each edge connecting this point to other points.
                    //newPoints = new MappingGridVector2[0];
                    for(int i = 0; i < TriangleIndicies.Length; i += 3)
                    {
                        if(TriangleIndicies[i] == iPoint)
                        {
                            
                    int[] EdgesIndicies = Array.Find<int>(this.TriangleIndicies, iPoint);
                }
                */

                //For single threaded debug, comment out threadpool and uncomment AddThreadObj.ThreadPoolCallback line
                ThreadPool.QueueUserWorkItem(AddThreadObj.ThreadPoolCallback);
//                AddThreadObj.ThreadPoolCallback(null);

                
                //newPoints.AddRange(AddThreadObj.newPoints);

                //newPoints.Sort();
                

#if false
                for (int iTest = 1; iTest < newPoints.Count; iTest++)
                {
                    Debug.Assert(newPoints[iTest - 1].ControlPoint != newPoints[iTest].ControlPoint); 
                }

                for (int iMap = 0; iMap < AddThreadObj.newPoints.Length; iMap++)
                {
                    mapPointList.Add(AddThreadObj.newPoints[iMap].MappedPoint);
                }

                mapPointList.Sort();

                for (int iMap = 1; iMap < mapPointList.Count; iMap++)
                {
                    Debug.Assert(GridVector2.Distance(mapPointList[iMap], mapPointList[iMap - 1]) > Global.epsilon);
                }
#endif
            }

            //Wait for the threads to finish processing.  There is a 64 handle limit for WaitAll so we wait on one at a time
            foreach (ManualResetEvent doneEvent in doneEvents)
                doneEvent.WaitOne();

            newPoints.Clear();

            foreach(AddTransformThreadObj obj in threadObjList)
            {
                if(obj.newPoints != null)
                    newPoints.AddRange(obj.newPoints);
            }

            //            Trace.WriteLine("Mapped " + newPoints.Count + " points", "Geometry"); 

#if false

            mapPointList.Clear(); 
            for (int iMap = 0; iMap < newPoints.Count; iMap++)
            {
                mapPointList.Add(newPoints[iMap].MappedPoint);
            }

            mapPointList.Sort();

            for (int iMap = 1; iMap < mapPointList.Count; iMap++)
            {
                Debug.Assert(GridVector2.Distance(mapPointList[iMap], mapPointList[iMap - 1]) > Global.epsilon);
            }
#endif

            //Remove duplicates: In the case that a line on the warpingGrid passes through a point on the fixedGrid then both ends of the line will map the point and we will get a duplicate
            newPoints.Sort();
            int iCompareStart = 0; 
            for (int iTest = 1; iTest < newPoints.Count; iTest++)
            {
            //   Debug.Assert(newPoints[iTest - 1].ControlPoint != newPoints[iTest].ControlPoint);
                //This is slow, but even though we sort on the X axis it doesn't mean a point that is not adjacent to the point on the list isn't too close
                for (int jTest = iCompareStart; jTest < iTest; jTest++)
                {
                    if (GridVector2.Distance(newPoints[jTest].ControlPoint, newPoints[iTest].ControlPoint) <= Global.Epsilon)
                    {
                        newPoints.RemoveAt(iTest);
                        iTest--;
                        break; 
                    }
                    
                    //Optimization, since the array is sorted we don't need to compare points once a point is distant enough
                    if (newPoints[iTest].ControlPoint.X - newPoints[jTest].ControlPoint.X > Global.Epsilon)
                    {
                        iCompareStart = jTest; 
                    }
                }
            }

            //            Trace.WriteLine("Ended with " + newPoints.Count + " points", "Geometry");
            this.mapPoints = newPoints.ToArray();

            //Edges are build on mapPoints, so we need to remove them so they'll be recalculates
            _edges = null;
            //Other datastructures are dependent on edges, so minimize memory will delete them
            MinimizeMemory();

            //            Trace.WriteLine("Finished GridTransform.Add with " + newPoints.Count.ToString() + " points", "Geometry"); 

            //Check whether these have been set yet or if I don't need to clear them again
            this.ControlSection = transform.ControlSection;
        }

        public double NearestLine(GridLineSegment L, out GridLineSegment foundCtrlLine, out GridLineSegment foundMapLine, out GridVector2 intersection)
        {
            double nearestIntersect = double.MaxValue;

            //For debugging only
            double nearestFailedIntersect = double.MaxValue;
            GridVector2 nearestFailedPoint = new GridVector2();
            GridLineSegment nearestFailedSegment;

            foundCtrlLine = new GridLineSegment();
            foundMapLine = new GridLineSegment(); 
            intersection = new GridVector2();

            IEnumerable<GridLineSegmentPair> _linePairs = _LineSegmentGrid.GetPotentialIntersections(L); 

            foreach (GridLineSegmentPair pair in _linePairs)
            {
                //Build the edge and find out if it intersects
                GridLineSegment mapLine = pair.mapLine; 

                if (mapLine.MinX > L.MaxX)
                    continue;
                if (mapLine.MaxX < L.MinX)
                    continue;
                if (mapLine.MinY > L.MaxY)
                    continue;
                if (mapLine.MaxY < L.MinY)
                    continue;
                
                GridVector2 result;
                bool bIntersected = mapLine.Intersects(L, out result);
                double distance = GridVector2.Distance(L.A, result);
                if (distance < nearestIntersect && bIntersected)
                {
                    nearestIntersect = distance;
                    intersection = result;
                    foundMapLine = mapLine;
                    foundCtrlLine = pair.ctrlLine; 
                }
                if (distance < nearestFailedIntersect && !bIntersected)
                {
                    nearestFailedPoint = result;
                    nearestFailedSegment = mapLine;
                    nearestFailedIntersect = distance;
                }
            }

            return nearestIntersect;

        }

        /*
        /// <summary>
        /// Takes two transforms and transforms the control grid of this section into the control grid space of the passed transfrom. Requires control section
        /// of this transform to match mapped section of adding transform
        /// 
        /// When we add a transform (tAdd) to another transform (this) we follow this algorithm:
        //We pass the control points of this to tAdd and find out if they can be mapped into the space of tAdd.   The triangle of this is ABC. 
        //If all three points of a triangle map inside the space we accept the new points as the control points and proceed.
        //If two points of a triangle map inside the space and we assume point A is outside space we find the intersection of
        //AB & AC with mapped space and create new points, D & E.  We bisect the line between BC to create point F 
        //(Asserting F is able to be mapped).  We then remove triangle ABC from the mapping list and add BDF, FDE, CFE.
        //Optimization:  Before bisecting we check if the lines intersecting AC and AB have a common origin point.  If they 
        //do we use this point as the bisection point.  This prevents erosion in the convex boundary case.
        //Problems: If there is a concave boundary then the line connecting ED may pass outside mapped space, but it should 
        //be mappable because we know all points of the triangle are in mappable space.
        //If two points of a triangle maps outside the space and we assume points BC are these points then we find the
        //intersection of AB and AC with mapped space to define points D & E.  We then redefine C=E and B=D. 
        //Optimization:  Check if lines intersecting AC and AB intersect outside ADE.  If they do define EDF and add it
        //to the triangle list.  This expands the mapped space in a concave boundary case.
        //If three points of a triangle map outside the space we discard the triangle for now.
        //Optimization: Check each line to ensure it doesn’t intersect with map space.  If it does…
        /// </summary>
        /// <param name="t1"></param>
        /// <param name="t2"></param>
        /// <returns></returns>
        public void Add(StosGridTransform transform)
        {
            
 //           Trace.WriteLine("Adding transforms: " + this.ToString() + " to " + transform.ToString(), "Geometry"); 
 //           Debug.Assert(transform.MappedSection == this.ControlSection, "Can't skip sections when assembling volume transforms");

            //Reset boundaries since they will be changed
            _CachedControlBounds = new Rectangle(int.MinValue, int.MinValue, 0, 0);
            _CachedMappedBounds = new Rectangle(int.MinValue, int.MinValue, 0, 0);

            List<AddTransformThreadObj> threadObjList = new List<AddTransformThreadObj>();
            List<ManualResetEvent> doneEvents = new List<ManualResetEvent>(); 

            //Search all mapping triangles and update control points, if they fall outside the grid then discard the triangle
            for (int iTri = 0; iTri < mappings.Count; iTri++ )
            {
                MappingTriangle mapTri = mappings[iTri];
                AddTransformThreadObj AddThreadObj = new AddTransformThreadObj(mapTri, transform);
                doneEvents.Add(AddThreadObj.DoneEvent);
                threadObjList.Add(AddThreadObj);

                ThreadPool.QueueUserWorkItem(AddThreadObj.ThreadPoolCallback);
               
            }

            //Wait for the threads to finish processing.  There is a 64 handle limit for WaitAll so we wait on one at a time
            foreach (ManualResetEvent doneEvent in doneEvents)
                doneEvent.WaitOne(); 

            mappings.Clear();

            foreach(AddTransformThreadObj obj in threadObjList)
            {
                mappings.AddRange(obj.newMaps);
            }

            _CachedControlBounds = new Rectangle(int.MinValue, int.MinValue, 0, 0);
            _CachedMappedBounds = new Rectangle(int.MinValue, int.MinValue, 0, 0);

            this.ControlSection = transform.ControlSection;
             
        }

        /// <summary>
        /// Given a mapping triangle with one vertex outside our mapping space (A) we return a list
        /// of mapping triangles which fill our mapping space and crop the unmappable space
        /// </summary>
        /// <param name="mapTri">Mapping triangle which doesn't fit</param>
        /// <returns></returns>
        internal MappingTriangle[] FitTriangleOneExternalVertex(MappingTriangle mapTri)
        {
            
            //Figure out which vertex is external and relabel vectors A,B,C in clockwise fashion
            //with A being outside mapped space
            Vector2 A,B,C;

            if(false == CanTransform(mapTri.Control.p1))
            {
                A = mapTri.Control.p1;
                B = mapTri.Control.p2;
                C = mapTri.Control.p3; 
            }
            else if (false == CanTransform(mapTri.Control.p2))
            {
                A = mapTri.Control.p2;
                B = mapTri.Control.p3;
                C = mapTri.Control.p1; 
            }
            else
            {
                Debug.Assert(CanTransform(mapTri.Control.p3) == false, "FitTriangleOneExternalVertex - all verts inside mapped space");
                A = mapTri.Control.p3;
                B = mapTri.Control.p1;
                C = mapTri.Control.p2; 
            }

            LineSegment AB = new LineSegment(A, B); //Leave points outside mapping as first term so I can calculate distance to closest point in mapped space
            LineSegment AC = new LineSegment(A, C);
            LineSegment BC = new LineSegment(B, C); 

            //Find the nearest point inside the grid from the external points
            LineSegment lineThroughAB;
            Vector2 D = new Vector2(); //The intersection point of BA with mapped space
            float nearestABintersect = float.MaxValue;
            LineSegment lineThroughAC;
            Vector2 E = new Vector2(); //The intersection point of CA with mapped space
            float nearestACintersect = float.MaxValue;

            LineSegment lineThroughBC;
            Vector2 F = new Vector2(); //The intersection point of CA with mapped space
            float nearestBCintersect = float.MaxValue;


            nearestABintersect = NearestLine(AB, out lineThroughAB, out D);
            nearestACintersect = NearestLine(AC, out lineThroughAC, out E);
            nearestBCintersect = NearestLine(BC, out lineThroughBC, out F);

            //TODO: Fiddle with epsilon or figure out why this error happens sometimes
            //Debug.Assert(lineThroughAB != null && lineThroughAC != null, "FitTriangleOneExternalVertex: Couldn't find intersection with mapped space");
            if (lineThroughAB == null || lineThroughAC == null)
                return new MappingTriangle[0]; 

            //OK, we know where the lines intersect with mapped space, find midpoint of BC to define F.  Then create
            //mapping triangles BDF, FDE, CFE

            //First we check to see if there is a common point between two intersection lines we can use as a node;
            if (nearestBCintersect >= float.MaxValue)
            {
                //TODO: If this fails to find a valid transform spot I want to remove the triangle from the transform.
                //hopefully this case is only hit when two nodes are in the same triangle.
                F =  BC.Bisect(); 
            }

            //Find the closest line intersection to BC

            //My approach to dealing with failed transforms due to floating point error is to remove those triangles

            Vector2 Bctrl;
            Vector2 Cctrl;
            Vector2 Dctrl;
            Vector2 Ectrl;
            Vector2 Fctrl;

            //Work backwards to find where new control points where in the mapped space
            Vector2 Bmap;
            Vector2 Cmap;
            Vector2 Dmap;
            Vector2 Emap;;
            Vector2 Fmap;

            try
            {
                //Transform all points into the new control space
                Bctrl = Transform(B);
                Cctrl = Transform(C);
                Dctrl = Transform(D);
                Ectrl = Transform(E);
                Fctrl = Transform(F);

                //Work backwards to find where new control points where in the mapped space
                Bmap = mapTri.InverseTransform(B);
                Cmap = mapTri.InverseTransform(C);
                Dmap = mapTri.InverseTransform(D);
                Emap = mapTri.InverseTransform(E);
                Fmap = mapTri.InverseTransform(F);
            }
            catch (ArgumentException e)
            {
                return new MappingTriangle[0]; 
            }

            List<MappingTriangle> newMaps = new List<MappingTriangle>();
            MappingTriangle newTri = null;
            try
            {
                newTri = new MappingTriangle(new Triangle(Cctrl, Fctrl, Ectrl, Color.Yellow), new Triangle(Cmap, Fmap, Emap, Color.Gold));
                newMaps.Add(newTri); 
            }catch(ArgumentException e)
            {
                Trace.WriteLine("Tried to create triangle which is really a line", "Geometry"); 
            }
            
            try
            {
                newTri = new MappingTriangle(new Triangle(Fctrl, Dctrl, Ectrl, Color.Violet), new Triangle(Fmap, Dmap, Emap, Color.Black));
                newMaps.Add(newTri); 
            }
            catch(ArgumentException e)
            {
                Trace.WriteLine("Tried to create triangle which is really a line", "Geometry"); 
            }

            try{
                newTri = new MappingTriangle(new Triangle(Fctrl, Bctrl, Dctrl, Color.Green), new Triangle(Fmap, Bmap, Dmap, Color.White));
                newMaps.Add(newTri); 
            }catch(ArgumentException e)
            {
                Trace.WriteLine("Tried to create triangle which is really a line", "Geometry"); 
            }

            return newMaps.ToArray(); 
             
        }

        /// <summary>
        /// Given a mapping triangle with two vertex (B&C) outside our mapping space we return a list
        /// of mapping triangles which fill our mapping space and crop the unmappable space
        /// </summary>
        /// <param name="mapTri">Mapping triangle which doesn't fit</param>
        /// <returns></returns>
        internal MappingTriangle[] FitTriangleTwoExternalVertex(MappingTriangle mapTri)
        {
            
            //Figure out which two vertex are external and relabel vectors A,B,C in clockwise fashion
            //with A being inside mapped space
            Vector2 A, B, C;

            if (CanTransform(mapTri.Control.p1))
            {
                A = mapTri.Control.p1;
                B = mapTri.Control.p2;
                C = mapTri.Control.p3;
            }
            else if (CanTransform(mapTri.Control.p2))
            {
                A = mapTri.Control.p2;
                B = mapTri.Control.p3;
                C = mapTri.Control.p1;
            }
            else
            {
                Debug.Assert(CanTransform(mapTri.Control.p3), "FitTriangleOneExternalVertex - all verts outside mapped space");
                A = mapTri.Control.p3;
                B = mapTri.Control.p1;
                C = mapTri.Control.p2;
            }

            LineSegment BA = new LineSegment(B, A); //Leave points outside mapping as first term so I can calculate distance to closest point in mapped space
            LineSegment CA = new LineSegment(C, A);
            LineSegment BC = new LineSegment(B, C);

            //Find the nearest point inside the grid from the external points
            LineSegment lineThroughBA;
            Vector2 D = new Vector2(); //The intersection point of BA with mapped space
            float nearestBAintersect = float.MaxValue;
            LineSegment lineThroughCA;
            Vector2 E = new Vector2(); //The intersection point of CA with mapped space
            float nearestCAintersect = float.MaxValue;

//            LineSegment lineThroughBC;
//            Vector2 F = new Vector2(); //The intersection point of CA with mapped space
//            float nearestBCintersect = float.MaxValue;


            nearestBAintersect = NearestLine(BA, out lineThroughBA, out D);
            nearestCAintersect = NearestLine(CA, out lineThroughCA, out E);
        //    nearestBCintersect = NearestLine(BC, out lineThroughBC, out F);

            //TODO: Figure out if we can prevent this from occuring
            //Debug.Assert(lineThroughBA != null && lineThroughCA != null, "FitTriangleOneExternalVertex: Couldn't find intersection with mapped space");
            if(lineThroughBA == null || lineThroughCA == null)
                return new MappingTriangle[0]; 

            //Transform all points into the new control space
            MappingTriangle[] newMaps = new MappingTriangle[1];

            try
            {
                Vector2 Actrl = Transform(A);
                Vector2 Dctrl = Transform(D);
                Vector2 Ectrl = Transform(E);

                //Work backwards to find where new control points where in the mapped space
                Vector2 Amap = mapTri.InverseTransform(A);
                Vector2 Dmap = mapTri.InverseTransform(D);
                Vector2 Emap = mapTri.InverseTransform(E);

                

                newMaps[0] = new MappingTriangle(new Triangle(Actrl, Dctrl, Ectrl, Color.Ivory), new Triangle(Amap, Dmap, Emap, Color.Gold));
            }
            catch (ArgumentException e)
            {
                Trace.WriteLine("Tried to create triangle which is really a line", "Geometry");
                return new MappingTriangle[0]; 
            }

            
            return newMaps;
        }
        */

        /*
        protected float NearestLine(LineSegment L, out LineSegment foundLine, out Vector2 intersection)
        {
            float nearestIntersect = float.MaxValue;

            //For debugging only
            float nearestFailedIntersect = float.MaxValue;
            Vector2 nearestFailedPoint = new Vector2(); 
            LineSegment nearestFailedSegment = null;


            foundLine = null;
            intersection = new Vector2(); 

            foreach (MappingTriangle T in mappings)
            {
                Vector2 result;

                LineSegment One = new LineSegment(T.Mapped.p1, T.Mapped.p2);
                LineSegment Two = new LineSegment(T.Mapped.p2, T.Mapped.p3);
                LineSegment Three = new LineSegment(T.Mapped.p3, T.Mapped.p1);

                bool bIntersected = One.Intersects(L, out result);
                float distance = Vector2.Distance(L.A, result);
                if (distance < nearestIntersect && bIntersected)
                {
                    nearestIntersect = distance;
                    intersection = result;
                    foundLine = One;
                }
                if(distance < nearestFailedIntersect && !bIntersected)
                {
                    nearestFailedPoint = result; 
                    nearestFailedSegment = One;
                    nearestFailedIntersect = distance;
                }

                bIntersected = Two.Intersects(L, out result);
                distance = Vector2.Distance(L.A, result);
                if (distance < nearestIntersect &&  bIntersected)
                {
                    nearestIntersect = distance;
                    intersection = result;
                    foundLine = Two;
                }
                if(distance < nearestFailedIntersect && !bIntersected)
                {
                    nearestFailedPoint = result; 
                    nearestFailedSegment = Two;
                    nearestFailedIntersect = distance;
                }

                bIntersected = Three.Intersects(L, out result);
                distance = Vector2.Distance(L.A, result);
                if (distance < nearestIntersect && bIntersected)
                {
                    nearestIntersect = distance;
                    intersection = result;   
                    foundLine = Three;
                }
                if(distance < nearestFailedIntersect && !bIntersected)
                {
                    nearestFailedPoint = result; 
                    nearestFailedSegment = Three;
                    nearestFailedIntersect = distance;
                }
            }

            return nearestIntersect;
            
        }*/
         
        /*
        public void Draw(GraphicsDevice graphicsDevice, BasicEffect basicEffect)
        {
            /*
            BasicEffect basicEffect = new BasicEffect(graphicsDevice, null);
            basicEffect.AmbientLightColor = templateEffect.AmbientLightColor;
            basicEffect.Projection = templateEffect.Projection;
            basicEffect.World = templateEffect.World;
            basicEffect.View = templateEffect.View;
            basicEffect.Projection = templateEffect.Projection;
             */

            /*
            basicEffect.TextureEnabled = false;
            basicEffect.VertexColorEnabled = true;
            basicEffect.CommitChanges();

            VertexDeclaration originalVertDeclare = graphicsDevice.VertexDeclaration;

            basicEffect.Begin();

            foreach (EffectPass pass in basicEffect.CurrentTechnique.Passes)
            {
                pass.Begin();
                foreach (MappingTriangle tri in mappings)
                {
                    tri.Draw(graphicsDevice);
                }
                pass.End();
            }

            basicEffect.End();

            graphicsDevice.VertexDeclaration = originalVertDeclare;
             * */
        //    return; 
     //   }


        
    }
}
