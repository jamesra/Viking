using Geometry;
using System;
using System.Collections;
using System.Collections.Generic;
using System.Collections.Specialized;
using System.Linq;
using System.Windows.Forms;
using VikingXNAGraphics;
using VikingXNAWinForms;

namespace Viking.UI
{
    public enum PathVertexAction
    {
        REMOVE, //Remove the newest vertex
        REPLACE, //Replace the newest vertex
        NONE,
        ADD, //Append a new vertex
        CUT_ERASE //Remove all verticies after a vertex
    };

    [Flags]
    public enum PenInputCompletionTriggers
    {
        None, //Never fire the OnPathComplete event
        Closure, //Signal the path is complete when a closed path is formed
        Tap, //Signal the path is complete when the pen is removed from the surface and contacts the path
    }


    public class PenPathChangedEventArgs : NotifyCollectionChangedEventArgs
    {
        readonly PenEventArgs PenState;

        public PenPathChangedEventArgs(PenEventArgs penState, NotifyCollectionChangedAction action) : base(action)
        {
            PenState = penState;
        }

        public PenPathChangedEventArgs(PenEventArgs penState, NotifyCollectionChangedAction action, object changedItem) : base(action, changedItem)
        {
            PenState = penState;
        }

        public PenPathChangedEventArgs(PenEventArgs penState, NotifyCollectionChangedAction action, IList changedItems) : base(action, changedItems)
        {
            PenState = penState;
        }

        public PenPathChangedEventArgs(PenEventArgs penState, NotifyCollectionChangedAction action, object changedItem, int index) : base(action, changedItem, index)
        {
            PenState = penState;
        }

        public PenPathChangedEventArgs(PenEventArgs penState, NotifyCollectionChangedAction action, IList changedItems, int startingIndex) : base(action, changedItems, startingIndex)
        {
            PenState = penState;
        }

        public PenPathChangedEventArgs(PenEventArgs penState, NotifyCollectionChangedAction action, object newItem, object oldItem) : base(action, newItem, oldItem)
        {
            PenState = penState;
        }

        public PenPathChangedEventArgs(PenEventArgs penState, NotifyCollectionChangedAction action, IList newItems, IList oldItems) : base(action, newItems, oldItems)
        {
            PenState = penState;
        }

        public PenPathChangedEventArgs(PenEventArgs penState, NotifyCollectionChangedAction action, object newItem, object oldItem, int index) : base(action, newItem, oldItem, index)
        {
            PenState = penState;
        }

        public PenPathChangedEventArgs(PenEventArgs penState, NotifyCollectionChangedAction action, IList newItems, IList oldItems, int startingIndex) : base(action, newItems, oldItems, startingIndex)
        {
            PenState = penState;
        }

        public PenPathChangedEventArgs(PenEventArgs penState, NotifyCollectionChangedAction action, object changedItem, int index, int oldIndex) : base(action, changedItem, index, oldIndex)
        {
            PenState = penState;
        }

        public PenPathChangedEventArgs(PenEventArgs penState, NotifyCollectionChangedAction action, IList changedItems, int index, int oldIndex) : base(action, changedItems, index, oldIndex)
        {
            PenState = penState;
        }
    }

    public delegate void PenPathChangedEventHandler(object sender, PenPathChangedEventArgs e);


    public class PenInputHelper : System.Collections.Specialized.INotifyCollectionChanged
    {
        /// <summary>
        /// When the user attempts to place the pen cursor on the shape they have drawn to complete it, how many screen pixels the have to be within to succeed
        /// </summary>
        public static double completionDistance = 50;

        /// <summary>
        /// When the pen loses contact with the surface, the maximum distance the user can resume appending from the end of the path when the 
        /// pen contacts the surface again.  Prevents the user from accidentally attempting to click something and drawing a crazy shape.
        /// 
        /// If set to NULL positive infinity is used so any touch resumes the drawing.
        /// </summary>
        public static double? resumeDistance = 50;

        /// <summary>
        /// When the pen regains contact with the surface, the distance beyond which input can be safely assumed to not relate to the path being drawn.  This allows the user of the PenInputHelper to perform other tasks
        /// with that input without conflicting with the pen input helper.
        /// </summary>
        public static double interactDistance = 50;

