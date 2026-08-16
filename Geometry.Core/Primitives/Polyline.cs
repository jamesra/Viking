using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;

namespace Geometry
{
    /// <summary>
    /// Geometric polyline for predicates and intersection. Distinct from UI <c>Path</c> on the Geometry facade, which raises change events.
    /// </summary>
    public class Polyline : IPolyLine2D, IHasControlPoints, IEquatable<Polyline>, IEquatable<IPolyLine2D>, IEquatable<ILineSegment2D>
    {
        protected readonly List<IPoint2D> _Points;

        public readonly bool AllowsSelfIntersection = false;

        /// <summary>First crossing found when <see cref="AllowsSelfIntersection"/> is true; unused otherwise.</summary>
        private LineSegment? KnownSelfIntersection;

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

        /// <summary>Spatial index of current segments; created on first <see cref="Add"/>.</summary>
        private BoundingBoxIndex<LineSegment> rTree = null;

        public int PointCount => _Points.Count;

        public int NumUniqueVertices => _Points.Count;

        public int LineCount => LineSegments.Count;

        public Polyline(bool AllowSelfIntersection = false)
        {
            this.AllowsSelfIntersection = AllowSelfIntersection;
            _Points = [];
        }

        public Polyline(int capacity, bool AllowSelfIntersection = false) : this(AllowSelfIntersection)
        {
            _Points = new List<Geometry.IPoint2D>(capacity);
        }

        public Polyline(IEnumerable<IPoint2D> points, bool AllowSelfIntersection = false)
        {
            this.AllowsSelfIntersection = AllowSelfIntersection;

            _Points = new List<IPoint2D>(points.Count());

            foreach (var p in points)
            {
                this.Add(p);
            }
        }

        public Polyline(IEnumerable<Vector2> points, bool AllowSelfIntersection = false)
        {
            this.AllowsSelfIntersection = AllowSelfIntersection;

            _Points = [.. points.Cast<IPoint2D>()];
        }

        public static explicit operator IPoint2D[](Polyline src)
        {
            return [.. src._Points];
        }

        public static explicit operator List<IPoint2D>(Polyline src)
        {
            return [.. src._Points];
        }

        public static explicit operator Vector2[](Polyline src)
        {
            return [.. src._Points.Select(p => new Vector2(p))];
        }

        public static explicit operator List<Vector2>(Polyline src)
        {
            return [.. src._Points.Select(p => new Vector2(p))];
        }

        public Vector2 this[PolylineIndex index] => _Points[index.VertexIndex].ToVector2();

        /// <summary>
        /// True if <paramref name="next"/> can be appended without violating self-intersection rules.
        /// </summary>
        public bool CanAdd(in IPoint2D next)
        {
            if (_Points.Count == 0)
                return true;

            if (AllowsSelfIntersection)
                return true;

            if (_Points.Contains(next))
                return false;

            LineSegment line = new(_Points.Last(), next);

            if (_Points.Count == 1)
                return true;

            //var Existing = this.LineSegments;
            List<LineSegment> intersectionCandidates = rTree.Intersects(line.BoundingBox);
            if (line.SelfIntersects([.. this.LineSegments.Where(l => intersectionCandidates.Contains(l))], LineSetOrdering.Polyline))
            {
                return false;
            }

            return true;
        }

        public void Add(IPoint2D next)
        {
            rTree ??= new BoundingBoxIndex<LineSegment>();

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

            LineSegment line = new(_Points.Last(), next);

            if (_Points.Count == 1)
            {
                _Points.Add(next);
                rTree.Add(line.BoundingBox, line);
                this._LineSegments = [];
                _LineSegments.Add(line);
                return;
            }
            else if (AllowsSelfIntersection == false || AllowsSelfIntersection && KnownSelfIntersection.HasValue == false)
            {
                List<LineSegment> intersectionCandidates = rTree.Intersects(line.BoundingBox);

                if (line.SelfIntersects([.. this.LineSegments.Where(l => intersectionCandidates.Contains(l))], LineSetOrdering.Polyline, out LineSegment? intersected))
                {
                    this.KnownSelfIntersection = AllowsSelfIntersection == false
                        ? throw new ArgumentException("Added point created self-intersecting line in Polyline")
                        : intersected;
                }
            }

            var Existing = this._LineSegments;
            _Points.Add(next);
            Existing.Add(line);
            rTree.Add(line.BoundingBox, line);
            this._LineSegments = Existing;
        }

