using System;
using System.Collections.Generic;
using System.Collections.Specialized;
using System.Diagnostics;
using System.Linq;

namespace Geometry
{
    /// <summary>
    /// A mutable polyline used by UI and annotation input. Raises change events.
    /// Distinct from Core <see cref="Polyline"/>, which is a geometry value used for predicates and intersection.
    /// Do not merge the two types.
    /// </summary>
    public class Path : IPolyLine2D, System.Collections.Specialized.INotifyCollectionChanged, IEquatable<IPolyLine2D>, IEquatable<ILineSegment2D>
    {

        public delegate void LoopChangedEventHandler(object sender, bool HasLoop);

        /// <summary>
        /// Fires an event when a loop in the path is found or removed
        /// </summary>
        public event LoopChangedEventHandler OnLoopChanged;

        private void FireOnLoopChangedEvent(bool HasLoop) =>
            //Trace.WriteLine(string.Format("FireOnLoopChangedEvent: {0}", HasLoop));

            this.OnLoopChanged?.Invoke(this, HasLoop);

        public event System.Collections.Specialized.NotifyCollectionChangedEventHandler OnPathChanged;

        event NotifyCollectionChangedEventHandler System.Collections.Specialized.INotifyCollectionChanged.CollectionChanged
        {
            add => this.OnPathChanged += value;
            remove => this.OnPathChanged -= value;
        }

        private void FireOnPathChangedEvent(NotifyCollectionChangedEventArgs e) => this.OnPathChanged?.Invoke(this, e);

        public List<Vector2> Points = [];

        public double Length => Segments.Sum(s => s.Length);

        /// <summary>Catmull-Rom samples per span when building <see cref="SimplifiedPath"/>.</summary>
        private readonly uint _SimplifiedPathInterpolations = 5;

        /// <summary>Backing store for <see cref="SimplifiedPathTolerance"/>.</summary>
        private double _SimplifiedPathTolerance = 1.0;

        /// <summary>
        /// Max deviation of the Catmull-Rom simplified path from <see cref="Points"/>.
        /// Changing this nulls <see cref="SimplifiedPath"/> and simplified-loop caches.
        /// </summary>
        public double SimplifiedPathTolerance
        {
            get => _SimplifiedPathTolerance;
            set
            {
                if (value == _SimplifiedPathTolerance)
                    return;

                _SimplifiedPathTolerance = value;
                _SimplifiedPath = null;
                _SimplifiedLoop = null;
                _SimplifiedLoopSegments = null;
            }
        }


        /// <summary>Null means dirty; rebuilt from <see cref="Points"/> and <see cref="SimplifiedPathTolerance"/>.</summary>
        private Vector2[] _SimplifiedPath;
        public Vector2[] SimplifiedPath
        {
            get
            {
                if (_SimplifiedPath is null)
                {
                    try
                    {
                        _SimplifiedPath = [.. CatmullRomControlPointSimplification.IdentifyControlPoints(this.Points, SimplifiedPathTolerance, false, _SimplifiedPathInterpolations)];
                    }
                    catch (ArgumentException)
                    {
                        Trace.WriteLine("Could not simplify path, trying tighter tolerance...");
                        try
                        {
                            _SimplifiedPath = [.. CatmullRomControlPointSimplification.IdentifyControlPoints(this.Points, SimplifiedPathTolerance / 2.0, false, _SimplifiedPathInterpolations)];
                        }
                        catch (ArgumentException)
                        {
                            Trace.WriteLine("Could not simplify path, using original path...");
                            _SimplifiedPath = [.. this.Points];
                        }
                    }
                }

                return _SimplifiedPath;
            }
        }

        /// <summary>Last two points as a segment (A = newest, B = previous), matching <see cref="_Segments"/> order.</summary>
        public LineSegment NewestSegment
        {
            get
            {
                int count = Points.Count;
                return new LineSegment(Points[count - 1], Points[count - 2]);
            }
        }

        /// <summary>
        /// Segments stored with A = newer control point and B = older. Keep this order; hit-testing and loop detection depend on it.
        /// </summary>
        private readonly List<LineSegment> _Segments = [];
        public IReadOnlyList<LineSegment> Segments => _Segments;

        /// <summary>
        /// True if the path has at least two points (one segment).
        /// </summary>
        public bool HasSegment => Points.Count >= 2;

        public bool HasSelfIntersection => _Loop != null;

        /// <summary>Vertices of the first self-intersection loop; null if none. Invalidated when the path changes.</summary>
        private Vector2[] _Loop = null;

