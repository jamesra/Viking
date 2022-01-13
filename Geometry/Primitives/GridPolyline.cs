using Geometry.JSON;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
 
namespace Geometry
{
    /// <summary>
    /// A set of lines where the endpoint of each line in the set is the starting point of the next
    /// </summary>
    public class GridPolyline : IPolyLine2D, IEquatable<GridPolyline>, IEquatable<IPolyLine2D>, IEquatable<ILineSegment2D>
    {
        protected List<IPoint2D> _Points;

        public readonly bool AllowsSelfIntersection = false;

        private GridLineSegment? KnownSelfIntersection;
         
        public bool HasSelfIntersection
        {
            get
            {
                if (AllowsSelfIntersection == false)
                    return false;

                if (KnownSelfIntersection.HasValue)
                    return true;

                return false;
            }
        }

        private RTree.RTree<GridLineSegment> rTree = null;

        public int PointCount => _Points.Count;

        public int NumUniqueVerticies => _Points.Count;

        public int LineCount => LineSegments.Count;

        public GridPolyline(bool AllowSelfIntersection = false)
        {
            this.AllowsSelfIntersection = AllowSelfIntersection;
            _Points = new List<Geometry.IPoint2D>();
        }

        public GridPolyline(int capacity, bool AllowSelfIntersection = false) : this(AllowSelfIntersection)
        {
            _Points = new List<Geometry.IPoint2D>(capacity);
        }

        public GridPolyline(IEnumerable<IPoint2D> points, bool AllowSelfIntersection = false)
        {
            this.AllowsSelfIntersection = AllowSelfIntersection;

            _Points = new List<IPoint2D>(points.Count());

            foreach (var p in points)
            {
                this.Add(p);
            }
        }

        public GridPolyline(IEnumerable<GridVector2> points, bool AllowSelfIntersection = false)
        {
            this.AllowsSelfIntersection = AllowSelfIntersection;

            _Points = points.Cast<IPoint2D>().ToList();
        }
        
        public static explicit operator IPoint2D[](GridPolyline src)
        {
            return src._Points.ToArray();
        }

        public static explicit operator List<IPoint2D>(GridPolyline src)
        {
            return src._Points.ToList();
        }

        public static explicit operator GridVector2[](GridPolyline src)
        {
            return src._Points.Select(p => new GridVector2(p)).ToArray();
        }

        public static explicit operator List<GridVector2>(GridPolyline src)
        {
            return src._Points.Select(p => new GridVector2(p)).ToList();
        }

        public GridVector2 this[PolylineIndex index] => _Points[index.iVertex].ToGridVector2();
         
        /// <summary>
        /// Returns true if the point can be added without violating self-intersection restrictions
        /// </summary>
        /// <param name="next"></param>
        /// <returns></returns>
        public bool CanAdd(in IPoint2D next)
        {
            if (_Points.Count == 0)
                return true;

            if (AllowsSelfIntersection)
                return true;

            if (_Points.Contains(next))
                return false;

            GridLineSegment line = new GridLineSegment(_Points.Last(), next);

            if (_Points.Count == 1)
                return true;

            //var Existing = this.LineSegments;
            List<GridLineSegment> intersectionCandidates = rTree.Intersects(line.BoundingBox);
            if (line.SelfIntersects(this.LineSegments.Where(l => intersectionCandidates.Contains(l)).ToList(), LineSetOrdering.POLYLINE))
            {
                return false;
            }

            return true;
        }