        public void Insert(int index, IPoint2D value)
        {
            rTree ??= new BoundingBoxIndex<LineSegment>();

            if (index < 0 || index > _Points.Count)
                throw new IndexOutOfRangeException($"{nameof(Polyline)}.{nameof(Insert)}: {index} out of bounds");

            /////////////////////////////////////////////////
            //Simple cases where intersection is not a factor
            ///////////////////////////////////////////////// 

            //Case for adding to the beginning of the polyline
            if (_Points.Count == 0)
            {
                Add(value);
                return;
            }
            else if (_Points.Count == 1)
            {
                if (_Points[0].Equals(value))
                    throw new ArgumentException("Inserting point already in Polyline identical to an adjacent point");

                _Points.Insert(index, value);
                LineSegment line = new(_Points[0], _Points[1]);
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
            PolylineIndex insert_index = new(index, this.NumUniqueVertices);

            //Check for adjacent duplicate points
            bool duplicate_point = _Points.Contains(value);
            if (duplicate_point)
            {
                if (AllowsSelfIntersection == false)
                    throw new ArgumentException("Inserting point already in Polyline that does not allow self-intersection");
                else
                {
                    //Ensure the adjacent points are not duplicates... perhaps this should be a no-op, but for now throw an exception
                    if (this[insert_index] == value)
                        throw new ArgumentException("Inserting duplicate point into polyline adjacent to the duplicate.");

                    if (false == insert_index.IsFirstIndex)
                    {
                        if (this[insert_index.Previous.Value] == value)
                            throw new ArgumentException("Inserting duplicate point into polyline adjacent to the duplicate.");
                    }
                }
            }

            //Copy the existing line segments so we can test new segments against the existing ones minus the replaced segment
            List<LineSegment> segments = [.. this.LineSegments];
            List<LineSegment> new_segments = [];
            List<LineSegment> removed_segments = [];

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
                new_segments.Add(new LineSegment(value, _Points[index]));
            }
            else
            {
                new_segments.Add(new LineSegment(_Points[index - 1], value));
                new_segments.Add(new LineSegment(value, _Points[index]));
            }


            if (AllowsSelfIntersection == false || AllowsSelfIntersection && KnownSelfIntersection.HasValue == false)
            {
                foreach (var new_seg in new_segments)
                {
                    List<LineSegment> intersectionCandidates = [.. rTree.Intersects(new_seg.BoundingBox).Where(l => removed_segments.Contains(l) == false)];

                    if (new_seg.SelfIntersects([.. this.LineSegments.Where(l => intersectionCandidates.Contains(l))], LineSetOrdering.Polyline, out LineSegment? intersected))
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
                segments.InsertRange(index - 1, new_segments);
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

        public List<Vector2> AddPointsAtIntersections(Polyline other)
        {
            var candidates = other.rTree.Intersects(this.BoundingBox);

            List<Vector2> found_or_added_intersections = [];

            List<Vector2> newPolyline = new(_Points.Count);

            var otherLineSegments = other.LineSegments.ToArray();

            foreach (var other_ls in candidates)
            {
                found_or_added_intersections.AddRange(this.AddPointsAtIntersections(other_ls));
            }

            return found_or_added_intersections;
        }

        public List<Vector2> AddPointsAtIntersections(LineSegment other)
        {
            Rectangle? overlap = this.BoundingBox.Intersection(other.BoundingBox);
            if (!overlap.HasValue)
                return [];

            List<Vector2> found_or_added_intersections = [];
            var LineSegmentsCopy = this.LineSegments.ToArray();

            for (int i = LineSegmentsCopy.Length - 1; i >= 0; i--) //Go in reverse order so we do not change the index we are inserting into
            {
                LineSegment ls = LineSegments[i];

                var intersects = ls.Intersects(other, true, out var intersection);
                if (intersects)
                {
                    if (intersection is IPoint2D point)
                    {
                        Vector2 p = point.ToVector2();
                        found_or_added_intersections.Insert(0, p);
                        System.Diagnostics.Debug.Assert(false == _Points.Contains(point));
                        this.Insert(i + 1, point);
                    }
                }
            }

            return found_or_added_intersections;
        }

        /// <summary>
        /// Indices of polyline vertices that match any of <paramref name="points"/>.
        /// </summary>
        public List<PolylineIndex> TryGetIndices(ICollection<Vector2> points)
        {
            List<PolylineIndex> found = new(points.Count);
            var candidates = points.Where(p => BoundingBox.Covers(p));
            List<Vector2> notExterior = new(points.Count);

            foreach (Vector2 point in points)
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

        public Rectangle BoundingBox
        {
            get
            {
                var MinX = _Points.Min(p => p.X);
                var MaxX = _Points.Max(p => p.X);
                var MinY = _Points.Min(p => p.Y);
                var MaxY = _Points.Max(p => p.Y);

                return new Rectangle(MinX, MaxX, MinY, MaxY);
            }
        }

        public ShapeType2D ShapeType => ShapeType2D.Polyline;

        /// <summary>
        /// Cached segments. Null or wrong count means dirty; <see cref="LineSegments"/> rebuilds from <see cref="_Points"/>.
        /// Explicit <see cref="IPolyLine2D.LineSegments"/> must use that getter, not this field.
        /// </summary>
        private List<LineSegment> _LineSegments;

        /// <summary>Rebuilds from points when the cache is null or stale. Returns a copy.</summary>
        public List<LineSegment> LineSegments
        {
            get
            {
                if (_LineSegments != null && _LineSegments.Count == _Points.Count - 1)
                {
                    return [.. _LineSegments];
                }

                _LineSegments = new List<LineSegment>(this._Points.Count);

                for (int i = 0; i < _Points.Count - 1; i++)
                {
                    _LineSegments.Add(new LineSegment(_Points[i], _Points[i + 1]));
                }

                return [.. _LineSegments];
            }
        }

        /// <summary>Uses the public getter so a null/stale <see cref="_LineSegments"/> cache is rebuilt.</summary>
        IReadOnlyList<ILineSegment2D> IPolyLine2D.LineSegments => [.. LineSegments.Cast<ILineSegment2D>()];


        public IReadOnlyList<IPoint2D> Points => this._Points;

        IReadOnlyList<IPoint2D> IHasControlPoints.ControlPoints => _Points;

        public bool Contains(in IPoint2D p) => GetRelation(p).IsContains();

        public bool Covers(in IPoint2D p) => GetRelation(p).IsCovers();

        public ShapeRelation GetRelation(in IPoint2D p)
        {
            if (_Points.Count == 0)
                return ShapeRelation.None;

            Vector2 v = new(p.X, p.Y);
            if (!LineSegments.Any(line => line.Covers(v)))
                return ShapeRelation.None;

            bool atStart = Vector2.DistanceSquared(v, _Points[0]) <= Tolerance.EpsilonSquared;
            bool atEnd = Vector2.DistanceSquared(v, _Points[_Points.Count - 1]) <= Tolerance.EpsilonSquared;
            if (atStart || atEnd)
                return ShapeRelation.Touching;

            return ShapeRelation.Contained;
        }

        ShapeRelation IShape2D.GetRelation(in Geometry.ILineSegment2D line) => GetRelation(line.Convert());

        public ShapeRelation GetRelation(in LineSegment line)
        {
            ShapeRelation output = ShapeRelation.None;
            const ShapeRelation exitCondition = ShapeRelation.Intersecting | ShapeRelation.Touching;
            foreach (LineSegment seg in LineSegments)
            {
                output |= seg.GetRelation(line);
                if (output.HasFlag(exitCondition))
                    return output;
            }

            return output;
        }

        public bool Intersects(in IShape2D shape)
        {
            IShape2D shp = shape;
            return this.LineSegments.Any(line => line.Intersects(shp));
        }

        IShape2D IShape2D.Translate(in IPoint2D offset) => this.Translate(offset);

        public Polyline Translate(in IPoint2D offset)
        {
            Vector2 local_offset = new(offset.X, offset.Y);
            var translatedPoints = this._Points.Select(p => new Vector2(p.X + local_offset.X, p.Y + local_offset.Y));
            return new Polyline(translatedPoints);
        }

        /// <summary>
        /// Clone with coordinates rounded to <paramref name="precision"/> decimal places; consecutive duplicates are dropped.
        /// </summary>
        public Polyline Round(int precision)
        {
            Vector2[] roundedPoints = [.. this.Points.Select(e => e.Round(precision))];
            for (int i = roundedPoints.Length - 1; i > 0; i--)
            {
                if (roundedPoints[i] == roundedPoints[i - 1])
                    roundedPoints.RemoveAt(i);
            }

            Polyline clone = new(roundedPoints);
            return clone;
        }

        public override string ToString() => string.Format("PolyLine: {0}", string.Join(" ", _Points));

        public Polyline Clone() => new Polyline(this.Points.ToArray(), this.AllowsSelfIntersection);

        public override int GetHashCode() => 0; //Use a constant since the polyline can change

        public override bool Equals(object obj)
        {
            if (obj is Polyline other)
                return Equals(other);

            if (obj is IShape2D otherShape)
                return Equals(otherShape);

            return base.Equals(obj);
        }

        public bool Equals(Polyline other)
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
