using Geometry;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows.Forms;
using VikingXNAWinForms;
using System.Collections.Specialized;
using VikingXNAGraphics;

namespace Viking.UI
{
    public enum VERTEXACTION
    {
        REMOVE, //Remove the newest vertex
        REPLACE, //Replace the newest vertex
        NONE,
        ADD, //Append a new vertex
        CUT_ERASE //Remove all verticies after a vertex
    };

    public class PenInputHelper:  System.Collections.Specialized.INotifyCollectionChanged
    {
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

        
        private double _SimplifiedPathToleranceInPixels;
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
                if(value != _SimplifiedPathToleranceInPixels)
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
                return new GridLineSegment(Points[0], Points[1]);
            }
        }

        public GridLineSegment? ProposedNextSegment
        {
            get
            {
                if (LastPenPosition.HasValue && LastPenPosition.Value != Points[0])
                {
                    return new GridLineSegment(Points[0], LastPenPosition.Value);
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
            _NextID = _NextID + 1;
        }


        public PenInputHelper(Viking.UI.Controls.SectionViewerControl Parent, double simplifiedPathToleranceInPixels = 4.0)
        {
            AssignID();
            PenIsComplete = false;
            
            this.Parent = Parent;
            NumCurveInterpolations = Geometry.Global.NumCurveInterpolationPoints(false);

            Parent.MouseMove += this.OnMouseMove;
            Parent.MouseUp += this.OnMouseUp;
            Parent.Camera.PropertyChanged += this.OnCameraPropertyChanged;

            this.SimplifiedPathToleranceInPixels = simplifiedPathToleranceInPixels;
            System.Diagnostics.Trace.WriteLine(string.Format("PenInputHelper {0} Subscribed to events", this.ID));

        }

        public void UnsubscribeEvents()
        {
            Parent.MouseMove -= this.OnMouseMove;
            Parent.MouseUp -= this.OnMouseUp;
            Parent.Camera.PropertyChanged -= this.OnCameraPropertyChanged;
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
            if(args.PropertyName == "Downsample")
            {
                
                this.path.SimplifiedPathTolerance = Parent.Camera.Downsample * SimplifiedPathToleranceInPixels; //Adjust our tolerance to match the camera's downsample level times a multiplier that lets simplified path drift by almost imperceptable amounts
            }
        }

        private void OnMouseMove(object sender, MouseEventArgs e)
        {
            GridVector2 cursor_position = Parent.ScreenToWorld(e.X, e.Y);

            //Don't report miniscule changes in distance
            if (LastPenPosition.HasValue && GridVector2.DistanceSquared(cursor_position, LastPenPosition) < Geometry.Global.EpsilonSquared)
            {
                return;
            }

            LastPenPosition = cursor_position;

            if (e.Button.Middle() && Points.Count > 1)
            {
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
            }
            else if (e.Button.LeftOnly())
            {
                VERTEXACTION vertex_action = GetActionForFullResolutionPath(cursor_position);
                ApplyVertexAction(vertex_action, cursor_position);
            }
        }

        /// <summary>
        /// Add, Remove, or Replace the current path
        /// </summary>
        /// <param name="action">ADD: Adds the input to the currrent path
        ///                      REPLACE: Replaces the newest vertex in the path with the input
        ///                      REMOVE: If there is input, finds the nearest path vertex and removes all verticies in the path placed after that vertex.  If there is no input, removes the most recently placed vertex/param>
        /// <param name="input"></param>
        public void ApplyVertexAction(VERTEXACTION action, GridVector2? input)
        {
            switch (action)
            {
                case VERTEXACTION.NONE:
                    break;
                case VERTEXACTION.ADD:
                    if (this.CanControlPointBePlaced(input.Value))
                    {
                        this.Push(input.Value);
                        this.FireOnProposedNextSegmentChanged(this.ProposedNextSegment);
                    }
                    break;
                case VERTEXACTION.REPLACE:
                    path.Replace(input.Value);
                    this.FireOnProposedNextSegmentChanged(this.ProposedNextSegment);
                    break;
                case VERTEXACTION.REMOVE:
                    GridVector2 removed = this.Pop();
                    this.FireOnProposedNextSegmentChanged(this.ProposedNextSegment);
                    break;
                case VERTEXACTION.CUT_ERASE:
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
            if(e.Button.LeftOnly())
            {
                if(Points[0] == Points[Points.Count - 1] && OnPathCompleted != null)
                {
                    OnPathCompleted(this, this.Points.ToArray());
                }
            }
        }

        public VERTEXACTION GetActionForFullResolutionPath(GridVector2 pen_position)
        {
            
            if (Points.Count == 0)
                return VERTEXACTION.ADD;

            double distanceToLast = GridVector2.Distance(pen_position, Points[0]);
            

            //if(HasPenTravelledAwayFromLastControlPoint && distanceToLast < ControlPointSelectionRadius)
            //{
            //    return VERTEXACTION.REMOVE;
            //}

            
            if (distanceToLast >= this.PointIntervalOnDrag)
            {
                if(pen_position != Points[0])
                    return VERTEXACTION.ADD;
            }

            return VERTEXACTION.NONE;
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
                GridLineSegment[] proposed_back_curve_segments = GridLineSegment.SegmentsFromPoints(curveVerticies.CurvePointsBetweenControlPoints(controlPoints.Last(), worldPos));
                GridLineSegment[] proposed_front_curve_segments = GridLineSegment.SegmentsFromPoints(curveVerticies.CurvePointsBetweenControlPoints(worldPos, controlPoints[0]));
                GridLineSegment[] existing_curve_segments = GridLineSegment.SegmentsFromPoints(curveVerticies.CurvePointsBetweenControlPoints(controlPoints[0], controlPoints.Last()));

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