        /// <summary>
        /// The distance we can erase in pixels at max pressure
        /// </summary>
        public static double maxEraseDistance = 50;

        /// <summary>
        /// The distance we can erase in pixels at min pressure
        /// </summary>
        public static double minEraseDistance = 5;


        /// <summary>
        /// When set to true this property informs callers that the PenInputHelper will all ignore pen input until the pen is removed from the surface and touches within the interact distance.  
        /// 
        /// This allows other UI interactions to occur without affecting our path.
        /// </summary>
        public bool IgnoringThisPenContact { get; private set; }

        public Path path = new Path();

        private double PointIntervalOnDrag
        {
            get
            {
                return Parent.Downsample * 4.0;
            }
        }

        public bool PenIsComplete;
        Viking.UI.Controls.SectionViewerControl Parent;
        // GridVector2[] Verticies;

        public bool CanPathSelfIntersect = false;


        /// <summary>
        /// Set to true if the pen leaves contact with the surface.  If true, the next pen move action in contact with the surface must ensure the pen is in range before continuing the path
        /// </summary>
        private bool MustCheckResumeDistance = false;


        private double _SimplifiedPathToleranceInPixels = 1.0;
        /// <summary>
        /// How far can the simplified path drift from actual path in pixels?
        /// </summary>
        public double SimplifiedPathToleranceInPixels
        {
            get
            {
                return _SimplifiedPathToleranceInPixels;
            }
            set
            {
                if (value != _SimplifiedPathToleranceInPixels)
                {
                    _SimplifiedPathToleranceInPixels = value;
                    path.SimplifiedPathTolerance = value * Parent.Camera.Downsample;
                }

            }
        }

        /// <summary>
        /// The user has stopped drawing
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="Path"></param>
        public delegate void OnPathCompleteEventHandler(object sender, GridVector2[] Path);
        public event OnPathCompleteEventHandler OnPathCompleted;

        public event System.Collections.Specialized.NotifyCollectionChangedEventHandler OnPathChanged
        {
            add
            {
                path.OnPathChanged += value;
            }
            remove
            {
                path.OnPathChanged -= value;
            }
        }

        event NotifyCollectionChangedEventHandler System.Collections.Specialized.INotifyCollectionChanged.CollectionChanged
        {
            add
            {
                path.OnPathChanged += value;
            }
            remove
            {
                path.OnPathChanged -= value;
            }
        }

        public event Path.LoopChangedEventHandler OnPathLoop
        {
            add
            {
                path.OnLoopChanged += value;
            }
            remove
            {
                path.OnLoopChanged -= value;
            }
        }

        /// <summary>
        /// The path has been extended
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="segment"></param>
        public delegate void OnProposedNextSegmentChangedHandler(object sender, GridLineSegment? segment);
        public event OnProposedNextSegmentChangedHandler OnProposedNextSegmentChanged;

        public delegate bool CanControlPointBePlacedDelegate(GridVector2 position);

        /// <summary>
        /// Users of PenInputHelper can override this function to control which points can be added to the path.
        /// Returning false prevents the point from being added
        /// </summary>
        public CanControlPointBePlacedDelegate CanControlPointBePlaced = ControlPointCanAlwaysBePlaced;

        /// <summary>
        /// Default implementation for CanControlPointBePlaced delegate that always allows placing control points
        /// </summary>
        /// <param name="p"></param>
        /// <returns></returns>
        private static bool ControlPointCanAlwaysBePlaced(GridVector2 p)
        {
            return true;
        }


        /// <summary>
        /// The path has self-intersected itself
        /// </summary>
        //public delegate void OnPathClosedHandler(object sender, GridLineSegment? segment);
        //public event OnPathClosedHandler OnPathClosed;
        public List<GridVector2> Points
        {
            get
            {
                return path.Points;
            }
        }

        public GridVector2[] SimplifiedPath
        {
            get
            {
                return path.SimplifiedPath;
            }
        }

        public GridVector2? LastPenPosition;

        public uint NumCurveInterpolations
        {
            get;
        }

        public GridLineSegment NewestSegent
        {
            get
            {
                int count = Points.Count;
                return new GridLineSegment(Points[Points.Count - 1], Points[Points.Count - 2]);
            }
        }

