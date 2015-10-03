using System;
using System.Collections.Generic;
using System.Text;
using System.Diagnostics;
using System.Threading;
using System.Runtime.Serialization;

namespace Geometry.Transforms
{
    /// <summary>
    /// This is the parent class of all transforms.  All transforms, even continuos, are implemented by placing a grid 
    /// of control points on the mapped and control section.  
    /// </summary>
    [Serializable]
    public class MeshTransform : TriangulationTransform, ICloneable, ISerializable
    {
        

        #region Edges

            protected ReaderWriterLockSlim rwLockEdges = new ReaderWriterLockSlim();
            private PairedLineSearchGrid _LineSegmentGrid;

            private List<int>[] _edges; 
            public override List<int>[] Edges
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
                    //CaclulateEdges will take a write lock
                    return CalculateEdges();
                }
                protected set
                {
                    try
                    {
                        rwLockEdges.EnterWriteLock();

                        _edges = value;
                    }
                    finally
                    {
                        if (rwLockEdges.IsWriteLockHeld)
                            rwLockEdges.ExitWriteLock();
                    }


                }
            }
        
        #endregion
   
        public MeshTransform(MappingGridVector2[] points, TransformInfo info) : base(points, info) 
        {
            
        }

        protected MeshTransform(SerializationInfo info, StreamingContext context)
            : base(info, context)
        {
            if (info == null)
                throw new ArgumentNullException("info");

            this._edges = info.GetValue("_Edges", typeof(List<int>[])) as List<int>[]; 
        }

        public override void GetObjectData(SerializationInfo info, StreamingContext context)
        {
            if (info == null)
                throw new ArgumentNullException("info");

            info.AddValue("_Edges", _edges);

            base.GetObjectData(info, context);
        }

        public void BuildDataStructures()
        {
            CalculateEdges();
            BuildTriangleQuadTree();
        }

        /// <summary>
        /// Save a transform using the itk transform text format
        /// </summary>
        /// <param name="stream"></param>
        /// <param name="Downsample">Divide all spatial data by this value</param>
        public override void SaveMosaic(System.IO.StreamWriter stream)
        {
            if (stream == null)
                throw new ArgumentNullException("stream");

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

            foreach(MappingGridVector2 p in this.MapPoints)
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

        #region ISerializable Members

        

        #endregion

        #region ICloneable

            public MeshTransform Copy()
            {
                return ((ICloneable)this).Clone() as MeshTransform; 
            }

            object ICloneable.Clone()
            {
                MeshTransform newObj;

                newObj = this.MemberwiseClone() as MeshTransform;

                List<MappingGridVector2> TempList = new List<MappingGridVector2>();
            
                foreach (MappingGridVector2 pt in MapPoints)
                {
                    TempList.Add((MappingGridVector2)pt.Copy());
                }

                //Setting the mapPoints will sort and recalculate triangles
                newObj.MapPoints = TempList.ToArray(); 

                return newObj;
            }

        #endregion

        #region Transforms

            /// <summary>
            /// Return the mapping triangle which can map the point
            /// </summary>
            /// <param name="Point"></param>
            /// <returns></returns>
            internal override MappingGridTriangle GetTransform(GridVector2 Point)
            {
                //TODO: Optimize the search

                //Having a smaller epsilon caused false positives.  
                //We just want to know if we are close enough to check with the more time consuming math
                double epsilon = 5;

                if (Point.X < MappedBounds.Left - epsilon)
                    return null;
                if (Point.X > MappedBounds.Right + epsilon)
                    return null;
                if (Point.Y > MappedBounds.Top + epsilon)
                    return null;
                if (Point.Y < MappedBounds.Bottom - epsilon)
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
            /// Return the mapping triangle which can map the point
            /// </summary>
            /// <param name="Point"></param>
            /// <returns></returns>
            internal override MappingGridTriangle GetInverseTransform(GridVector2 Point)
            {
                //TODO: Optimize the search

                //Having a smaller epsilon caused false positives.  
                //We just want to know if we are close enough to check with the more time consuming math
                double epsilon = 5;

                if (Point.X < ControlBounds.Left - epsilon)
                    return null;
                if (Point.X > ControlBounds.Right + epsilon)
                    return null;
                if (Point.Y > ControlBounds.Top + epsilon)
                    return null;
                if (Point.Y < ControlBounds.Bottom - epsilon)
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

                foreach (MappingGridTriangle t in triangles)
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

        #endregion

        protected virtual List<int>[] CalculateEdges()
        {
            try
            {
                //Make sure someone hasn't populated _edges already
                rwLockEdges.EnterUpgradeableReadLock();
                if (_edges != null)
                    return _edges;

                try
                {
                    rwLockEdges.EnterWriteLock();

                    //In this case someone went through this routine ahead of us exit if the data structure is built
                    if (_edges != null)
                        return _edges; 

                    _edges = new List<int>[MapPoints.Length];
                    for (int i = 0; i < MapPoints.Length; i++)
                    {
                        _edges[i] = new List<int>(8); //Estimated number of edges per point
                    }

                    for (int i = 0; i < this.TriangleIndicies.Length; i += 3)
                    {
                        int iOne = TriangleIndicies[i];
                        int iTwo = TriangleIndicies[i + 1];
                        int iThree = TriangleIndicies[i + 2];

                        //Add edges to the lists for each node
                        _edges[iOne].Add(iTwo);
                        _edges[iOne].Add(iThree);

                        _edges[iTwo].Add(iOne);
                        _edges[iTwo].Add(iThree);

                        _edges[iThree].Add(iOne);
                        _edges[iThree].Add(iTwo);
                    }

                    //Remove duplicates from edge list
                    foreach (List<int> indexList in _edges)
                    {
                        //Sort indicies and remove duplicates
                        indexList.Sort();
                        for (int iTest = 1; iTest < indexList.Count; iTest++)
                        {
                            if (Math.Abs(indexList[iTest - 1] - indexList[iTest]) < Global.Epsilon)
                            {
                                indexList.RemoveAt(iTest);
                                iTest--;
                            }
                        }

                        indexList.TrimExcess();
                    }

                    _LineSegmentGrid = new PairedLineSearchGrid(this.MapPoints, MappedBounds, _edges);

                    return _edges;
                }
                finally
                {
                    if (rwLockEdges.IsWriteLockHeld)
                        rwLockEdges.ExitWriteLock();
                }
            }
            finally
            {
                if (rwLockEdges.IsUpgradeableReadLockHeld)
                    rwLockEdges.ExitUpgradeableReadLock();
            }
        }

        /// <summary>
        /// Returns the line on the convex hull which intersects the line L which intersects the convex hull.
        /// Returns double.MaxVal if the line L does not intersect the Convex hull
        /// </summary>
        /// <param name="L">Line intersecting convex hull</param>
        /// <param name="OutsidePoint">Point on line outside the convex hull</param>
        /// <param name="foundCtrlLine"></param>
        /// <param name="foundMapLine"></param>
        /// <param name="intersection"></param>
        /// <returns></returns>
        public override double ConvexHullIntersection(GridLineSegment L, GridVector2 OutsidePoint, out GridLineSegment foundCtrlLine, out GridLineSegment foundMapLine, out GridVector2 intersection)
        {
            double nearestIntersect = double.MaxValue;

            //Make sure the point we are testing is actually on the line
            Debug.Assert(OutsidePoint == L.A || OutsidePoint == L.B);

            //FIXME, multiple threads can get stuck here
            CalculateEdges(); 

            //For debugging only
            double nearestFailedIntersect = double.MaxValue;
            GridVector2 nearestFailedPoint = new GridVector2();

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
                double distance = GridVector2.Distance(OutsidePoint, result);
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
//                    nearestFailedSegment = mapLine;
                    nearestFailedIntersect = distance;
                }
            }

            return nearestIntersect;
        }

       public override void MinimizeMemory()
       {
           base.MinimizeMemory();

           _LineSegmentGrid = null;
       }

       /*
        protected GridRectangle _CachedMappedBounds = new GridRectangle(double.MinValue, double.MinValue, double.MinValue, double.MinValue);
        public GridRectangle CachedMappedBounds
        {
            get
            {
                if (_CachedMappedBounds.Left == double.MaxValue ||
                    _CachedMappedBounds.Left == double.MinValue)
                {
                    this._CachedMappedBounds = MappingGridVector2.CalculateMappedBounds(this.MapPoints);
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
                    this._CachedControlBounds = MappingGridVector2.CalculateControlBounds(this.MapPoints);
                }

                return _CachedControlBounds;
            }
        }
        */
        

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


       protected override void Dispose(bool disposing)
       {
           if (disposing)
           {
               if (rwLockEdges != null)
               {
                   rwLockEdges.Dispose();
                   rwLockEdges = null;
               }

               
           }

           base.Dispose(disposing); 

       }
    }
}
