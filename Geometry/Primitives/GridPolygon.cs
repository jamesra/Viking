using System;
using System.Collections;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;

namespace Geometry
{
    public enum Concavity
    {
        CONCAVE = -1,
        PARALLEL = 0,
        CONVEX = 1
    } 
     
    /// <summary>
    /// A polygon with interior rings representing holes
    /// Rings are described by points.  The first and last point should match
    /// Uses Counter-Clockwise winding order
    /// </summary>
    [Serializable()]
    public class GridPolygon : ICloneable, IPolygon2D, IEquatable<GridPolygon>, IEquatable<IPolygon2D>
    {
        /// <summary>
        /// Cached area of ExteriorRing, not subtracting any interior holes.
        /// Must be updated if ExteriorRing changes
        /// </summary>
        double _ExteriorRingArea;


        GridVector2[] _ExteriorRing;

        /// <summary>
        /// A counter-clockwise closed ring (ExteriorRing.First() == ExteriorRing.Last() of points that define the outer contour of the polygon
        /// </summary>
        public GridVector2[] ExteriorRing
        {
            get { return _ExteriorRing; }
            set
            {
                _ExteriorRingArea = value.PolygonArea();
                if (_ExteriorRingArea < 0) //Negative area indicates Clockwise orientation, we use counter-clockwise
                {
                    _ExteriorRingArea = -_ExteriorRingArea;
                    _ExteriorRing = value.Reverse().ToArray();
                }
                else
                {
                    _ExteriorRing = value;
                }

                _Centroid = null;
                _BoundingRect = _ExteriorRing.BoundingBox();
                _ExteriorSegments = CreateLineSegments(_ExteriorRing);
                //                _ExteriorSegmentRTree = null;
                _SegmentRTree = null;
            }
        }

        /// <summary>
        /// Bounding box of the Polygon.
        /// Must be updated if the verticies change
        /// </summary>
        GridRectangle _BoundingRect;

        /// <summary>
        /// Bounding box of the Polygon verticies
        /// </summary>
        public GridRectangle BoundingBox
        {
            get
            {
                return _BoundingRect;
            }
        }

        /// <summary>
        /// Exterior segments of the polygon, this must be updated if verticies change. The ordering of these segments matches the ordering of ExteriorRing
        /// </summary>
        GridLineSegment[] _ExteriorSegments;

        /// <summary>
        /// Read only array of Exterior segment of the polygon. The ordering of these segments matches the ordering of ExteriorRing
        /// </summary>
        public GridLineSegment[] ExteriorSegments
        {
            get
            {
                return _ExteriorSegments;
            }
        }

        [NonSerialized]
        RTree.RTree<PolygonIndex> _SegmentRTree = null;

        /// <summary>
        /// An RTree containing every segment, exterior and interior, of this polygon
        /// </summary>
        internal RTree.RTree<PolygonIndex> SegmentRTree
        {
            get
            {
                if (_SegmentRTree == null)
                {
                    _SegmentRTree = CreatePointIndexSegmentBoundingBoxRTree(this);
                }

                return _SegmentRTree;
            }
        }



        /// <summary>
        /// Test if a line segment is one of the polygons exterior segments
        /// </summary>
        /// <param name="segment"></param>
        /// <returns></returns>
        public bool IsExteriorSegment(GridLineSegment segment)
        {
            if (_ExteriorSegments.Length < 20)
            {
                return _ExteriorSegments.Contains(segment);
            }
            else
            {
                //No need to check in further detail because they should be identical GridLineSegments
                //return ExteriorSegmentRTree.Intersects(segment.BoundingBox.ToRTreeRect(0)).Contains(segment);
                //return SegmentRTree.Intersects(segment.BoundingBox.ToRTreeRectEpsilonPadded()).Where(i => i.IsInner == false).Select(p => p.Segment(this)).Contains(segment);
                return SegmentRTree.Intersects(segment.BoundingBox.ToRTreeRectEpsilonPadded()).Any(i => i.IsInner == false && i.Segment(this) == segment);
            }
        }

        /// <summary>
        /// Test if a line segment is one of the polygons exterior or interior segments
        /// </summary>
        /// <param name="segment"></param>
        /// <returns></returns>
        public bool IsExteriorOrInteriorSegment(GridLineSegment segment)
        {
            return SegmentRTree.Intersects(segment.BoundingBox.ToRTreeRectEpsilonPadded()).Any(p => p.Segment(this) == segment);  //No need to check in further detail because they should be identical GridLineSegments
        }

        /// <summary>
        /// Cached centroid of the polygon
        /// </summary>
        [NonSerialized]
        GridVector2? _Centroid;

        public GridVector2 Centroid
        {
            get
            {
                if (!_Centroid.HasValue)
                {
                    _Centroid = CalculateCentroid(ExteriorRing);
                }

                return _Centroid.Value;
            }
        }


        List<GridPolygon> _InteriorPolygons = new List<GridPolygon>();

        /// <summary>
        /// Read only please
        /// </summary>
        public IReadOnlyList<GridPolygon> InteriorPolygons
        {
            get
            {
                return _InteriorPolygons.AsReadOnly();
            }
        }

        /// <summary>
        /// Read only please
        /// </summary>
        public IReadOnlyList<GridVector2[]> InteriorRings
        {
            get
            {
                return _InteriorPolygons.Select(p => p._ExteriorRing).ToList();
            }
        }

        /// <summary>
        /// Return a list of all exterior and interior line segments
        /// </summary>
        public List<GridLineSegment> AllSegments
        {
            get
            {
                List<GridLineSegment> listLines = this.ExteriorSegments.ToList();

                listLines.AddRange(this.InteriorPolygons.SelectMany(inner => inner.AllSegments));

                return listLines;
            }
        }

        public bool HasInteriorRings
        {
            get
            {
                return _InteriorPolygons.Count > 0;
            }
        }

        /// <summary>
        /// Returns the point at the specified Index.  The iPoly value is not checked.
        /// </summary>
        /// <param name="index"></param>
        /// <returns></returns>
        public virtual GridVector2 this[PolygonIndex index]
        {
            get { return index.Point(this); }
            set { SetVertex(index, value); }
        }

        public GridPolygon(IEnumerable<IPoint2D> exteriorRing) : this(exteriorRing.Select(p => p.Convert()).ToArray())
        { }

        public GridPolygon(IEnumerable<GridVector2> exteriorRing) : this(exteriorRing.ToArray())
        { }

        public GridPolygon(GridVector2[] exteriorRing)
        {
            Debug.Assert(exteriorRing.Length < 1000, "This is a huge polygon, why?");

            if (!exteriorRing.IsValidClosedRing())
            {
                throw new ArgumentException("Exterior polygon ring must be valid");
            }

            //The only duplicate point should be the first and the last.  If not throw an exception
            var nonDuplicatedPoints = exteriorRing.RemoveDuplicates();
            if (nonDuplicatedPoints.Length != exteriorRing.Length - 1)
            {
                throw new ArgumentException("Duplicate point found in exterior ring");
            }

            if (exteriorRing.AreClockwise())
            {
                exteriorRing = exteriorRing.Reverse().ToArray();
            }

            ExteriorRing = exteriorRing;
        }


        public GridPolygon(IEnumerable<IPoint2D> exteriorRing, IEnumerable<IPoint2D[]> interiorRings)
            : this(exteriorRing.Select(p => p.Convert()).ToArray(),
                   interiorRings.Select(inner_ring => inner_ring.Select(p => p.Convert()).ToArray()).ToArray())
        {
        }

        public GridPolygon(GridVector2[] exteriorRing, IEnumerable<GridVector2[]> interiorRings)
        {
            Debug.Assert(exteriorRing.Length < 1000, "This is a huge polygon, why?");

            if (!exteriorRing.IsValidClosedRing())
            {
                throw new ArgumentException("Exterior polygon ring must be valid");
            }

            ExteriorRing = exteriorRing;

            foreach (GridVector2[] interiorRing in interiorRings)
            {
                Debug.Assert(interiorRing.Length < 1000, "This is a huge polygon, why?");
                AddInteriorRing(interiorRing);
            }
        }

        /// <summary>
        /// Area of the polygon, which is exterior ring area minus and interior ring areas.
        /// </summary>
        public double Area
        {
            get
            {
                double area = _ExteriorRingArea;
                double inner_area = _InteriorPolygons.Sum(ip => ip.Area);
                area -= inner_area;
                return area;
            }
        }

        public double Perimeter
        {
            get
            {
                return ExteriorRing.PerimeterLength();
            }
        }


        public ShapeType2D ShapeType
        {
            get
            {
                return ShapeType2D.POLYGON;
            }
        }

        IReadOnlyList<IPoint2D> IPolygon2D.ExteriorRing
        {
            get
            {
                return this.ExteriorRing.Select(p => p as IPoint2D).ToArray();
            }
        }

        IReadOnlyList<IPoint2D[]> IPolygon2D.InteriorRings
        {
            get
            {
                return this.InteriorRings.Select(ir => ir.Select(p => p as IPoint2D).ToArray()).ToArray();
            }
        }

        IReadOnlyList<IPolygon2D> IPolygon2D.InteriorPolygons => this._InteriorPolygons; //.Select(inner => inner as IPolygon2D).ToArray();

        /// <summary>
        /// All unique verticies.  This is calculated for every use
        /// </summary>
        public GridVector2[] AllVerticies
        {
            get
            {
                return ExteriorRing.Union(InteriorRings.SelectMany(i => i)).Distinct().ToArray();
            }
        }

        /// <summary>
        /// Total verticies, including the duplicate verticies at the end of each ring
        /// </summary>
        public int TotalVerticies
        {
            get
            {
                return ExteriorRing.Length + InteriorRings.Sum(ir => ir.Length);
            }
        }

        /// <summary>
        /// Total verticies, minus the duplicate verticies at the end of each ring
        /// </summary>
        public int TotalUniqueVerticies
        {
            get
            {
                return (ExteriorRing.Length - 1) + InteriorRings.Sum(ir => ir.Length - 1);
            }
        }
          
        IPoint2D ICentroid.Centroid => Centroid;
        
        /// <summary>
        /// Adds an Interior Ring to this polygon.  Input must not intersect the exterior ring or existing interior rings.
        /// </summary>
        /// <param name="interiorRing"></param>
        public void AddInteriorRing(IEnumerable<GridVector2> interiorRing)
        {
            GridPolygon innerPoly = new Geometry.GridPolygon(interiorRing);

            //TODO: Make sure the inner poly does not  intersect the outer ring or any existing inner ring
            AddInteriorRing(innerPoly);
        }

        /// <summary>
        /// Adds an Interior Ring to this polygon.  Input must not intersect the exterior ring or existing interior rings.
        /// </summary>
        public void AddInteriorRing(GridPolygon innerPoly)
        {
            //TODO: Make sure the inner poly does not intersect the outer ring or any existing inner ring

            if (this._InteriorPolygons.Any(p => p.Intersects(innerPoly)))
                throw new ArgumentException("Cannot add interior polygon that intersects and existing interior polygon");

            if (this.ExteriorSegments.Any(line => line.Intersects(innerPoly)))
                throw new ArgumentException("Cannot add interior polygon that intersects a polygon's exterior boundary");

            int iInner = _InteriorPolygons.Count;
            this._InteriorPolygons.Add(innerPoly);

            //We don't pass True to checking for intersections with other interior polygons because we checked at the start of this function
            if (this.IsInnerValid(iInner, false) == false)
            {
                this._InteriorPolygons.RemoveAt(iInner);
                throw new ArgumentException("Replacement inner polygon is not a valid addition");
            }
            else
            {
                AddRingToRTree(iInner);
            }

        }

        /// <summary>
        /// Remove the specied interior ring
        /// </summary>
        public void RemoveInteriorRing(int iInner)
        {
            this._InteriorPolygons.RemoveAt(iInner);

            //this._SegmentRTree = null; //Reset our RTree since yanking a polygon and changing the indicies are a pain
            RemoveRingFromRTree(iInner);
        }

        /// <summary>
        /// Replace the specied interior ring with a different polygon
        /// </summary>
        public void ReplaceInteriorRing(int iInner, GridPolygon replacement)
        {
            GridPolygon original = this._InteriorPolygons[iInner];

            RemoveRingFromRTree(iInner);
            this._InteriorPolygons.RemoveAt(iInner);

            if (this._InteriorPolygons.Any(p => p.Intersects(replacement)))
                throw new ArgumentException("Cannot add interior polygon that intersects and existing interior polygon");

            if (this.ExteriorSegments.Any(line => line.Intersects(replacement)))
                throw new ArgumentException("Cannot add interior polygon that intersects a polygon's exterior boundary");

            this._InteriorPolygons.Insert(iInner, replacement);

            if (this.IsInnerValid(iInner, true) == false)
            {
                this._InteriorPolygons[iInner] = original;
                AddRingToRTree(iInner);
                throw new ArgumentException("Replacement inner polygon is not a valid addition");
            }

            AddRingToRTree(iInner);
        }

        /// <summary>
        /// Remove the interior polygon that contains the hole position
        /// </summary>
        /// <param name="holePosition"></param>
        public bool TryRemoveInteriorRing(GridVector2 holePosition)
        {
            for (int iPoly = 0; iPoly < _InteriorPolygons.Count; iPoly++)
            {
                if (_InteriorPolygons[iPoly].Contains(holePosition))
                {
                    _InteriorPolygons.RemoveAt(iPoly);
                    RemoveRingFromRTree(iPoly);
                    return true;
                }
            }

            return false;
        }

        /// <summary>
        /// Remove the interior polygon that contains the hole position
        /// </summary>
        /// <param name="holePosition"></param>
        public bool TryRemoveInteriorRing(int innerPoly)
        {
            if (innerPoly >= this.InteriorPolygons.Count || innerPoly < 0)
                return false;

            _InteriorPolygons.RemoveAt(innerPoly);
            RemoveRingFromRTree(innerPoly);
            return true;
        }

        /// <summary>
        /// Adds a vertex to the polygon on the segment nearest to the point, including interior polygons.
        /// If the point is already a vertex no action is taken
        /// </summary>
        /// <param name="NewControlPointPosition"></param>
        public void AddVertex(GridVector2 NewControlPointPosition)
        {
            //Find the line segment the NewControlPoint intersects
            double segment_distance = this.NearestSegment(NewControlPointPosition, out PolygonIndex nearestSegment);

            //Don't bother adding a point that already exists
            if (segment_distance < Global.Epsilon && this[nearestSegment] == NewControlPointPosition)
                return;

            //Insert the new point as the new endpoint for the closest segment
            InsertVertex(NewControlPointPosition, nearestSegment.Next);
        }

