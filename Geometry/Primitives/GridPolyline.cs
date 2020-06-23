using System;
using Geometry.JSON;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

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
        public int LineCount { get { return LineSegments.Count; } }

        public GridPolyline(bool AllowSelfIntersection= false)
        {
            this.AllowsSelfIntersection = AllowSelfIntersection;
            _Points = new List<Geometry.IPoint2D>();
        }

        public GridPolyline(int capacity, bool AllowSelfIntersection= false) : this(AllowSelfIntersection)
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

            _Points = new List<IPoint2D>(points.Count());

            foreach (var p in points)
            {
                this.Add(p);
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
            if (_Points.Contains(next))
                throw new ArgumentException("Point already in Polyline that does not allow self-intersection");

            GridLineSegment line = new GridLineSegment(_Points.Last(), next);

            if (_Points.Count == 1)
            {
                _Points.Add(next);
                rTree.Add(line.BoundingBox, line);
            }
            else if(AllowsSelfIntersection == false)
            {
                List<GridLineSegment> intersectionCandidates = rTree.Intersects(line.BoundingBox);

                if (AllowsSelfIntersection == false || AllowsSelfIntersection && KnownSelfIntersection.HasValue == false)
                {
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

        public IShape2D Translate(IPoint2D offset)
        {
            List<IPoint2D> translatedPoints = new List<Geometry.IPoint2D>(this._Points.Count);

            translatedPoints = this._Points.Select(p => new GridVector2(p.X + offset.X, p.Y + offset.Y)).Cast<IPoint2D>().ToList();

            return new GridPolyline(translatedPoints);
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

            for(int i = 0; i < this.PointCount; i++)
            {
                if (this._Points[i] != other._Points[i])
                    return false;
            }

            return true;
        }
    }
}