        public void Add(IPoint2D next)
        {
            if (rTree == null)
                rTree = new RTree.RTree<GridLineSegment>();

            if (_Points.Count == 0)
            {
                _Points.Add(next);
                return;
            }

            //Figure out why we can't add and throw an exception
            if (_Points.Contains(next) && AllowsSelfIntersection == false)
                throw new ArgumentException("Point already in Polyline that does not allow self-intersection");

            if (_Points.Last().Equals(next))
                throw new ArgumentException("Inserting duplicate point into polyline adjacent to the duplicate.");

            GridLineSegment line = new GridLineSegment(_Points.Last(), next);

            if (_Points.Count == 1)
            {
                _Points.Add(next);
                rTree.Add(line.BoundingBox, line);
            }
            else if (AllowsSelfIntersection == false || AllowsSelfIntersection && KnownSelfIntersection.HasValue == false)
            {
                List<GridLineSegment> intersectionCandidates = rTree.Intersects(line.BoundingBox);

                if (line.SelfIntersects(this.LineSegments.Where(l => intersectionCandidates.Contains(l)).ToList(), LineSetOrdering.POLYLINE, out GridLineSegment? intersected))
                {
                    if (AllowsSelfIntersection == false)
                        throw new ArgumentException("Added point created self-intersecting line in Polyline");
                    else
                        this.KnownSelfIntersection = intersected;
                }
            }
            
            _Points.Add(next);
            var Existing = this.LineSegments;
            Existing.Add(line);
            rTree.Add(line.BoundingBox, line);
            this._LineSegments = Existing; 
        }

        public void Insert(int index, IPoint2D value)
        {
            if (rTree == null)
                rTree = new RTree.RTree<GridLineSegment>();

            if (index < 0 || index > _Points.Count)
                throw new IndexOutOfRangeException($"{nameof(GridPolyline)}.{nameof(Insert)}: {index} out of bounds");

            /////////////////////////////////////////////////
            //Simple cases where intersection is not a factor
            ///////////////////////////////////////////////// 

            //Case for adding to the beginning of the polyline
            if (_Points.Count == 0)
            {
                Add(value);
                return;
            }
            else if(_Points.Count == 1)
            {
                if (_Points[0].Equals(value))
                    throw new ArgumentException("Inserting point already in Polyline identical to an adjacent point");

                _Points.Insert(index, value);
                GridLineSegment line = new GridLineSegment(_Points[0], _Points[1]);
                rTree.Add(line.BoundingBox, line);
                return;
            }

            //Case for appending to the end of the polyline
            if (_Points.Count == index)
            {
                if (_Points[index - 1].Equals(value))
                    throw new ArgumentException("Inserting duplicate point into polyline adjacent to the duplicate.");

                Add(value);
                return;
            }

            /////////////////////////////////////////////////
            //End simple cases
            /////////////////////////////////////////////////

            //Position the point will be inserted into
            PolylineIndex insert_index = new PolylineIndex(index, this.NumUniqueVerticies);

            //Check for adjacent duplicate points
            bool duplicate_point = _Points.Contains(value);
            if (duplicate_point)
            {
                if (AllowsSelfIntersection == false)
                    throw new ArgumentException("Inserting point already in Polyline that does not allow self-intersection");
                else
                {
                    //Ensure the adjacent points are not duplicates... perhaps this should be a no-op, but for now throw an exception
                    if(this[insert_index] == value)
                        throw new ArgumentException("Inserting duplicate point into polyline adjacent to the duplicate.");

                    if (false == insert_index.IsFirstIndex)
                    {
                        if (this[insert_index.Previous.Value] == value)
                            throw new ArgumentException("Inserting duplicate point into polyline adjacent to the duplicate.");
                    }
                }
            }

            //Copy the existing line segments so we can test new segments against the existing ones minus the replaced segment
            var segments = this.LineSegments.ToList();
            List<GridLineSegment> new_segments = new List<GridLineSegment>();
            List<GridLineSegment> removed_segments = new List<GridLineSegment>();

            Debug.Assert(_Points[index].Equals(value) == false, "Seems a bit odd to be inserting a point with the same value into the polyline, creating a duplicate");

            //Remove the segments that will be replaced by the new vertex from our test set

            if (insert_index.IsFirstIndex)
            {
                //No segments to remove, we are inserting at either end of the polyline
            } 
            else
            {
                removed_segments.Add(segments[index - 1]);
                segments.RemoveAt(index - 1);
            }

            //Create the new segments using the new vertex
            
            if (insert_index.IsFirstIndex)
            {
                new_segments.Add(new GridLineSegment(value, _Points[index]));
            }
            else
            {
                new_segments.Add(new GridLineSegment(_Points[index - 1], value));
                new_segments.Add(new GridLineSegment(value, _Points[index]));
            } 

            
            if (AllowsSelfIntersection == false || AllowsSelfIntersection && KnownSelfIntersection.HasValue == false)
            {
                foreach (var new_seg in new_segments)
                {
                    List<GridLineSegment> intersectionCandidates = rTree.Intersects(new_seg.BoundingBox).Where(l => removed_segments.Contains(l) == false).ToList();

                    if (new_seg.SelfIntersects(this.LineSegments.Where(l => intersectionCandidates.Contains(l)).ToList(), LineSetOrdering.POLYLINE, out GridLineSegment? intersected))
                    {
                        if (AllowsSelfIntersection == false)
                        {
                            throw new ArgumentException("Added point created self-intersecting line in Polyline");
                        }
                        else
                        {
                            this.KnownSelfIntersection = intersected;
                            break;
                        }
                    }
                }
            }

            //Looks like we passed self-intersection tests.  Update the segments, rtree, and return
            _Points.Insert(index, value);

            if (insert_index.IsFirstIndex)
            {
                segments.InsertRange(0, new_segments);
            }
            else
            {
                segments.InsertRange(index-1, new_segments);
            }

            this._LineSegments = segments;

            foreach (var removed_segment in removed_segments)
            {
                rTree.Delete(removed_segment, out var removed_item);
            }

            foreach (var added_segment in new_segments)
            {
                rTree.Add(added_segment.BoundingBox, added_segment);
            }
        }