        public GridLineSegment? ProposedNextSegment
        {
            get
            {
                if (Points.Count == 0)
                    return new GridLineSegment?();

                if (LastPenPosition.HasValue && LastPenPosition.Value != Points.Last())
                {
                    return new GridLineSegment(Points.Last(), LastPenPosition.Value);
                }

                return new GridLineSegment?();
            }
        }

        /// <summary>
        /// Segments are ordered so that A is the newer control point and B is the older control point in the path
        /// </summary>
        public IReadOnlyList<GridLineSegment> Segments
        {
            get
            {
                return path.Segments;
            }
        }


        /// <summary>
        /// Returns the line segments composing the first loop described by the path, or null if no self-intersection exists
        /// </summary>
        public GridVector2[] Loop
        {
            get
            {
                return path.Loop;
            }
        }


        /// <summary>
        /// Returns the line segments composing the first loop described by the path, or null if no self-intersection exists
        /// </summary>
        public GridLineSegment[] LoopSegments
        {
            get
            {
                return path.LoopSegments;
            }
        }


        /// <summary>
        /// Returns the line segments composing the first loop described by the path, or null if no self-intersection exists
        /// </summary>
        public GridVector2[] SimplifiedFirstLoop
        {
            get
            {
                return path.SimplifiedFirstLoop;
            }
        }


        /// <summary>
        /// Returns the line segments composing the first loop described by the path, or null if no self-intersection exists
        /// </summary>
        public GridLineSegment[] SimplifiedLoopSegments
        {
            get
            {
                return path.SimplifiedLoopSegments;
            }
        }

        public static int _NextID = 0;
        public int ID;

        private void AssignID()
        {
            this.ID = _NextID;
            _NextID++;
        }

        /// <summary>
        /// This constructor is used for test cases
        /// </summary>
        /// <param name="simplifiedPathToleranceInPixels"></param>
        internal PenInputHelper(double simplifiedPathToleranceInPixels = 4.0)
        {
            AssignID();
            PenIsComplete = false;
            NumCurveInterpolations = Geometry.Global.NumCurveInterpolationPoints(false);
        }

        public PenInputHelper(Viking.UI.Controls.SectionViewerControl Parent, double simplifiedPathToleranceInPixels = 4.0) : this()
        {
            this.Parent = Parent;

            Parent.MouseMove += this.OnMouseMove;
            Parent.MouseUp += this.OnMouseUp;
            Parent.Camera.PropertyChanged += this.OnCameraPropertyChanged;

            Parent.OnPenEnterRange += OnPenEnterRange;
            Parent.OnPenLeaveRange += OnPenLeaveRange;
            Parent.OnPenContact += OnPenContact;
            Parent.OnPenLeaveContact += OnPenLeaveContact;
            Parent.OnPenMove += OnPenMove;

            this.SimplifiedPathToleranceInPixels = simplifiedPathToleranceInPixels;
            if(Global.TracePenEvents)
                System.Diagnostics.Trace.WriteLine(string.Format("PenInputHelper {0} Subscribed to events", this.ID));
        }

        public void UnsubscribeEvents()
        {
            Parent.MouseMove -= this.OnMouseMove;
            Parent.MouseUp -= this.OnMouseUp;
            Parent.Camera.PropertyChanged -= this.OnCameraPropertyChanged;

            Parent.OnPenEnterRange -= OnPenEnterRange;
            Parent.OnPenLeaveRange -= OnPenLeaveRange;
            Parent.OnPenContact -= OnPenContact;
            Parent.OnPenLeaveContact -= OnPenLeaveContact;
            Parent.OnPenMove -= OnPenMove;

            if (Global.TracePenEvents)
                System.Diagnostics.Trace.WriteLine(string.Format("PenInputHelper {0} Unsubscribed from events", this.ID));
        }

        public void Push(GridVector2 p)
        {
            path.Push(p);

            //Make sure we have the right number of segments for points in the path
            System.Diagnostics.Debug.Assert(Segments.Count == this.Points.Count - 1);
        }

        public GridVector2 Pop()
        {
            GridVector2 p = path.Pop();

            //Make sure we have the right number of segments for points in the path
            System.Diagnostics.Debug.Assert(Segments.Count == this.Points.Count - 1);
            return p;
        }

        public GridVector2 Peek()
        {
            return this.path.Peek();
        }

        public bool HasSelfIntersection
        {
            get
            {
                return path.HasSelfIntersection;
            }
        }