        /// <summary>Vertices of the first loop, or null if the path does not self-intersect.</summary>
        public Vector2[] Loop => _Loop;

        /// <summary>Segments of <see cref="_Loop"/>; null if no loop. Same A-newer/B-older order as <see cref="_Segments"/>.</summary>
        private LineSegment[] _LoopSegments = null;

        /// <summary>Segments of <see cref="Loop"/>, or null if none.</summary>
        public LineSegment[] LoopSegments => _LoopSegments;


        /// <summary>Catmull-Rom simplification of <see cref="_Loop"/>; null means dirty.</summary>
        private Vector2[] _SimplifiedLoop = null;

        /// <summary>Simplified vertices of the first loop, or null if none.</summary>
        public Vector2[] SimplifiedFirstLoop
        {
            get
            {
                if (_SimplifiedLoop is null)
                {
                    if (HasSelfIntersection)
                        this._SimplifiedLoop = [.. this._Loop.IdentifyControlPoints(this.SimplifiedPathTolerance, true, _SimplifiedPathInterpolations).EnsureClosedRing()];
                    else
                        return null;
                }

                return _SimplifiedLoop;
            }
        }


        /// <summary>Segments of <see cref="_SimplifiedLoop"/>; null means dirty.</summary>
        private LineSegment[] _SimplifiedLoopSegments = null;

        /// <summary>Segments of <see cref="SimplifiedFirstLoop"/>, or null if none.</summary>
        public LineSegment[] SimplifiedLoopSegments
        {
            get
            {
                if (_SimplifiedLoopSegments is null)
                {
                    if (HasSelfIntersection)
                    {
                        _SimplifiedLoopSegments = this._SimplifiedLoop.ToLineSegments();
                    }
                    else
                    {
                        return null;
                    }
                }

                return _SimplifiedLoopSegments;
            }
        }

        public Path()
        {

        }

        public void Push(Vector2 p)
        {
            bool HasLoop = this.HasSelfIntersection;
            Push_NoEvent(p);
            bool HasLoopAfterPush = this.HasSelfIntersection;

            FireOnPathChangedEvent(new NotifyCollectionChangedEventArgs(NotifyCollectionChangedAction.Add, p, 0));

            if (HasLoop != HasLoopAfterPush)
            {
                FireOnLoopChangedEvent(HasLoopAfterPush);
            }
        }

        private void Push_NoEvent(Vector2 p)
        {
            bool FoundLoop = CheckForSelfIntersectionBeforePush(p);  //If we don't already have a self intersection detected, check if this creates one. Do this before adding a new segment

            //Add the new line segment to our list
            if (this.Points.Count > 0)
            {
                Vector2 lastPoint = this.Peek();
                LineSegment newSegment = new(p, lastPoint);
#if DEBUG
                if (_Segments.Count > 0)
                {
                    System.Diagnostics.Debug.Assert(_Segments.Last().A == lastPoint); //Ensure our line segments are contiguous
                }
#endif
                _Segments.Add(newSegment);
            }

            this.Points.Add(p);
            _SimplifiedPath = null;  //TODO: This could be optimized to only calculate the new segment

            //Make sure we have the right number of segments for points in the path
            System.Diagnostics.Debug.Assert(_Segments.Count == this.Points.Count - 1);
        }

        public Vector2 Pop()
        {
            bool HasLoop = this.HasSelfIntersection;
            Vector2 removed = this.Pop_NoEvent();
            bool HasLoopAfterPush = this.HasSelfIntersection;

            FireOnPathChangedEvent(new NotifyCollectionChangedEventArgs(NotifyCollectionChangedAction.Remove, removed, 0));

            if (HasLoop != HasLoopAfterPush)
            {
                FireOnLoopChangedEvent(HasLoopAfterPush);
            }

            return removed;
        }

        private Vector2 Pop_NoEvent()
        {
            CheckForSelfIntersectionLossBeforePop();

            Vector2 p = this.Points.First();
            this.Points.RemoveAt(this.Points.Count - 1);

            if (this._Segments.Count > 0)
            {
                _Segments.RemoveAt(this._Segments.Count - 1);
            }

            _SimplifiedPath = null;  //TODO: This could be optimized to only calculate the new segment

            //Make sure we have the right number of segments for points in the path
            System.Diagnostics.Debug.Assert(_Segments.Count == this.Points.Count - 1);
            return p;
        }

        public Vector2 Peek() => this.Points[this.Points.Count - 1];