        /// <summary>
        /// Adds a vertex to the polygon at the specified point index
        /// If the point is already a vertex no action is taken
        /// If the insertion would result in an invalid state an ArgumentException is thrown and the polygon is not changed.
        /// </summary>
        /// <param name="iVertex">The point we will be inserting before, the new points index will be this index when we are done</param>
        /// <param name="NewControlPointPosition"></param>
        /// <returns>True if the vertex was inserted.  False if it was not inserted because it already exists.</returns>
        public bool InsertVertex(GridVector2 NewControlPointPosition, PolygonIndex iVertex)
        {
            //Trace.WriteLine(string.Format("Add new Vertex {0} at {1}", iVertex, NewControlPointPosition));

            if (iVertex.iPoly != 0)
                iVertex = iVertex.Reindex(0);

            if (iVertex.IsInner)
            {
                GridPolygon original_poly = iVertex.Polygon(this).Clone() as GridPolygon;

                //If InserrVertex throws an exception it should have restored the inner polygon state, so we don't need to react to an exception here

                //If InsertVertex returns false, the vertex already existed and we don't need to update our own data structures or check validity.
                if (this.InteriorPolygons[iVertex.iInnerPoly.Value].InsertVertex(NewControlPointPosition, iVertex.ReindexToOuter(0)))
                {

                    //However, after the update we need to make sure the new inner polygon is valid in the context of the outer polygon
                    //so restore our state if we throw an exception
                    try
                    {
                        if (IsInnerValid(iVertex.iInnerPoly.Value, CheckForIntersectionWithOtherInnerPolygons: true))
                        {
                            UpdateSegmentRTreeForInsert(iVertex);
                        }
                        else
                        {
                            throw new ArgumentException("Inner polygon was valid itself, but invalid in the context of the exterior polygon");
                        }
                    }
                    catch (ArgumentException)
                    {
                        //Restore the inner polygon to a known good state before forwarding the exception
                        ReplaceInteriorRing(iVertex.iInnerPoly.Value, original_poly);
                        throw;
                    }
                }
            }
            else
            {
                //Ensure the new point is not on either endpoint of the segment we are inserting between
                if (iVertex.Point(this) == NewControlPointPosition)
                    return false;

                if (iVertex.Next.Point(this) == NewControlPointPosition)
                    return false;

                var original_verts = this.ExteriorRing;
                var original_bbox = this.BoundingBox;
                var original_area = this._ExteriorRingArea;
                var original_centroid = this._Centroid;
                var original_segments = this._ExteriorSegments;

                //Insert the new vertex into a copy of our exterior segments
                GridVector2[] updated_ring = this.ExteriorRing.InsertIntoClosedRing(iVertex.iVertex, NewControlPointPosition);

                //GridLineSegment[] updatedSegments = this.ExteriorSegments.Insert(NewControlPointPosition, iVertex.iVertex);
                //GridVector2[] updated_ring = updatedSegments.Verticies();
                double updated_area = updated_ring.PolygonArea();
                if (updated_area < 0)
                {
                    //An easy case we can catch before adjusting any data structures. 
                    //Reverse the change before throwing the exception
                    //this.ExteriorRing[iVertex.iVertex] = old_point;

                    //We could help the caller by reversing the winding... should we?
                    throw new ArgumentException($"Inserting vertex {iVertex} = {NewControlPointPosition} changed polygon winding order.");
                }

                this._ExteriorRingArea = updated_area;
                this._ExteriorRing = updated_ring;
                //this._ExteriorSegments = updatedSegments;
                this._ExteriorSegments = CreateLineSegments(_ExteriorRing);
                _Centroid = null;

                UpdateBoundingBoxForAdd(NewControlPointPosition);
                UpdateSegmentRTreeForInsert(iVertex);

                if (this.IsValid() == false)
                {
                    //Restore our state to a known good state before throwing the exception
                    //this.ExteriorRing = original_verts;


                    this._ExteriorRingArea = original_area;
                    this._BoundingRect = original_bbox;
                    this._ExteriorRing = original_verts;
                    this._Centroid = original_centroid;
                    this._ExteriorSegments = original_segments;

                    UpdateSegmentRTreeForRemoval(iVertex.ReindexToSize(_ExteriorRing.Length - 1));

                    throw new ArgumentException("Adding vertex resulted in an invalid state.");
                }
            }

            //this._SegmentRTree = null; //Reset our RTree since yanking a polygon and changing the indicies are a pain
            return true;
        }

        /// <summary>
        /// Set the specified vertex to the new position.
        /// If the new position results in an invalid polygon the polygon is restored to the original state and an ArgumentException is thrown.
        /// </summary>
        /// <param name="iVertex"></param>
        /// <param name="value"></param>
        public void SetVertex(PolygonIndex iVertex, GridVector2 value)
        {
            if (iVertex.iPoly != 0)
                iVertex = iVertex.Reindex(0);

            if (iVertex.IsInner)
            {
                GridPolygon original_poly = iVertex.Polygon(this).Clone() as GridPolygon;

                try
                {
                    GridPolygon poly = iVertex.Polygon(this);
                    poly.SetVertex(iVertex.ReindexToOuter(), value);

                    this._InteriorPolygons[iVertex.iInnerPoly.Value] = poly;

                    UpdateSegmentRTreeForUpdate(iVertex);

                    if (this.IsInnerValid(iVertex.iInnerPoly.Value, CheckForIntersectionWithOtherInnerPolygons: true) == false)
                    {
                        //this.ExteriorRing = original_verts;
                        throw new ArgumentException(string.Format("Changing vertex {0} to {1} resulted in an invalid state.", iVertex, value));
                    }
                }
                catch (ArgumentException)
                {
                    //Restore our state
                    ReplaceInteriorRing(iVertex.iInnerPoly.Value, original_poly);
                    throw;
                }
            }
            else
            {
                GridVector2 old_point = this.ExteriorRing[iVertex.iVertex];
                this.ExteriorRing[iVertex.iVertex] = value;

                if (_ExteriorRingArea < 0)
                {
                    //An easy case we can catch before adjusting data structures. 
                    //Reverse the change before throwing the exception
                    this.ExteriorRing[iVertex.iVertex] = old_point;

                    //We could help the caller by reversing the winding... should we?
                    throw new ArgumentException(string.Format("Changing vertex { 0 } to {1} changed polygon winding order.", iVertex, value));
                }

                //Update our data structures, then check that we are still valid:
                UpdateBoundingBoxForAdd(value);
                UpdateBoundingBoxForRemove(old_point);

                UpdateSegmentRTreeForUpdate(iVertex);

                _Centroid = null;

                if (this.IsValid() == false)
                {
                    //Restore our ExteriorRing
                    this.ExteriorRing[iVertex.iVertex] = old_point;

                    //Restore our bounding box
                    UpdateBoundingBoxForRemove(value);
                    UpdateBoundingBoxForAdd(old_point);

                    //Restore our RTree
                    UpdateSegmentRTreeForUpdate(iVertex);

                    throw new ArgumentException(string.Format("Changing vertex {0} to {1} resulted in an invalid state.", iVertex, value));
                }
            }
        }


        /// <summary>
        /// Removes the vertex closest to the passed point
        /// </summary>
        /// <param name="RemovedControlPointPosition"></param>
        public void RemoveVertex(GridVector2 RemovedControlPointPosition)
        {
            double MinDistance = this.NearestVertex(RemovedControlPointPosition, out PolygonIndex index);

            RemoveVertex(index);
        }

        public void RemoveVertex(PolygonIndex iVertex)
        {
            if (iVertex.iPoly != 0)
                iVertex = iVertex.Reindex(0);
            //GridPolygon poly = iVertex.Polygon(this);

            //poly.RemoveVertex(iVertex.iVertex);

            if (iVertex.IsInner)
            {
                GridPolygon original_poly = iVertex.Polygon(this).Clone() as GridPolygon;

                this._InteriorPolygons[iVertex.iInnerPoly.Value].RemoveVertex(iVertex.ReindexToOuter());
                //this.InteriorRings[iVertex.iInnerPoly.Value] = this.InteriorPolygons[iVertex.iInnerPoly.Value]._ExteriorRing;
                try
                {
                    if (this.IsInnerValid(iVertex.iInnerPoly.Value, CheckForIntersectionWithOtherInnerPolygons: true))
                    {
                        UpdateSegmentRTreeForRemoval(iVertex);
                    }
                    else
                    {
                        throw new ArgumentException($"Removing vertex {iVertex} resulted in an invalid state.");
                    }
                }
                catch (ArgumentException)
                {
                    this.ReplaceInteriorRing(iVertex.iInnerPoly.Value, original_poly);
                }
            }
            else
            {
                //We must have at least 3 points to create a polygon
                if (ExteriorSegments.Length <= 3)
                {
                    throw new ArgumentException("Cannot remove vertex.  Polygon's must have three verticies.");
                }

                GridVector2 removedVertex = this[iVertex];

                var original_verts = this.ExteriorRing;
                var original_bbox = this.BoundingBox;
                var original_area = this._ExteriorRingArea;
                var original_centroid = this._Centroid;
                var original_segments = this._ExteriorSegments;

                GridVector2[] updated_ring = this.ExteriorRing.RemoveFromClosedRing(iVertex.iVertex);
                double updated_area = updated_ring.PolygonArea();
                if (updated_area < 0)
                {
                    //An easy case we can catch before adjusting any data structures. 
                    //We could help the caller by reversing the winding... should we?
                    throw new ArgumentException(string.Format("Removing vertex {0} changed polygon winding order.", iVertex));
                }

                this._ExteriorRingArea = updated_area;
                this._ExteriorRing = updated_ring;
                this._ExteriorSegments = CreateLineSegments(_ExteriorRing);
                this._Centroid = null;
                this.UpdateBoundingBoxForRemove(removedVertex);
                UpdateSegmentRTreeForRemoval(iVertex);

                //this._SegmentRTree = null; //Reset our RTree since yanking a polygon and changing the indicies are a pain

                if (this.IsValid() == false)
                {
                    this._ExteriorRingArea = original_area;
                    this._BoundingRect = original_bbox;
                    this._ExteriorRing = original_verts;
                    this._Centroid = original_centroid;
                    this._ExteriorSegments = original_segments;

                    //Restore our state to a known good state before throwing the exception
                    UpdateSegmentRTreeForInsert(iVertex);
                    throw new ArgumentException(string.Format("Removing vertex {0} of {1} from polygon resulted in an invalid state", iVertex, this.ExteriorRing.Length - 1));
                }
            }

            //this._SegmentRTree = null; //Reset our RTree since yanking a polygon and changing the indicies are a pain
        }

        /// <summary>
        /// Removes the vertex from the exterior ring of a polgon only
        /// </summary>
        /// <param name="iVertex"></param>
        public void RemoveVertex(int iVertex)
        {
            RemoveVertex(new PolygonIndex(0, iVertex, this.ExteriorRing.Length - 1));
            /*
            //We must have at least 3 points to create a polygon
            if (ExteriorSegments.Length <= 3)
            {
                throw new ArgumentException("Cannot remove vertex.  Polygon's must have three verticies.");
            }

            GridVector2 removedVertex = this.ExteriorRing[iVertex];
            GridVector2[] original_verts = this.ExteriorRing;

            UpdateSegmentRTreeForRemoval(new PointIndex(0, iVertex, this._ExteriorRing.Length - 1));
            this.UpdateBoundingBoxForRemove(removedVertex);

            //Find the line segment the NewControlPoint intersects
            GridLineSegment[] updatedLineSegments = ExteriorSegments.Remove(iVertex);
            
            this._ExteriorRing = updatedLineSegments.Verticies();
            this._ExteriorSegments = updatedLineSegments;
            this._ExteriorRingArea = this._ExteriorRing.PolygonArea();
            if(_ExteriorRingArea < 0)
            {
                InsertVertex(removedVertex, new PointIndex(0, iVertex, this._ExteriorRing.Length-1));
                //ExteriorRing = original_verts;
                throw new ArgumentException(string.Format("Removing vertex {0} of {1} reversed winding order.", iVertex, this.ExteriorRing.Length - 1));
            }

            this._Centroid = null;

            //this._SegmentRTree = null; //Reset our RTree since yanking a polygon and changing the indicies are a pain

            if(this.IsValid() == false)
            {
                //Brute force restoration of the vertex
                //ExteriorRing = original_verts;
                InsertVertex(removedVertex, new PointIndex(0, iVertex, this._ExteriorRing.Length - 1));
                throw new ArgumentException(string.Format("Removing vertex {0} of {1} from polygon resulted in an invalid state", iVertex, this.ExteriorRing.Length - 1));
            }*/
        }

        #region Cached Values Update Code

        /// <summary>
        /// Update the ExteriorSegments of this polygon to account for a vertex insert
        /// </summary>
        /// <param name="index">The index of the vertex that was already inserted into the exterior ring</param>
        private void UpdateSegmentRTreeForInsert(PolygonIndex index)
        {
            if (_SegmentRTree == null)
                return;

            if (index.NumUniqueInRing != index.Polygon(this).ExteriorRing.Length - 1)
            {
                index = new PolygonIndex(index.iPoly, index.iInnerPoly, index.iVertex, index.Polygon(this).ExteriorRing.Length - 1);
            }

            /////////////////////////////////////////////////////////////////
            //Adjust the size of the ring for all PointIndicies in the RTree
            //For the remaining rectangles they are unchanged, but the indicies need to be updated to make room for our updates
            PolygonIndex updateIndex = index.LastInRing;
            while (updateIndex != index)
            {
                _SegmentRTree.Update(updateIndex.Previous.ReindexToSize(updateIndex.NumUniqueInRing - 1), updateIndex); //Increment all of the indicies in the current ring after the index we inserted
                updateIndex = updateIndex.Previous;
            }

            updateIndex = updateIndex.Previous;

            //The remaining indicies are unchanged, but update the size of the ring they index
            while (updateIndex != index.LastInRing)
            {
                _SegmentRTree.Update(updateIndex.ReindexToSize(updateIndex.NumUniqueInRing - 1), updateIndex); //Increment all of the indicies in the current ring after the index we inserted
                updateIndex = updateIndex.Previous;
            }
            /////////////////////////////////////////////////////////////////

            //This function needs a revisit.  I haven't decided whether the passed index should represent the expanded ring or the current ring.
            GridLineSegment oldSeg = new GridLineSegment(this[index.Previous], this[index.Next]);

            GridLineSegment newSeg = new GridLineSegment(this[index.Previous], this[index]);
            GridLineSegment newNextSeg = new GridLineSegment(this[index], this[index.Next]);

            bool RTreePreviousItemFound = _SegmentRTree.Delete(index.Previous, out PolygonIndex rTreeRemovedPreviousItem);
            Debug.Assert(RTreePreviousItemFound, "Expected to find removed segment (previous) in the RTree");

            //We should have renamed the index mapped segment, so no need to remove here
            //bool RTreeItemFound = _SegmentRTree.Delete(index, out PointIndex rTreeRemovedItem);
            //Debug.Assert(RTreeItemFound, "Expected to find removed segment in the RTree");

            //Add the two new segments
            _SegmentRTree.Add(newSeg.BoundingBox, index.Previous);
            _SegmentRTree.Add(newNextSeg.BoundingBox, index);
        }

