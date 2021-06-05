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
    public class GridPolyline : IPolyLine2D, IEquatable<GridPolyline>
    {
        protected List<IPoint2D> _Points;

        public readonly bool AllowsSelfIntersection = false;

        private GridLineSegment? KnownSelfIntersection;

        public bool Closed => _Points.Count < 3 ? false
                                    : _Points.First() == _Points.Last();

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

        public int PointCount { get { return _Points.Count; } }

        public int NumUniqueVerticies { get
            {
                return _Points.Count - (this.Closed ? 1 : 0);
            }
        }

        public int LineCount { get { return LineSegments.Count; } }

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

        /*
        public static implicit operator IPoint2D[](GridPolyline src)
        {
            return src._Points.ToArray();
        }

        public static implicit operator List<IPoint2D>(GridPolyline src)
        {
            return src._Points.ToList();
        }

        public static implicit operator GridVector2[](GridPolyline src)
        {
            return src._Points.Select(p => new GridVector2(p)).ToArray();
        }

        public static implicit operator List<GridVector2>(GridPolyline src)
        {
            return src._Points.Select(p => new GridVector2(p)).ToList();
        }
        */

        public GridVector2 this[PolylineIndex index]
        {
            get
            {
                return _Points[index.iVertex].ToGridVector2();
            }
        }

        public IPoint2D this[PolylineIndex index]
        {
            get
            {
                return _Points[index.iVertex];
            }
        }

        /// <summary>
        /// Returns true if the point can be added without violating self-intersection restrictions
        /// </summary>
        /// <param name="next"></param>
        /// <returns></returns>
        public bool CanAdd(IPoint2D next)
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
                _Points.Add(value);
                return;
            }
            else if(_Points.Count == 1)
            {
                if (_Points[0] == value)
                    throw new ArgumentException("Inserting point already in Polyline identical to an adjacent point");

                _Points.Insert(index, value);
                GridLineSegment line = new GridLineSegment(_Points[0], _Points[1]);
                rTree.Add(line.BoundingBox, line);
            }

            //Case for appending to the end of the polyline
            if (_Points.Count == index && Closed == false)
            {
                _Points.Add(value);
                GridLineSegment line = new GridLineSegment(_Points[_Points.Count - 2], _Points[_Points.Count -1]);
                rTree.Add(line.BoundingBox, line);
                return;
            }

            /////////////////////////////////////////////////
            //End simple cases
            /////////////////////////////////////////////////

            //Position the point will be inserted into
            PolylineIndex insert_index = new PolylineIndex(index, this.NumUniqueVerticies, this.Closed);

            //Check for adjacent duplicate points
            bool duplicate_point = _Points.Contains(value);
            if (duplicate_point)
            {
                if (AllowsSelfIntersection == false)
                    throw new ArgumentException("Inserting point already in Polyline that does not allow self-intersection");
                else
                {
                    //Ensure the adjacent points are not duplicates... perhaps this should be a no-op, but for now throw an exception
                    if(_Points[index] == value)
                        throw new ArgumentException("Inserting duplicate point into polyline adjacent to the duplicate.");

                    if (false == insert_index.IsFirstIndex)
                    {
                        if (_Points[index-1] == value)
                            throw new ArgumentException("Inserting duplicate point into polyline adjacent to the duplicate.");
                    }
                }
            }

            //Copy the existing line segments so we can test new segments against the existing ones minus the replaced segment
            var segments = this.LineSegments.ToList();
            List<GridLineSegment> new_segments = new List<GridLineSegment>();
            List<GridLineSegment> removed_segments = new List<GridLineSegment>();

            Debug.Assert(segments[index] != value, "Seems a bit odd to be inserting a point with the same value into the polyline, creating a duplicate");

            //Remove the segments that will be replaced by the new vertex from our test set

            if (this.Closed)
            {
                if (insert_index.IsFirstIndex || insert_index.IsLastIndex)
                {
                    removed_segments.Add(segments[segments.Count - 1]);

                    segments.RemoveAt(0);
                    segments.RemoveAt(segments.Count-1);
                }
                else
                {
                    removed_segments.Add(segments[insert_index.iVertex]);
                    removed_segments.Add(segments[insert_index.Previous.iVertex]);

                    segments.RemoveAt(insert_index.iVertex);
                    segments.RemoveAt(insert_index.Previous.iVertex);
                }
            }
            else
            {
                if (insert_index.IsFirstIndex)
                {
                    removed_segments.Add(segments[0]);
                    segments.RemoveAt(0);
                }
                else if (insert_index.IsLastIndex)
                {
                    removed_segments.RemoveAt(segments[segments.Count - 1]);
                    segments.RemoveAt(segments.Count - 1)
                }
                else
                {
                    removed_segments.Add(segments[insert_index.iVertex]);
                    removed_segments.Add(segments[insert_index.Previous.iVertex]);

                    segments.RemoveAt(insert_index.iVertex);
                    segments.RemoveAt(insert_index.Previous.iVertex);
                }
            }

            foreach (var removed_segment in removed_segments)
            {
                rTree.Delete(removed_segment, out var removed_item);
            }

            //Create the new segments using the new vertex
            if (this.Closed)
            {   
                new_segments.Add(new GridLineSegment(value, this[index]));
                new_segments.Add(new GridLineSegment(this[index.Previous], value);   
            }
            else
            {
                if (insert_index.IsFirstIndex)
                {
                    new_segments.Add(new GridLineSegment(value, this[index]));
                }
                else
                {
                    new_segments.Add(new GridLineSegment(value, this[index]));
                    new_segments.Add(new GridLineSegment(this[index.Previous], value);
                }
            }

            foreach (var added_segment in new_segments)
            {
                rTree.Add(added_segment.BoundingBox, added_segment);
            }
            
            if (AllowsSelfIntersection == false)
            {
                List<GridLineSegment> intersectionCandidates = rTree.Intersects(line.BoundingBox).Where(l => removed_segments.Contains(l) == false).ToList();

                if (AllowsSelfIntersection == false || AllowsSelfIntersection && KnownSelfIntersection.HasValue == false)
                {
                    if (line.SelfIntersects(segments.Where(l => intersectionCandidates.Contains(l)).ToList(), LineSetOrdering.POLYLINE, out GridLineSegment? intersected))
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
        }

        public double Area
        {
            get
            {
                throw new ArgumentException("No area for Polyline");
            }
        }

        public GridRectangle BoundingBox
        {
            get
            {
                double MinX = _Points.Min(p => p.X);
                double MaxX = _Points.Max(p => p.X);
                double MinY = _Points.Min(p => p.Y);
                double MaxY = _Points.Max(p => p.Y);

                return new GridRectangle(MinX, MaxX, MinY, MaxY);
            }
        }

        public ShapeType2D ShapeType
        {
            get
            {
                return ShapeType2D.POLYLINE;
            }
        }

        private List<GridLineSegment> _LineSegments;

        public List<GridLineSegment> LineSegments
        {
            get
            {
                if (_LineSegments == null || _LineSegments.Count != _Points.Count - 1)
                {
                    _LineSegments = new List<GridLineSegment>(this._Points.Count);

                    for (int i = 0; i < _Points.Count - 1; i++)
                    {
                        _LineSegments.Add(new GridLineSegment(_Points[i], _Points[i + 1]));
                    }
                }

                return _LineSegments.ToList();
            }
        }

        IReadOnlyList<ILineSegment2D> IPolyLine2D.LineSegments
        {
            get
            {
                return _LineSegments.Cast<ILineSegment2D>().ToList();
            }
        }


        public IReadOnlyList<IPoint2D> Points
        {
            get
            {
                return this._Points;
            }
        }

        public bool Contains(IPoint2D p)
        {
            return this.LineSegments.Any(line => line.Contains(p));
        }

        public bool Intersects(IShape2D shape)
        {
            return this.LineSegments.Any(line => line.Intersects(shape));
        }

        IShape2D IShape2D.Translate(IPoint2D offset) => this.Translate(offset);

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

        public bool Equals(GridPolyline other)
        {
            if (object.ReferenceEquals(this, other))
                return true;

            if (object.ReferenceEquals(other, null))
                return false;

            if (this.PointCount != other.PointCount)
                return false;

            for (int i = 0; i < this.PointCount; i++)
            {
                if (this._Points[i] != other._Points[i])
                    return false;
            }

            return true;
        }
    }
}