        public void Clear()
        {
            bool HadLoop = this.HasSelfIntersection;

            Points = [];
            _SimplifiedPath = null;
            ResetLoop();
            FireOnPathChangedEvent(new NotifyCollectionChangedEventArgs(NotifyCollectionChangedAction.Reset));

            if (HadLoop)
            {
                FireOnLoopChangedEvent(HasLoop: false);
            }
        }

        /// <summary>
        /// Erase points up to and including the passed index
        /// </summary>
        /// <param name="iDeletePoint"></param>
        /// <returns>True if part of the path was erased</returns>
        public bool Erase(int iDeletePoint)
        {
            if (iDeletePoint >= 0)
            {
                bool HadLoop = this.HasSelfIntersection;

                int NumExpectedToDelete = Points.Count - iDeletePoint;
                Vector2[] removedEntries = new Vector2[NumExpectedToDelete];
                Points.CopyTo(iDeletePoint, removedEntries, 0, NumExpectedToDelete);

                int NumDeleted = 0;

                while (NumDeleted < NumExpectedToDelete)
                //while (iDeletePoint >= 0)
                {
                    this.Pop_NoEvent();
                    NumDeleted++;
                }
                System.Diagnostics.Debug.Assert(NumDeleted == removedEntries.Length);

                FireOnPathChangedEvent(new NotifyCollectionChangedEventArgs(NotifyCollectionChangedAction.Remove, removedEntries, iDeletePoint));

                if (HadLoop != this.HasSelfIntersection)
                {
                    FireOnLoopChangedEvent(this.HasSelfIntersection);
                }

                return true;
            }

            return false;
        }

        /// <summary>
        /// Erase the path to the closest vertex to the passed point
        /// </summary>
        /// <param name="p"></param>
        /// <returns>True if part of the path was erased</returns>
        public bool Erase(Vector2 input)
        {
            double[] distances = [.. Points.Select(v => Vector2.Distance(v, input))];
            double min_distance = distances.Min();

            int iDeletePoint = Array.IndexOf(distances, distances.Min());

            return this.Erase(iDeletePoint);
        }

        /// <summary>
        /// Replace the top of the path with the new value
        /// </summary>
        /// <param name="p"></param>
        public void Replace(Vector2 p)
        {
            if (p == this.Peek())
                return; //Do nothing if the points are the same

            bool HadLoop = this.HasSelfIntersection;

            Vector2 oldValue = this.Pop_NoEvent();
            bool HadLoopAfterPop = this.HasSelfIntersection;
            this.Push_NoEvent(p);

            bool HasLoopAfterPush = this.HasSelfIntersection;

            FireOnPathChangedEvent(new NotifyCollectionChangedEventArgs(NotifyCollectionChangedAction.Replace, p, oldValue, Points.Count - 1));

            //Check if we added a loop, or if we removed a loop and then re-added it.
            if (HadLoop != HasLoopAfterPush || HadLoop != HadLoopAfterPop)
            {
                FireOnLoopChangedEvent(HasLoopAfterPush);
            }
        }

        /// <summary>
        /// Resets the loops stored in this path
        /// </summary>
        private void SetLoop(List<Vector2> loopPoints)
        {
            this._Loop = [.. loopPoints.EnsureClosedRing()];
            this._LoopSegments = this._Loop.ToLineSegments();
            this._SimplifiedLoop = null; //Recalculated on demand
            this._SimplifiedLoopSegments = null; //Recalculated on demand
            System.Diagnostics.Debug.Assert(_LoopSegments[0].A == _LoopSegments[_LoopSegments.Length - 1].B);
        }

        /// <summary>
        /// Resets the loops stored in this path
        /// </summary>
        private void ResetLoop()
        {
            this._Loop = null;
            this._LoopSegments = null;
            this._SimplifiedLoop = null;
            this._SimplifiedLoopSegments = null;
        }


