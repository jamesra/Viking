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

namespace WebAnnotation.UI.Commands
{
    enum VERTEXACTION
    {
        REMOVE,
        REPLACE,
        NONE,
        ADD
    };

    class PenInputHelper
    {
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
        /// The user has stopped drawing
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="Path"></param>
        public delegate void OnPathCompleteEventHandler(object sender, GridVector2[] Path);
        public event OnPathCompleteEventHandler OnPathCompleted;

        public event System.Collections.Specialized.NotifyCollectionChangedEventHandler OnPathChanged;

        /// <summary>
        /// The path has been extended
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="segment"></param>
        public delegate void OnProposedNextSegmentChangedHandler(object sender, GridLineSegment? segment);
        public event OnProposedNextSegmentChangedHandler OnProposedNextSegmentChanged;

        /// <summary>
        /// The path has self-intersected itself
        /// </summary>
        //public delegate void OnPathClosedHandler(object sender, GridLineSegment? segment);
        //public event OnPathClosedHandler OnPathClosed;

        public List<GridVector2> Path = new List<GridVector2>();

        public List<GridVector2> SimplifiedPath = new List<GridVector2>();

        public GridVector2 cursor_position;

        public GridVector2? LastPenPosition;

        public uint NumCurveInterpolations
        {
            get;
        }
        
        public GridLineSegment LastSegment
        {
            get
            {
                int count = Path.Count;
                return new GridLineSegment(Path[0], Path[1]);
            }
        }

        public GridLineSegment? ProposedNextSegment
        {
            get
            {
                if (LastPenPosition.HasValue && LastPenPosition.Value != Path[0])
                {
                    return new GridLineSegment(Path[0], LastPenPosition.Value);
                }

                return new GridLineSegment?();
            }
        }

        public GridLineSegment[] Segments
        {
            get
            {
                return Path.ToLineSegments();
            }
        }

        public static int _NextID = 0;
        public int ID;

        private void AssignID()
        {
            this.ID = _NextID;
            _NextID = _NextID + 1;
        }


        public PenInputHelper(Viking.UI.Controls.SectionViewerControl Parent)
        {
            AssignID();
            PenIsComplete = false;
            
            this.Parent = Parent;
            NumCurveInterpolations = Global.NumCurveInterpolationPoints(false);

            Parent.MouseMove += this.OnMouseMove;
            Parent.MouseUp += this.OnMouseUp;
            System.Diagnostics.Trace.WriteLine(string.Format("PenInputHelper {0} Subscribed to events", this.ID));
        }

        public void UnsubscribeEvents()
        {
            Parent.MouseMove -= this.OnMouseMove;
            Parent.MouseUp -= this.OnMouseUp;
            System.Diagnostics.Trace.WriteLine(string.Format("PenInputHelper {0} Unsubscribed from events", this.ID));
        }

        private bool CanControlPointBePlaced(GridVector2 position)
        {
            return true;
        }

        public void Push(GridVector2 p)
        {
            this.Path.Insert(0, p);
        }

        public GridVector2 Pop()
        {
            GridVector2 p = this.Path.First();
            this.Path.RemoveAt(0);
            return p;
        }

        public GridVector2 Peek()
        {
            return this.Path.First();
        }

        private void FireOnPathChangedEvent(NotifyCollectionChangedEventArgs e)
        {
            if (this.OnPathChanged != null)
            {
                this.OnPathChanged(this, e);
            }
        }

        private void FireOnProposedNextSegmentChanged(GridLineSegment? line)
        {
            if (this.OnProposedNextSegmentChanged != null)
            {
                this.OnProposedNextSegmentChanged(this, line);
            }
        }

        public void OnMouseMove(object sender, MouseEventArgs e)
        {
            this.cursor_position = Parent.ScreenToWorld(e.X, e.Y);

            if (e.Button.Middle() && Path.Count > 1)
            {
                LastPenPosition = cursor_position;

                double delete_distance = Parent.Scene.Camera.Downsample * 20.0;
                /*
                bool PathChanged = false;
                while(Path.Count > 1)
                {
                    GridVector2 top = Path[0];
                    
                    if (GridVector2.Distance(top, cursor_position) < delete_distance)
                    {
                        PathChanged = true;
                        Path.RemoveAt(0);
                    }
                    else
                    {
                        break;
                    }
                }
                */
                int iDeletePoint = Path.FindIndex(v => GridVector2.Distance(v, cursor_position) < delete_distance);

                if (iDeletePoint >= 0)
                {
                    Path.RemoveRange(0, iDeletePoint + 1);
                    FireOnPathChangedEvent(new NotifyCollectionChangedEventArgs(NotifyCollectionChangedAction.Add, cursor_position, 0));
                }                

            }
            else if (e.Button.LeftOnly())
            {
                GridVector2 cursor_position = Parent.ScreenToWorld(e.X, e.Y);
                LastPenPosition = cursor_position;
                VERTEXACTION placeVertex = GetActionForFullResolutionPath(cursor_position);
                if (CanControlPointBePlaced(cursor_position))
                {
                    if (placeVertex == VERTEXACTION.REPLACE)
                    {
                        GridVector2 oldValue = this.Pop();

                        this.Push(cursor_position);
                        this.FireOnProposedNextSegmentChanged(this.ProposedNextSegment);
                        //FireOnPathChangedEvent(new NotifyCollectionChangedEventArgs(NotifyCollectionChangedAction.Replace, cursor_position, oldValue, 0));
                    }
                    else if (placeVertex == VERTEXACTION.REMOVE)
                    {
                        GridVector2 removed = this.Pop();
                        FireOnPathChangedEvent(new NotifyCollectionChangedEventArgs(NotifyCollectionChangedAction.Remove, removed, 0));
                        this.FireOnProposedNextSegmentChanged(this.ProposedNextSegment);
                    }
                    else if (placeVertex == VERTEXACTION.ADD)
                    {
                        this.Push(cursor_position);
                        FireOnPathChangedEvent(new NotifyCollectionChangedEventArgs(NotifyCollectionChangedAction.Add, cursor_position, 0));
                        this.FireOnProposedNextSegmentChanged(this.ProposedNextSegment);
                    }
                }
            }
        }

        public void OnMouseUp(object sender, MouseEventArgs e)
        {
            if(e.Button.LeftOnly())
            {
                if(Path[0] == Path[Path.Count - 1] && OnPathCompleted != null)
                {
                    OnPathCompleted(this, this.Path.ToArray());
                }
            }
        }

        public VERTEXACTION GetActionForFullResolutionPath(GridVector2 pen_position)
        {
            
            if (Path.Count == 0)
                return VERTEXACTION.ADD;

            double distanceToLast = GridVector2.Distance(pen_position, Path[0]);
            

            //if(HasPenTravelledAwayFromLastControlPoint && distanceToLast < ControlPointSelectionRadius)
            //{
            //    return VERTEXACTION.REMOVE;
            //}

            
            if (distanceToLast >= this.PointIntervalOnDrag)
            {
                if(pen_position != Path[0])
                    return VERTEXACTION.ADD;
            }

            return VERTEXACTION.NONE;
        }

        protected CurveViewControlPoints AppendProposedPointToPathCurve(GridVector2 worldPos)
        {
            List<GridVector2> listControlPoints = new List<GridVector2>(this.Path);
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

            if (this.Path.Count < 3)
                return retval;

            if (worldPos != Peek())
            {
                CurveViewControlPoints curveVerticies = AppendProposedPointToPathCurve(worldPos);
                GridVector2[] controlPoints = this.Path.ToArray();
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
            Path = new List<GridVector2>();

        }

    }

}