        private void FireOnProposedNextSegmentChanged(GridLineSegment? line)
        {
            if (this.OnProposedNextSegmentChanged != null)
            {
                this.OnProposedNextSegmentChanged(this, line);
            }
        }

        private void OnCameraPropertyChanged(object sender, System.ComponentModel.PropertyChangedEventArgs args)
        {
            if (args.PropertyName == "Downsample")
            {

                this.path.SimplifiedPathTolerance = Parent.Camera.Downsample * SimplifiedPathToleranceInPixels; //Adjust our tolerance to match the camera's downsample level times a multiplier that lets simplified path drift by almost imperceptable amounts
            }
        }

        private void OnMouseMove(object sender, MouseEventArgs e)
        {
            GridVector2 cursor_position = Parent.ScreenToWorld(e.X, e.Y);

            bool EraseButtonDown = e.Button.Middle(); ;

            //Don't report miniscule changes in distance
            if (LastPenPosition.HasValue && GridVector2.DistanceSquared(cursor_position, LastPenPosition) < Geometry.Global.EpsilonSquared)
            {
                return;
            }

            if (EraseButtonDown && Points.Count > 1)
            {
                GridLineSegment testLine = new GridLineSegment(this.LastPenPosition, cursor_position);
                EraseAlongLine(testLine);
                /*
                double delete_distance = Parent.Scene.Camera.Downsample * 20.0;
                
                int iDeletePoint = Points.FindIndex(v => GridVector2.Distance(v, cursor_position) < delete_distance);

                if (iDeletePoint >= 0)
                {
                    double distance = GridVector2.Distance(Points[iDeletePoint], cursor_position);
                    ApplyVertexAction(VERTEXACTION.CUT_ERASE, cursor_position);
                    if (distance > 0)
                    {
                        ApplyVertexAction(VERTEXACTION.ADD, cursor_position);
                    }
                }                 
                */
            }
            else if (e.Button.LeftOnly())
            {
                PathVertexAction vertex_action = GetActionForFullResolutionPath(cursor_position);
                ApplyVertexAction(vertex_action, cursor_position);
            }

            LastPenPosition = cursor_position;
        }

        /// <summary>
        /// Add, Remove, or Replace the current path
        /// </summary>
        /// <param name="action">ADD: Adds the input to the currrent path
        ///                      REPLACE: Replaces the newest vertex in the path with the input
        ///                      REMOVE: If there is input, finds the nearest path vertex and removes all verticies in the path placed after that vertex.  If there is no input, removes the most recently placed vertex/param>
        /// <param name="input"></param>
        public void ApplyVertexAction(PathVertexAction action, GridVector2? input)
        {
            switch (action)
            {
                case PathVertexAction.NONE:
                    break;
                case PathVertexAction.ADD:
                    if (this.CanControlPointBePlaced(input.Value))
                    {
                        this.Push(input.Value);
                        this.FireOnProposedNextSegmentChanged(this.ProposedNextSegment);
                    }
                    break;
                case PathVertexAction.REPLACE:
                    path.Replace(input.Value);
                    this.FireOnProposedNextSegmentChanged(this.ProposedNextSegment);
                    break;
                case PathVertexAction.REMOVE:
                    GridVector2 removed = this.Pop();
                    this.FireOnProposedNextSegmentChanged(this.ProposedNextSegment);
                    break;
                case PathVertexAction.CUT_ERASE:
                    bool Erased = path.Erase(input.Value);
                    if (Erased)
                    {
                        this.FireOnProposedNextSegmentChanged(this.ProposedNextSegment);
                    }
                    break;
                default:
                    throw new ArgumentException("Unexpected path action");
            }

            return;
        }

        private void OnMouseUp(object sender, MouseEventArgs e)
        {
            if (e.Button.LeftOnly())
            {
                if (Points.Count <= 1)
                    return;

                if (OnPathCompleted != null)
                {
                    OnPathCompleted(this, this.Points.ToArray());
                }
            }
        }

        protected virtual void OnPenEnterRange(object sender, PenEventArgs e)
        {
        }

        protected virtual void OnPenLeaveRange(object sender, PenEventArgs e)
        {
            if (IgnoringThisPenContact)
                return;

            if (OnPathCompleted != null)
                OnPathCompleted(this, this.Points.ToArray());
        }