        /// <summary>
        /// If we add the passed point will it intersect our path?
        /// </summary>
        /// <param name="new_point"></param>
        /// <returns>True if a NEW loop was found</returns>
        public bool CheckForSelfIntersectionBeforePush(in Vector2 p)
        {
            if (HasSelfIntersection)
            {
                return false;
            }

            //Need at least four points for a self intersection
            if (this.Points.Count < 3)
            {
                return false;
            }

            this._LoopSegments = null;
            this._SimplifiedLoopSegments = null;
            LineSegment newSegment = new(p, this.Peek());
            List<LineSegment> loopSegments = new(this._Segments.Count);

            List<Vector2> loopPoints = [];

            //This function looks odd because the lines are reversed. A is closer to the most recently placed point in the path

            int IntersectionCount = 0;
            for (int iPathLine = 0; iPathLine < this._Segments.Count; iPathLine++)
            {
                LineSegment path_line = this._Segments[iPathLine];
                if (newSegment.Intersects(path_line, out Vector2 intersection))
                {
                    IntersectionCount += 1;

                    if (IntersectionCount == 1)
                    {
                        //Add the line from the intersection to the near point of the path
                        if (path_line.IsEndpoint(intersection))
                        {
                            if (path_line.B == intersection) //The entire line belongs in the loop
                            {
                                //loop_segment = path_line;
                                loopPoints.Add(intersection);
                                loopPoints.Add(path_line.A);
                            }
                            else //We intersected the end of this line and none of it is in the loop.  We should add the next segment in the path instead.
                            {
                                IntersectionCount -= 1;
                                continue;
                            }
                        }
                        else
                        {
                            //The intersection is along the line, add the intersection point and the end of our line
                            loopPoints.Add(intersection);
                            loopPoints.Add(path_line.A);
                        }

                        //loopSegments.Add(loop_segment); //Start populating the loop
                    }
                    else if (IntersectionCount == 2)
                    {
                        //We found the closing point of the loop.
                        loopPoints.Add(intersection);
                        break;
                        //Add the bit from the path point to the intersection
                        /*
                        System.Diagnostics.Debug.Assert(path_line.IsEndpoint(intersection));
                        if (path_line.IsEndpoint(intersection))
                        {
                            loopPoints.Add(path_line.B);
                            if (path_line.B != intersection)
                                loopPoints.Add(intersection);

                            break;
                            /*
                            if (path_line.B == intersection) //The loop ends at the start of our line, do nothing
                            {
                                break;
                            }
                            else //We intersected the end of this line and it entirely belongs in the loop.
                            {
                                loopSegments.Add(path_line);
                                continue;
                            }
                        }
                        else
                        {
                            //Add the part from the start of our line to the intersection
                            loop_segment = new LineSegment(path_line.B, intersection);
                            loopSegments.Add(loop_segment);
                            break;
                        */
                        /*
                        }
                        */

                    }
                }
                else if (IntersectionCount == 1)
                {
                    //No intersection, just add this line's endpoint to the list of points in the loop
                    loopPoints.Add(path_line.A);
                }
            }

            if (IntersectionCount == 2)
            {
                SetLoop(loopPoints);
                return true;
            }
            else
            {
                ResetLoop();
                return false;
            }

            //List<LineSegment> intersectingSegments = newSegment.Intersections(this.Segments, false, out Vector2[] intersectionPoints);
            //intersectionPoints = intersectionPoints.Where(p => newSegment.B != p).ToArray(); //We know that the most recent point in the path will share an endpoint, so remove these from results
            //intersectingSegments = intersectingSegments.Where(s => s != this.NewestSegent).ToList();
            //if (intersectionPoints.Length > 0)
            //{
            //    System.Diagnostics.Debug.Assert(intersectionPoints.Length == 1); //We should only find one self intersection, then stop looking

            //    this.FirstSelfIntersectingSegmentPair = new LineSegment[] { intersectingSegments[0], newSegment };

            //    return true;
            //}

            //return false;
        }

        /// <summary>
        /// If we pop the top point will it remove an existing self-intersection?
        /// </summary>
        /// <param name="new_point"></param>
        /// <returns>True if popping the point will break an existing loop</returns>
        private bool CheckForSelfIntersectionLossBeforePop()
        {
            if (false == this.HasSegment)
            {
                return false;
            }

            LineSegment lostSegment = this.NewestSegment;
            if (false == this.HasSelfIntersection)
            {
                return false;
            }

            //If we are popping one of the line segments in the pair, then clear the self intersection array
            if (lostSegment.B == _Loop[_Loop.Length - 2])
            {
                ResetLoop();
                return true;
            }

            return false;
        }

        public double Distance(in Vector2 p)
        {
            if (this.Points.Count == 0)
            {
                throw new ArgumentException("No points in path to calculate distance");
            }
            else if (this.Points.Count == 1)
            {
                return Vector2.Distance(this.Points[0], in p);
            }
            else
            {
                Vector2 pnt = p;
                return this.Segments.Min(seg => seg.DistanceToPoint(pnt));
            }
        }

        #region IPolyLine2D
        public Rectangle BoundingBox
        {
            get
            {
                double MinX = Points.Min(p => p.X);
                double MaxX = Points.Max(p => p.X);
                double MinY = Points.Min(p => p.Y);
                double MaxY = Points.Max(p => p.Y);

                return new Rectangle(MinX, MaxX, MinY, MaxY);
            }
        }