        /// <summary>
        /// Update the ExteriorSegments of this polygon to account for a vertex change
        /// </summary>
        private void UpdateSegmentRTreeForUpdate(PolygonIndex index)
        {
            if (_SegmentRTree == null)
                return;

            GridLineSegment newPrevSeg = new GridLineSegment(this[index.Previous], this[index]);
            GridLineSegment newSeg = new GridLineSegment(this[index], this[index.Next]);

            _ExteriorSegments[index.Previous.iVertex] = newPrevSeg;
            _ExteriorSegments[index.iVertex] = newSeg;

            bool RTreePreviousItemFound = _SegmentRTree.Delete(index.Previous, out PolygonIndex rTreeRemovedPreviousItem);
            Debug.Assert(RTreePreviousItemFound, "Expected to find removed segment (previous) in the RTree");

            bool RTreeItemFound = _SegmentRTree.Delete(index, out PolygonIndex rTreeRemovedItem);
            Debug.Assert(RTreeItemFound, "Expected to find removed segment in the RTree");

            _SegmentRTree.Add(newSeg.BoundingBox, index);
            _SegmentRTree.Add(newPrevSeg.BoundingBox, index.Previous);
        }

        /// <summary>
        /// Update the ExteriorSegments of this polygon to account for a vertex removal.  
        /// Called after the vertex has been removed from the ring
        /// </summary>
        private void UpdateSegmentRTreeForRemoval(PolygonIndex removed_index)
        {
            if (_SegmentRTree == null)
                return;

            GridPolygon poly = removed_index.Polygon(this);

            //Ensure the removed index has the correct ring length
            if (removed_index.NumUniqueInRing != poly.ExteriorRing.Length)
            {
                removed_index = new PolygonIndex(removed_index.iPoly, removed_index.iInnerPoly, removed_index.iVertex, poly.ExteriorRing.Length);
            }

            //The index scaled to the new ring size
            PolygonIndex new_index = new PolygonIndex(removed_index.iPoly, removed_index.iInnerPoly, removed_index.iVertex, poly.ExteriorRing.Length - 1);

            GridLineSegment newSeg = new GridLineSegment(this[new_index.Previous], this[new_index]);

            bool RTreeItemFound = _SegmentRTree.Delete(removed_index, out PolygonIndex rTreeRemovedItem);
            Debug.Assert(RTreeItemFound, "Expected to find removed segment in the RTree");

            bool RTreePreviousItemFound = _SegmentRTree.Delete(removed_index.Previous, out PolygonIndex rTreeRemovedPreviousItem);
            Debug.Assert(RTreePreviousItemFound, "Expected to find removed segment (previous) in the RTree");

            _SegmentRTree.Add(newSeg.BoundingBox, new_index.Previous);

            //Adjust the index of all remaining points in the ring.

            PolygonIndex updateIndex = removed_index;
            while (updateIndex != updateIndex.LastInRing && updateIndex.Next != removed_index.Previous) //Second test is for edge case of remove_index == 0
            {
                _SegmentRTree.Update(updateIndex.Next, updateIndex.ReindexToSize(updateIndex.NumUniqueInRing - 1));
                updateIndex = updateIndex.Next;
            }

            //No need to adjust indicies if we adjusted index 0 already
            if (removed_index == updateIndex.FirstInRing)
                return;

            updateIndex = updateIndex.FirstInRing;
            while (updateIndex != removed_index.Previous)
            {
                _SegmentRTree.Update(updateIndex, updateIndex.ReindexToSize(updateIndex.NumUniqueInRing - 1));
                updateIndex = updateIndex.Next;
            }
        }

        /*
        /// <summary>
        /// Update the ExteriorSegments of this polygon to account for a vertex removal.  
        /// Called before the vertex has been removed from the ring
        /// </summary>
        private void UpdateSegmentRTreeForRemoval(PointIndex index)
        {
            if (_SegmentRTree == null)
                return;

            GridLineSegment removedSegment = new GridLineSegment(this[index], this[index.Next]);
            GridLineSegment removedPrevSeg = new GridLineSegment(this[index.Previous], this[index]);
            GridLineSegment newSeg = new GridLineSegment(this[index.Previous], this[index.Next]);

            bool RTreeItemFound = _SegmentRTree.Delete(index, out PointIndex rTreeRemovedItem);
            Debug.Assert(RTreeItemFound, "Expected to find removed segment in the RTree");

            bool RTreePreviousItemFound = _SegmentRTree.Delete(index.Previous, out PointIndex rTreeRemovedPreviousItem);
            Debug.Assert(RTreePreviousItemFound, "Expected to find removed segment (previous) in the RTree");

            _SegmentRTree.Add(newSeg.BoundingBox, index.Previous.ReindexToSize(index.NumUniqueInRing-1));

            //Adjust the index of all remaining points in the ring.
            PointIndex updateIndex = index; 
            while (updateIndex.IsLastIndexInRing() == false)
            {
                _SegmentRTree.Update(updateIndex.Next, updateIndex.ReindexToSize(updateIndex.NumUniqueInRing-1));
                updateIndex = updateIndex.Next;
            }

            updateIndex = updateIndex.FirstInRing;
            while (updateIndex != index.Previous)
            {
                _SegmentRTree.Update(updateIndex, updateIndex.ReindexToSize(updateIndex.NumUniqueInRing - 1));
                updateIndex = updateIndex.Next;
            }
        }
        */

        private void AddRingToRTree(int iInnerRing)
        {
            if (_SegmentRTree == null)
                return;

            PolygonIndex index = new PolygonIndex(0, iInnerRing, 0, this.InteriorRings[iInnerRing].Length - 1);
            do
            {
                _SegmentRTree.Add(index.Segment(this).BoundingBox, index);
                index = index.Next;
            }
            while (index != index.FirstInRing);
        }

        private void RemoveRingFromRTree(int iInnerRing)
        {
            if (_SegmentRTree == null)
                return;

            PolygonIndex index = new PolygonIndex(0, iInnerRing, 0, this.InteriorRings[iInnerRing].Length - 1);
            do
            {
                bool found = _SegmentRTree.Delete(index, out PolygonIndex removed);
                Debug.Assert(found, $"Expected index {index} missing from RTree");
                index = index.Next;
            }
            while (index != index.FirstInRing);
        }


        /// <summary>
        /// Grow a bounding box if an added point falls outside it's boundaries
        /// </summary>
        /// <param name="bbox"></param>
        /// <param name="oldPoint"></param>
        /// <param name="newPoint"></param>
        /// <returns>True if the bounding box changed</returns>
        private bool UpdateBoundingBoxForAdd(GridVector2 point)
        {
            return _BoundingRect.Union(point);
        }

        /// <summary>
        /// Shrink a bounding box if a removed point was on the boundaries
        /// </summary>
        /// <param name="bbox"></param>
        /// <param name="oldPoint"></param>
        /// <param name="newPoint"></param>
        /// <returns>True if the bounding box changed</returns>
        private bool UpdateBoundingBoxForRemove(GridVector2 removed_point)
        {
            if (_BoundingRect.ContainsExt(removed_point) == OverlapType.TOUCHING)
            {
                _BoundingRect = _ExteriorRing.BoundingBox();
                return true;
            }

            return false;
        }
        #endregion

        public bool IsValid()
        {
            if (this.ExteriorRing.Distinct().Count() != this.ExteriorRing.Length - 1)
                return false;

            //if (this.ExteriorSegments.SelfIntersects(LineSetOrdering.CLOSED))
            if (GridPolygon.SelfIntersects(this))
                return false;

            //Check that the interior polygons are inside the exterior ring
            if (this.InteriorPolygons.Count == 0)
            {
                return true;
            }
            else
            {
                GridPolygon externalPolyOnly = new GridPolygon(this.ExteriorRing);

                //Check interior polygons for validity against the exterior
                for (int iInnerPoly = 0; iInnerPoly < this.InteriorPolygons.Count; iInnerPoly++)
                {
                    if (IsInnerValid(iInnerPoly, CheckForIntersectionWithOtherInnerPolygons: false) == false)
                        return false;
                }

                //Check interior polygons for intersection with other inner polygons
                if (AnyInnerPolygonsIntersect())
                    return false;
            }

            return true;
        }

        /// <summary>
        /// Return true if the exterior ring intersects itself
        /// </summary>
        /// <param name="lines"></param>
        /// <param name="IsClosedRing">True if the polyline forms a closed ring, in which case the first and last points are allowed to overlap</param>
        /// <returns></returns>
        private static bool SelfIntersects(GridPolygon poly)
        {
            IReadOnlyList<GridLineSegment> lines = poly.ExteriorSegments;

            PolygonIndex Index = new PolygonIndex(0, 0, poly.ExteriorRing.Length - 1);
            PolygonIndex FirstRingIndex = Index.FirstInRing;

            do
            {
                GridLineSegment ls = Index.Segment(poly);
                var candidates = poly.IntersectingSegments(ls);

                foreach (var candidate in candidates.Values)
                {
                    if (candidate == Index)
                        continue;
                    if (candidate.AreAdjacent(Index))
                        continue;

                    return true;
                }

                Index = Index.Next;
            }
            while (Index != FirstRingIndex);

            return false;

            /*

            for (int iLine = 0; iLine < lines.Count; iLine++)
            {
                

                foreach(var candidate in candidates.Values)
                {
                    if(candidate.)
                }

                for (int jLine = iLine + 1; jLine < lines.Count; jLine++)
                {
                    //For polyline and closed loops for adjacent lines we only need to check that the endpoints aren't equal to know that the lines do not overlap
                    if (iLine + 1 == jLine)
                    {
                        if (lines[iLine].A != lines[jLine].B)
                            continue;
                    }

                    bool EndpointsOnRingDoNotIntersect = LineSetOrdering.CLOSED.IsEndpointIntersectionExpected(iLine, jLine, lines.Count);

                    if (lines[iLine].Intersects(lines[jLine], EndpointsOnRingDoNotIntersect: EndpointsOnRingDoNotIntersect))
                        return true;
                }
            }

            return false;*/
        }

        /// <summary>
        /// Return true if any interior polygons intersect each other
        /// </summary>
        /// <returns></returns>
        private bool AnyInnerPolygonsIntersect()
        {
            int[] InnerIndicies = this.InteriorPolygons.Select((p, i) => i).ToArray();
            foreach (var combo in InnerIndicies.CombinationPairs())
            {
                GridPolygon A = InteriorPolygons[combo.A];
                GridPolygon B = InteriorPolygons[combo.B];

                if (A.Intersects(B))
                    return true;
            }

            return false;
        }

        /// <summary>
        /// Assumes exterior and other inner rings are valid.  Checks if the inner polygon at the specified index is valid.
        /// </summary>
        /// <param name="iInner"></param>
        /// <param name="CheckForIntersectionWithOtherInnerPolygons">If false only worry about the interior polygons validity and that it does not collide with the exterior.  
        /// Setting to false is currently done to optimize when we want to check all interior polygons against each other. </param>
        /// <returns></returns>
        private bool IsInnerValid(int iInner, bool CheckForIntersectionWithOtherInnerPolygons = false)
        {
            GridPolygon innerPoly = this.InteriorPolygons[iInner];

            if (innerPoly.IsValid() == false)
                return false;

            GridPolygon externalPolyOnly = new GridPolygon(this.ExteriorRing);

            //Do a quick sanity check that all interior verticies are inside the external polygon
            if (innerPoly.ExteriorRing.Any(v => externalPolyOnly.BoundingBox.Contains(v) == false))
                return false;

            //Perform the expensive intersection test
            if (innerPoly.Intersects(externalPolyOnly))
            {
                return false;
            }

            if (CheckForIntersectionWithOtherInnerPolygons)
            {
                //Check against the other interior polygons to ensure they do not intersect
                for (int i = 0; i < this.InteriorRings.Count; i++)
                {
                    //Don't check inner ring against itself
                    if (i == iInner)
                        continue;

                    GridPolygon otherInner = this.InteriorPolygons[i];

                    if (innerPoly.Intersects(otherInner))
                        return false;
                }
            }

            return true;
        }

        /// <summary>
        /// Return true if the point is one of the polygon verticies
        /// </summary>
        /// <param name="point"></param>
        /// <returns></returns>
        public bool IsVertex(GridVector2 point)
        {
            if (!this.BoundingBox.Contains(point))
            {
                return false;
            }

            if (this.ExteriorRing.Contains(point))
                return true;

            foreach (GridPolygon inner in this.InteriorPolygons)
            {
                if (!inner.BoundingBox.Contains(point))
                {
                    continue;
                }

                if (inner.IsVertex(point))
                    return true;
            }

            return false;
        }

        /// <summary>
        /// Return true if the point is one of the polygon verticies
        /// </summary>
        /// <param name="point">The PointIndex of the point if it is a vertex</param>
        /// <returns></returns>
        public bool TryGetIndex(GridVector2 point, out PolygonIndex index)
        {

            if (!this.BoundingBox.Contains(point))
            {
                index = new PolygonIndex();
                return false;
            }

            int iVert = this.ExteriorRing.IndexOf(point);
            if (iVert >= 0)
            {
                index = new PolygonIndex(0, iVert, this.ExteriorRing.Length - 1);
                return true;
            }

            for (int iInner = 0; iInner < InteriorPolygons.Count; iInner++)
            {
                GridPolygon inner = InteriorPolygons[iInner];
                if (!inner.BoundingBox.Contains(point))
                {
                    continue;
                }

                if (inner.TryGetIndex(point, out index))
                {
                    index = index.ReindexToInner(iInner, 0);
                    return true;
                }
            }

            index = new PolygonIndex();
            return false;
        }

        /// <summary>
        /// Return true if the point is one of the polygon verticies
        /// </summary>
        /// <param name="point"></param>
        /// <returns></returns>
        public List<PolygonIndex> TryGetIndicies(ICollection<GridVector2> points)
        {
            List<PolygonIndex> found = new List<PolygonIndex>(points.Count);
            var candidates = points.Where(p => BoundingBox.Contains(p));
            List<GridVector2> notExterior = new List<GridVector2>(points.Count);

            foreach (GridVector2 point in points)
            {
                int iVert = this.ExteriorRing.IndexOf(point);
                if (iVert >= 0)
                {
                    found.Add(new PolygonIndex(0, iVert, this.ExteriorRing.Length - 1));
                    continue;
                }
                else
                {
                    for (int iInner = 0; iInner < InteriorPolygons.Count; iInner++)
                    {
                        if (InteriorPolygons[iInner].Contains(point) == false)
                            continue;

                        if (this.InteriorPolygons[iInner].TryGetIndex(point, out PolygonIndex innerIndex))
                        {
                            found.Add(innerIndex.ReindexToInner(iInner, 0));
                            break;
                        }
                    }
                }
            }

            return found;
        }

        /// <summary>
        /// Returns true if the vertex on the exterior ring is concave
        /// </summary>
        /// <param name="iVert"></param>
        /// <returns></returns>
        public Concavity IsVertexConcave(int iVert, out double Angle)
        {
            int A = iVert - 1 < 0 ? ExteriorRing.Length - 2 : iVert - 1;
            int Origin = iVert;
            int B = iVert + 1 >= ExteriorRing.Length ? 1 : iVert + 1;

            Angle = GridVector2.AbsArcAngle(ExteriorRing[A], ExteriorRing[Origin], ExteriorRing[B], Clockwise: true);

            if (Angle == 0)
                return Concavity.PARALLEL;
            else if (Angle < Global.Epsilon)
            {
                var AB = new GridLineSegment(ExteriorRing[A], ExteriorRing[B]);
                if (AB.DistanceToPoint(ExteriorRing[iVert]) < Global.Epsilon)
                    return Concavity.PARALLEL;
            }

            if (Angle > Math.PI)
            {
                return Concavity.CONCAVE;
            }
            else
            {
                return Concavity.CONVEX;
            }
        }

