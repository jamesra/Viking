using System;
using System.Collections;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;

namespace Geometry
{
    public enum Concavity
    {
        Concave = -1,
        Parallel = 0,
        Convex = 1
    }

    /// <summary>
    /// A polygon with interior rings representing holes.
    /// Rings are closed (first vertex equals last) and counter-clockwise.
    /// </summary>
    [Serializable()]
    public class Polygon : ICloneable, IPolygon2D, IHasControlPoints, IEquatable<Polygon>, IEquatable<IPolygon2D>
    {
        /// <summary>
        /// Exterior ring area only (holes not subtracted). Keep in sync when <see cref="_ExteriorRing"/> changes.
        /// </summary>
        double _ExteriorRingArea;


        /// <summary>Authoritative vertices. <see cref="ExteriorRing"/> get/set copies this; mutators must write here.</summary>
        Vector2[] _ExteriorRing;

        /// <summary>Internal alias of <see cref="_ExteriorRing"/>. Do not use <see cref="ExteriorRing"/> for in-place edits (that copies).</summary>
        internal Vector2[] RingStorage => _ExteriorRing;

        /// <summary>
        /// A counter-clockwise closed ring (first vertex equals last) of the outer contour.
        /// Get and set copy the array so callers cannot alias the polygon's storage.
        /// </summary>
        public Vector2[] ExteriorRing
        {
            get => [.. _ExteriorRing];
            set
            {
                Vector2[] copy = [.. value];
                _ExteriorRingArea = copy.PolygonArea();
                if (_ExteriorRingArea < 0) //Negative area indicates Clockwise orientation, we use counter-clockwise
                {
                    _ExteriorRingArea = -_ExteriorRingArea;
                    _ExteriorRing = [.. ((IEnumerable<Vector2>)copy).Reverse()];
                }
                else
                {
                    _ExteriorRing = copy;
                }

                _Centroid = null;
                _BoundingRect = _ExteriorRing.BoundingBox();
                _ExteriorSegments = CreateLineSegments(_ExteriorRing);
                //                _ExteriorSegmentRTree = null;
                _SegmentRTree = null;
            }
        }

        /// <summary>
        /// Cached AABB of current vertices. Mutators that change vertices must recompute or grow/shrink this.
        /// </summary>
        Rectangle _BoundingRect;

        public Rectangle BoundingBox => _BoundingRect;

        /// <summary>
        /// Cached exterior edges in ring order. Mutators that change vertices must rebuild or patch this.
        /// </summary>
        LineSegment[] _ExteriorSegments;

        public LineSegment[] ExteriorSegments => _ExteriorSegments;

        /// <summary>
        /// Spatial index of every segment (exterior and holes). Null means dirty.
        /// Mutators must null this field or patch it; do not assign <see cref="SegmentRTree"/>.
        /// </summary>
        [NonSerialized]
        BoundingBoxIndex<PolygonIndex> _SegmentRTree = null;

        /// <summary>Rebuilds from current rings when <see cref="_SegmentRTree"/> is null.</summary>
        internal BoundingBoxIndex<PolygonIndex> SegmentRTree
        {
            get
            {
                _SegmentRTree ??= CreatePointIndexSegmentBoundingBoxRTree(this);

                return _SegmentRTree;
            }
        }

        /// <summary>
        /// Test if a line segment is one of the polygons exterior segments
        /// </summary>
        /// <param name="segment"></param>
        /// <returns></returns>
        public bool IsExteriorSegment(LineSegment segment)
        {
            if (_ExteriorSegments.Length < 20)
            {
                return _ExteriorSegments.Any(s => s.EquivalentUndirected(segment));
            }
            else
            {
                //No need to check in further detail because they should be identical GridLineSegments
                //return ExteriorSegmentRTree.Intersects(segment.BoundingBox).Contains(segment);
                //return SegmentRTree.Intersects(Rectangle.Pad(segment.BoundingBox, Tolerance.Epsilon)).Where(i => i.IsInner == false).Select(p => p.Segment(this)).Contains(segment);
                return SegmentRTree.Intersects(Rectangle.Pad(segment.BoundingBox, Tolerance.Epsilon)).Any(i => i.IsInner == false && i.Segment(this).EquivalentUndirected(segment));
            }
        }

        /// <summary>
        /// Test if a line segment is one of the polygons exterior or interior segments
        /// </summary>
        /// <param name="segment"></param>
        /// <returns></returns>
        public bool IsExteriorOrInteriorSegment(LineSegment segment) => SegmentRTree.Intersects(Rectangle.Pad(segment.BoundingBox, Tolerance.Epsilon)).Any(p => p.Segment(this).EquivalentUndirected(segment));

        /// <summary>
        /// Cached area-weighted centroid of the exterior ring (holes ignored). Null means dirty; the getter recomputes.
        /// Mutators that change vertices must set this to null.
        /// </summary>
        [NonSerialized]
        Vector2? _Centroid;

        /// <summary>Area-weighted centroid of the exterior ring; holes do not pull the center.</summary>
        public Vector2 Centroid
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

        readonly List<Polygon> _InteriorPolygons = [];

        /// <summary>Holes. Mutate via Add/Remove/Replace interior-ring methods, not this list.</summary>
        public IReadOnlyList<Polygon> InteriorPolygons => _InteriorPolygons.AsReadOnly();

        /// <summary>Copies of each hole's exterior ring.</summary>
        public IReadOnlyList<Vector2[]> InteriorRings => [.. _InteriorPolygons.Select(p => p._ExteriorRing)];

        /// <summary>
        /// Return a list of all exterior and interior line segments
        /// </summary>
        public List<LineSegment> AllSegments
        {
            get
            {
                List<LineSegment> listLines = [.. this.ExteriorSegments];

                listLines.AddRange(this.InteriorPolygons.SelectMany(inner => inner.AllSegments));

                return listLines;
            }
        }

        public bool HasInteriorRings => _InteriorPolygons.Count > 0;

        /// <summary>
        /// Returns the point at the specified Index.  The iPoly value is not checked.
        /// </summary>
        /// <param name="index"></param>
        /// <returns></returns>
        public virtual Vector2 this[PolygonIndex index]
        {
            get => index.Point(this);
            set => SetVertex(index, value);
        }

        public Polygon(IEnumerable<IPoint2D> exteriorRing) : this(exteriorRing.Select(p => p.ToVector2()).ToArray())
        { }

        public Polygon(IEnumerable<Vector2> exteriorRing) : this(exteriorRing.ToArray())
        { }

        public Polygon(Vector2[] exteriorRing)
        {
            //Debug.Assert(exteriorRing.Length < 1000, "This is a huge polygon, why?");

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
                exteriorRing = [.. ((IEnumerable<Vector2>)exteriorRing).Reverse()];
            }

            ExteriorRing = exteriorRing;
        }


        public Polygon(IEnumerable<IPoint2D> exteriorRing, IEnumerable<IPoint2D[]> interiorRings)
            : this([.. exteriorRing.Select(p => p.ToVector2())],
                   [.. interiorRings.Select(inner_ring => inner_ring.Select(p => p.ToVector2()).ToArray())])
        {
        }