        /// <summary>
        /// When the user is drawing an open curve, allow the user to click the path they have drawn to finalize it.
        /// Taps closer than the resume distance are ignored
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="e"></param>
        protected virtual void OnPenContact(object sender, PenEventArgs e)
        {
            if (Points.Count == 0)
            {
                IgnoringThisPenContact = false;
                return;
            }

            GridVector2 cursor_position = Parent.ScreenToWorld(e.X, e.Y);
            IgnoringThisPenContact = this.path.Distance(cursor_position) > MaxInteractDistanceInPixels(Parent.Camera.Downsample);

            if (IgnoringThisPenContact)
                return;

            //Now check whether the user clicked on an already drawn shape to cancel the command
            if (Points.Count <= 1)
            {
                //Do not complete a path without any segments
                return;
            }

            //Do not complete the path on an erase contact
            if (e.Erase)
                return;

            double distanceToEnd = GridVector2.Distance(Points.Last(), cursor_position);
            double distanceToStart = GridVector2.Distance(Points.First(), cursor_position);

            //Check that we are outside the resume distance, if not, check that we are closer to the start of the path than the end (For short path case)
            double resume_distance = ResumeDistanceInPixels(Parent.Camera.Downsample);

            if (distanceToEnd < resume_distance && distanceToStart > distanceToEnd)
                return;

            double complete_distance = CompletionDistanceInPixels(Parent.Camera.Downsample);

            bool pen_contacted_path = path.Segments.Any(seg => seg.DistanceToPoint(cursor_position) <= complete_distance && seg.IsNearestPointWithinLineSegment(cursor_position));
            if (pen_contacted_path)
            {
                OnPathCompleted(this, this.Points.ToArray());
            }
        }

        protected virtual void OnPenLeaveContact(object sender, PenEventArgs e)
        {
            MustCheckResumeDistance = true;
            LastPenPosition = new GridVector2?();
        }

        public static double MaxInteractDistanceInPixels(double downsample)
        {
            return downsample * interactDistance;
        }

        private static double ResumeDistanceInPixels(double downsample)
        {
            if (resumeDistance.HasValue)
                return downsample * resumeDistance.Value;

            return double.PositiveInfinity;
        }

        private static double EraseDistanceInPixels(double NormalizedPressure, double downsample)
        {
            double scalar = (minEraseDistance + ((maxEraseDistance - minEraseDistance) * NormalizedPressure));
            return downsample * scalar;
        }

        private static double CompletionDistanceInPixels(double downsample)
        {
            return downsample * completionDistance;
        }

        protected virtual void OnPenMove(object sender, PenEventArgs e)
        {
            if (IgnoringThisPenContact)
                return;

            if (!e.InContact)
                return;

            GridVector2 cursor_position = Parent.ScreenToWorld(e.X, e.Y);

            bool EraseActive = e.Erase && e.InContact;
            bool DrawActive = !e.Erase && e.InContact;

            //Don't report miniscule changes in distance
            if (LastPenPosition.HasValue && GridVector2.DistanceSquared(cursor_position, LastPenPosition) < Geometry.Global.EpsilonSquared)
            {
                return;
            }

            if (EraseActive && Points.Count > 1 && this.LastPenPosition.HasValue)
            {
                GridLineSegment testLine = new GridLineSegment(this.LastPenPosition, cursor_position);
                EraseAlongLine(testLine);
            }
            else if (DrawActive)
            {
                PathVertexAction vertex_action = GetActionForFullResolutionPath(cursor_position);
                ApplyVertexAction(vertex_action, cursor_position);
            }

            LastPenPosition = cursor_position;
        }