        public List<GridVector2> AddPointsAtIntersections(GridPolyline other)
        {
            var candidates = other.rTree.Intersects(this.BoundingBox);

            List<GridVector2> found_or_added_intersections = new List<GridVector2>();

            List<GridVector2> newPolyline = new List<GridVector2>(_Points.Count);

            var otherLineSegments = other.LineSegments.ToArray();

            foreach(var other_ls in candidates)
            {
                found_or_added_intersections.AddRange(this.AddPointsAtIntersections(other_ls));
            }

            return found_or_added_intersections;
        }

        public List<GridVector2> AddPointsAtIntersections(GridLineSegment other)
        { 
            GridRectangle? overlap = this.BoundingBox.Intersection(other.BoundingBox);
            if (!overlap.HasValue)
                return new List<GridVector2>();

            List<GridVector2> found_or_added_intersections = new List<GridVector2>();   
            var LineSegmentsCopy = this.LineSegments.ToArray();

            for(int i = LineSegmentsCopy.Length-1; i >= 0; i--) //Go in reverse order so we do not change the index we are inserting into
            {
                GridLineSegment ls = LineSegments[i];

                var intersects = ls.Intersects(other, true, out var intersection);
                if (intersects)
                {
                    if (intersection is IPoint2D point)
                    {
                        GridVector2 p = point.ToGridVector2();
                        found_or_added_intersections.Insert(0, p);
                        System.Diagnostics.Debug.Assert(false == _Points.Contains(point));
                        this.Insert(i + 1, point);
                    }
                }
            }

            return found_or_added_intersections;
        }

        /// <summary>
        /// Return true if the point is one of the polygon verticies
        /// </summary>
        /// <param name="point"></param>
        /// <returns></returns>
        public List<PolylineIndex> TryGetIndicies(ICollection<GridVector2> points)
        {
            List<PolylineIndex> found = new List<PolylineIndex>(points.Count);
            var candidates = points.Where(p => BoundingBox.Contains(p));
            List<GridVector2> notExterior = new List<GridVector2>(points.Count);

            foreach (GridVector2 point in points)
            {
                int iVert = this._Points.IndexOf(point);
                if (iVert < 0)
                    continue;

                found.Add(new PolylineIndex(iVert, this.PointCount));
            }

            return found;
        }

        public double Area => throw new ArgumentException("No area for Polyline");

        public double Length => LineSegments.Sum(l => l.Length);

        public GridRectangle BoundingBox
        {
            get
            {
                var MinX = _Points.Min(p => p.X);
                var MaxX = _Points.Max(p => p.X);
                var MinY = _Points.Min(p => p.Y);
                var MaxY = _Points.Max(p => p.Y);

                return new GridRectangle(MinX, MaxX, MinY, MaxY);
            }
        }