        IReadOnlyList<ILineSegment2D> IPolyLine2D.LineSegments
        {
            get
            {
                List<ILineSegment2D> listSegments = new(this.Points.Count - 1);

                for (int i = 0; i < Points.Count - 1; i++)
                {
                    listSegments.Add(new LineSegment(Points[i], Points[i + 1]));
                }

                return listSegments;
            }
        }

        IReadOnlyList<IPoint2D> IPolyLine2D.Points => [.. this.Points.Select(p => (IPoint2D)p)];

        public ShapeType2D ShapeType => ShapeType2D.Polyline;

        public double Area => throw new ArgumentException("No area for Polyline");

        bool IShape2D.Contains(in IPoint2D p)
        {
            IPoint2D pnt = p;
            return this.Segments.Any(line => line.Contains(pnt));
        }

        bool IShape2D.Covers(in IPoint2D p)
        {
            IPoint2D pnt = p;
            return this.Segments.Any(line => line.Covers(pnt));
        }

        ShapeRelation IShape2D.GetRelation(in IPoint2D p)
        {
            IPoint2D pnt = p;
            if (!this.Segments.Any(line => line.Covers(pnt)))
                return ShapeRelation.None;

            Vector2 v = pnt.ToVector2();
            if (Points.Count > 0 &&
                (Vector2.DistanceSquared(v, Points[0]) <= Tolerance.EpsilonSquared ||
                 Vector2.DistanceSquared(v, Points[Points.Count - 1]) <= Tolerance.EpsilonSquared))
                return ShapeRelation.Touching;

            return ShapeRelation.Contained;
        }

        ShapeRelation IShape2D.GetRelation(in ILineSegment2D line)
        {
            ShapeRelation output = ShapeRelation.None;
            if (this.BoundingBox.GetRelation(line) == ShapeRelation.None)
                return ShapeRelation.None;

            const ShapeRelation exitCondition = ShapeRelation.Intersecting | ShapeRelation.Touching;
            foreach (LineSegment seg in this.LoopSegments)
            {
                output |= seg.GetRelation(line);
                if (output.HasFlag(exitCondition))
                    return output;
            }

            return output;
        }

        bool IShape2D.Contains(in IShape2D other) => ((IShape2D)this).GetRelation(other).IsContains();

        bool IShape2D.Covers(in IShape2D other) => ((IShape2D)this).GetRelation(other).IsCovers();

        ShapeRelation IShape2D.GetRelation(in IShape2D other)
        {
            if (other is null)
                throw new ArgumentNullException(nameof(other));

            IShape2D self = this;
            if (other.ShapeType == ShapeType2D.Point)
                return self.GetRelation((IPoint2D)other);
            if (other.ShapeType == ShapeType2D.Line)
                return self.GetRelation((ILineSegment2D)other);

            List<ShapeRelation> parts = new(Segments.Count);
            foreach (LineSegment seg in Segments)
                parts.Add(seg.GetRelation(other));
            return ShapeRelationHelpers.CombineParts(parts);
        }

        bool IShape2D.Intersects(in IShape2D shape) =>
            ((IShape2D)this).GetRelation(shape) != ShapeRelation.None;

        IShape2D IShape2D.Translate(in IPoint2D offset)
        {
            List<IPoint2D> translatedPoints = new(this.Points.Count);

            var X = offset.X;
            var Y = offset.Y;
            translatedPoints = [.. this.Points.Select(p => new Vector2(p.X + X, p.Y + Y)).Cast<IPoint2D>()];

            return new Polyline(translatedPoints);
        }

        public bool Equals(IShape2D other)
        {
            if (other is IPolyLine2D otherPolyline)
            {
                if (this.Points.Count != otherPolyline.Points.Count)
                    return false;

                for (int i = 0; i < this.Points.Count; i++)
                {
                    if (false == this.Points[i].Equals(otherPolyline.Points[i]))
                        return false;
                }

                return true;
            }

            return false;
        }

        public bool Equals(ILineSegment2D other)
        {
            if (this.Points.Count != 2)
                return false;

            return (Points[0].Equals(other.A) && Points[1].Equals(other.B)) ||
                   (Points[1].Equals(other.A) && Points[0].Equals(other.B));
        }

        public bool Equals(IPolyLine2D other)
        {
            if (this.Points.Count != other.Points.Count)
                return false;

            for (int i = 0; i < this.Points.Count; i++)
            {
                if (false == this.Points[i].Equals(other.Points[i]))
                    return false;
            }

            return true;
        }

        #endregion

    }
}