        /// <summary>
        /// Returns true if the vertex on the exterior ring is concave
        /// </summary>
        /// <param name="iVert"></param>
        /// <returns></returns>
        public Concavity[] VertexConcavity(out double[] Angles)
        {
            Concavity[] results = new Concavity[ExteriorRing.Length];
            Angles = new double[ExteriorRing.Length];

            for (int i = 0; i < ExteriorRing.Length - 1; i++)
            {
                results[i] = IsVertexConcave(i, out Angles[i]);
                //Trace.WriteLine(string.Format("{0}: {1} {2}", i, results[i], Angles[i]));
            }

            results[ExteriorRing.Length - 1] = results[0];
            Angles[ExteriorRing.Length - 1] = Angles[0];

            return results;
        }

        /// <summary>
        /// Returns true if all verticies on the exterior ring are convex or parallel
        /// </summary>
        /// <param name="iVert"></param>
        /// <returns></returns>
        public bool IsConvex()
        {
            return this.VertexConcavity(out double[] angles).All(c => c != Concavity.CONCAVE);
        }

        /// <summary>
        /// Returns the Polygon vertex closest to the point.  May return interior verticies
        /// </summary>
        /// <param name="polygon"></param>
        /// <param name="WorldPosition"></param> 
        /// <param name="nearestPoly">Nearest polygon</param>
        /// <param name="intersectingPoly">Index of vertex in the ring</param>
        /// <returns></returns>
        public double NearestVertex(GridVector2 WorldPosition, out PolygonIndex nearestVertex)
        {
            nearestVertex = new PolygonIndex(0, 0, ExteriorRing.Length - 1);
            double nearestVertexDistance = GridVector2.Distance(WorldPosition, ExteriorRing[0]);
            bool CloserVertexFound = false;

            do
            {
                CloserVertexFound = false;
                var bbox = new GridRectangle(WorldPosition, nearestVertexDistance);
                //Try to find a nearer segment than our initial point, if we do, then repeat the search
                foreach (PolygonIndex index in SegmentRTree.IntersectionGenerator(bbox))
                {
                    var seg = index.Segment(this);
                    double measured_distance = GridVector2.DistanceSquared(seg.A, WorldPosition);
                    if (measured_distance < nearestVertexDistance)
                    {
                        nearestVertexDistance = measured_distance;
                        nearestVertex = index;
                        CloserVertexFound = true;

                        //If it is a perfect match then stop searching
                        if (seg.A == WorldPosition)
                        {
                            return measured_distance;
                        }
                    }

                    measured_distance = GridVector2.DistanceSquared(seg.B, WorldPosition);
                    if (measured_distance < nearestVertexDistance)
                    {
                        nearestVertexDistance = measured_distance;
                        nearestVertex = index.Next;
                        CloserVertexFound = true;

                        //If it is a perfect match then stop searching
                        if (seg.B == WorldPosition)
                        {
                            return measured_distance;
                        }
                    }
                }
            }
            while (CloserVertexFound);

            return nearestVertexDistance;
            /*
            for (int iRing = 0; iRing < InteriorPolygons.Count; iRing++)
            {
                GridPolygon innerPoly = InteriorPolygons[iRing];
                double distance = innerPoly.NearestVertex(WorldPosition, out PointIndex foundIndex);
                if (distance < nearestPolyDistance)
                {
                    nearestVertex = new PointIndex(0, iRing, foundIndex.iVertex, innerPoly.ExteriorRing.Length - 1);
                    nearestPolyDistance = distance;
                }
            }

            double[] distances = ExteriorRing.Select(p => GridVector2.Distance(p, WorldPosition)).ToArray();
            double MinDistance = distances.Min();

            if (MinDistance < nearestPolyDistance)
            {
                int iVert = Array.IndexOf(distances, distances.Min());
                nearestVertex = new PointIndex(0, iVert, ExteriorRing.Length - 1);
                nearestPolyDistance = MinDistance;
            }

            return nearestPolyDistance;*/

        }

        /// <summary>
        /// Returns the nearest segment to the point and the PointIndex of the line, use the Next function to obtain the vertex after the line
        /// In the case where the segments are a poly-line and p is an endpoint, the segment with segment.A == p is returned.
        /// </summary>
        /// <param name="WorldPosition">Point we are measuring against</param>
        /// <param name="nearestVertex">The index of the first ("A") endpoint of the segment.</param>
        /// <returns></returns>
        public double NearestSegment(GridVector2 WorldPosition, out PolygonIndex nearestVertex)
        {
            //Start with a random bounding box, and check all intersections, shrinking the bounding box each time
            double nearestPolyDistance = GridVector2.Distance(WorldPosition, ExteriorRing[0]);
            nearestVertex = new PolygonIndex(0, 0, ExteriorRing.Length - 1);
            bool CloserSegmentFound = false;

            do
            {
                CloserSegmentFound = false;

                //Create a search box around our point of the minimum distance we know of
                var bbox = new GridRectangle(WorldPosition, nearestPolyDistance);

                //Try to find a nearer segment than our initial point, if we do, then repeat the search
                foreach (PolygonIndex index in SegmentRTree.IntersectionGenerator(bbox))
                {
                    if (index == nearestVertex)
                        continue; //No need to recheck the current winner

                    var seg = index.Segment(this);
                    double measured_distance = seg.DistanceToPoint(WorldPosition);
                    if (measured_distance < nearestPolyDistance)
                    {
                        nearestPolyDistance = measured_distance;
                        nearestVertex = index;
                        CloserSegmentFound = true;

                        //If we are super close to a segment then just make sure that if we are equal to  vertex we are returning the correct one
                        if (measured_distance < Global.Epsilon)
                        {
                            if (seg.B == WorldPosition)
                            {
                                nearestVertex = nearestVertex.Next;
                                return nearestPolyDistance;
                            }
                            else
                            {
                                return nearestPolyDistance;
                            }
                        }

                        break;
                    }
                }
            }
            while (CloserSegmentFound);

            return nearestPolyDistance;
        }


        public bool Contains(IPoint2D point_param)
        {
            return ContainsExt(point_param) != OverlapType.NONE;
        }

        public OverlapType ContainsExt(IPoint2D point_param)
        {
            if (!_BoundingRect.Contains(point_param))
                return OverlapType.NONE;

            GridVector2 p = new GridVector2(point_param.X, point_param.Y);

            //Create a line we know must pass outside the polygon
            //There is an edge case where the test line passes through a polygon vertex, so make sure the test line does not cross any verticies
            //GridVector2 targetPoint = new GridLineSegment(this.ExteriorRing[0], this.ExteriorRing[1]).Bisect();
            //GridVector2 targetPoint = new GridLineSegment(p.X, p.Y + this.ExteriorRing[0], this.ExteriorRing[1]).Bisect();

            //GridLine test_ray = new GridLine(point_param, targetPoint - point_param);

            //GridLineSegment test_line = test_ray.ToLine(Math.Max(BoundingBox.Width, BoundingBox.Height) * 2);


            List<GridLineSegment> segmentsToTest;

            if (_ExteriorSegments.Length > 32)// || HasInteriorRings)
            {
                segmentsToTest = _ExteriorSegments.ToList();

                ///This doesn't work because rTree returns the points in arbitrary order, and the line list must be passed to IsPointInsidePolygon in the order they appear on the ring.
                /*
                GridVector2 line_endpoint_translation = new GridVector2(BoundingBox.Width * 1.5, 0);
                GridLineSegment test_line_seg = new Geometry.GridLineSegment(p - line_endpoint_translation, p + line_endpoint_translation);
                var intersectingSegments = this.GetIntersectingSegments(test_line_seg.BoundingBox);
                segmentsToTest = this.AllSegments.Where(s => intersectingSegments.Contains(s)).ToList();
                */
            }
            else
            {
                segmentsToTest = _ExteriorSegments.ToList();
            }

            //Make a horizontal line
            GridLine test_line = new GridLine(p, GridVector2.UnitX);

            //Test all of the line segments for both interior and exterior polygons
            //return IsPointInsidePolygonByWindingTest(segmentsToTest, test_line); 
            OverlapType result = IsPointInsidePolygonByWindingTest(segmentsToTest, test_line);
            if (result == OverlapType.CONTAINED)
            {
                foreach (GridPolygon inner in this.InteriorPolygons)
                {
                    OverlapType inner_result = inner.ContainsExt(p);
                    //if (inner_result != OverlapType.NONE) //Including TOUCHING results probably breaks Bajaj generation, but it is correct
                    if (inner_result == OverlapType.CONTAINED)
                        return OverlapType.NONE; //The point is in the inner polygon, therefore not part of this polygon
                }
            }

            return result;
        }

        /*
        static Random random = new Random();
        public bool ContainsWithPolyRayTest(IPoint2D point_param)
        {
            if (!_BoundingRect.Contains(point_param))
                return false;

            GridVector2 p = new GridVector2(point_param.X, point_param.Y);
            GridLineSegment? test_line = new GridLineSegment?();
            GridLine test_ray;
            //Create a line we know must pass outside the polygon
            //There is an edge case where the test line passes through a polygon vertex, so make sure the test line does not cross any verticies
            double test_line_length = Math.Max(BoundingBox.Width, BoundingBox.Height) * 2;
            GridVector2[] AllVerticies = this.AllVerticies;

            if (AllVerticies.Any(v => v == point_param))
                return true; 

            while (test_line.HasValue == false)
            {
                foreach (GridLineSegment s in this.ExteriorSegments)
                {
                    
                    GridVector2 targetPoint = s.PointAlongLine(random.NextDouble());
                    if (targetPoint == point_param)
                        continue;

                    test_ray = new GridLine(point_param, targetPoint - point_param);

                    test_line = test_ray.ToLine(test_line_length);
                    if (AllVerticies.Any(v => test_line.Value.DistanceToPoint(v) <= Global.Epsilon))
                    {
                        test_line = null; 
                        continue; //Too close to a vertex.  Try another target
                    }

                    break;
                }
            }
            
            
            //GridLineSegment test_line = new Geometry.GridLineSegment(p, new GridVector2(p.X + (BoundingBox.Width*2), p.Y));

            List<GridLineSegment> segmentsToTest;

            if (_ExteriorSegments.Length > 32 || HasInteriorRings)
            {
                segmentsToTest = this.GetIntersectingSegments(test_line.Value);
            }
            else
            {
                segmentsToTest = _ExteriorSegments.ToList();
            }

            //Test all of the line segments for both interior and exterior polygons
            return IsPointInsidePolygonByRayTest(segmentsToTest, test_line.Value);
        }*/

        public bool Contains(GridLineSegment line)
        {
            if (line.BoundingBox.ContainsExt(this.BoundingBox) == OverlapType.NONE)
                return false;

            //Ensure both endpoints are inside and a point in the center.
            //Test the center because if the line crosses a concave region with both endpoints exactly on the exterior ring we'd not have any intersections but the poly would not contain the line.
            if (!(this.Contains(line.A) && this.Contains(line.B) && this.Contains(line.PointAlongLine(0.5))))
                return false;

            IEnumerable<GridLineSegment> segmentsToTest;

            if (_ExteriorSegments.Length > 32 || HasInteriorRings)
            {
                segmentsToTest = this.GetIntersectingSegments(line);
            }
            else
            {
                segmentsToTest = _ExteriorSegments.ToList();
            }

            bool intersects = line.Intersects(segmentsToTest, true); //It is OK for endpoints to be on the exterior ring.
            if (intersects)
            {
                //The line intersects some of the polygon segments, but was it just the endpoint?
                return false; //Line is not entirely inside the polygon
            }

            foreach (GridPolygon innerPoly in this.InteriorPolygons)
            {
                if (innerPoly.Intersects(line) || innerPoly.Contains(line))
                    return false;
            }

            return true;
        }

        public OverlapType ContainsExt(GridLineSegment line)
        {
            if (line.BoundingBox.ContainsExt(this.BoundingBox) == OverlapType.NONE)
                return OverlapType.NONE;

            //Ensure both endpoints are inside and a point in the center.
            //Test the center because if the line crosses a concave region with both endpoints exactly on the exterior ring we'd not have any intersections but the poly would not contain the line.
            if (!(this.Contains(line.A) && this.Contains(line.B) && this.Contains(line.PointAlongLine(0.5))))
                return OverlapType.NONE;

            IEnumerable<GridLineSegment> segmentsToTest;

            if (_ExteriorSegments.Length > 32 || HasInteriorRings)
            {
                segmentsToTest = this.GetIntersectingSegments(line);
            }
            else
            {
                segmentsToTest = _ExteriorSegments.ToList();
            }

            bool intersects = line.Intersects(segmentsToTest, true); //It is OK for endpoints to be on the exterior ring.
            if (intersects)
            {
                //The line intersects some of the polygon segments, but was it just the endpoint?
                return OverlapType.INTERSECTING; //Line is not entirely inside the polygon
            }

            foreach (GridPolygon innerPoly in this.InteriorPolygons)
            {
                var innerResult = innerPoly.ContainsExt(line);
                if (innerResult == OverlapType.INTERSECTING || innerResult == OverlapType.TOUCHING)
                    return innerResult;
                else if (innerResult == OverlapType.CONTAINED)
                    return OverlapType.NONE; //It is entirely inside the hole, so it has no overlap 
            }

            return OverlapType.CONTAINED;
        }


        /// <summary>
        /// Return true if the polygon completely contains the circle
        /// </summary>
        /// <param name="other"></param>
        /// <returns></returns>
        public bool Contains(GridCircle other)
        {
            GridRectangle? overlap = BoundingBox.Intersection(other.BoundingBox);
            if (!overlap.HasValue)
                return false;

            //We cannot contain the other shape if the overlapping bounding box is not identical
            if (overlap.Value != other.BoundingBox)
                return false;

            //We must contain the center of the circle
            if (!this.Contains(other.Center))
            {
                return false;
            }

            //If our borders intersect we do not entirely contain the circle
            if (this.Intersects(other))
            {
                return false;
            }

            //If we have an interior hole inside the circle we don't entirely contain the circle.
            if (this.InteriorRings.Any(ir => other.Contains(ir[0])))
            {
                return false;
            }

            //Check case of line segment passing through a convex polygon or an interior polygon
            return true;
        }

        /// <summary>
        /// Return true if the polygon completely contains the circle
        /// </summary>
        /// <param name="other"></param>
        /// <returns></returns>
        public OverlapType ContainsExt(GridCircle other)
        {
            throw new NotImplementedException();
            /*
            GridRectangle? overlap = BoundingBox.Intersection(other.BoundingBox);
            if (!overlap.HasValue)
                return OverlapType.NONE;

            bool CanContain = true;
            //We cannot contain the other shape if the overlapping bounding box is not identical
            if (overlap.Value != other.BoundingBox)
                CanContain = false;

            //Check if we intersect the circle
            if(!CanContain)
            {
                if (this.Intersects(other))
                    return OverlapType.INTERSECTING;
                else
                    return OverlapType.NONE; //TODO: TOUCHING is not supported here
            }

            //We must contain the center of the circle
            if (!this.Contains(other.Center))
            {
                return false;
            }

            //If our borders intersect we do not entirely contain the circle
            if (this.Intersects(other))
            {
                return false;
            }

            //If we have an interior hole inside the circle we don't entirely contain the circle.
            if (this.InteriorRings.Any(ir => other.Contains(ir[0])))
            {
                return false;
            }

            //Check case of line segment passing through a convex polygon or an interior polygon
            return true;
            */
        }