        /// <summary>
        /// Given a line, erase the first path segment the line crosses, measured from A to B
        /// </summary>
        /// <param name="eraseLine"></param>
        private void EraseAlongLine(GridLineSegment eraseLine)
        {
            List<GridLineSegment> intersections = eraseLine.Intersections(path.Segments, out GridVector2[] intersectionPoints);
            if (intersections.Any())
            {
                GridVector2 firstIntersection = intersectionPoints.OrderByDescending(p => eraseLine.DistanceToPoint(p)).First();
                int iFirstIntersection = intersectionPoints.ToList().IndexOf(firstIntersection);
                GridLineSegment deleteSegment = intersections[iFirstIntersection];
                int iDeleteSegment = path.Segments.ToList().IndexOf(deleteSegment);
                int iDeletePoint = iDeleteSegment + 1;

                if (iDeletePoint > 0)
                {
                    ApplyVertexAction(PathVertexAction.CUT_ERASE, deleteSegment.A);
                    ApplyVertexAction(PathVertexAction.ADD, firstIntersection);
                }

                /*

                //Sort to find the first line we crossed as we travelled from the previous cursor point to here
                intersections.OrderByDescending((i, seg) => testLine.DistanceToPoint(intersectionPoints[i]) / testLine.Length);
                var randomChoice = intersections.First(); //Todo: Should probably pick the first line we cross from the previous point...
                path.Segments.ToList().IndexOf(intersections);
                */
            }
        }

        public PathVertexAction GetActionForFullResolutionPath(GridVector2 pen_position)
        {
            if (Points.Count == 0)
                return PathVertexAction.ADD;

            double distanceToLast = GridVector2.Distance(pen_position, Points.Last());

            //Check the resume distance until we succeed, prevents user from drawing geometry across the screen trying to click a button.
            if (MustCheckResumeDistance)
            {
                if (distanceToLast > ResumeDistanceInPixels(Parent.Camera.Downsample))
                    return PathVertexAction.NONE;

                MustCheckResumeDistance = false;
            }


            //if(HasPenTravelledAwayFromLastControlPoint && distanceToLast < ControlPointSelectionRadius)
            //{
            //    return VERTEXACTION.REMOVE;
            //}


            if (distanceToLast >= this.PointIntervalOnDrag)
            {
                if (pen_position != Points.Last())
                    return PathVertexAction.ADD;
            }

            return PathVertexAction.NONE;
        }

        protected CurveViewControlPoints AppendProposedPointToPathCurve(GridVector2 worldPos)
        {
            List<GridVector2> listControlPoints = new List<GridVector2>(this.Points);
            listControlPoints.Add(worldPos);
            return new CurveViewControlPoints(listControlPoints, this.NumCurveInterpolations, TryToClose: false);
        }

        /// <summary>
        /// Return true if a line to the world position from the last vertex will intersect our curve
        /// </summary>
        /// <param name="worldPos"></param>
        /// <returns></returns>
        protected GridVector2? ProposedControlPointSelfIntersection(GridVector2 worldPos)
        {
            GridVector2? retval = new GridVector2?();

            if (this.Points.Count < 3)
                return retval;

            if (worldPos != Peek())
            {
                CurveViewControlPoints curveVerticies = AppendProposedPointToPathCurve(worldPos);
                GridVector2[] controlPoints = this.Points.ToArray();
                GridLineSegment[] proposed_back_curve_segments = GridLineSegment.SegmentsFromPoints(curveVerticies.CurvePointsBetweenControlPoints(controlPoints.First(), worldPos));
                GridLineSegment[] proposed_front_curve_segments = GridLineSegment.SegmentsFromPoints(curveVerticies.CurvePointsBetweenControlPoints(worldPos, controlPoints.Last()));
                GridLineSegment[] existing_curve_segments = GridLineSegment.SegmentsFromPoints(curveVerticies.CurvePointsBetweenControlPoints(controlPoints.Last(), controlPoints.First()));

                proposed_front_curve_segments = proposed_front_curve_segments.ShortenLastVertex();
                existing_curve_segments = existing_curve_segments.ShortenLastVertex();

                GridVector2[] intersections = proposed_front_curve_segments.Select(pcs => existing_curve_segments.IntersectionPoint(pcs, false)).Where(p => p.HasValue).Select(p => p.Value).ToArray();
                if (intersections.Length > 0)
                {
                    retval = intersections.First();
                    return retval;
                }

                intersections = proposed_back_curve_segments.Select(pcs => existing_curve_segments.IntersectionPoint(pcs, false)).Where(p => p.HasValue).Select(p => p.Value).ToArray();
                if (intersections.Length > 0)
                {
                    retval = intersections.First();
                    return retval;
                }
            }

            return retval;
        }

        public void Clear()
        {
            path.Clear();
            this.FireOnProposedNextSegmentChanged(this.ProposedNextSegment);
        }



    }

}