        public Polygon(Vector2[] exteriorRing, IEnumerable<Vector2[]> interiorRings)
        {
            //Keep in sync with SqlGeometryUtils.SqlToMyGeometryConverters.MaxPolygonRingPointsBeforeSimplify.
            const int MaxExteriorRingPointsForAssert = 5000;
            Debug.Assert(exteriorRing.Length < MaxExteriorRingPointsForAssert,
                "This is a huge polygon, why? Callers that load SQL geometry should pre-simplify via ToPolygon(tolerance).");

            if (!exteriorRing.IsValidClosedRing())
            {
                throw new ArgumentException("Exterior polygon ring must be valid");
            }

            ExteriorRing = exteriorRing;

            foreach (Vector2[] interiorRing in interiorRings)
            {
                //Debug.Assert(interiorRing.Length < 1000, "This is a huge polygon, why?");
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

        public double Perimeter => ExteriorRing.PerimeterLength();


        public ShapeType2D ShapeType => ShapeType2D.Polygon;

        IReadOnlyList<IPoint2D> IPolygon2D.ExteriorRing => [.. this.ExteriorRing.Select(p => p as IPoint2D)];

        IReadOnlyList<IPoint2D> IHasControlPoints.ControlPoints => ((IPolygon2D)this).ExteriorRing;

        IReadOnlyList<IPoint2D[]> IPolygon2D.InteriorRings => [.. this.InteriorRings.Select(ir => ir.Select(p => p as IPoint2D).ToArray())];

        IReadOnlyList<IPolygon2D> IPolygon2D.InteriorPolygons => this._InteriorPolygons; //.Select(inner => inner as IPolygon2D).ToArray();

        /// <summary>
        /// All unique verticies.  This is calculated for every use
        /// </summary>
        public Vector2[] AllVertices => [.. ExteriorRing.Union(InteriorRings.SelectMany(i => i)).Distinct()];

        /// <summary>
        /// Total verticies, including the duplicate verticies at the end of each ring
        /// </summary>
        public int TotalVertices => ExteriorRing.Length + InteriorRings.Sum(ir => ir.Length);

        /// <summary>
        /// Total verticies, minus the duplicate verticies at the end of each ring
        /// </summary>
        public int TotalUniqueVertices => (ExteriorRing.Length - 1) + InteriorRings.Sum(ir => ir.Length - 1);

        IPoint2D ICentroid.Centroid => Centroid;

        /// <summary>
        /// Adds an Interior Ring to this polygon.  Input must not intersect the exterior ring or existing interior rings.
        /// </summary>
        /// <param name="interiorRing"></param>
        public void AddInteriorRing(IEnumerable<Vector2> interiorRing)
        {
            Polygon innerPoly = new(interiorRing);

            //TODO: Make sure the inner poly does not  intersect the outer ring or any existing inner ring
            AddInteriorRing(innerPoly);
        }

        /// <summary>
        /// Adds an Interior Ring to this polygon.  Input must not intersect the exterior ring or existing interior rings.
        /// </summary>
        public void AddInteriorRing(Polygon innerPoly)
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
        public void ReplaceInteriorRing(int iInner, Polygon replacement)
        {
            Polygon original = this._InteriorPolygons[iInner];

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
        public bool TryRemoveInteriorRing(Vector2 holePosition)
        {
            for (int iPoly = 0; iPoly < _InteriorPolygons.Count; iPoly++)
            {
                if (_InteriorPolygons[iPoly].Covers(holePosition))
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
        public void AddVertex(Vector2 NewControlPointPosition)
        {
            //Find the line segment the NewControlPoint intersects
            double segment_distance = this.NearestSegment(NewControlPointPosition, out PolygonIndex nearestSegment);

            //Don't bother adding a point that already exists
            if (segment_distance < Tolerance.Epsilon && this[nearestSegment] == NewControlPointPosition)
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
        public bool InsertVertex(Vector2 NewControlPointPosition, PolygonIndex iVertex)
        {
            //Trace.WriteLine(string.Format("Add new Vertex {0} at {1}", iVertex, NewControlPointPosition));

            if (iVertex.ShapeIndex != 0)
                iVertex = iVertex.Reindex(0);

            if (iVertex.IsInner)
            {
                Polygon original_poly = iVertex.Polygon(this).Clone() as Polygon;

                //If InserrVertex throws an exception it should have restored the inner polygon state, so we don't need to react to an exception here

                //If InsertVertex returns false, the vertex already existed and we don't need to update our own data structures or check validity.
                if (this.InteriorPolygons[iVertex.InnerShapeIndex.Value].InsertVertex(NewControlPointPosition, iVertex.ReindexToOuter(0)))
                {

                    //However, after the update we need to make sure the new inner polygon is valid in the context of the outer polygon
                    //so restore our state if we throw an exception
                    try
                    {
                        if (IsInnerValid(iVertex.InnerShapeIndex.Value, CheckForIntersectionWithOtherInnerPolygons: true))
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
                        ReplaceInteriorRing(iVertex.InnerShapeIndex.Value, original_poly);
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
                Vector2[] updated_ring = this.ExteriorRing.InsertIntoClosedRing(iVertex.VertexIndex, NewControlPointPosition);

                //LineSegment[] updatedSegments = this.ExteriorSegments.Insert(NewControlPointPosition, iVertex.VertexIndex);
                //Vector2[] updated_ring = updatedSegments.Vertices();
                double updated_area = updated_ring.PolygonArea();
                if (updated_area < 0)
                {
                    //An easy case we can catch before adjusting any data structures. 
                    //Reverse the change before throwing the exception
                    //this.ExteriorRing[iVertex.VertexIndex] = old_point;

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
        internal void SetVertex(PolygonIndex iVertex, Vector2 value)
        {
            if (iVertex.ShapeIndex != 0)
                iVertex = iVertex.Reindex(0);

            if (iVertex.IsInner)
            {
                Polygon original_poly = iVertex.Polygon(this).Clone() as Polygon;

                try
                {
                    Polygon poly = iVertex.Polygon(this);
                    poly.SetVertex(iVertex.ReindexToOuter(), value);

                    this._InteriorPolygons[iVertex.InnerShapeIndex.Value] = poly;

                    UpdateSegmentRTreeForUpdate(iVertex);

                    if (this.IsInnerValid(iVertex.InnerShapeIndex.Value, CheckForIntersectionWithOtherInnerPolygons: true) == false)
                    {
                        //this.ExteriorRing = original_verts;
                        throw new ArgumentException(
                            $"Changing vertex {iVertex} to {value} resulted in an invalid state.");
                    }
                }
                catch (ArgumentException)
                {
                    //Restore our state
                    ReplaceInteriorRing(iVertex.InnerShapeIndex.Value, original_poly);
                    throw;
                }
            }
            else
            {
                Vector2 old_point = this._ExteriorRing[iVertex.VertexIndex];
                if (iVertex.IsFirstIndexInRing())
                {
                    this._ExteriorRing[0] = value;
                    this._ExteriorRing[_ExteriorRing.Length - 1] = value;
                }
                else
                {
                    this._ExteriorRing[iVertex.VertexIndex] = value;
                }

                if (_ExteriorRingArea < 0)
                {
                    //An easy case we can catch before adjusting data structures. 
                    //Reverse the change before throwing the exception
                    this._ExteriorRing[iVertex.VertexIndex] = old_point;

                    //We could help the caller by reversing the winding... should we?
                    throw new ArgumentException($"Changing vertex {iVertex} to {value} changed polygon winding order.");
                }

                //Update our data structures, then check that we are still valid:
                UpdateBoundingBoxForAdd(value);
                UpdateBoundingBoxForRemove(old_point);

                UpdateSegmentRTreeForUpdate(iVertex);

                _Centroid = null;

                if (this.IsValid() == false)
                {
                    //Restore our ExteriorRing
                    this._ExteriorRing[iVertex.VertexIndex] = old_point;

                    //Restore our bounding box
                    UpdateBoundingBoxForRemove(value);
                    UpdateBoundingBoxForAdd(old_point);

                    //Restore our RTree
                    UpdateSegmentRTreeForUpdate(iVertex);

                    throw new ArgumentException($"Changing vertex {iVertex} to {value} resulted in an invalid state.");
                }
            }
        }


        /// <summary>
        /// Removes the vertex closest to the passed point
        /// </summary>
        /// <param name="RemovedControlPointPosition"></param>
        public void RemoveVertex(Vector2 RemovedControlPointPosition)
        {
            double MinDistance = this.NearestVertex(RemovedControlPointPosition, out PolygonIndex index);

            RemoveVertex(index);
        }

        public void RemoveVertex(PolygonIndex iVertex)
        {
            if (iVertex.ShapeIndex != 0)
                iVertex = iVertex.Reindex(0);
            //Polygon poly = iVertex.Polygon(this);

            //poly.RemoveVertex(iVertex.VertexIndex);

            if (iVertex.IsInner)
            {
                Polygon original_poly = iVertex.Polygon(this).Clone() as Polygon;

                this._InteriorPolygons[iVertex.InnerShapeIndex.Value].RemoveVertex(iVertex.ReindexToOuter());
                //this.InteriorRings[iVertex.InnerShapeIndex.Value] = this.InteriorPolygons[iVertex.InnerShapeIndex.Value]._ExteriorRing;
                try
                {
                    if (this.IsInnerValid(iVertex.InnerShapeIndex.Value, CheckForIntersectionWithOtherInnerPolygons: true))
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
                    this.ReplaceInteriorRing(iVertex.InnerShapeIndex.Value, original_poly);
                }
            }
            else
            {
                //We must have at least 3 points to create a polygon
                if (ExteriorSegments.Length <= 3)
                {
                    throw new ArgumentException("Cannot remove vertex.  Polygon's must have three verticies.");
                }

                Vector2 removedVertex = this[iVertex];

                var original_verts = this.ExteriorRing;
                var original_bbox = this.BoundingBox;
                var original_area = this._ExteriorRingArea;
                var original_centroid = this._Centroid;
                var original_segments = this._ExteriorSegments;

                Vector2[] updated_ring = this.ExteriorRing.RemoveFromClosedRing(iVertex.VertexIndex);
                double updated_area = updated_ring.PolygonArea();
                if (updated_area < 0)
                {
                    //An easy case we can catch before adjusting any data structures. 
                    //We could help the caller by reversing the winding... should we?
                    throw new ArgumentException($"Removing vertex {iVertex} changed polygon winding order.");
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
                    throw new ArgumentException(
                        $"Removing vertex {iVertex} of {this.ExteriorRing.Length - 1} from polygon resulted in an invalid state");
                }
            }

            //this._SegmentRTree = null; //Reset our RTree since yanking a polygon and changing the indicies are a pain
        }

        /// <summary>
        /// Removes the vertex from the exterior ring of a polgon only
        /// </summary>
        /// <param name="iVertex"></param>
        public void RemoveVertex(int iVertex) => RemoveVertex(new PolygonIndex(0, iVertex, this.ExteriorRing.Length - 1));/*
            //We must have at least 3 points to create a polygon
            if (ExteriorSegments.Length <= 3)
            {
                throw new ArgumentException("Cannot remove vertex.  Polygon's must have three verticies.");
            }

            Vector2 removedVertex = this.ExteriorRing[iVertex];
            Vector2[] original_verts = this.ExteriorRing;

            UpdateSegmentRTreeForRemoval(new PointIndex(0, iVertex, this._ExteriorRing.Length - 1));
            this.UpdateBoundingBoxForRemove(removedVertex);

            //Find the line segment the NewControlPoint intersects
            LineSegment[] updatedLineSegments = ExteriorSegments.Remove(iVertex);
            
            this._ExteriorRing = updatedLineSegments.Vertices();
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

        #region Cached Values Update Code

        /// <summary>
        /// Update the ExteriorSegments of this polygon to account for a vertex insert
        /// </summary>
        /// <param name="index">The index of the vertex that was already inserted into the exterior ring</param>
        private void UpdateSegmentRTreeForInsert(PolygonIndex index)
        {
            if (_SegmentRTree is null)
                return;

            if (index.NumUniqueInRing != index.Polygon(this).ExteriorRing.Length - 1)
            {
                index = new PolygonIndex(index.ShapeIndex, index.InnerShapeIndex, index.VertexIndex, index.Polygon(this).ExteriorRing.Length - 1);
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
            LineSegment oldSeg = new(this[index.Previous], this[index.Next]);

            LineSegment newSeg = new(this[index.Previous], this[index]);
            LineSegment newNextSeg = new(this[index], this[index.Next]);

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
            if (_SegmentRTree is null)
                return;

            LineSegment newPrevSeg = new(this[index.Previous], this[index]);
            LineSegment newSeg = new(this[index], this[index.Next]);

            //Update the exterior segments if we are not updating an internal polygon, 
            //if this is an internal polygon it should have updated its own exterior segments
            //before reaching this point
            if (false == index.IsInner)
            {
                _ExteriorSegments[index.Previous.VertexIndex] = newPrevSeg;
                _ExteriorSegments[index.VertexIndex] = newSeg;
            }

            bool RTreePreviousItemFound = _SegmentRTree.Delete(index.Previous, out PolygonIndex rTreeRemovedPreviousItem);
            //Debug.Assert(RTreePreviousItemFound, $"Expected to find removed segment {index.Previous} (previous) in the RTree");
            if (RTreePreviousItemFound == false)
                throw new InvalidOperationException($"Expected to find removed segment {index.Previous} in the RTree");

            bool RTreeItemFound = _SegmentRTree.Delete(index, out PolygonIndex rTreeRemovedItem);
            //Debug.Assert(RTreeItemFound, $"Expected to find removed segment {index} in the RTree");
            if (RTreeItemFound == false)
                throw new InvalidOperationException($"Expected to find removed segment {index} in the RTree");

            _SegmentRTree.Add(newSeg.BoundingBox, index);
            _SegmentRTree.Add(newPrevSeg.BoundingBox, index.Previous);
        }

        /// <summary>
        /// Update the ExteriorSegments of this polygon to account for a vertex removal.  
        /// Called after the vertex has been removed from the ring
        /// </summary>
        private void UpdateSegmentRTreeForRemoval(PolygonIndex removed_index)
        {
            if (_SegmentRTree is null)
                return;

            Polygon poly = removed_index.Polygon(this);

            //Ensure the removed index has the correct ring length
            if (removed_index.NumUniqueInRing != poly.ExteriorRing.Length)
            {
                removed_index = new PolygonIndex(removed_index.ShapeIndex, removed_index.InnerShapeIndex, removed_index.VertexIndex, poly.ExteriorRing.Length);
            }

            //The index scaled to the new ring size
            PolygonIndex new_index = new(removed_index.ShapeIndex, removed_index.InnerShapeIndex, removed_index.VertexIndex, poly.ExteriorRing.Length - 1);

            LineSegment newSeg = new(this[new_index.Previous], this[new_index]);

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
            if (_SegmentRTree is null)
                return;

            LineSegment removedSegment = new LineSegment(this[index], this[index.Next]);
            LineSegment removedPrevSeg = new LineSegment(this[index.Previous], this[index]);
            LineSegment newSeg = new LineSegment(this[index.Previous], this[index.Next]);

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
            if (_SegmentRTree is null)
                return;

            PolygonIndex index = new(0, iInnerRing, 0, this.InteriorRings[iInnerRing].Length - 1);
            do
            {
                _SegmentRTree.Add(index.Segment(this).BoundingBox, index);
                index = index.Next;
            }
            while (index != index.FirstInRing);
        }

        private void RemoveRingFromRTree(int iInnerRing)
        {
            if (_SegmentRTree is null)
                return;

            var toRemove = _SegmentRTree.Items.Where(i => i.IsInner && i.InnerShapeIndex == iInnerRing);

            foreach (var item in toRemove)
            {
                bool found = _SegmentRTree.Delete(item, out var removedItem);
                Debug.Assert(found, $"Expected index {item} missing from RTree");
            }
        }


        /// <summary>Expands <see cref="_BoundingRect"/> to include <paramref name="point"/>.</summary>
        private void UpdateBoundingBoxForAdd(Vector2 point) => _BoundingRect += point;

        /// <summary>
        /// If <paramref name="removed_point"/> sat on the AABB, recompute <see cref="_BoundingRect"/> from the ring.
        /// </summary>
        /// <returns>True if the box changed.</returns>
        private bool UpdateBoundingBoxForRemove(Vector2 removed_point)
        {
            if (_BoundingRect.GetRelation((IPoint2D)removed_point) == ShapeRelation.Touching)
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

            //if (this.ExteriorSegments.SelfIntersects(LineSetOrdering.Closed))
            if (Polygon.SelfIntersects(this))
                return false;

            //Check that the interior polygons are inside the exterior ring
            if (this.InteriorPolygons.Count == 0)
            {
                return true;
            }
            else
            {
                Polygon externalPolyOnly = new(this.ExteriorRing);

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

            if (this.Area < Tolerance.Epsilon)
                return false;

            return true;
        }

        /// <summary>
        /// Return true if the exterior ring intersects itself
        /// </summary>
        /// <param name="lines"></param>
        /// <param name="IsClosedRing">True if the polyline forms a closed ring, in which case the first and last points are allowed to overlap</param>
        /// <returns></returns>
        private static bool SelfIntersects(in Polygon poly)
        {
            IReadOnlyList<LineSegment> lines = poly.ExteriorSegments;

            PolygonIndex Index = new(0, 0, poly.ExteriorRing.Length - 1);
            PolygonIndex FirstRingIndex = Index.FirstInRing;

            do
            {
                LineSegment ls = Index.Segment(poly);
                var candidates = poly.IntersectingSegmentIndices(ls);

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

                    bool EndpointsOnRingDoNotIntersect = LineSetOrdering.Closed.IsEndpointIntersectionExpected(iLine, jLine, lines.Count);

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
            int[] InnerIndices = [.. this.InteriorPolygons.Select((p, i) => i)];
            foreach (var combo in InnerIndices.CombinationPairs())
            {
                Polygon A = InteriorPolygons[combo.A];
                Polygon B = InteriorPolygons[combo.B];

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
            Polygon innerPoly = this.InteriorPolygons[iInner];

            if (innerPoly.IsValid() == false)
                return false;

            Polygon externalPolyOnly = new(this.ExteriorRing);

            //Do a quick sanity check that all interior verticies are inside the external polygon
            if (innerPoly.ExteriorRing.Any(v => externalPolyOnly.BoundingBox.Covers(v) == false))
                return false;

            if (innerPoly.ExteriorRing.Any(v => externalPolyOnly.Contains(v) == false))
                return false;

            if (Polygon.SegmentsIntersect(innerPoly, externalPolyOnly))
                return false;

            if (CheckForIntersectionWithOtherInnerPolygons)
            {
                //Check against the other interior polygons to ensure they do not intersect
                for (int i = 0; i < this.InteriorRings.Count; i++)
                {
                    //Don't check inner ring against itself
                    if (i == iInner)
                        continue;

                    Polygon otherInner = this.InteriorPolygons[i];

                    if (Polygon.SegmentsIntersect(innerPoly, otherInner))
                        return false;

                    if (otherInner.Contains(innerPoly.ExteriorRing[0]) || innerPoly.Contains(otherInner.ExteriorRing[0]))
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
        public bool IsVertex(in Vector2 point)
        {
            if (!this.BoundingBox.Covers(point))
            {
                return false;
            }

            if (this.ExteriorRing.Contains(point))
                return true;

            foreach (Polygon inner in this.InteriorPolygons)
            {
                if (!inner.BoundingBox.Covers(point))
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
        public bool TryGetIndex(in Vector2 point, out PolygonIndex index)
        {

            if (!this.BoundingBox.Covers(point))
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
                Polygon inner = InteriorPolygons[iInner];
                if (!inner.BoundingBox.Covers(point))
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
        public List<PolygonIndex> TryGetIndices(ICollection<Vector2> points)
        {
            List<PolygonIndex> found = new(points.Count);
            var candidates = points.Where(p => BoundingBox.Covers(p));
            List<Vector2> notExterior = new(points.Count);

            foreach (Vector2 point in points)
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
                        if (InteriorPolygons[iInner].Covers(point) == false)
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

            Angle = Vector2.AbsArcAngle(ExteriorRing[A], ExteriorRing[Origin], ExteriorRing[B], Clockwise: true);

            if (Angle == 0)
                return Concavity.Parallel;
            else if (Angle < Tolerance.Epsilon)
            {
                LineSegment AB = new(ExteriorRing[A], ExteriorRing[B]);
                if (AB.DistanceToPoint(ExteriorRing[iVert]) < Tolerance.Epsilon)
                    return Concavity.Parallel;
            }

            if (Angle > Math.PI)
            {
                return Concavity.Concave;
            }
            else
            {
                return Concavity.Convex;
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
        public bool IsConvex() => this.VertexConcavity(out double[] angles).All(c => c != Concavity.Concave);

        /// <summary>
        /// Returns the Polygon vertex closest to the point.  May return interior verticies
        /// </summary>
        /// <param name="polygon"></param>
        /// <param name="WorldPosition"></param> 
        /// <param name="nearestPoly">Nearest polygon</param>
        /// <param name="intersectingPoly">Index of vertex in the ring</param>
        /// <returns></returns>
        public double NearestVertex(in Vector2 WorldPosition, out PolygonIndex nearestVertex)
        {
            nearestVertex = new PolygonIndex(0, 0, ExteriorRing.Length - 1);
            double nearestVertexDistance = Vector2.Distance(WorldPosition, ExteriorRing[0]);
            bool CloserVertexFound = false;

            do
            {
                CloserVertexFound = false;
                Rectangle bbox = new(WorldPosition, nearestVertexDistance);
                //Try to find a nearer segment than our initial point, if we do, then repeat the search
                foreach (PolygonIndex index in SegmentRTree.IntersectionGenerator(bbox))
                {
                    var seg = index.Segment(this);
                    double measured_distance = Vector2.DistanceSquared(seg.A, WorldPosition);
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

                    measured_distance = Vector2.DistanceSquared(seg.B, WorldPosition);
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
                Polygon innerPoly = InteriorPolygons[iRing];
                double distance = innerPoly.NearestVertex(WorldPosition, out PointIndex foundIndex);
                if (distance < nearestPolyDistance)
                {
                    nearestVertex = new PointIndex(0, iRing, foundIndex.VertexIndex, innerPoly.ExteriorRing.Length - 1);
                    nearestPolyDistance = distance;
                }
            }

            double[] distances = ExteriorRing.Select(p => Vector2.Distance(p, WorldPosition)).ToArray();
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
        public double NearestSegment(in Vector2 WorldPosition, out PolygonIndex nearestVertex)
        {
            //Start with a random bounding box, and check all intersections, shrinking the bounding box each time
            double nearestPolyDistance = Vector2.Distance(WorldPosition, ExteriorRing[0]);
            nearestVertex = new PolygonIndex(0, 0, ExteriorRing.Length - 1);
            bool CloserSegmentFound = false;

            do
            {
                CloserSegmentFound = false;

                //Create a search box around our point of the minimum distance we know of
                Rectangle bbox = new(WorldPosition, nearestPolyDistance);

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
                        if (measured_distance < Tolerance.Epsilon)
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


        public bool Contains(in IPoint2D point_param) => GetRelation(point_param).IsContains();

        public bool Covers(in IPoint2D point_param) => GetRelation(point_param).IsCovers();

        public bool Contains(in Vector2 p) => GetRelation((IPoint2D)p).IsContains();

        public bool Covers(in Vector2 p) => GetRelation((IPoint2D)p).IsCovers();

        public ShapeRelation GetRelation(in Vector2 p) => GetRelation((IPoint2D)p);

        /// <summary>
        /// Point-in-polygon via winding number. Holes are exterior of this polygon (Contained inside a hole is None).
        /// </summary>
        public ShapeRelation GetRelation(in IPoint2D point_param)
        {
            if (!_BoundingRect.Covers(point_param))
                return ShapeRelation.None;

            Vector2 p = new(point_param.X, point_param.Y);

            //Create a line we know must pass outside the polygon
            //There is an edge case where the test line passes through a polygon vertex, so make sure the test line does not cross any verticies
            //Vector2 targetPoint = new LineSegment(this.ExteriorRing[0], this.ExteriorRing[1]).Bisect();
            //Vector2 targetPoint = new LineSegment(p.X, p.Y + this.ExteriorRing[0], this.ExteriorRing[1]).Bisect();

            //Line test_ray = new Line(point_param, targetPoint - point_param);

            //LineSegment test_line = test_ray.ToLine(Math.Max(BoundingBox.Width, BoundingBox.Height) * 2);


            //Make a horizontal line
            Line test_line = new(p, Vector2.UnitX);

            //Test all of the line segments for both interior and exterior polygons
            //The winding test requires every exterior segment, so no RTree narrowing is possible here.
            ShapeRelation result = IsPointInsidePolygonByWindingTest(_ExteriorSegments, test_line);
            if (result == ShapeRelation.Contained)
            {
                foreach (Polygon inner in this.InteriorPolygons)
                {
                    ShapeRelation inner_result = inner.GetRelation((IPoint2D)p);
                    //if (inner_result != ShapeRelation.None) //Including TOUCHING results probably breaks Bajaj generation, but it is correct
                    if (inner_result == ShapeRelation.Contained)
                        return ShapeRelation.None; //The point is in the inner polygon, therefore not part of this polygon

                    //Is a point on an inner polygon touching the polygon or contained?
                    if (inner_result == ShapeRelation.Touching)
                        return inner_result;
                }
            }

            return result;
        }

        /*
        static Random random = new Random();
        public bool ContainsWithPolyRayTest(IPoint2D point_param)
        {
            if (!_BoundingRect.Covers(point_param))
                return false;

            Vector2 p = new Vector2(point_param.X, point_param.Y);
            LineSegment? test_line = new LineSegment?();
            Line test_ray;
            //Create a line we know must pass outside the polygon
            //There is an edge case where the test line passes through a polygon vertex, so make sure the test line does not cross any verticies
            double test_line_length = Math.Max(BoundingBox.Width, BoundingBox.Height) * 2;
            Vector2[] AllVertices = this.AllVertices;

            if (AllVertices.Any(v => v == point_param))
                return true; 

            while (test_line.HasValue == false)
            {
                foreach (LineSegment s in this.ExteriorSegments)
                {
                    
                    Vector2 targetPoint = s.PointAlongLine(random.NextDouble());
                    if (targetPoint == point_param)
                        continue;

                    test_ray = new Line(point_param, targetPoint - point_param);

                    test_line = test_ray.ToLine(test_line_length);
                    if (AllVertices.Any(v => test_line.Value.DistanceToPoint(v) <= Tolerance.Epsilon))
                    {
                        test_line = null; 
                        continue; //Too close to a vertex.  Try another target
                    }

                    break;
                }
            }
            
            
            //LineSegment test_line = new Geometry.LineSegment(p, new Vector2(p.X + (BoundingBox.Width*2), p.Y));

            List<LineSegment> segmentsToTest;

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

        public bool Contains(in LineSegment line) => GetRelation(line).IsContains();

        public bool Covers(in LineSegment line) => GetRelation(line).IsCovers();

        ShapeRelation IShape2D.GetRelation(in ILineSegment2D line) => GetRelation(line.ToLineSegment());

        public ShapeRelation GetRelation(in LineSegment line)
        {
            if (line.BoundingBox.GetRelation(this.BoundingBox) == ShapeRelation.None)
                return ShapeRelation.None;

            //Ensure both endpoints are inside and a point in the center.
            //Test the center because if the line crosses a concave region with both endpoints exactly on the exterior ring we'd not have any intersections but the poly would not contain the line.
            if (!(this.Covers(line.A) && this.Covers(line.B) && this.Covers(line.PointAlongLine(0.5))))
                return ShapeRelation.None;

            IEnumerable<LineSegment> segmentsToTest = _ExteriorSegments.Length > 32 || HasInteriorRings ? this.GetIntersectingSegments(line) : [.. _ExteriorSegments];
            bool intersects = line.Intersects(segmentsToTest, true); //It is OK for endpoints to be on the exterior ring.
            if (intersects)
            {
                //The line intersects some of the polygon segments, but was it just the endpoint?
                return ShapeRelation.Intersecting; //Line is not entirely inside the polygon
            }

            foreach (Polygon innerPoly in this.InteriorPolygons)
            {
                var innerResult = innerPoly.GetRelation(line);
                if (innerResult == ShapeRelation.Intersecting || innerResult == ShapeRelation.Touching)
                    return innerResult;
                else if (innerResult == ShapeRelation.Contained)
                    return ShapeRelation.None; //It is entirely inside the hole, so it has no overlap 
            }

            return ShapeRelation.Contained;
        }


        /// <summary>
        /// OGC Contains: the disk's interior lies in this polygon's interior (boundary contact is false).
        /// </summary>
        public bool Contains(Circle other) => GetRelation(other).IsContains();

        /// <summary>
        /// OGC Covers: the closed disk lies in this closed polygon.
        /// </summary>
        public bool Covers(Circle other) => GetRelation(other).IsCovers();

        /// <summary>
        /// How this polygon relates to the circle: contained (disk is inside the polygon), intersecting, touching, or none.
        /// </summary>
        public ShapeRelation GetRelation(in Circle other)
        {
            Circle circle = other;
            if (!BoundingBox.Intersects(circle.BoundingBox))
                return ShapeRelation.None;

            bool boundary = CircleIntersectsBoundary(circle);
            ShapeRelation centerRel = GetRelation((IPoint2D)circle.Center);
            bool centerInside = centerRel.IsContains();

            if (centerInside && !boundary)
            {
                if (InteriorRings.Any(ir => circle.Covers(ir[0])))
                    return ShapeRelation.Intersecting;

                foreach (Polygon inner in InteriorPolygons)
                {
                    ShapeRelation hole = inner.GetRelation(circle);
                    if (hole == ShapeRelation.Contained)
                        return ShapeRelation.None;
                    if (hole == ShapeRelation.Intersecting || hole == ShapeRelation.Touching)
                        return hole;
                }

                return ShapeRelation.Contained;
            }

            if (boundary)
            {
                double dist = Distance(circle.Center);
                if (Math.Abs(dist - circle.Radius) <= Tolerance.Epsilon)
                    return ShapeRelation.Touching;
                return ShapeRelation.Intersecting;
            }

            if (circle.Covers(ExteriorRing[0]))
                return ShapeRelation.Intersecting;

            return ShapeRelation.None;
        }

        internal bool CircleIntersectsBoundary(in Circle circle)
        {
            foreach (LineSegment seg in AllSegments)
            {
                if (circle.Intersects(seg))
                    return true;
            }

            return false;
        }

        /// <summary>
        /// OGC Contains: <paramref name="other"/> lies in this polygon's interior.
        /// </summary>
        public bool Contains(in Polygon other) => GetRelation(other).IsContains();

        /// <summary>
        /// OGC Covers: <paramref name="other"/> lies in this closed polygon.
        /// </summary>
        public bool Covers(in Polygon other) => GetRelation(other).IsCovers();

        public bool Contains(in IShape2D other) => GetRelation(other).IsContains();

        public bool Covers(in IShape2D other) => GetRelation(other).IsCovers();

        public ShapeRelation GetRelation(in IShape2D other)
        {
            if (other is null)
                throw new ArgumentNullException(nameof(other));

            return other.ShapeType switch
            {
                ShapeType2D.Point => GetRelation((IPoint2D)other),
                ShapeType2D.Line => GetRelation(((ILineSegment2D)other).ToLineSegment()),
                ShapeType2D.Circle => GetRelation(((ICircle2D)other).ToCircle()),
                ShapeType2D.Polygon => GetRelation(((IPolygon2D)other).ToPolygon()),
                ShapeType2D.Rectangle => GetRelation(ShapeRelationHelpers.RectangleAsPolygon(((IRectangle2D)other).ToRectangle())),
                ShapeType2D.Triangle => GetRelation(ShapeRelationHelpers.TriangleAsPolygon(((ITriangle2D)other).ToTriangle())),
                ShapeType2D.Quad => GetRelation(ShapeRelationHelpers.QuadAsPolygon((Quad)other)),
                ShapeType2D.Polyline => RelationToPolyline((IPolyLine2D)other),
                ShapeType2D.InfiniteLine => RelationToInfiniteLine((Line)other),
                ShapeType2D.Collection => ShapeRelationHelpers.RelationToCollection(this, (IShapeCollection2D)other),
                _ => ShapeRelation.None,
            };
        }

        ShapeRelation RelationToPolyline(IPolyLine2D line)
        {
            List<ShapeRelation> parts = new(line.LineSegments.Count);
            foreach (ILineSegment2D seg in line.LineSegments)
                parts.Add(GetRelation(seg.ToLineSegment()));
            return ShapeRelationHelpers.CombineParts(parts);
        }

        ShapeRelation RelationToInfiniteLine(in Line line)
        {
            foreach (LineSegment seg in AllSegments)
            {
                if (line.Intersects(seg, out _))
                    return ShapeRelation.Intersecting;
            }

            return ShapeRelation.None;
        }

        /// <summary>
        /// How <paramref name="other"/> relates to this polygon: nested interior, shared boundary, crossing, or disjoint.
        /// </summary>
        public ShapeRelation GetRelation(in Polygon other)
        {
            Rectangle? overlap = BoundingBox.Intersection(other.BoundingBox);
            if (!overlap.HasValue)
                return ShapeRelation.None;

            bool properCross = false;
            bool boundaryContact = false;
            List<LineSegment> candidates = GetIntersectingSegments(overlap.Value);
            foreach (LineSegment candidate in candidates)
            {
                foreach (LineSegment otherSeg in other.GetIntersectingSegments(candidate.BoundingBox))
                {
                    ShapeRelation segRel = candidate.GetRelation(otherSeg, out IShape2D intersection);
                    if (segRel == ShapeRelation.None)
                        continue;

                    if (segRel == ShapeRelation.Intersecting && intersection is Vector2)
                        properCross = true;
                    else
                        boundaryContact = true;
                }
            }

            if (properCross)
                return ShapeRelation.Intersecting;

            bool anyContained = false;
            bool anyTouching = false;
            bool anyExterior = false;
            int n = other.ExteriorRing.Length;
            int last = n > 1 && other.ExteriorRing[0] == other.ExteriorRing[n - 1] ? n - 1 : n;
            for (int i = 0; i < last; i++)
            {
                ShapeRelation vertRel = GetRelation((IPoint2D)other.ExteriorRing[i]);
                if (vertRel == ShapeRelation.Contained)
                    anyContained = true;
                else if (vertRel == ShapeRelation.Touching)
                    anyTouching = true;
                else
                    anyExterior = true;
            }

            if (GetRelation((IPoint2D)other.Centroid) == ShapeRelation.Contained)
                anyContained = true;

            if (anyContained && !anyExterior)
                return anyTouching || boundaryContact ? ShapeRelation.Touching : ShapeRelation.Contained;

            if (anyContained)
                return ShapeRelation.Intersecting;

            if (anyTouching || boundaryContact)
                return ShapeRelation.Touching;

            if (other.Covers(ExteriorRing[0]))
                return ShapeRelation.Intersecting;

            return ShapeRelation.None;
        }

        public bool InteriorPolygonContains(in Vector2 p) => InteriorPolygonContains(p, out Polygon intersectedPoly);

        public bool InteriorPolygonContains(in Vector2 p, out Polygon interiorPolygon)
        {
            interiorPolygon = null;
            if (!_BoundingRect.Covers(p))
                return false;

            //Check that our point is not inside an interior hole
            foreach (Polygon innerPoly in _InteriorPolygons)
            {
                if (innerPoly.Covers(p))
                {
                    interiorPolygon = innerPoly;
                    return true;
                }
            }

            return false;
        }

        public bool InteriorPolygonIntersects(in LineSegment line) => InteriorPolygonIntersects(line, out Polygon intersectedPoly);

        public bool InteriorPolygonIntersects(in LineSegment line, out Polygon interiorPolygon)
        {
            interiorPolygon = null;
            if (!_BoundingRect.Intersects(line.BoundingBox))
                return false;

            //Check that our point is not inside an interior hole
            foreach (Polygon innerPoly in _InteriorPolygons)
            {
                if (innerPoly.Intersects(line))
                {
                    interiorPolygon = innerPoly;
                    return true;
                }
            }

            return false;
        }

        /// <summary>
        /// Not the true incircle (tangent to every edge). Center is the centroid; radius is the
        /// distance to the nearest vertex, so the disk can extend past edges.
        /// </summary>
        public Circle InscribedCircle()
        {
            Vector2 center = this.Centroid;
            double Radius = ExteriorRing.Select(p => Vector2.Distance(center, p)).Min();
            return new Circle(center, Radius);
        }

        /// <summary>
        /// The results of whether a polygon segment is left, right, or on a test line
        /// </summary>
        private readonly struct SegmentIsLeftData(int a_is_left, int b_is_left, LineSegment seg, int? is_p_left_of_seg)
        {
            /// <summary>
            /// Is S.A left of the line?
            /// </summary>
            public readonly int A_is_left = a_is_left;

            /// <summary>
            /// Is S.B left of the line?
            /// </summary>
            public readonly int B_is_left = b_is_left;

            /// <summary>The polygon segment that was tested, not the test line.</summary>
            public readonly LineSegment S = seg;

            public readonly int? IsPLeftOfSeg = is_p_left_of_seg;

            /// <summary>
            /// The segment crosses the line, with one endpoint on one side and the the other across it
            /// </summary>
            public bool CrossesLine => TouchesLine == false && A_is_left != B_is_left;

            /// <summary>
            /// The segment touches the line, one endpoint is off the line, the other is on the line
            /// </summary>
            public bool TouchesLine => (A_is_left == 0 ^ B_is_left == 0);

            /// <summary>
            /// The segment is perfectly on the line, both endpoints are on the line
            /// </summary>
            public bool OnTheLine => A_is_left == 0 && B_is_left == 0;

            /// <summary>
            /// The segment is to one side or the other, but does not intersect
            /// </summary>
            public bool SameSideOfLine => A_is_left == B_is_left && A_is_left != 0;

        }
        /*
        private static List<IsLeftData> RemoveSameSideSegments(List<LineSegment> polygonSegments, Line test_line)
        {
            Vector2 test_point = test_line.Origin;

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

        /// <summary>
        /// Winding-number point-in-polygon. Non-zero winding is Contained; a point on an edge is Touching.
        /// </summary>
        /// <remarks>
        /// Hormann and Agathos, "The point in polygon problem for arbitrary polygons,"
        /// Computational Geometry 20(3):131–144 (2001). Adjacent edges that only touch the
        /// test line are merged so a vertex on the ray is not counted twice.
        /// </remarks>
        private static ShapeRelation IsPointInsidePolygonByWindingTest(IReadOnlyList<LineSegment> polygonSegments, Line test_line)
        {
            Vector2 test_point = test_line.Origin;
#if DEBUG
            List<LineSegment> OriginalSegments = [.. polygonSegments]; //Create a copy so we can examine the debugger
#endif
            //OK, now we need to condense any instance where IsLeft.A or IsLeft.B == 0.  That is, the segment does not cross the line, mearly touches it. 
            //If we have opposite IsLeftValues we create a new edge that entirely crosses the line.  Otherwise we ignore the edge, which is the case where the segment touches the test_line but does not cross.

            List<SegmentIsLeftData> IsLeft = new(polygonSegments.Count);

            for (int i = 0; i < polygonSegments.Count; i++)
            {
                LineSegment s = polygonSegments[i];
                if (s.IsEndpoint(test_line.Origin))
                {
                    return ShapeRelation.Touching;
                }

                SegmentIsLeftData seg = new(a_is_left: test_line.IsLeft(s.A), b_is_left: test_line.IsLeft(s.B), seg: s, is_p_left_of_seg: new int?());
                if (seg.TouchesLine)
                {
                    //Check the case of the segment crossing, contacting, or perfectly overlapped to the line within epsilon error limit
                    if (seg.S.DistanceToPoint(test_point) < Tolerance.Epsilon)
                        return ShapeRelation.Touching;

                }
                else if (seg.CrossesLine || seg.OnTheLine)
                {
                    //Check the case of the segment crossing, contacting, or perfectly overlapped to the line within epsilon error limit
                    if (seg.S.DistanceToPoint(test_point) < Tolerance.Epsilon)
                        return ShapeRelation.Touching;
                }

                if (seg.SameSideOfLine || seg.OnTheLine)
                {
                    continue;
                }

                IsLeft.Add(seg);
            }

            if (IsLeft.Count == 0)
                return ShapeRelation.None;

            //From here we mutate in parallel with IsLeft, so work on a private list instead of the caller's segments.
            List<LineSegment> workingSegments = [.. IsLeft.Select(left => left.S)];

            //Find all segments that touch the line.  Remove the endpoints that touch the line and create a virtual segment that runs between the endpoints that did not touch the line.  This prevents double-counting windings.
            //InfiniteSequentialIndexSet SegEnumerator = new InfiniteSequentialIndexSet(0, IsLeft.Count, 0);
            for (int i = 0; i < IsLeft.Count; i++)
            {
                int iNext = i + 1 >= IsLeft.Count ? 0 : i + 1; //The index of the next entry in the list
                var seg = IsLeft[i];
                if (seg.A_is_left != 0 && seg.B_is_left != 0)
                {
                    //Check the case of the point exactly on the line
                    if (seg.S.DistanceToPoint(test_point) < Tolerance.Epsilon)
                        return ShapeRelation.Touching;

                    continue;   //Segment does not end on the line, continue;
                }

                if (seg.B_is_left == 0) //Seg.A == 0 will be caught by a later iteration
                {
                    var nextSeg = IsLeft[iNext];
                    int nextSegIsLeft = nextSeg.A_is_left != 0 ? nextSeg.A_is_left : nextSeg.B_is_left; //Figure out which part of the next line is not on the test line.  Create a new virtual line or delete
                    Vector2 nextSegEndpoint = nextSeg.A_is_left != 0 ? nextSeg.S.A : nextSeg.S.B;

                    Debug.Assert(nextSeg.S.OppositeEndpoint(nextSegEndpoint).Y == seg.S.B.Y, "We expect the lines to be input in the order they appear in the ring.  Lines sharing endpoints must be adjacent.");

                    if (nextSegIsLeft == seg.A_is_left) //We touch the line and retreat.  We can remove both entries 
                    {
                        workingSegments.RemoveAt(Math.Max(i, iNext));
                        workingSegments.RemoveAt(Math.Min(i, iNext));

                        IsLeft.RemoveAt(Math.Max(i, iNext));
                        IsLeft.RemoveAt(Math.Min(i, iNext));

                        i -= i < iNext ? 1 : 2; //Adjust for wraparound case
                    }
                    else  //We touch the line and then cross over it.  We can remove both entries and add a new one
                    {
                        LineSegment virtualPolySegment = new(seg.S.A, nextSegEndpoint);
                        workingSegments.RemoveAt(i);
                        workingSegments.Insert(i, virtualPolySegment);
                        workingSegments.RemoveAt(iNext);

                        SegmentIsLeftData newEntry = new(a_is_left: seg.A_is_left,
                            b_is_left: nextSegIsLeft,
                            seg: virtualPolySegment,
                            is_p_left_of_seg: new int?(seg.S.IsLeft(test_point))); //Record whether the lines were left of the test_point in case the new line moves to the other side of the point.
                        IsLeft.RemoveAt(i);
                        IsLeft.Insert(i, newEntry);
                        IsLeft.RemoveAt(iNext);

                        //i = i; //Adjust to check the next record 
                    }
                }
            }

            var cross_or_parallel_segments = workingSegments; //polygonSegments.Where((s, i) => (IsLeft[i].A != IsLeft[i].B) || (IsLeft[i].A == 0 || IsLeft[i].B == 0)).ToArray(); //Find all segments that span the testline or are parallel

            //If we share endpoints then we are always inside the polygon.  Handles case where we ask if a polygon vertex is inside the polygon
            //if (cross_or_parallel_segments.Any(ps => ps.IsEndpoint(test_line.A)))
            //    return ShapeRelation.Touching;

            int wind_count = 0;
            for (int i = 0; i < cross_or_parallel_segments.Count; i++)
            {
                var SegData = IsLeft[i];
                LineSegment polySeg = SegData.S;
                int IsAboveToBelow;
                int pIsLeft;

                IsAboveToBelow = SegData.S.A.Y.CompareTo(SegData.S.B.Y);

                pIsLeft = SegData.IsPLeftOfSeg.HasValue == false ? polySeg.IsLeft(test_point) : SegData.IsPLeftOfSeg.Value;

                if (IsAboveToBelow == 0)
                    continue;
                else if (IsAboveToBelow > 0)
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

            return wind_count != 0 ? ShapeRelation.Contained : ShapeRelation.None;
        }

        /// <summary>
        /// Even-odd (crossing-number) test. Duplicate intersections at vertices are collapsed so a ray
        /// through a vertex is not counted twice.
        /// </summary>
        /// <remarks>
        /// Haines, "Point in Polygon Strategies," in Graphics Gems IV, Academic Press, 1994.
        /// Prefer <see cref="IsPointInsidePolygonByWindingTest"/> for the public point predicate.
        /// </remarks>
        private static bool IsPointInsidePolygonByRayTest(ICollection<LineSegment> polygonSegments, LineSegment test_line)
        {
            //In cases where our test line passes exactly through a vertex on the other polygon we double count the line.  
            //This code removes duplicate intersection points to prevent duplicates

            //If we share endpoints then we are always inside the polygon.  Handles case where we ask if a polygon vertex is inside the polygon
            if (polygonSegments.Any(ps => ps.SharedEndPoint(test_line)))
                return true;

            List<Vector2> intersections;
            IEnumerable<LineSegment> IntersectedSegments;

            if (polygonSegments.Count > 128)
            {
                System.Collections.Concurrent.ConcurrentBag<Vector2> intersectionsBag = [];

                IntersectedSegments = polygonSegments.Where(line =>
                {
                    bool intersected = line.Intersects(test_line, out Vector2 Intersection);
                    if (intersected)
                    {
                        intersectionsBag.Add(Intersection);
                    }

                    return intersected;
                }).AsParallel().ToList(); //Need ToList here to ensure the query executes fully

                intersections = [.. intersectionsBag];
            }
            else
            {
                intersections = new List<Vector2>(polygonSegments.Count);

                IntersectedSegments = [.. polygonSegments.Where(line =>
                {
                    bool intersected = line.Intersects(test_line, out Vector2 Intersection);
                    if (intersected)
                    {
                        intersections.Add(Intersection);
                    }

                    return intersected;
                })]; //Need ToList here to ensure the query executes fully
            }

            //Ensure the line doesn't pass through on a line endpoint
            //SortedSet<Vector2> intersectionPoints = new SortedSet<Vector2>();
            Vector2[] UniqueIntersections = [.. intersections.Distinct()];

            if (UniqueIntersections.Any(p => test_line.IsEndpoint(p)))
                return true; //If the point is exactly on the line then we can often have two intersections as the line leaves the polygon which results in a false negative.
                             //This test short-circuits that problem

            //If the intersection point is exactly through a polygon vertex then two segments will be returned but we should count only one.
            //Inside the polygon if we intersect line segments of the border an odd number of times
            return UniqueIntersections.Length % 2 == 1;
        }


        /// <summary>
        /// Returns an array of GridLineSegments in the same order they appear in the ExteriorRing array.
        /// </summary>
        /// <param name="ring_points"></param>
        /// <returns></returns>
        private static LineSegment[] CreateLineSegments(Vector2[] ring_points)
        {
            Debug.Assert(ring_points[0] == ring_points[ring_points.Length - 1], "CreateLineSegments expects a closed ring as input");

            LineSegment[] lines = new LineSegment[ring_points.Length - 1];

            for (int iPoint = 0; iPoint < ring_points.Length - 1; iPoint++)
            {
                LineSegment line = new(ring_points[iPoint], ring_points[iPoint + 1]);
                lines[iPoint] = line;
            }

            return lines;
        }

        private static BoundingBoxIndex<LineSegment> CreateSegmentBoundingBoxRTree(LineSegment[] segments)
        {
            BoundingBoxIndex<LineSegment> R = new();

            foreach (LineSegment l in segments)
            {
                R.Add(Rectangle.Pad(l.BoundingBox, Tolerance.Epsilon), l);
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
        private static BoundingBoxIndex<PolygonIndex> CreatePointIndexSegmentBoundingBoxRTree(Polygon poly)
        {
            BoundingBoxIndex<PolygonIndex> R = new();

            PolygonVertexEnum enumerator = new(poly);
            foreach (PolygonIndex p in enumerator)
            {
                LineSegment s = p.Segment(poly);
                R.Add(Rectangle.Pad(s.BoundingBox, Tolerance.Epsilon), p);
            }

            return R;
        }

        /// <summary>
        /// Return all segments, both interior and exterior, that fall within the bounding rectangle
        /// </summary>
        /// <param name="bbox"></param>
        /// <returns></returns>
        public IEnumerable<LineSegment> GetIntersectingSegments(LineSegment line)
        {
            Rectangle bbox = line.BoundingBox;
            if (!this.BoundingBox.Intersects(bbox))
            {
                return Array.Empty<LineSegment>();
            }

            //return SegmentRTree.Intersects(bbox).Select(p => p.Segment(this)).Where(segment => line.Intersects(segment, false)).ToList();
            return SegmentRTree.IntersectionGenerator(Rectangle.Pad(bbox, Tolerance.Epsilon)).Select(p => p.Segment(this)).Where(segment => line.Intersects(segment, false));
        }

        /// <summary>
        /// Return all segments, both interior and exterior, that fall within the bounding rectangle
        /// </summary>
        /// <param name="bbox"></param>
        /// <returns></returns>
        public List<LineSegment> GetIntersectingSegments(Rectangle bbox)
        {
            if (!this.BoundingBox.Intersects(bbox))
            {
                return [];
            }

            var intersections = SegmentRTree.Intersects(Rectangle.Pad(bbox, Tolerance.Epsilon));
            var segments = intersections.Select(p => p.Segment(this));
            List<LineSegment> candidates = [.. segments.Where(segment => bbox.Intersects(segment))];
            return candidates;

        }

        /// <summary>
        /// True if a disk of <paramref name="controlPointRadius"/> around <paramref name="worldPosition"/>
        /// covers an exterior or hole vertex. Recurses into holes.
        /// </summary>
        public bool PointIntersectsAnyPolygonVertex(Vector2 worldPosition, double controlPointRadius, out Polygon intersectingPoly)
        {
            if (!PaddedBoundingBoxCovers(controlPointRadius, worldPosition))
            {
                intersectingPoly = null;
                return false;
            }

            foreach (Polygon innerPoly in InteriorPolygons)
            {
                if (innerPoly.PointIntersectsAnyPolygonVertex(worldPosition, controlPointRadius, out intersectingPoly))
                    return true;
            }

            Circle testCircle = new(worldPosition, controlPointRadius);
            if (ExteriorRing.Any(v => testCircle.Covers(v)))
            {
                intersectingPoly = this;
                return true;
            }

            intersectingPoly = null;
            return false;
        }

        /// <summary>
        /// True if <paramref name="worldPosition"/> is within half <paramref name="lineWidth"/> of an
        /// exterior or hole segment. Recurses into holes.
        /// </summary>
        public bool PointIntersectsAnyPolygonSegment(Vector2 worldPosition, double lineWidth, out Polygon intersectingPoly)
        {
            if (!PaddedBoundingBoxCovers(lineWidth / 2.0, worldPosition))
            {
                intersectingPoly = null;
                return false;
            }

            foreach (Polygon innerPoly in InteriorPolygons)
            {
                if (innerPoly.PointIntersectsAnyPolygonSegment(worldPosition, lineWidth, out intersectingPoly))
                    return true;
            }

            ExteriorSegments.NearestSegment(worldPosition, out double minDistance);
            if (minDistance < lineWidth / 2.0)
            {
                intersectingPoly = this;
                return true;
            }

            intersectingPoly = null;
            return false;
        }

        bool PaddedBoundingBoxCovers(double padding, Vector2 position)
        {
            Rectangle padded = BoundingBox + padding;
            return padded.Covers(position);
        }

        /// <summary>
        /// Ring vertex indices of segments that meet <paramref name="line"/>, keyed by distance from <paramref name="line"/>.A.
        /// Distinct from <see cref="GetIntersectingSegments(LineSegment)"/>, which returns the segments themselves.
        /// </summary>
        public SortedDictionary<double, PolygonIndex> IntersectingSegmentIndices(in LineSegment line)
        {
            SortedDictionary<double, PolygonIndex> output = [];

            PolygonIndex[] candidates = [.. SegmentRTree.Intersects(line.BoundingBox)];
            List<PolygonIndex> addedVertices = [];

            foreach (PolygonIndex index in candidates)
            {
                if (addedVertices.Contains(index))
                    continue;

                LineSegment segment = index.Segment(this);
                if (!segment.Intersects(in line, false, out IShape2D intersection))
                    continue;

                if (intersection is not IPoint2D p)
                {
                    if (output.ContainsKey(0))
                        continue;

                    AddSegmentIndex(output, 0, index);
                    addedVertices.Add(index);
                    continue;
                }

                Vector2 p2 = new(p.X, p.Y);
                double distance = Vector2.Distance(line.A, p2);

                if (segment.IsEndpoint(p2))
                {
                    if (output.ContainsKey(distance))
                        continue;

                    PolygonIndex intersectionIndex = index;
                    if (p2 == segment.B)
                    {
                        intersectionIndex = index.Next;
                        if (addedVertices.Contains(intersectionIndex))
                            continue;
                    }

                    AddSegmentIndex(output, distance, intersectionIndex);
                    addedVertices.Add(intersectionIndex);
                }
                else
                {
                    AddSegmentIndex(output, distance, index);
                    addedVertices.Add(index);
                }
            }

            return output;
        }

        /// <summary>
        /// Ring vertex indices of segments that meet <paramref name="path"/>, keyed by distance along the path.
        /// </summary>
        public SortedDictionary<double, PolygonIndex> IntersectingSegmentIndices(LineSegment[] path)
        {
            SortedDictionary<double, PolygonIndex> output = [];

            for (int iRing = 0; iRing < InteriorRings.Count; iRing++)
            {
                Polygon innerPoly = InteriorPolygons[iRing];
                SortedDictionary<double, PolygonIndex> ringIntersections = innerPoly.IntersectingSegmentIndices(path);
                foreach (var item in ringIntersections)
                    AddSegmentIndex(output, item.Key, new PolygonIndex(0, iRing, item.Value.VertexIndex, innerPoly.ExteriorRing.Length - 1));
            }

            double totalLength = 0;
            for (int iPath = 0; iPath < path.Length; iPath++)
            {
                LineSegment line = path[iPath];

                for (int iSegment = 0; iSegment < ExteriorSegments.Length; iSegment++)
                {
                    LineSegment segment = ExteriorSegments[iSegment];
                    if (!segment.Intersects(line, false, out IShape2D intersection))
                        continue;

                    IPoint2D p = intersection as IPoint2D;
                    Vector2 p2 = new(p.X, p.Y);
                    double distance = Vector2.Distance(line.A, p2) + totalLength;
                    if (segment.IsEndpoint(p2))
                    {
                        if (p2 == segment.B)
                            iSegment++;

                        AddSegmentIndex(output, distance, new PolygonIndex(0, iSegment, ExteriorSegments.Length));
                    }
                    else
                    {
                        AddSegmentIndex(output, distance, new PolygonIndex(0, iSegment, ExteriorSegments.Length));
                    }
                }

                totalLength += line.Length;
            }

            return output;
        }

        static void AddSegmentIndex(SortedDictionary<double, PolygonIndex> dict, double key, PolygonIndex index) =>
            dict.Add(key, index);

        /// <summary>
        /// Rotate the polygon by the spefied angle around the specified origin
        /// </summary>
        /// <param name="angle"></param>
        /// <param name="origin">Defaults to Centroid if not specified</param>
        /// <returns>A rotated copy of this polygon</returns>
        public Polygon Rotate(double angle, Vector2? origin = null)
        {
            if (!origin.HasValue)
            {
                origin = this.Centroid;
            }

            Vector2[] RotatedRing = this.ExteriorRing.Rotate(angle, origin.Value);

            Polygon poly = new(RotatedRing);

            foreach (Polygon innerRing in this._InteriorPolygons)
            {
                Polygon rotated_inner = innerRing.Rotate(angle, origin);
                poly.AddInteriorRing(rotated_inner);
            }

            return poly;
        }

        public Polygon Scale(double scalar, Vector2? origin = null) => this.Scale(new Vector2(scalar, scalar), origin);

        /// <summary>
        /// Scale from <paramref name="origin"/> (centroid if omitted).
        /// </summary>
        public Polygon Scale(Vector2 scalar, Vector2? origin = null)
        {
            if (!origin.HasValue)
            {
                origin = this.Centroid;
            }

            Vector2[] ScaledRing = this.ExteriorRing.Scale(scalar, origin.Value);

            Polygon poly = new(ScaledRing);

            foreach (Polygon innerRing in this._InteriorPolygons)
            {
                Polygon scaled_inner = innerRing.Scale(scalar, origin);
                poly.AddInteriorRing(scaled_inner);
            }

            return poly;
        }

        /// <summary>
        /// Translate the polygon
        /// </summary>
        /// <param name="offset"></param>
        /// <returns>A translated copy of this polygon</returns>
        public Polygon Translate(Vector2 offset)
        {
            Vector2[] TranslatedRing = this.ExteriorRing.Translate(offset);

            Polygon poly = new(TranslatedRing);

            foreach (Polygon innerRing in this._InteriorPolygons)
            {
                Polygon translated_inner = innerRing.Translate(offset);
                poly.AddInteriorRing(translated_inner);
            }

            return poly;
        }

        /// <summary>
        /// Area-weighted centroid of a closed ring. Translates to the mean first so large coordinates do not overflow.
        /// </summary>
        public static Vector2 CalculateCentroid(Vector2[] ExteriorRing, bool ValidateRing = true)
        {
            double accumulator_X = 0;
            double accumulator_Y = 0;

            //To prevent precision errors we subtract the average value and add it again
            ExteriorRing = [.. ExteriorRing.EnsureClosedRing()];
            Vector2 Average = ExteriorRing.Average();
            Vector2[] translated_Points = ExteriorRing.Translate(-Average);

            for (int i = 0; i < translated_Points.Length - 1; i++)
            {
                Vector2 p0 = translated_Points[i];
                Vector2 p1 = translated_Points[i + 1];
                double SharedTerm = ((p0.X * p1.Y) - (p1.X * p0.Y));
                accumulator_X += (p0.X + p1.X) * SharedTerm;
                accumulator_Y += (p0.Y + p1.Y) * SharedTerm;
            }

            double ExteriorArea = translated_Points.PolygonArea();
            double scalar = ExteriorArea * 6;

            return new Vector2((accumulator_X / scalar) + Average.X, (accumulator_Y / scalar) + Average.Y);
        }

        public double Distance(Vector2 p) => this.ExteriorSegments.Min(line => line.DistanceToPoint(p));

        public double Distance(Vector2 p, out LineSegment nearestLine)
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
        public double Distance(LineSegment p)
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
        public double Distance(Polygon other)
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
        public double DistanceFromCenterNormalized(Vector2 p)
        {
            Vector2 center = Centroid;
            Vector2 offset = p - center;
            double pointDist = offset.Magnitude;
            if (pointDist <= Tolerance.Epsilon)
                return 0;

            Line ray = new(center, offset);
            double nearestOutward = double.PositiveInfinity;
            foreach (LineSegment seg in _ExteriorSegments)
            {
                if (!ray.Intersects(seg, out Vector2 intersection))
                    continue;

                Vector2 fromCenter = intersection - center;
                if (Vector2.Dot(fromCenter, offset) <= 0)
                    continue;

                double centerDist = fromCenter.Magnitude;
                if (centerDist + Tolerance.Epsilon < pointDist)
                    continue;

                if (centerDist < nearestOutward)
                    nearestOutward = centerDist;
            }

            if (double.IsPositiveInfinity(nearestOutward) || nearestOutward <= Tolerance.Epsilon)
                return Contains(p) ? 0 : 1;

            return pointDist / nearestOutward;
        }

        public int[] VerticesOnConvexHull()
        {
            Vector2[] convex_hull_verts = this.ExteriorRing.ConvexHull(out int[] indicies);

            return indicies;
        }

        public object Clone()
        {
            Polygon clone = new(this.ExteriorRing.Clone() as Vector2[]);
            foreach (Polygon innerPoly in this.InteriorPolygons)
            {
                Polygon innerClone = innerPoly.Clone() as Polygon;
                clone.AddInteriorRing(innerClone);
            }

            return clone;
        }

        /// <summary>
        /// Round all coordinates in the clone of the Polygon to the nearest precision
        /// </summary>
        /// <param name="precision"></param>
        /// <returns></returns>
        public Polygon Round(int precision)
        {
            Vector2[] roundedPoints = [.. this.ExteriorRing.Select(e => e.Round(precision))];
            for (int i = roundedPoints.Length - 1; i > 0; i--)
            {
                if (roundedPoints[i] == roundedPoints[i - 1])
                    roundedPoints.RemoveAt(i);
            }

            Polygon clone = new(roundedPoints);
            foreach (Polygon innerPoly in this.InteriorPolygons)
            {
                Polygon innerClone = innerPoly.Round(precision);
                clone.AddInteriorRing(innerClone);
            }

            return clone;
        }

        public override string ToString()
        {
            if (this.HasInteriorRings)
            {
                return $"Poly with {this.TotalUniqueVertices} verts, {this.InteriorRings.Count} interior rings";
            }
            else
            {
                return $"Poly with {this.TotalUniqueVertices} verts";
            }
        }

        public bool Intersects(in IShape2D shape) => GetRelation(shape) != ShapeRelation.None;


        public bool Intersects(in ICircle2D c)
        {
            Circle circle = c.ToCircle();
            return this.Intersects(circle);
        }

        public bool Intersects(in Circle circle) => PolygonIntersectionExtensions.Intersects(this, circle);

        public bool Intersects(in Rectangle rect) => RectangleIntersectionExtensions.Intersects(rect, this);


        public bool Intersects(in ILineSegment2D l)
        {
            LineSegment line = l.ToLineSegment();
            return this.Intersects(line);
        }

        public bool Intersects(in LineSegment line) => PolygonIntersectionExtensions.Intersects(this, line);

        public bool Intersects(in ITriangle2D t)
        {
            Triangle tri = t.ToTriangle();
            return this.Intersects(tri);
        }

        public bool Intersects(in Triangle tri) => PolygonIntersectionExtensions.Intersects(this, tri);

        public bool Intersects(in IPolygon2D p)
        {
            Polygon poly = p.ToPolygon();
            return this.Intersects(poly);
        }

        /// <summary>
        /// Return true if the polygon contains or intersects the other polygon
        /// </summary>
        /// <param name="poly"></param>
        /// <param name="other"></param>
        /// <returns></returns>
        public bool Intersects(in Polygon other)
        {
            Rectangle? Intersection = this.BoundingBox.Intersection(other.BoundingBox);
            if (!Intersection.HasValue)
                return false;

            //Check the case of the other polygon entirely inside
            if (this.Covers(other.ExteriorRing[0])) //If it is entirely inside then all other verts must be inside so only check one
                return true;

            if (other.Covers(this.ExteriorRing[0]))
                return true;

            return SegmentsIntersect(this, other);
        }

        /// <summary>
        /// Return true if segments of the polygons intersect.  Returns false if the other triangle is entirely contained by poly
        /// </summary>
        /// <param name="poly"></param>
        /// <param name="other"></param>
        /// <returns></returns>
        public static bool SegmentsIntersect(in Polygon poly, in Polygon other)
        {
            Rectangle? Intersection = poly.BoundingBox.Intersection(other.BoundingBox);
            if (!Intersection.HasValue)
                return false;

            //Check the case for a line segment passing entirely through the polygon.
            Rectangle overlap = Intersection.Value;

            List<LineSegment> CandidateSegments = poly.GetIntersectingSegments(overlap);

            foreach (LineSegment candidate in CandidateSegments)
            {
                IEnumerable<LineSegment> OtherSegments = other.GetIntersectingSegments(candidate);
                if (OtherSegments.Any())
                    return true;
            }

            return false;
        }

        IShape2D IShape2D.Translate(in IPoint2D offset)
        {
            Vector2 v = offset.ToVector2();
            return this.Translate(v);
        }

        /// <summary>
        /// Add a vertex to our rings everywhere the other polygon intersects one of our segments
        /// </summary>
        /// <param name="other"></param>
        /// <returns>All intersection points, including pre-existing and added</returns>
        public List<Vector2> AddPointsAtIntersections(Polygon other)
        {
            List<Vector2> found_or_added_intersections = [];
            Rectangle? overlap = this.BoundingBox.Intersection(other.BoundingBox);

            //No work to do if there is no overlap
            if (!overlap.HasValue)
                return found_or_added_intersections;

            List<Vector2> newRing = [];

            PolygonVertexEnum vertEnumerator = new(this, reverse: true);

            UniquePointSet addedVertexQuad = new();

            //Enumerate in reverse so we do not break our index values as we insert
            //Handle an edge case where we insert at the end of the loop, but the .Next index wraps to zero which changes the index of every item in the loop.
            foreach (PolygonIndex originalpolyIndex in vertEnumerator)
            {
                Vector2[] IntersectionPoints = [];

                //This block identifies intersection points
                {
                    PolygonIndex polyIndex = originalpolyIndex.ReindexToSize(this);
                    LineSegment ls = polyIndex.Segment(this);

                    //LineSegment ls = new LineSegment(ExteriorRing[i], ExteriorRing[i + 1]);
                    //PointIndex polyIndex = new PointIndex(0, i, ExteriorRing.Length - 1);
                    //newRing.Add(ExteriorRing[i]);
                    //newRing.Add(this[polyIndex]);
                    var polyvertex = this[polyIndex];
                    if (!addedVertexQuad.TryAdd(polyvertex))
                        Debug.Assert(other.AllVertices.Contains(polyvertex));

                    //Since we want the out parameter just get a quick list of candidates with the ls.bounding box in instead of running the full intersection test twice.
                    var otherSegmentCandidates = other.GetIntersectingSegments(ls.BoundingBox);
                    List<LineSegment> candidates = ls.Intersections(otherSegmentCandidates, false,
                        out IntersectionPoints);
                    if (candidates.Count == 0)
                        continue;
                }

                //Reverse the intersection list so we are adding points furthest to nearest.  This prevents our polyIndex from pointing at the wrong index after adding a point when there are multiple intersections for a segment.
                IntersectionPoints = [.. ((IEnumerable<Vector2>)IntersectionPoints).Reverse()];

                //Remove any duplicates of the existing endpoints 
                for (int iInter = 0; iInter < IntersectionPoints.Length; iInter++)
                {
                    PolygonIndex polyIndex = originalpolyIndex.ReindexToSize(this);
                    Vector2 p = IntersectionPoints[iInter];

                    try
                    {
                        bool found =
                            addedVertexQuad.TryFindNearest(p, out var found_nearest, out double nearest_distance);
                        Debug.Assert(found);
                        if (nearest_distance >
                            Tolerance.Epsilon) //Our nearest vertex is too far away so we need to add a vertex to ourselves
                        {
                            double other_segment_distance =
                                other.NearestSegment(p, out PolygonIndex other_nearest_segment);

                            Debug.Assert(
                                other_nearest_segment.NumUniqueInRing ==
                                other_nearest_segment.Polygon(other).ExteriorRing.Length - 1,
                                "Index found with incorrect number of verticies in ring."); //An old bug I want to check for where the index ring size in RTree was not updating as points were added

                            //There is a horrible case where a very thin triangle can have two corresponding points that are < epsilon distance apart.  I 
                            //solved this by looking for the nearest segment on the other triangle, and then using that to check for an existing vertex
                            PolygonIndex other_vertex_index = other_nearest_segment;
                            double other_vertex_distance = Vector2.Distance(other[other_nearest_segment], p);

                            //double other_vertex_distance = other.NearestVertex(p, out PointIndex other_vertex_index);

                            //We need to cover an edge case here.  We insert at Index.Next.  For the last point in the ring this will return index 0.  That will change the indexing of the ring.  Instead we explicitly state we want a new point added at the end of the ring.
                            PolygonIndex InsertIndex = polyIndex.IsLastIndexInRing()
                                ? new PolygonIndex(0, polyIndex.InnerShapeIndex, polyIndex.VertexIndex + 1,
                                    polyIndex.NumUniqueInRing)
                                : polyIndex.Next;
                            PolygonIndex OtherInsertIndex = other_nearest_segment.IsLastIndexInRing()
                                ? new PolygonIndex(0, other_nearest_segment.InnerShapeIndex,
                                    other_nearest_segment.VertexIndex + 1, other_nearest_segment.NumUniqueInRing)
                                : other_nearest_segment.Next;

                            //If we intersect close enough to another vertex on the other polygon, just add that point to ourselves.
                            if (other_vertex_distance == 0)
                            {
                                //Vertex exists in the other polygon at exact position
                                //newRing.Add(p);
                                addedVertexQuad.Add(p);
                                //Add a vertex between the point we tested and the next
                                InsertVertex(p, InsertIndex);

                                Debug.Assert(false == found_or_added_intersections.Contains(p));
                                found_or_added_intersections.Add(p);
                            }
                            else if (other_vertex_distance < Tolerance.Epsilon)
                            {
                                //Use the position of the existing vertex in the other polygon for our own position
                                //newRing.Add(other_vertex_index.Point(other));
                                addedVertexQuad.Add(other_vertex_index.Point(other));
                                InsertVertex(other_vertex_index.Point(other), InsertIndex);

                                Debug.Assert(false ==
                                             found_or_added_intersections.Contains(other_vertex_index.Point(other)));
                                found_or_added_intersections.Add(other_vertex_index.Point(other));
                            }
                            else
                            {
                                //Intersection point is not a  vertex on either polygon
                                //double other_segment_distance = other.NearestSegment(p, out PointIndex other_nearest_segment);

                                //newRing.Add(p);
                                addedVertexQuad.Add(p);
                                InsertVertex(p, InsertIndex);
                                other.InsertVertex(p, OtherInsertIndex);

                                Debug.Assert(false == found_or_added_intersections.Contains(p));
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

                            double other_segment_distance =
                                other.NearestSegment(p, out PolygonIndex other_nearest_segment);
                            PolygonIndex other_vertex_index = other_nearest_segment;
                            double other_vertex_distance = Vector2.Distance(other[other_nearest_segment], p);

                            PolygonIndex OtherInsertIndex = other_nearest_segment.IsLastIndexInRing()
                                ? new PolygonIndex(0, other_nearest_segment.InnerShapeIndex,
                                    other_nearest_segment.VertexIndex + 1, other_nearest_segment.NumUniqueInRing)
                                : other_nearest_segment.Next;

                            //We need the point to be exact, so adjust our point accordingly
                            if (other_vertex_distance == 0)
                            {
                                //No action needed.  Vertex exists in the other polygon at exact position and in this polygon at exact position.
                                //We still report the intersection though

                                //Debug.Assert(found_or_added_intersections.Contains(other_vertex_index.Point(other)));
                                //found_or_added_intersections.Add(other_vertex_index.Point(other));
                                found_or_added_intersections.Add(other_vertex_index.Point(other));

                            }
                            else if (
                                other_vertex_distance <
                                Tolerance.Epsilon) //Use the position of the existing vertex in the other polygon for our own position
                            {
                                //Q: Shouldn't we check if we intersect with the near or far endpoint of our segment before nudging?
                                //A: No, because we enumerate in reverse order, so the far endpoint would be tested previously... unless it is the first last vertex in the loop...
                                Vector2 other_vert_pos = other_vertex_index.Point(other);
                                if (Vector2.Distance(polyIndex.Segment(this).B, other_vert_pos) < Tolerance.Epsilon)
                                {
                                    //This should be a very rare case
                                    this.SetVertex(polyIndex.Next, other_vertex_index.Point(other));
                                }
                                else
                                {
                                    this.SetVertex(polyIndex, other_vertex_index.Point(other));
                                }

                                //newRing[existingIndex] = other_vert_pos;
                                //Remove, then add to eliminate issues with verts being an epsilon equivalent point but being assigned to different quadrants with strict comparison tests
                                bool removed = addedVertexQuad.TryRemove(p, out Vector2 _);
                                addedVertexQuad.Add(other_vert_pos);

                                //Update the position reported in our list since we nudged ourselves
                                //and the vertex was inserted originally using our position
                                found_or_added_intersections.Remove(p);
                                //Debug.Assert(false == found_or_added_intersections.Contains(other_vert_pos));
                                found_or_added_intersections.Add(other_vert_pos);
                            }
                            else
                            {
                                //We have the vertex, but it is not in the other polygon.  Add the vertex to the other polygon
                                //double other_segment_distance = other.NearestSegment(p, out PointIndex other_nearest_segment);

                                //other.AddVertex(p);
                                other.InsertVertex(p, OtherInsertIndex);

                                Debug.Assert(false == found_or_added_intersections.Contains(p));
                                found_or_added_intersections.Add(p);
                            }
                        }
                    }
                    catch (ArgumentException e)
                    {
                        Trace.WriteLine($"{this} could not add corresponding point {polyIndex} : {p} ");
                        continue;
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
        public void AddPointsAtIntersections(in LineSegment other)
        {
            Rectangle? overlap = this.BoundingBox.Intersection(other.BoundingBox);

            //No work to do if there is no overlap
            if (!overlap.HasValue)
                return;

            List<Vector2> newRing = new(ExteriorRing.Length);

            for (int i = 0; i < ExteriorRing.Length - 1; i++)
            {
                LineSegment ls = new(ExteriorRing[i], ExteriorRing[i + 1]);

                newRing.Add(ExteriorRing[i]);


                var intersects = ls.Intersects(other, true, out IShape2D intersection); //Don't check the endpoints of the segment because we are already adding them

                if (intersects)
                {
                    //The intersection could be a line, which we can't really add an infinite number of points for... we could add internal endpoints, but for now we add point intersections only.
                    if (intersection is IPoint2D point)
                    {
                        Vector2 p = new(point.X, point.Y);
                        System.Diagnostics.Debug.Assert(!newRing.Contains(p));
                        newRing.Add(p);
                    }
                }
            }

            newRing.Add(ExteriorRing[ExteriorRing.Length - 1]);

            //Ensure we are not accidentally adding duplicate points, other than to close the ring
            System.Diagnostics.Debug.Assert(newRing.Count == newRing.Distinct().Count() + 1);

            this.ExteriorRing = [.. newRing];

            foreach (Polygon innerPolygon in this._InteriorPolygons)
            {
                innerPolygon.AddPointsAtIntersections(other);
            }

            this._SegmentRTree = null; //Reset our RTree since yanking a polygon and changing the indicies are a pain
        }


        /// <summary>
        /// Add a vertex to our rings everywhere the other polygon intersects one of the passed segments
        /// </summary>
        /// <param name="other"></param>
        public void AddPointsAtIntersections(in LineSegment[] input)
        {
            //Only check the lines that could intersect our polygon
            var other = input.Where(o => this.BoundingBox.Intersects(o.BoundingBox)).ToArray();

            List<Vector2> newRing = [];

            for (int i = 0; i < ExteriorRing.Length - 1; i++)
            {
                LineSegment ls = new(ExteriorRing[i], ExteriorRing[i + 1]);

                //Don't add the point if it is too close
                if (newRing.Count == 0 || Vector2.DistanceSquared(newRing.Last(), ExteriorRing[i]) > Tolerance.EpsilonSquared)
                    newRing.Add(ExteriorRing[i]);

                List<LineSegment> candidates = ls.Intersections(other, out Vector2[] IntersectionPoints);

                //Remove any duplicates of the existing endpoints 
                foreach (Vector2 p in IntersectionPoints)
                {
                    System.Diagnostics.Debug.Assert(!newRing.Contains(p));
                    //Don't add the point if it is too close
                    if (newRing.Count == 0 || Vector2.DistanceSquared(newRing.Last(), p) > Tolerance.EpsilonSquared)
                        newRing.Add(p);
                }
            }

            if (newRing.Count == 0 || Vector2.DistanceSquared(newRing.Last(), ExteriorRing[ExteriorRing.Length - 1]) > Tolerance.EpsilonSquared)
                newRing.Add(ExteriorRing[ExteriorRing.Length - 1]);

            //Ensure we are not accidentally adding duplicate points, other than to close the ring
            System.Diagnostics.Debug.Assert(newRing.Count == newRing.Distinct().Count() + 1);

            this.ExteriorRing = [.. newRing];

            foreach (Polygon innerPolygon in this._InteriorPolygons)
            {
                innerPolygon.AddPointsAtIntersections(other);
            }

            this._SegmentRTree = null; //Reset our RTree since yanking a polygon and changing the indicies are a pain
        }

        public static Polygon WalkPolygonCut(Polygon input, RotationDirection direction, IList<Vector2> cutLine) => WalkPolygonCut(input, direction, cutLine, out PolygonIndex FirstIntersection, out PolygonIndex LastIntersection, out List<Vector2> intersecting_cutline_verts);


        /// <summary>
        /// Given a polyline, find two locations where it intersects the polygon and walk the polygon in either clockwise/counter-clockwise direction from the first intersection of the cutline to the second, add the cutline to close the ring, and return the resulting polygon.
        /// </summary>
        /// <param name="start_index"></param>
        /// <param name="input">The polygon to cut/extend</param>
        /// <param name="direction">The direction we will walk to connect the starting and ending cut points</param>
        /// <param name="cutLine">The line cutting the polygon.  It should intersect the same polygonal ring in two locations without intersecting any others</param>
        /// <param name="FirstIntersect">The polygon vertex before the intersected segment, use intersect_index.next to get the endpoint of the intersected segment of the polygon</param>
        /// <returns></returns>
        public static Polygon WalkPolygonCut(Polygon input, RotationDirection direction, IList<Vector2> cutLine, out PolygonIndex FirstIntersection, out PolygonIndex LastIntersection, out List<Vector2> intersecting_cutline_verts)
        {

            //Find a possible intersection point for the retrace
            LineSegment[] cutLines = cutLine.ToLineSegments();
            intersecting_cutline_verts = []; //Every vert in the path that crosses the two polygon
            List<PolygonIndex> IntersectingPointIndices = [];
            bool FirstCutIntersectionFound = false;

            //Add the intersection points to the polygon
            Polygon output = input.Clone() as Polygon;
            output.AddPointsAtIntersections(cutLines);

            //Identify where the cut crosses the polygon rings 
            for (int iVert = 0; iVert < cutLine.Count - 1; iVert++)
            {
                LineSegment segment = new(cutLine[iVert], cutLine[iVert + 1]);

                var intersections = output.IntersectingSegmentIndices(segment);

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

                IntersectingPointIndices.AddRange(intersections.Values);

                if (IntersectingPointIndices.Count >= 2)
                {
                    //intersecting_cutline_verts.Add(cutLine[iVert + 1]);
                    break;
                }
            }

            if (IntersectingPointIndices.Count == 0)
            {
                throw new ArgumentException("cutLine must intersect a polygon ring");
            }
            else if (IntersectingPointIndices.Count == 1)
            {
                FirstIntersection = IntersectingPointIndices[0];
                throw new ArgumentException("cutline must intersect a polygon ring a second time.");
            }

            //Identify the first vertex of the segment of the polygon that intersects the cut line
            FirstIntersection = IntersectingPointIndices[IntersectingPointIndices.Count - 2];
            LastIntersection = IntersectingPointIndices[IntersectingPointIndices.Count - 1];

            if (false == FirstIntersection.AreOnSameRing(LastIntersection))
            {
                throw new ArgumentException("Cut line must cross segments on the same ring of the polygon");
            }

            if (FirstIntersection == LastIntersection)
            {
                throw new ArgumentException(
                    $"Start and End index must be different to cut polygon. Both are {FirstIntersection}");
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
        public static Polygon WalkPolygonCut(PolygonIndex start_index, PolygonIndex end_index, Polygon originPolygon, RotationDirection direction, IList<Vector2> cutLine)
        {
            if (false == end_index.AreOnSameRing(start_index))
            {
                throw new ArgumentException("Cut must run between the same ring of the polygon without intersecting other rings");
            }

            if (start_index == end_index)
            {
                throw new ArgumentException(
                    $"Start and End index must be different to cut polygon. Both are {start_index}");
            }

            //Walk the ring using Next to find perimeter on one side, the walk using prev to find perimeter on the other
            List<Vector2> walkedPoints = [];
            PolygonIndex current = start_index;

            //Add the points from the polygon
            do
            {
                Debug.Assert(walkedPoints.Contains(current.Point(originPolygon)) == false);
                walkedPoints.Add(current.Point(originPolygon));
                current = direction == RotationDirection.Counterclockwise ? current.Next : current.Previous;

            }
            while (current != end_index);

            walkedPoints.Add(end_index.Point(originPolygon));

            //Add the intersection point of where we crossed the boundary 
            //List<Vector2> SimplifiedPath = CurveSimplificationExtensions.DouglasPeuckerReduction(cutLine, Global.PenSimplifyThreshold);
            //Since we start walking the polygon from the first intersection point we always add the cutline in reverse order to return to the cirst intersection point.
            List<Vector2> SimplifiedPath = [.. cutLine.Reverse()];

            //The intersection point marks where we enter the polygon.  The first point in the path is not added because it indicates where the line exited the cut region. 
            //Add the PenInput.Path 

            //Temp for debugging ///////////////
            for (int iCut = 0; iCut < SimplifiedPath.Count; iCut++)
            {
                Debug.Assert(walkedPoints.Contains(SimplifiedPath[iCut]) == false);
                if (Vector2.DistanceSquared(SimplifiedPath[iCut], walkedPoints.Last()) <= Tolerance.EpsilonSquared)
                {
                    //int i = 5; //Temp for debugging
                    continue;
                }

                walkedPoints.Add(SimplifiedPath[iCut]);
            }
            /////////////////////////////////////
            //
            //walkedPoints.AddRange(cutLine);
#if DEBUG
            //Ensure we do not have duplicates in our list
            Vector2[] walkedPoints_noduplicates = walkedPoints.RemoveDuplicates();
            Debug.Assert(walkedPoints_noduplicates.Length == walkedPoints.Count);
#endif

            //Close the ring
            walkedPoints.Add(start_index.Point(originPolygon));

            /*
            Debug.Assert(walkedPoints.ToArray().AreClockwise() == (direction == RotationDirection.Clockwise));
            
            if(direction == RotationDirection.Clockwise)
            {
                walkedPoints.Reverse();
            }
             */
            Polygon output = new(walkedPoints.EnsureClosedRing());

            //Add any interior polygons contained within our cut
            for (int iRing = 0; iRing < originPolygon.InteriorRings.Count; iRing++)
            {
                //We should be safe quickly testing a single point of each interior polygon because we test that the cut intersects the same ring only
                if (output.Covers(originPolygon.InteriorRings[iRing].First()))
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
            if (other is null)
                return false;

            if (object.ReferenceEquals(this, other))
                return true;

            if (other.ShapeType != this.ShapeType)
                return false;

            if (other is IPolygon2D otherPoly)
                return this.Equals(otherPoly);

            return false;
        }

        public bool Equals(IPolygon2D other)
        {
            if (other is null)
                return false;

            if (object.ReferenceEquals(this, other))
                return true;

            if (this.ExteriorRing.Length != other.ExteriorRing.Count)
                return false;

            if (this.TotalUniqueVertices != other.TotalUniqueVertices)
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

        public bool Equals(Polygon other)
        {
            if (other is null)
                return false;

            if (this.ExteriorRing.Length != other.ExteriorRing.Length)
                return false;

            if (this.TotalUniqueVertices != other.TotalUniqueVertices)
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

        public override bool Equals(object obj)
        {
            if (obj is Polygon poly)
                return Equals(poly);

            if (obj is IPolygon2D ipoly)
                return Equals(ipoly);

            if (obj is IShape2D shape)
                return Equals(shape);

            return false;
        }

        public override int GetHashCode()
        {
            throw new InvalidOperationException(
                "Cannot use hash codes for shapes that can change/have epsilon based comparisons.  See Vector2.GetHashCode");
        }
    }
}