        /// <summary>
        /// Return true if the polygon is completely inside the other
        /// </summary>
        /// <param name="poly"></param>
        /// <returns></returns>
        public bool Contains(GridPolygon other)
        {
            GridRectangle? overlap = BoundingBox.Intersection(other.BoundingBox);
            if (!overlap.HasValue)
                return false;

            //We cannot contain the other shape if the overlapping bounding box is not identical
            if (overlap.Value != other.BoundingBox)
                return false;

            bool HasInteriorVertex = this.Contains(other.ExteriorRing[0]);
            bool HasSegmentIntersections = GridPolygon.SegmentsIntersect(this, other);
            if (HasSegmentIntersections == false && HasInteriorVertex)
                //return OverlapType.INTERSECTING;
                return true;

            return false;
            /*
            //Check case of interior polygon intersection
            if (!other.ExteriorRing.All(p => this.Contains(p)))
            {
                return false;
            }

            //Check case of line segment passing through a convex polygon or an interior polygon
            return !GridPolygon.SegmentsIntersect(this, other);
            */
        }


        /// <summary>
        /// Return true if the polygon is completely inside the other
        /// </summary>
        /// <param name="poly"></param>
        /// <returns></returns>
        public OverlapType ContainsExt(GridPolygon other)
        {
            GridRectangle? overlap = BoundingBox.Intersection(other.BoundingBox);
            if (!overlap.HasValue)
                return OverlapType.NONE;

            bool HasSegmentIntersections = GridPolygon.SegmentsIntersect(this, other);
            if (HasSegmentIntersections)
                return OverlapType.INTERSECTING;

            bool HasInteriorVertex = this.Contains(other.ExteriorRing[0]);
            if (HasInteriorVertex)
                return OverlapType.CONTAINED;

            //TODO: OverlapType.Touching is not implemented
            return OverlapType.NONE;
        }

        public bool InteriorPolygonContains(GridVector2 p)
        {
            GridPolygon intersectedPoly;
            return InteriorPolygonContains(p, out intersectedPoly);
        }

        public bool InteriorPolygonContains(GridVector2 p, out GridPolygon interiorPolygon)
        {
            interiorPolygon = null;
            if (!_BoundingRect.Contains(p))
                return false;

            //Check that our point is not inside an interior hole
            foreach (GridPolygon innerPoly in _InteriorPolygons)
            {
                if (innerPoly.Contains(p))
                {
                    interiorPolygon = innerPoly;
                    return true;
                }
            }

            return false;
        }

        public bool InteriorPolygonIntersects(GridLineSegment line)
        {
            return InteriorPolygonIntersects(line, out GridPolygon intersectedPoly);
        }

        public bool InteriorPolygonIntersects(GridLineSegment line, out GridPolygon interiorPolygon)
        {
            interiorPolygon = null;
            if (!_BoundingRect.Intersects(line.BoundingBox))
                return false;

            //Check that our point is not inside an interior hole
            foreach (GridPolygon innerPoly in _InteriorPolygons)
            {
                if (innerPoly.Intersects(line))
                {
                    interiorPolygon = innerPoly;
                    return true;
                }
            }

            return false;
        }

        public GridCircle InscribedCircle()
        {
            GridVector2 center = this.Centroid;
            double Radius = ExteriorRing.Select(p => GridVector2.Distance(center, p)).Min();
            return new GridCircle(center, Radius);
        }

        /// <summary>
        /// The results of whether a polygon segment is left, right, or on a test line
        /// </summary>
        private struct SegmentIsLeftData
        {
            /// <summary>
            /// Is S.A left of the line?
            /// </summary>
            public int A_is_left;

            /// <summary>
            /// Is S.B left of the line?
            /// </summary>
            public int B_is_left;

            /// <summary>
            /// The polygon segment that was tested.  (Not the 
            /// </summary>
            public GridLineSegment S;

            public int? IsPLeftOfSeg;

            public bool CrossesLine
            {
                get
                {
                    return A_is_left != B_is_left;
                }
            }

            public bool OnTheLine
            {
                get
                {
                    return A_is_left == 0 && B_is_left == 0;
                }
            }

            public bool SameSideOfLine
            {
                get
                {
                    return A_is_left == B_is_left && A_is_left != 0;
                }
            }

        }
        /*
        private static List<IsLeftData> RemoveSameSideSegments(List<GridLineSegment> polygonSegments, GridLine test_line)
        {
            GridVector2 test_point = test_line.Origin;

            var IsLeft = polygonSegments.Select((s, i) => new IsLeftData { A = test_line.IsLeft(s.A), B = test_line.IsLeft(s.B), S = s, IsPLeftOfSeg = new int?() }).Where(seg => seg.SameSideOfLine == false).ToList();

            //List<IsLeftData> SortedKeepList = new List<IsLeftData>(polygonSegments.Count);

            //OK, now we need to condense any instance where IsLeft.A or IsLeft.B == 0.  That is, the segment does not cross the line, mearly touches it. 
            //If we have opposite IsLeftValues we create a new edge that entirely crosses the line.  Otherwise we ignore the edge, which is the case where the segment touches the test_line but does not cross.
            
            for (int i = 0; i < polygonSegments.Count; i++)
            {
                var seg = new IsLeftData { A = test_line.IsLeft(s.A), B = test_line.IsLeft(s.B), S = s, IsPLeftOfSeg = new int?() };

                if (seg.SameSideOfLine) //Remove all segments that are on the same side of the line or parallel to the line.  This leaves only segments that cross or touch the line
                {
                    //We can remove this segment entirely as it is perfectly parallel to our test line
                    //polygonSegments.RemoveAt(i); 
                    //IsLeft.RemoveAt(i);
                    //i = i - 1;
                    continue;
                }

                SortedKeepList.Add(seg);
            }
            

            //return IsLeft;

            return IsLeft;
        }*/

        private static OverlapType IsPointInsidePolygonByWindingTest(List<GridLineSegment> polygonSegments, GridLine test_line)
        {
            GridVector2 test_point = test_line.Origin;
            /*
            if (polygonSegments.Any(ps => ps.IsEndpoint(test_line.Origin)))
                return OverlapType.TOUCHING;
            */
#if DEBUG
            var OriginalSegments = polygonSegments.ToList(); //Create a copy so we can examine the debugger
#endif
            //OK, now we need to condense any instance where IsLeft.A or IsLeft.B == 0.  That is, the segment does not cross the line, mearly touches it. 
            //If we have opposite IsLeftValues we create a new edge that entirely crosses the line.  Otherwise we ignore the edge, which is the case where the segment touches the test_line but does not cross.

            List<SegmentIsLeftData> IsLeft = new List<SegmentIsLeftData>(polygonSegments.Count);

            for (int i = 0; i < polygonSegments.Count; i++)
            {
                GridLineSegment s = polygonSegments[i];
                if (s.IsEndpoint(test_line.Origin))
                {
                    return OverlapType.TOUCHING;
                }

                var seg = new SegmentIsLeftData { A_is_left = test_line.IsLeft(s.A), B_is_left = test_line.IsLeft(s.B), S = s, IsPLeftOfSeg = new int?() };
                if (seg.CrossesLine || seg.OnTheLine)
                {
                    //Check the case of the point exactly on the line
                    if (seg.S.DistanceToPoint(test_point) < Global.Epsilon)
                        return OverlapType.TOUCHING;
                }

                if (seg.SameSideOfLine)
                {
                    continue;
                }

                IsLeft.Add(seg);
            }

            /*
            //var IsLeft = polygonSegments.Select((s,i) => new { A = test_line.IsLeft(s.A), B = test_line.IsLeft(s.B), S = s, IsPLeftOfSeg=new int?()}).ToList();



            //OK, now we need to condense any instance where IsLeft.A or IsLeft.B == 0.  That is, the segment does not cross the line, mearly touches it. 
            //If we have opposite IsLeftValues we create a new edge that entirely crosses the line.  Otherwise we ignore the edge, which is the case where the segment touches the test_line but does not cross.
            for(int i = 0; i < IsLeft.Count; i++)
            {
                var seg = IsLeft[i];
                if (seg.A != seg.B || (seg.A == 0 && seg.B == 0))
                {
                    //Check the case of the point exactly on the line
                    if (seg.S.DistanceToPoint(test_point) < Global.Epsilon)
                        return OverlapType.TOUCHING;
                }

                if (seg.A == seg.B) //Remove all segments that are on the same side of the line or parallel to the line.  This leaves only segments that cross or touch the line
                {
                    //We can remove this segment entirely as it is perfectly parallel to our test line
                    //polygonSegments.RemoveAt(i); 
                    IsLeft.RemoveAt(i);
                    i = i - 1;
                    continue;
                }
            }
            */

            if (IsLeft.Count == 0)
                return OverlapType.NONE;

            polygonSegments = IsLeft.Select(left => left.S).ToList();

            //Find all segments that touch the line.  Remove the endpoints that touch the line and create a virtual segment that runs between the endpoints that did not touch the line.  This prevents double-counting windings.
            //InfiniteSequentialIndexSet SegEnumerator = new InfiniteSequentialIndexSet(0, IsLeft.Count, 0);
            for (int i = 0; i < IsLeft.Count; i++)
            {
                int iNext = i + 1 >= IsLeft.Count ? 0 : i + 1; //The index of the next entry in the list
                var seg = IsLeft[i];
                if (seg.A_is_left != 0 && seg.B_is_left != 0)
                {
                    //Check the case of the point exactly on the line
                    if (seg.S.DistanceToPoint(test_point) < Global.Epsilon)
                        return OverlapType.TOUCHING;

                    continue;   //Segment does not end on the line, continue;
                }

                if (seg.B_is_left == 0) //Seg.A == 0 will be caught by a later iteration
                {
                    var nextSeg = IsLeft[iNext];
                    int nextSegIsLeft = nextSeg.A_is_left != 0 ? nextSeg.A_is_left : nextSeg.B_is_left; //Figure out which part of the next line is not on the test line.  Create a new virtual line or delete
                    GridVector2 nextSegEndpoint = nextSeg.A_is_left != 0 ? nextSeg.S.A : nextSeg.S.B;

                    Debug.Assert(nextSeg.S.OppositeEndpoint(nextSegEndpoint).Y == seg.S.B.Y, "We expect the lines to be input in the order they appear in the ring.  Lines sharing endpoints must be adjacent.");

                    if (nextSegIsLeft == seg.A_is_left) //We touch the line and retreat.  We can remove both entries 
                    {
                        polygonSegments.RemoveAt(Math.Max(i, iNext));
                        polygonSegments.RemoveAt(Math.Min(i, iNext));

                        IsLeft.RemoveAt(Math.Max(i, iNext));
                        IsLeft.RemoveAt(Math.Min(i, iNext));

                        i -= i < iNext ? 1 : 2; //Adjust for wraparound case
                    }
                    else  //We touch the line and then cross over it.  We can remove both entries and add a new one
                    {
                        GridLineSegment virtualPolySegment = new GridLineSegment(seg.S.A, nextSegEndpoint);
                        polygonSegments.RemoveAt(i);
                        polygonSegments.Insert(i, virtualPolySegment);
                        polygonSegments.RemoveAt(iNext);

                        var newEntry = new SegmentIsLeftData { A_is_left = seg.A_is_left, B_is_left = nextSegIsLeft, S = virtualPolySegment, IsPLeftOfSeg = new int?(seg.S.IsLeft(test_point)) }; //Record whether the lines were left of the test_point in case the new line moves to the other side of the point.
                        IsLeft.RemoveAt(i);
                        IsLeft.Insert(i, newEntry);
                        IsLeft.RemoveAt(iNext);

                        //i = i; //Adjust to check the next record 
                    }
                }
            }

            var cross_or_parallel_segments = polygonSegments; //polygonSegments.Where((s, i) => (IsLeft[i].A != IsLeft[i].B) || (IsLeft[i].A == 0 || IsLeft[i].B == 0)).ToArray(); //Find all segments that span the testline or are parallel

            //If we share endpoints then we are always inside the polygon.  Handles case where we ask if a polygon vertex is inside the polygon
            //if (cross_or_parallel_segments.Any(ps => ps.IsEndpoint(test_line.A)))
            //    return OverlapType.TOUCHING;

            int wind_count = 0;
            for (int i = 0; i < cross_or_parallel_segments.Count; i++)
            {
                var SegData = IsLeft[i];
                GridLineSegment polySeg = SegData.S;
                int IsAboveToBelow;
                int pIsLeft;

                IsAboveToBelow = SegData.S.A.Y.CompareTo(SegData.S.B.Y);

                if (SegData.IsPLeftOfSeg.HasValue == false)
                {
                    pIsLeft = polySeg.IsLeft(test_point);
                }
                else
                {
                    pIsLeft = SegData.IsPLeftOfSeg.Value;
                }

                /*if(IsAboveToBelow == 0) //Case of parallel line
                {
                    if(polySeg.BoundingBox.Left <= test_point.X && polySeg.BoundingBox.Right >= test_point.X)
                    {
                        return OverlapType.TOUCHING; //Test point is within the line segment, return true   
                                    //We aren't using epsilon here, perhaps we should?
                    }
                    continue;
                }
                else*/
                if (IsAboveToBelow > 0)
                {
                    if (pIsLeft >= 0)
                        wind_count += 1;
                }
                else //IsAbove < 0
                {
                    if (pIsLeft <= 0)
                        wind_count -= 1;
                }
            }

            return wind_count != 0 ? OverlapType.CONTAINED : OverlapType.NONE;
        }

        private static bool IsPointInsidePolygonByRayTest(ICollection<GridLineSegment> polygonSegments, GridLineSegment test_line)
        {
            //In cases where our test line passes exactly through a vertex on the other polygon we double count the line.  
            //This code removes duplicate intersection points to prevent duplicates

            //If we share endpoints then we are always inside the polygon.  Handles case where we ask if a polygon vertex is inside the polygon
            if (polygonSegments.Any(ps => ps.SharedEndPoint(test_line)))
                return true;

            List<GridVector2> intersections;
            IEnumerable<GridLineSegment> IntersectedSegments;

            if (polygonSegments.Count > 128)
            {
                System.Collections.Concurrent.ConcurrentBag<GridVector2> intersectionsBag = new System.Collections.Concurrent.ConcurrentBag<Geometry.GridVector2>();

                IntersectedSegments = polygonSegments.Where(line =>
                {
                    GridVector2 Intersection;
                    bool intersected = line.Intersects(test_line, out Intersection);
                    if (intersected)
                    {
                        intersectionsBag.Add(Intersection);
                    }

                    return intersected;
                }).AsParallel().ToList(); //Need ToList here to ensure the query executes fully

                intersections = new List<GridVector2>(intersectionsBag);
            }
            else
            {
                intersections = new List<GridVector2>(polygonSegments.Count);

                IntersectedSegments = polygonSegments.Where(line =>
                {
                    GridVector2 Intersection;
                    bool intersected = line.Intersects(test_line, out Intersection);
                    if (intersected)
                    {
                        intersections.Add(Intersection);
                    }

                    return intersected;
                }).ToList(); //Need ToList here to ensure the query executes fully
            }

            //Ensure the line doesn't pass through on a line endpoint
            //SortedSet<GridVector2> intersectionPoints = new SortedSet<GridVector2>();
            GridVector2[] UniqueIntersections = intersections.Distinct().ToArray();

            if (UniqueIntersections.Any(p => test_line.IsEndpoint(p)))
                return true; //If the point is exactly on the line then we can often have two intersections as the line leaves the polygon which results in a false negative.
                             //This test short-circuits that problem

            //If the intersection point is exactly through a polygon vertex then two segments will be returned but we should count only one.
            if (UniqueIntersections.Length != intersections.Count)
            {
                throw new NotImplementedException("This is an edge case where the line passes through a vertex of the polygon.");

                //The fix is to create a new testline that does not pass through any verticies
            }

            //Inside the polygon if we intersect line segments of the border an odd number of times
            return UniqueIntersections.Length % 2 == 1;
        }


