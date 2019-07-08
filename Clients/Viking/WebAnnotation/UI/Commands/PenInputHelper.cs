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
        private double lastAngle;
        private double PointIntervalOnDrag;
        private double PenAngleThreshold;
        public bool PenIsComplete;
        Viking.UI.Controls.SectionViewerControl Parent;
        // GridVector2[] Verticies;

        public bool CanPathSelfIntersect = false;

        public delegate void OnPathCompleteEventHandler(object sender, GridVector2[] Path);
        public event OnPathCompleteEventHandler OnPathCompleted;

        public event System.Collections.Specialized.NotifyCollectionChangedEventHandler OnPathChanged;

        public delegate void OnProposedNextSegmentChangedHandler(object sender, GridLineSegment? segment);
        public event OnProposedNextSegmentChangedHandler OnProposedNextSegmentChanged;
          
        public List<GridVector2> Path = new List<GridVector2>();

        public List<GridVector2> SimplifiedPath = new List<GridVector2>();

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

        public PenInputHelper(Viking.UI.Controls.SectionViewerControl Parent)
        {
            lastAngle = 0;
            PenIsComplete = false;
            PointIntervalOnDrag = 6; //Parent.Downsample * 16;
            PenAngleThreshold = Math.PI / 36;//.36f;
            this.Parent = Parent;
            NumCurveInterpolations = Global.NumCurveInterpolationPoints(false);

            Parent.MouseMove += this.OnMouseMove;
            Parent.MouseUp += this.OnMouseUp;
        }

        public void UnsubscribeEvents()
        {
            Parent.MouseMove -= this.OnMouseMove;
            Parent.MouseUp -= this.OnMouseUp;
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
            if(e.Button.Middle())
            {
                GridVector2 cursor_position = Parent.ScreenToWorld(e.X, e.Y);
                LastPenPosition = cursor_position;
                if(GridVector2.Distance(Path[0], cursor_position) < 20)
                {
                    Pop();
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
            double ControlPointSelectionRadius = this.PointIntervalOnDrag / 4.0;
            HasPenTravelledAwayFromLastControlPoint |= distanceToLast > ControlPointSelectionRadius;

            if(HasPenTravelledAwayFromLastControlPoint && distanceToLast < ControlPointSelectionRadius)
            {
                return VERTEXACTION.REMOVE;
            }

            if (Path.Count == 1)
            {
                
                if(distanceToLast >= this.PointIntervalOnDrag)
                {
                    return VERTEXACTION.ADD;
                }
            }
            else
            {
                if (distanceToLast >= this.PointIntervalOnDrag)
                {
                    return VERTEXACTION.ADD;
                }
            }

            return VERTEXACTION.NONE;
        }

        private bool HasPenTravelledAwayFromLastControlPoint = false; 

        //Returns whether the next vertex will be added, removed, or replaced.
        public VERTEXACTION GetNextVertex(GridVector2 cursor_position)
        {
            if (Path.Count == 0)
                return VERTEXACTION.ADD;

            if(CanPathSelfIntersect == true)
            {
                GridVector2? SelfIntersectionPoint = this.ProposedControlPointSelfIntersection(cursor_position);
                if(SelfIntersectionPoint.HasValue)
                {
                    return VERTEXACTION.NONE;
                }
            }

            double distanceToLast = GridVector2.Distance(cursor_position, Path[0]);

            //Creates a new verticie when the mouse moves set distance away
            if (distanceToLast > this.PointIntervalOnDrag)
            {
                //double angle;

                //Measure the slope between the two most recent vertices
                //if (Path.Count >= 3)
                {
                    //angle = GridVector2.ArcAngle(Path[0], cursor_position, Path[1]);

                    //if (Math.Abs(angle) >= PenAngleThreshold)
                    {
                        //Remove the last vertex
                        //return VERTEXACTION.REPLACE;
                        //HasPenTravelledAwayFromLastControlPoint = false;
                       // return VERTEXACTION.ADD;

                    }

                    //Set new slope to be between this point and the NEW last vertice (should be the one that came before the one we just removed)
                    //lastAngle = GridVector2.ArcAngle(Path[0], cursor_position, Path[1]);
                }
                //else if(distanceToLast > this.PointIntervalOnDrag)
                {
                   // return VERTEXACTION.ADD;
                }

                return VERTEXACTION.ADD;
                //this.PushVertex(cursor_position);
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

    }

}