        public ShapeType2D ShapeType => ShapeType2D.POLYLINE;

        private List<GridLineSegment> _LineSegments;

        public List<GridLineSegment> LineSegments
        {
            get
            {
                if (_LineSegments != null && _LineSegments.Count == _Points.Count - 1)
                {
                    return _LineSegments.ToList();
                }

                _LineSegments = new List<GridLineSegment>(this._Points.Count);

                for (int i = 0; i < _Points.Count - 1; i++)
                {
                    _LineSegments.Add(new GridLineSegment(_Points[i], _Points[i + 1]));
                }

                return _LineSegments.ToList();
            }
        }

        IReadOnlyList<ILineSegment2D> IPolyLine2D.LineSegments => _LineSegments.Cast<ILineSegment2D>().ToList();


        public IReadOnlyList<IPoint2D> Points => this._Points;

        public bool Contains(in IPoint2D p)
        {
            IPoint2D pnt = p;
            return this.LineSegments.Any(line => line.Contains(in pnt));
        }

        public bool Intersects(in IShape2D shape)
        {
            IShape2D shp = shape;
            return this.LineSegments.Any(line => line.Intersects(shp));
        }

        IShape2D IShape2D.Translate(in IPoint2D offset) => this.Translate(offset);

        public GridPolyline Translate(IPoint2D offset)
        {  
            var translatedPoints = this._Points.Select(p => new GridVector2(p.X + offset.X, p.Y + offset.Y)); 
            return new GridPolyline(translatedPoints);
        }

        /// <summary>
        /// Round all coordinates in the clone of the GridPolygon to the nearest precision
        /// </summary>
        /// <param name="precision"></param>
        /// <returns></returns>
        public GridPolyline Round(int precision)
        {
            GridVector2[] roundedPoints = this.Points.Select(e => e.Round(precision)).ToArray();
            for (int i = roundedPoints.Length - 1; i > 0; i--)
            {
                if (roundedPoints[i] == roundedPoints[i - 1])
                    roundedPoints.RemoveAt(i);
            }

            GridPolyline clone = new Geometry.GridPolyline(roundedPoints); 
            return clone;
        }

        public override string ToString()
        {
            return string.Format("PolyLine: {0}", _Points.ToJSON());
        }

        public GridPolyline Clone()
        {
            return new GridPolyline(this.Points.ToArray(), this.AllowsSelfIntersection);
        }

        public GridPolyline Smooth(uint NumInterpolations)
        {
            return this.CalculateCurvePoints(NumInterpolations);
        }

        public override int GetHashCode()
        {
            return 0; //Use a constant since the polyline can change
        }

        public override bool Equals(object obj)
        {
            if (obj is GridPolyline other)
                return Equals(other);

            if (obj is IShape2D otherShape)
                return Equals(otherShape);

            return base.Equals(obj);
        }

        public bool Equals(GridPolyline other)
        {
            if (object.ReferenceEquals(this, other))
                return true;

            if (other is null)
                return false;

            if (this.PointCount != other.PointCount)
                return false;

            for (int i = 0; i < this.PointCount; i++)
            {
                if (false == this._Points[i].Equals(other._Points[i]))
                    return false;
            }

            return true;
        }

        public bool Equals(IShape2D other)
        {
            if (other is IPolyLine2D otherPolyline)
                return Equals(otherPolyline);
            if (other is ILineSegment2D otherLine)
                return Equals(otherLine);
            
            return false; 
        }

        public bool Equals(ILineSegment2D other)
        {
            if (this.PointCount != 2)
                return false;

            return (Points[0].Equals(other.A) && Points[1].Equals(other.B)) ||
                   (Points[1].Equals(other.A) && Points[0].Equals(other.B));
        }

        public bool Equals(IPolyLine2D other)
        {
            if (this.PointCount != other.Points.Count)
                return false;

            for (int i = 0; i < this.PointCount; i++)
            {
                if (false == this._Points[i].Equals(other.Points[i]))
                    return false;
            }

            return true;
        }
    }
}