        /// <summary>
        /// Returns an array of GridLineSegments in the same order they appear in the ExteriorRing array.
        /// </summary>
        /// <param name="ring_points"></param>
        /// <returns></returns>
        private GridLineSegment[] CreateLineSegments(GridVector2[] ring_points)
        {
            Debug.Assert(ring_points[0] == ring_points[ring_points.Length - 1], "CreateLineSegments expects a closed ring as input");

            GridLineSegment[] lines = new GridLineSegment[ring_points.Length - 1];

            for (int iPoint = 0; iPoint < ring_points.Length - 1; iPoint++)
            {
                GridLineSegment line = new Geometry.GridLineSegment(ring_points[iPoint], ring_points[iPoint + 1]);
                lines[iPoint] = line;
            }

            return lines;
        }

        private static RTree.RTree<GridLineSegment> CreateSegmentBoundingBoxRTree(GridLineSegment[] segments)
        {
            RTree.RTree<GridLineSegment> R = new RTree.RTree<GridLineSegment>();

            foreach (GridLineSegment l in segments)
            {
                R.Add(l.BoundingBox.ToRTreeRectEpsilonPadded(0), l);
            }

            return R;
        }

        /// <summary>
        /// Returns an RTree containing each segment in the polygon, exterior and interior
        /// The PointIndex for each segment in the RTree is the origin of the segment with the 
        /// next PointIndex being the endpoint of the segment
        /// </summary>
        /// <param name="poly"></param>
        /// <returns></returns>
        private static RTree.RTree<PolygonIndex> CreatePointIndexSegmentBoundingBoxRTree(GridPolygon poly)
        {
            RTree.RTree<PolygonIndex> R = new RTree.RTree<PolygonIndex>();

            PolygonVertexEnum enumerator = new PolygonVertexEnum(poly);
            foreach (PolygonIndex p in enumerator)
            {
                GridLineSegment s = p.Segment(poly);
                R.Add(s.BoundingBox.ToRTreeRectEpsilonPadded(0), p);
            }

            return R;
        }

        /// <summary>
        /// Return all segments, both interior and exterior, that fall within the bounding rectangle
        /// </summary>
        /// <param name="bbox"></param>
        /// <returns></returns>
        public IEnumerable<GridLineSegment> GetIntersectingSegments(GridLineSegment line)
        {
            GridRectangle bbox = line.BoundingBox;
            if (!this.BoundingBox.Intersects(bbox))
            {
                return new List<Geometry.GridLineSegment>(0);
            }

            //return SegmentRTree.Intersects(bbox.ToRTreeRect(0)).Select(p => p.Segment(this)).Where(segment => line.Intersects(segment, false)).ToList();
            return SegmentRTree.IntersectionGenerator(bbox.ToRTreeRectEpsilonPadded(0)).Select(p => p.Segment(this)).Where(segment => line.Intersects(segment, false));
        }

        /// <summary>
        /// Return all segments, both interior and exterior, that fall within the bounding rectangle
        /// </summary>
        /// <param name="bbox"></param>
        /// <returns></returns>
        public List<GridLineSegment> GetIntersectingSegments(GridRectangle bbox)
        {
            if (!this.BoundingBox.Intersects(bbox))
            {
                return new List<Geometry.GridLineSegment>(0);
            }

            return SegmentRTree.Intersects(bbox.ToRTreeRectEpsilonPadded(0)).Select(p => p.Segment(this)).Where(segment => bbox.Intersects(segment)).ToList();
        }

        /// <summary>
        /// Rotate the polygon by the spefied angle around the specified origin
        /// </summary>
        /// <param name="angle"></param>
        /// <param name="origin">Defaults to Centroid if not specified</param>
        /// <returns>A rotated copy of this polygon</returns>
        public GridPolygon Rotate(double angle, GridVector2? origin = null)
        {
            if (!origin.HasValue)
            {
                origin = this.Centroid;
            }

            GridVector2[] RotatedRing = this.ExteriorRing.Rotate(angle, origin.Value);

            GridPolygon poly = new GridPolygon(RotatedRing);

            foreach (GridPolygon innerRing in this._InteriorPolygons)
            {
                GridPolygon rotated_inner = innerRing.Rotate(angle, origin);
                poly.AddInteriorRing(rotated_inner);
            }

            return poly;
        }

        public GridPolygon Scale(double scalar, GridVector2? origin = null)
        {
            return this.Scale(new GridVector2(scalar, scalar), origin);
        }

        /// <summary>
        /// Scale the polygon by the specified factor from the specified origin
        /// </summary>
        /// <param name="scalar"></param>
        /// <param name="origin">Defaults to Centroid if not specified</param>
        /// <returns>A scaled copy of this polygon</returns>
        public GridPolygon Scale(GridVector2 scalar, GridVector2? origin = null)
        {
            if (!origin.HasValue)
            {
                origin = this.Centroid;
            }

            GridVector2[] ScaledRing = this.ExteriorRing.Scale(scalar, origin.Value);

            GridPolygon poly = new GridPolygon(ScaledRing);

            foreach (GridPolygon innerRing in this._InteriorPolygons)
            {
                GridPolygon scaled_inner = innerRing.Scale(scalar, origin);
                poly.AddInteriorRing(scaled_inner);
            }

            return poly;
        }

        /// <summary>
        /// Translate the polygon
        /// </summary>
        /// <param name="offset"></param>
        /// <returns>A translated copy of this polygon</returns>
        public GridPolygon Translate(GridVector2 offset)
        {
            GridVector2[] TranslatedRing = this.ExteriorRing.Translate(offset);

            GridPolygon poly = new GridPolygon(TranslatedRing);

            foreach (GridPolygon innerRing in this._InteriorPolygons)
            {
                GridPolygon translated_inner = innerRing.Translate(offset);
                poly.AddInteriorRing(translated_inner);
            }

            return poly;
        }

        public static GridVector2 CalculateCentroid(GridVector2[] ExteriorRing, bool ValidateRing = true)
        {
            double accumulator_X = 0;
            double accumulator_Y = 0;

            //To prevent precision errors we subtract the average value and add it again
            ExteriorRing = ExteriorRing.EnsureClosedRing().ToArray();
            GridVector2 Average = ExteriorRing.Average();
            GridVector2[] translated_Points = ExteriorRing.Translate(-Average);

            for (int i = 0; i < translated_Points.Length - 1; i++)
            {
                GridVector2 p0 = translated_Points[i];
                GridVector2 p1 = translated_Points[i + 1];
                double SharedTerm = ((p0.X * p1.Y) - (p1.X * p0.Y));
                accumulator_X += (p0.X + p1.X) * SharedTerm;
                accumulator_Y += (p0.Y + p1.Y) * SharedTerm;
            }

            double ExteriorArea = translated_Points.PolygonArea();
            double scalar = ExteriorArea * 6;

            return new GridVector2((accumulator_X / scalar) + Average.X, (accumulator_Y / scalar) + Average.Y);
        }

        public GridPolygon Smooth(uint NumInterpolationPoints)
        {
            return GridPolygon.Smooth(this, NumInterpolationPoints);
        }

        public static GridPolygon Smooth(GridPolygon poly, uint NumInterpolationPoints)
        {
            GridVector2[] smoothedCurve = poly.ExteriorRing.CalculateCurvePoints(NumInterpolationPoints, true);

            //GridVector2[] simplifiedCurve = smoothedCurve.DouglasPeuckerReduction(.5, poly.ExteriorRing).EnsureClosedRing().ToArray();

            GridPolygon smoothed_poly = new GridPolygon(smoothedCurve);

            foreach (GridPolygon inner_poly in poly.InteriorPolygons)
            {
                GridPolygon smoother_inner_poly = GridPolygon.Smooth(inner_poly, NumInterpolationPoints);
                smoothed_poly.AddInteriorRing(smoother_inner_poly);
            }

            Trace.WriteLine(string.Format("Smooth Polygon {0} into {1}", poly, smoothed_poly));

            return smoothed_poly;
        }

        /// <summary>
        /// Returns an approximately equal polygon with fewer control points
        /// </summary>
        /// <param name="MaxDistanceFromSimplifiedToIdeal">How far the path can stray from the original polygon contour</param>
        /// <returns></returns>
        public GridPolygon SimplifyControlPoints(double MaxDistanceFromSimplifiedToIdeal = 1.0)
        {
            GridVector2[] simpler_ring = CatmullRomControlPointSimplification.IdentifyControlPoints(this.ExteriorRing, MaxDistanceFromSimplifiedToIdeal, true).ToArray();
            GridPolygon output = new GridPolygon(simpler_ring);

            foreach (var inner_ring in this.InteriorRings)
            {
                var simpler_inner_ring = CatmullRomControlPointSimplification.IdentifyControlPoints(inner_ring, MaxDistanceFromSimplifiedToIdeal, true);
                output.AddInteriorRing(simpler_inner_ring);
            }

            return output;
        }

        public double Distance(GridVector2 p)
        {
            return this.ExteriorSegments.Min(line => line.DistanceToPoint(p));
        }

        public double Distance(GridVector2 p, out GridLineSegment nearestLine)
        {
            double minDistance = double.MaxValue;
            nearestLine = ExteriorSegments.First();

            for (int i = 0; i < ExteriorSegments.Length; i++)
            {
                double dist = ExteriorSegments[i].DistanceToPoint(p);
                if (dist < minDistance)
                {
                    nearestLine = ExteriorSegments[i];
                    minDistance = dist;
                }
            }

            return minDistance;
        }

        /// <summary>
        /// Brute force search for distance
        /// </summary>
        /// <param name="p"></param>
        /// <returns></returns>
        public double Distance(GridLineSegment p)
        {
            double minDistanceA = Distance(p.A);
            double minDistanceB = Distance(p.B);
            double minDistanceLine = ExteriorRing.Min(es => p.DistanceToPoint(es));

            return new double[] { minDistanceA, minDistanceB, minDistanceLine }.Min();
        }

        /// <summary>
        /// Brute force search for distance
        /// </summary>
        /// <param name="p"></param>
        /// <returns></returns>
        public double Distance(GridPolygon other)
        {
            if (this.Intersects(other))
                return 0;

            double minDistanceToOtherLineSegment = this.ExteriorRing.Min(p => other.Distance(p));
            double minDistanceToThisLineSegment = other.ExteriorRing.Min(p => this.Distance(p));

            return Math.Min(minDistanceToOtherLineSegment, minDistanceToThisLineSegment);
        }

        /// <summary>
        /// Given a point inside the polygon return the normalized distance.
        /// Create a line passing through the centroid and the point. 
        /// Locate the nearest intersecting line segment in the exterior ring.
        /// Measure the distance
        /// </summary>
        /// <param name="p"></param>
        public double DistanceFromCenterNormalized(GridVector2 p)
        {
            GridLine line = new Geometry.GridLine(Centroid, p - Centroid);

            List<GridVector2> Intersections = new List<Geometry.GridVector2>(ExteriorRing.Length);
            for (int i = 0; i < _ExteriorSegments.Length; i++)
            {
                GridVector2 intersection;
                if (line.Intersects(this._ExteriorSegments[i], out intersection))
                {
                    double CenterDist = GridVector2.Distance(Centroid, intersection);
                    double PointDist = GridVector2.Distance(p, intersection);
                    //Since the line is infinite we need to ignore cases where the intersection is between the point and the line, or on the other side of the center point
                    //  I------P---------I-----I------C-------I
                    //  In the example above slicing through a concave poly we'd want the first Intersection (I) to determine the distance from center normalized

                }
            }

            throw new NotImplementedException();
        }

        public int[] VerticiesOnConvexHull()
        {
            int[] indicies;
            GridVector2[] convex_hull_verts = this.ExteriorRing.ConvexHull(out indicies);

            return indicies;
        }

        public object Clone()
        {
            GridPolygon clone = new Geometry.GridPolygon(this.ExteriorRing.Clone() as GridVector2[]);
            foreach (GridPolygon innerPoly in this.InteriorPolygons)
            {
                GridPolygon innerClone = innerPoly.Clone() as GridPolygon;
                clone.AddInteriorRing(innerClone);
            }

            return clone;
        }

        /// <summary>
        /// Round all coordinates in the clone of the GridPolygon to the nearest precision
        /// </summary>
        /// <param name="precision"></param>
        /// <returns></returns>
        public GridPolygon Round(int precision)
        {
            GridVector2[] roundedPoints = this.ExteriorRing.Select(e => e.Round(precision)).ToArray();
            for (int i = roundedPoints.Length - 1; i > 0; i--)
            {
                if (roundedPoints[i] == roundedPoints[i - 1])
                    roundedPoints.RemoveAt(i);
            }

            GridPolygon clone = new Geometry.GridPolygon(roundedPoints);
            foreach (GridPolygon innerPoly in this.InteriorPolygons)
            {
                GridPolygon innerClone = innerPoly.Round(precision);
                clone.AddInteriorRing(innerClone);
            }

            return clone;
        }

        public override string ToString()
        {
            if (this.HasInteriorRings)
            {
                return string.Format("Poly with {0} verts, {1} interior rings", this.TotalUniqueVerticies, this.InteriorRings.Count);
            }
            else
            {
                return string.Format("Poly with {0} verts", this.TotalUniqueVerticies);
            }
        }

        public bool Intersects(IShape2D shape)
        {
            return ShapeExtensions.PolygonIntersects(this, shape);
        }


        public bool Intersects(ICircle2D c)
        {
            GridCircle circle = c.Convert();
            return this.Intersects(circle);
        }

        public bool Intersects(GridCircle circle)
        {
            return PolygonIntersectionExtensions.Intersects(this, circle);
        }

        public bool Intersects(GridRectangle rect)
        {
            return RectangleIntersectionExtensions.Intersects(rect, this);
        }


        public bool Intersects(ILineSegment2D l)
        {
            GridLineSegment line = l.Convert();
            return this.Intersects(line);
        }

        public bool Intersects(GridLineSegment line)
        {
            return PolygonIntersectionExtensions.Intersects(this, line);
        }

        public bool Intersects(ITriangle2D t)
        {
            GridTriangle tri = t.Convert();
            return this.Intersects(tri);
        }

        public bool Intersects(GridTriangle tri)
        {
            return PolygonIntersectionExtensions.Intersects(this, tri);
        }

        public bool Intersects(IPolygon2D p)
        {
            GridPolygon poly = p.Convert();
            return this.Intersects(poly);
        }

        /// <summary>
        /// Return true if the polygon contains or intersects the other polygon
        /// </summary>
        /// <param name="poly"></param>
        /// <param name="other"></param>
        /// <returns></returns>
        public bool Intersects(GridPolygon other)
        {
            GridRectangle? Intersection = this.BoundingBox.Intersection(other.BoundingBox);
            if (!Intersection.HasValue)
                return false;

            //Check the case of the other polygon entirely inside
            if (this.Contains(other.ExteriorRing[0])) //If it is entirely inside then all other verts must be inside so only check one
                return true;

            return SegmentsIntersect(this, other);
        }

        /// <summary>
        /// Return true if segments of the polygons intersect.  Returns false if the other triangle is entirely contained by poly
        /// </summary>
        /// <param name="poly"></param>
        /// <param name="other"></param>
        /// <returns></returns>
        public static bool SegmentsIntersect(GridPolygon poly, GridPolygon other)
        {
            GridRectangle? Intersection = poly.BoundingBox.Intersection(other.BoundingBox);
            if (!Intersection.HasValue)
                return false;

            //Check the case for a line segment passing entirely through the polygon.
            GridRectangle overlap = Intersection.Value;

            List<GridLineSegment> CandidateSegments = poly.GetIntersectingSegments(overlap);

            foreach (GridLineSegment candidate in CandidateSegments)
            {
                IEnumerable<GridLineSegment> OtherSegments = other.GetIntersectingSegments(candidate);
                if (OtherSegments.Any())
                    return true;
            }

            return false;
        }

        bool IShape2D.Contains(IPoint2D p)
        {
            return this.Contains(p.Convert());
        }

        IShape2D IShape2D.Translate(IPoint2D offset)
        {
            GridVector2 v = offset.Convert();
            return this.Translate(v);
        }

        /// <summary>
        /// Add a vertex to our rings everywhere the other polygon intersects one of our segments
        /// </summary>
        /// <param name="other"></param>
        /// <returns>All intersection points, including pre-existing and added</returns>
        public List<GridVector2> AddPointsAtIntersections(GridPolygon other)
        {
            List<GridVector2> found_or_added_intersections = new List<GridVector2>();
            GridRectangle? overlap = this.BoundingBox.Intersection(other.BoundingBox);

            //No work to do if there is no overlap
            if (!overlap.HasValue)
                return found_or_added_intersections;

            List<GridVector2> newRing = new List<Geometry.GridVector2>();

            //int i = 0;
            //while (i < ExteriorRing.Length - 1)
            //for (int i = 0; i < ExteriorRing.Length - 1; i++)
            var vertEnumerator = new PolygonVertexEnum(this, reverse: true);

            QuadTree<GridVector2> addedVertexQuad = new QuadTree<GridVector2>();

            //Enumerate in reverse so we do not break our index values as we insert
            //Handle an edge case where we insert at the end of the loop, but the .Next index wraps to zero which changes the index of every item in the loop.
            foreach (PolygonIndex originalpolyIndex in vertEnumerator)
            {
                PolygonIndex polyIndex = originalpolyIndex.ReindexToSize(this);
                GridLineSegment ls = polyIndex.Segment(this);

                //GridLineSegment ls = new GridLineSegment(ExteriorRing[i], ExteriorRing[i + 1]);
                //PointIndex polyIndex = new PointIndex(0, i, ExteriorRing.Length - 1);
                //newRing.Add(ExteriorRing[i]);
                //newRing.Add(this[polyIndex]);
                addedVertexQuad.Add(this[polyIndex], this[polyIndex]);

                GridVector2[] IntersectionPoints;
                //Since we want the out parameter just get a quick list of candidates with the ls.bounding box in instead of running the full intersection test twice.
                List<GridLineSegment> candidates = ls.Intersections(other.GetIntersectingSegments(ls.BoundingBox), out IntersectionPoints);
                if (candidates.Count == 0)
                    continue;

                //Reverse the intersection list so we are adding points furthest to nearest.  This prevents our polyIndex from pointing at the wrong index after adding a point when there are multiple intersections for a segment.
                IntersectionPoints = IntersectionPoints.Reverse().ToArray();

                //Remove any duplicates of the existing endpoints 
                for (int iInter = 0; iInter < IntersectionPoints.Length; iInter++)
                {
                    GridVector2 p = IntersectionPoints[iInter];
                    /*if(iInter != 0)
                        this.NearestSegment(p, out polyIndex); //After adding a point to the polygon our indexing may be off, so just calculate the correct index
                        */
                    //System.Diagnostics.Debug.Assert(!newRing.Contains(p));
                    //if (!newRing.Any(nr => nr == p)) //We can't use contains because the equality operator uses an epsilon and contains does not
                    GridVector2 found_nearest = addedVertexQuad.FindNearest(p, out double nearest_distance);
                    if (nearest_distance > Global.Epsilon) //Our nearest vertex is too far away so we need to add a vertex to ourselves
                    {
                        double other_segment_distance = other.NearestSegment(p, out PolygonIndex other_nearest_segment);

                        Debug.Assert(other_nearest_segment.NumUniqueInRing == other_nearest_segment.Polygon(other).ExteriorRing.Length - 1, "Index found with incorrect number of verticies in ring."); //An old bug I want to check for where the index ring size in RTree was not updating as points were added

                        //There is a horrible case where a very thin triangle can have two corresponding points that are < epsilon distance apart.  I 
                        //solved this by looking for the nearest segment on the other triangle, and then using that to check for an existing vertex
                        PolygonIndex other_vertex_index = other_nearest_segment;
                        double other_vertex_distance = GridVector2.Distance(other[other_nearest_segment], p);

                        //double other_vertex_distance = other.NearestVertex(p, out PointIndex other_vertex_index);

                        //We need to cover an edge case here.  We insert at Index.Next.  For the last point in the ring this will return index 0.  That will change the indexing of the ring.  Instead we explicitly state we want a new point added at the end of the ring.
                        PolygonIndex InsertIndex = polyIndex.IsLastIndexInRing() ? new PolygonIndex(0, polyIndex.iInnerPoly, polyIndex.iVertex + 1, polyIndex.NumUniqueInRing) : polyIndex.Next;
                        PolygonIndex OtherInsertIndex = other_nearest_segment.IsLastIndexInRing() ? new PolygonIndex(0, other_nearest_segment.iInnerPoly, other_nearest_segment.iVertex + 1, other_nearest_segment.NumUniqueInRing) : other_nearest_segment.Next;

                        //If we intersect close enough to another vertex on the other polygon, just add that point to ourselves.
                        if (other_vertex_distance == 0)
                        {
                            //Vertex exists in the other polygon at exact position
                            //newRing.Add(p);
                            addedVertexQuad.Add(p, p);
                            //Add a vertex between the point we tested and the next
                            InsertVertex(p, InsertIndex);

                            found_or_added_intersections.Add(p);
                        }
                        else if (other_vertex_distance < Global.Epsilon)
                        {
                            //Use the position of the existing vertex in the other polygon for our own position
                            //newRing.Add(other_vertex_index.Point(other));
                            addedVertexQuad.Add(other_vertex_index.Point(other), other_vertex_index.Point(other));
                            InsertVertex(other_vertex_index.Point(other), InsertIndex);
                            found_or_added_intersections.Add(other_vertex_index.Point(other));
                        }
                        else
                        {
                            //Intersection point is not a  vertex on either polygon
                            //double other_segment_distance = other.NearestSegment(p, out PointIndex other_nearest_segment);

                            //newRing.Add(p);
                            addedVertexQuad.Add(p, p);
                            InsertVertex(p, InsertIndex);
                            other.InsertVertex(p, OtherInsertIndex);
                            found_or_added_intersections.Add(p);
                        }

                        //Skip the point we inserted so the next insert is in the correct place and we don't double check an inserted point
                        //i += 1;

                        //Trace.WriteLine(string.Format("Add Corresponding Point {0}", p));
                    }
                    else //Intersection is already one of our verticies or close enough
                    {
                        //Check if the intersection point occurs in the other polygon
                        //double other_vertex_distance = other.NearestVertex(p, out PointIndex other_vertex_index);

                        double other_segment_distance = other.NearestSegment(p, out PolygonIndex other_nearest_segment);
                        PolygonIndex other_vertex_index = other_nearest_segment;
                        double other_vertex_distance = GridVector2.Distance(other[other_nearest_segment], p);

                        PolygonIndex OtherInsertIndex = other_nearest_segment.IsLastIndexInRing() ? new PolygonIndex(0, other_nearest_segment.iInnerPoly, other_nearest_segment.iVertex + 1, other_nearest_segment.NumUniqueInRing) : other_nearest_segment.Next;


                        //We need the point to be exact, so adjust our point accordingly
                        if (other_vertex_distance == 0)
                        {
                            //No action needed.  Vertex exists in the other polygon at exact position and in this polygon at exact position.
                            //We still report the intersection though
                            found_or_added_intersections.Add(other_vertex_index.Point(other));

                        }
                        else if (other_vertex_distance < Global.Epsilon) //Use the position of the existing vertex in the other polygon for our own position
                        {
                            //Q: Shouldn't we check if we intersect with the near or far endpoint of our segment before nudging?
                            //A: No, because we enumerate in reverse order, so the far endpoing would be tested previously... unless it is the first last vertex in the loop...
                            GridVector2 other_vert_pos = other_vertex_index.Point(other);
                            if (polyIndex.IsLastIndexInRing() && GridVector2.Distance(ls.B, other_vert_pos) < Global.Epsilon)
                            {

                                //This should be a very rare case

                                this.SetVertex(polyIndex.Next, other_vertex_index.Point(other));
                            }
                            else
                            {

                                this.SetVertex(polyIndex, other_vertex_index.Point(other));
                            }

                            //newRing[existingIndex] = other_vert_pos;
                            addedVertexQuad.TryRemove(p, out GridVector2 removed_point);
                            addedVertexQuad.Add(other_vert_pos, other_vert_pos);
                            found_or_added_intersections.Add(other_vert_pos);
                        }
                        else
                        {
                            //We have the vertex, but it is not in the other polygon.  Add the vertex to the other polygon
                            //double other_segment_distance = other.NearestSegment(p, out PointIndex other_nearest_segment);

                            //other.AddVertex(p);
                            other.InsertVertex(p, OtherInsertIndex);
                            found_or_added_intersections.Add(p);
                        }
                    }
                }

                //i = i + 1;
            }

            return found_or_added_intersections;
        }

        /// <summary>
        /// Add a vertex to our rings everywhere the other polygon intersects one of our segments
        /// </summary>
        /// <param name="other"></param>
        public void AddPointsAtIntersections(GridLineSegment other)
        {
            GridRectangle? overlap = this.BoundingBox.Intersection(other.BoundingBox);

            //No work to do if there is no overlap
            if (!overlap.HasValue)
                return;

            List<GridVector2> newRing = new List<Geometry.GridVector2>(ExteriorRing.Length);

            for (int i = 0; i < ExteriorRing.Length - 1; i++)
            {
                GridLineSegment ls = new GridLineSegment(ExteriorRing[i], ExteriorRing[i + 1]);

                newRing.Add(ExteriorRing[i]);

                IShape2D intersection;

                var intersects = ls.Intersects(other, true, out intersection); //Don't check the endpoints of the segment because we are already adding them

                if (intersects)
                {
                    //The intersection could be a line, which we can't really add an infinite number of points for... we could add internal endpoints, but for now we add point intersections only.
                    IPoint2D point = intersection as IPoint2D;
                    if (point != null)
                    {
                        GridVector2 p = new GridVector2(point.X, point.Y);
                        System.Diagnostics.Debug.Assert(!newRing.Contains(p));
                        newRing.Add(p);
                    }
                }
            }

            newRing.Add(ExteriorRing[ExteriorRing.Length - 1]);

            //Ensure we are not accidentally adding duplicate points, other than to close the ring
            System.Diagnostics.Debug.Assert(newRing.Count == newRing.Distinct().Count() + 1);

            this.ExteriorRing = newRing.ToArray();

            foreach (GridPolygon innerPolygon in this._InteriorPolygons)
            {
                innerPolygon.AddPointsAtIntersections(other);
            }

            this._SegmentRTree = null; //Reset our RTree since yanking a polygon and changing the indicies are a pain
        }


        /// <summary>
        /// Add a vertex to our rings everywhere the other polygon intersects one of the passed segments
        /// </summary>
        /// <param name="other"></param>
        public void AddPointsAtIntersections(GridLineSegment[] other)
        {
            //Only check the lines that could intersect our polygon
            other = other.Where(o => this.BoundingBox.Intersects(o.BoundingBox)).ToArray();

            List<GridVector2> newRing = new List<Geometry.GridVector2>();

            for (int i = 0; i < ExteriorRing.Length - 1; i++)
            {
                GridLineSegment ls = new GridLineSegment(ExteriorRing[i], ExteriorRing[i + 1]);

                //Don't add the point if it is too close
                if (newRing.Count == 0 || GridVector2.DistanceSquared(newRing.Last(), ExteriorRing[i]) > Global.EpsilonSquared)
                    newRing.Add(ExteriorRing[i]);

                GridVector2[] IntersectionPoints;
                List<GridLineSegment> candidates = ls.Intersections(other, out IntersectionPoints);

                //Remove any duplicates of the existing endpoints 
                foreach (GridVector2 p in IntersectionPoints)
                {
                    System.Diagnostics.Debug.Assert(!newRing.Contains(p));
                    //Don't add the point if it is too close
                    if (newRing.Count == 0 || GridVector2.DistanceSquared(newRing.Last(), p) > Global.EpsilonSquared)
                        newRing.Add(p);
                }
            }

            if (newRing.Count == 0 || GridVector2.DistanceSquared(newRing.Last(), ExteriorRing[ExteriorRing.Length - 1]) > Global.EpsilonSquared)
                newRing.Add(ExteriorRing[ExteriorRing.Length - 1]);

            //Ensure we are not accidentally adding duplicate points, other than to close the ring
            System.Diagnostics.Debug.Assert(newRing.Count == newRing.Distinct().Count() + 1);

            this.ExteriorRing = newRing.ToArray();

            foreach (GridPolygon innerPolygon in this._InteriorPolygons)
            {
                innerPolygon.AddPointsAtIntersections(other);
            }

            this._SegmentRTree = null; //Reset our RTree since yanking a polygon and changing the indicies are a pain
        }

        /// <summary>
        /// Returns a dictionary mapping each vertex coordinate to an index
        /// </summary>
        /// <returns></returns>
        public Dictionary<GridVector2, PolygonIndex> CreatePointToPolyMap()
        {
            var map = CreatePointToPolyMap(new GridPolygon[] { this });
            Dictionary<GridVector2, PolygonIndex> flatMap = new Dictionary<Geometry.GridVector2, Geometry.PolygonIndex>(); //The map without the possibility of multiple verticies at the same position

            foreach (GridVector2 p in map.Keys)
            {
                flatMap.Add(p, map[p].First());
            }

            return flatMap;
        }

        /// <summary>
        /// Creates a lookup table for verticies to a polygon index.  Polygons may not share verticies.
        /// </summary>
        /// <param name="Polygons"></param>
        /// <returns></returns>
        public static Dictionary<GridVector2, PolygonIndex> CreatePointToPolyMap2D(GridPolygon[] Polygons)
        {
            Dictionary<GridVector2, PolygonIndex> pointToPoly = new Dictionary<GridVector2, PolygonIndex>();
            for (int iPoly = 0; iPoly < Polygons.Length; iPoly++)
            {
                GridPolygon poly = Polygons[iPoly];
                GridVector2[] polyPoints = poly.ExteriorRing;

                //Subtract one from ring length to prevent duplicate point key insertion since they are closed rings
                for (int iVertex = 0; iVertex < poly.ExteriorRing.Length - 1; iVertex++)
                {
                    GridVector2 p = poly.ExteriorRing[iVertex];
                    PolygonIndex value = new PolygonIndex(iPoly, iVertex, Polygons);

                    if (pointToPoly.ContainsKey(p))
                    {
                        throw new ArgumentException(string.Format("Duplicate vertex {0}", p));
                    }

                    pointToPoly.Add(p, value);
                }


                for (int iInnerPoly = 0; iInnerPoly < poly.InteriorPolygons.Count; iInnerPoly++)
                {
                    GridPolygon innerPolygon = poly.InteriorPolygons.ElementAt(iInnerPoly);

                    //Subtract one from ring length to prevent duplicate point key insertion since they are closed rings
                    for (int iVertex = 0; iVertex < innerPolygon.ExteriorRing.Length - 1; iVertex++)
                    {
                        GridVector2 p = innerPolygon.ExteriorRing[iVertex];

                        PolygonIndex value = new PolygonIndex(iPoly, iInnerPoly, iVertex, Polygons);
                        if (pointToPoly.ContainsKey(p))
                        {
                            throw new ArgumentException(string.Format("Duplicate inner polygon vertex {0}", p));
                        }

                        //List<PolygonIndex> indexList = new List<Geometry.PolygonIndex>();
                        //indexList.Add(value);
                        pointToPoly.Add(p, value);
                    }
                }
            }

            return pointToPoly;
        }

        /// <summary>
        /// Creates a lookup table for verticies to a polygon index.  Polygons may share verticies.
        /// </summary>
        /// <param name="Polygons">An array of N polygons.</param>
        /// <param name="iPoly">An array of N indicies.  If not null PointIndex values will use the corresponding entry in this array for the
        /// Polygon index instead of the position in the passed Polygons array.  This is useful when generating a map for a subset of a larger 
        /// collection of polygons. </param>
        /// <returns></returns>
        public static Dictionary<GridVector2, List<PolygonIndex>> CreatePointToPolyMap(GridPolygon[] Polygons, IReadOnlyList<int> PolygonIndicies = null)
        {
            Dictionary<GridVector2, List<PolygonIndex>> pointToPoly = new Dictionary<GridVector2, List<PolygonIndex>>();
            for (int iPoly = 0; iPoly < Polygons.Length; iPoly++)
            {
                int iPolygon = iPoly; //Used to adjust polygon index if PolygonIndicies is remapping those values
                if (PolygonIndicies != null)
                {
                    iPolygon = PolygonIndicies[iPoly];
                }

                GridPolygon poly = Polygons[iPoly];
                GridVector2[] polyPoints = poly.ExteriorRing;
                for (int iVertex = 0; iVertex < poly.ExteriorRing.Length - 1; iVertex++)
                {
                    GridVector2 p = poly.ExteriorRing[iVertex];
                    PolygonIndex value = new PolygonIndex(iPolygon, iVertex, Polygons);

                    if (pointToPoly.ContainsKey(p))
                    {
                        pointToPoly[p].Add(value);
                        continue;
                    }

                    List<PolygonIndex> indexList = new List<Geometry.PolygonIndex>();
                    indexList.Add(value);
                    pointToPoly.Add(p, indexList);
                }

                for (int iInnerPoly = 0; iInnerPoly < poly.InteriorPolygons.Count; iInnerPoly++)
                {
                    GridPolygon innerPolygon = poly.InteriorPolygons.ElementAt(iInnerPoly);

                    for (int iVertex = 0; iVertex < innerPolygon.ExteriorRing.Length - 1; iVertex++)
                    {
                        GridVector2 p = innerPolygon.ExteriorRing[iVertex];

                        PolygonIndex value = new PolygonIndex(iPolygon, iInnerPoly, iVertex, Polygons);
                        if (pointToPoly.ContainsKey(p))
                        {
                            pointToPoly[p].Add(value);
                            continue;
                        }

                        List<PolygonIndex> indexList = new List<Geometry.PolygonIndex>();
                        indexList.Add(value);
                        pointToPoly.Add(p, indexList);
                    }
                }
            }

            return pointToPoly;
        }

        public static GridPolygon WalkPolygonCut(GridPolygon input, RotationDirection direction, IList<GridVector2> cutLine)
        {
            return WalkPolygonCut(input, direction, cutLine, out PolygonIndex FirstIntersection, out PolygonIndex LastIntersection, out List<GridVector2> intersecting_cutline_verts);
        }


        /// <summary>
        /// Given a polyline, find two locations where it intersects the polygon and walk the polygon in either clockwise/counter-clockwise direction from the first intersection of the cutline to the second, add the cutline to close the ring, and return the resulting polygon.
        /// </summary>
        /// <param name="start_index"></param>
        /// <param name="input">The polygon to cut/extend</param>
        /// <param name="direction">The direction we will walk to connect the starting and ending cut points</param>
        /// <param name="cutLine">The line cutting the polygon.  It should intersect the same polygonal ring in two locations without intersecting any others</param>
        /// <param name="FirstIntersect">The polygon vertex before the intersected segment, use intersect_index.next to get the endpoint of the intersected segment of the polygon</param>
        /// <returns></returns>
        public static GridPolygon WalkPolygonCut(GridPolygon input, RotationDirection direction, IList<GridVector2> cutLine, out PolygonIndex FirstIntersection, out PolygonIndex LastIntersection, out List<GridVector2> intersecting_cutline_verts)
        {

            //Find a possible intersection point for the retrace
            GridLineSegment[] cutLines = cutLine.ToLineSegments();
            intersecting_cutline_verts = new List<GridVector2>(); //Every vert in the path that crosses the two polygon
            List<PolygonIndex> IntersectingPointIndicies = new List<PolygonIndex>();
            bool FirstCutIntersectionFound = false;

            //Add the intersection points to the polygon
            GridPolygon output = input.Clone() as GridPolygon;
            output.AddPointsAtIntersections(cutLines);

            //Identify where the cut crosses the polygon rings 
            for (int iVert = 0; iVert < cutLine.Count - 1; iVert++)
            {
                GridLineSegment segment = new GridLineSegment(cutLine[iVert], cutLine[iVert + 1]);

                var intersections = output.IntersectingSegments(segment);

                if (FirstCutIntersectionFound)
                {
                    if (intersections.Count == 0)
                    {
                        intersecting_cutline_verts.Add(segment.B);
                    }
                    else
                    {
                    }
                }
                else if (intersections.Count == 1)
                {
                    FirstCutIntersectionFound = true;
                    intersecting_cutline_verts.Add(segment.B);
                }
                else if (intersections.Count > 1)
                {
                    //We'll exit, but since we found two intersections at once none of the path is inside the polygon
                }

                IntersectingPointIndicies.AddRange(intersections.Values);

                if (IntersectingPointIndicies.Count >= 2)
                {
                    //intersecting_cutline_verts.Add(cutLine[iVert + 1]);
                    break;
                }
            }

            if (IntersectingPointIndicies.Count == 0)
            {
                throw new ArgumentException("cutLine must intersect a polygon ring");
            }
            else if (IntersectingPointIndicies.Count == 1)
            {
                FirstIntersection = IntersectingPointIndicies[0];
                throw new ArgumentException("cutline must intersect a polygon ring a second time.");
            }

            //Identify the first vertex of the segment of the polygon that intersects the cut line
            FirstIntersection = IntersectingPointIndicies[IntersectingPointIndicies.Count - 2];
            LastIntersection = IntersectingPointIndicies[IntersectingPointIndicies.Count - 1];

            if (false == FirstIntersection.AreOnSameRing(LastIntersection))
            {
                throw new ArgumentException("Cut line must cross segments on the same ring of the polygon");
            }

            if (FirstIntersection == LastIntersection)
            {
                throw new ArgumentException(string.Format("Start and End index must be different to cut polygon. Both are {0}", FirstIntersection));
            }

            //Drop the first cut intersection because it will be on the wrong side of the polygon border
            //intersecting_cutline_verts.RemoveAt(0);

            return WalkPolygonCut(FirstIntersection,
                                  LastIntersection,
                                  output,
                                  direction,
                                  intersecting_cutline_verts);
        }


        /// <summary>
        /// Given a polyline that crosses the same ring of the polygon at two points on the same ring, returns the polygon that results from walking the polygon either clockwise-or-counter clockwise around the cut line. 
        /// This can be used to cut a polygon into arbitrary parts.
        /// </summary>
        /// <param name="start_index">The vertex of the polygon the cut begins at</param>
        /// <param name="intersect_index">The vertex of the polygon the cut ends at</param>
        /// <param name="originPolygon">Polygon we are cutting</param>
        /// <param name="direction">Build the polygon with a clockwise or counterclockwise rotation order from the start_index</param>
        /// <param name="cutLine">The verticies of the cutline.  Must be entirely inside or outside the polygon and not intersect any rings</param>
        /// <returns></returns>
        public static GridPolygon WalkPolygonCut(PolygonIndex start_index, PolygonIndex end_index, GridPolygon originPolygon, RotationDirection direction, IList<GridVector2> cutLine)
        {
            if (false == end_index.AreOnSameRing(start_index))
            {
                throw new ArgumentException("Cut must run between the same ring of the polygon without intersecting other rings");
            }

            if (start_index == end_index)
            {
                throw new ArgumentException(string.Format("Start and End index must be different to cut polygon. Both are {0}", start_index));
            }

            //Walk the ring using Next to find perimeter on one side, the walk using prev to find perimeter on the other
            List<GridVector2> walkedPoints = new List<GridVector2>();
            PolygonIndex current = start_index;

            //Add the points from the polygon
            do
            {
                Debug.Assert(walkedPoints.Contains(current.Point(originPolygon)) == false);
                walkedPoints.Add(current.Point(originPolygon));
                if (direction == RotationDirection.COUNTERCLOCKWISE)
                    current = current.Next;
                else
                    current = current.Previous;

            }
            while (current != end_index);

            walkedPoints.Add(end_index.Point(originPolygon));

            //Add the intersection point of where we crossed the boundary 
            //List<GridVector2> SimplifiedPath = CurveSimplificationExtensions.DouglasPeuckerReduction(cutLine, Global.PenSimplifyThreshold);
            //Since we start walking the polygon from the first intersection point we always add the cutline in reverse order to return to the cirst intersection point.
            List<GridVector2> SimplifiedPath = cutLine.Reverse().ToList();

            //The intersection point marks where we enter the polygon.  The first point in the path is not added because it indicates where the line exited the cut region. 
            //Add the PenInput.Path 

            //Temp for debugging ///////////////
            for (int iCut = 0; iCut < SimplifiedPath.Count; iCut++)
            {
                Debug.Assert(walkedPoints.Contains(SimplifiedPath[iCut]) == false);
                if (GridVector2.DistanceSquared(SimplifiedPath[iCut], walkedPoints.Last()) <= Geometry.Global.EpsilonSquared)
                {
                    //int i = 5; //Temp for debugging
                    continue;
                }

                walkedPoints.Add(SimplifiedPath[iCut]);
            }
            /////////////////////////////////////
            ///
            //walkedPoints.AddRange(cutLine);
#if DEBUG
            //Ensure we do not have duplicates in our list
            GridVector2[] walkedPoints_noduplicates = walkedPoints.RemoveDuplicates();
            Debug.Assert(walkedPoints_noduplicates.Length == walkedPoints.Count);
#endif

            //Close the ring
            walkedPoints.Add(start_index.Point(originPolygon));

            /*
            Debug.Assert(walkedPoints.ToArray().AreClockwise() == (direction == RotationDirection.CLOCKWISE));
            
            if(direction == RotationDirection.CLOCKWISE)
            {
                walkedPoints.Reverse();
            }
             */
            GridPolygon output = new GridPolygon(walkedPoints.EnsureClosedRing());

            //Add any interior polygons contained within our cut
            for (int iRing = 0; iRing < originPolygon.InteriorRings.Count; iRing++)
            {
                //We should be safe quickly testing a single point of each interior polygon because we test that the cut intersects the same ring only
                if (output.Contains(originPolygon.InteriorRings[iRing].First()))
                    output.AddInteriorRing(originPolygon.InteriorPolygons[iRing]);
            }

            if (output.IsValid() == false)
            {
                throw new ArgumentException("Invalid polygon created by cut. (Does the cutting line have loops?)");
            }
            return output;
        }


        public bool Equals(IShape2D other)
        {
            if (object.ReferenceEquals(other, null))
                return false;

            if (object.ReferenceEquals(this, other))
                return true;

            if (other.ShapeType != this.ShapeType)
                return false;

            if(other is IPolygon2D otherPoly)
                return this.Equals(otherPoly);

            return false;
        }

        public bool Equals(IPolygon2D other)
        {
            if (object.ReferenceEquals(other, null))
                return false;

            if (object.ReferenceEquals(this, other))
                return true;

            if (this.ExteriorRing.Length != other.ExteriorRing.Count)
                return false;

            if (this.TotalUniqueVerticies != other.TotalUniqueVerticies)
                return false;

            if (this._InteriorPolygons.Count != other.InteriorRings.Count)
                return false;

            for (int iVert = 0; iVert < this.ExteriorRing.Length; iVert++)
            {
                if (false == ExteriorRing[iVert].Equals(other.ExteriorRing[iVert]))
                    return false;
            }

            for (int iInner = 0; iInner < this._InteriorPolygons.Count; iInner++)
            {
                if (false == this.InteriorPolygons[iInner].Equals(other.InteriorPolygons[iInner]))
                    return false;
            }

            return true;
        }

        public bool Equals(GridPolygon other)
        {
            if (object.ReferenceEquals(other, null))
                return false;

            if (this.ExteriorRing.Length != other.ExteriorRing.Length)
                return false;

            if (this.TotalUniqueVerticies != other.TotalUniqueVerticies)
                return false;

            if (this._InteriorPolygons.Count != other._InteriorPolygons.Count)
                return false;

            for (int iVert = 0; iVert < this.ExteriorRing.Length; iVert++)
            {
                if (false == ExteriorRing[iVert].Equals(other.ExteriorRing[iVert]))
                    return false;
            }

            for (int iInner = 0; iInner < this._InteriorPolygons.Count; iInner++)
            {
                if (false == this.InteriorPolygons[iInner].Equals(other.InteriorPolygons[iInner]))
                    return false;
            }

            return true;
        }

    }
}
