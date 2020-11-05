using Geometry;
using SqlGeometryUtils;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Collections.Specialized;
using System.ComponentModel;
using System.Windows.Forms;
using Viking.UI;
using VikingXNAGraphics;
using VikingXNAWinForms;

namespace WebAnnotation.UI.Commands
{

    class PlaceClosedCurveWithPenCommand : PlaceGeometryWithPenCommandBase
    {
        public override LineStyle Style
        {
            get
            {
                return LineStyle.HalfTube;
            }
        }

        public override uint NumCurveInterpolations
        {
            get
            {
                return Geometry.Global.NumClosedCurveInterpolationPoints;
            }
        }

        public float PointIntervalOnDrag
        {
            get
            {
                return 90;
            }
        }

        public float PenAngleThreshold
        {
            get
            {
                return .3f;
            }
        }

        public PlaceClosedCurveWithPenCommand(Viking.UI.Controls.SectionViewerControl parent,
                                        Microsoft.Xna.Framework.Color color,
                                        GridVector2 origin,
                                        double LineWidth,
                                        OnCommandSuccess success_callback)
            : base(parent, color, origin, LineWidth, success_callback)
        {
        }

        public PlaceClosedCurveWithPenCommand(Viking.UI.Controls.SectionViewerControl parent,
                                        System.Drawing.Color color,
                                        GridVector2 origin,
                                        double LineWidth,
                                        OnCommandSuccess success_callback)
            : base(parent, color, origin, LineWidth, success_callback)
        {
        }


        protected override void OnPathLoop(object sender, bool HasLoop)
        {
            if (HasLoop)
            {
                if (IsProposedClosedLoopValid(PenInput.SimplifiedFirstLoop))
                {
                    this.Execute(PenInput.SimplifiedFirstLoop);
                }
            }
        }



        protected virtual bool IsProposedClosedLoopValid(IReadOnlyCollection<GridVector2> proposed_curve)
        {
            return true;
        }


        protected override void OnPenProposedNextSegmentChanged(object sender, GridLineSegment? segment)
        {

        }


        protected override void OnPenPathComplete(object sender, GridVector2[] Path)
        {

        }

        /// <summary>
        /// Can the command be completed by clicking this point?
        /// </summary>
        /// <param name="WorldPos"></param>
        /// <returns></returns>
        protected override bool CanCommandComplete()
        {
            return this.PenInput.HasSelfIntersection;
        }

        protected override bool ShapeIsValid()
        {
            if (this.PenInput.Points.Count < 3 || this.PenInput.HasSelfIntersection == false)
                return false;

            try
            {
                return this.PenInput.Loop.ToPolygon().STIsValid().IsTrue;
            }
            catch (ArgumentException e)
            {
                return false;
            }
        }
    }

    class PlaceOpenCurveWithPenCommand : PlaceGeometryWithPenCommandBase
    {
        public override LineStyle Style
        {
            get
            {
                return LineStyle.Tubular;
            }
        }

        public override uint NumCurveInterpolations
        {
            get
            {
                return Geometry.Global.NumOpenCurveInterpolationPoints;
            }
        }

        public PlaceOpenCurveWithPenCommand(Viking.UI.Controls.SectionViewerControl parent,
                                        Microsoft.Xna.Framework.Color color,
                                        GridVector2 origin,
                                        double LineWidth,
                                        OnCommandSuccess success_callback)
            : base(parent, color, origin, LineWidth, success_callback)
        {
        }

        public PlaceOpenCurveWithPenCommand(Viking.UI.Controls.SectionViewerControl parent,
                                        System.Drawing.Color color,
                                        GridVector2 origin,
                                        double LineWidth,
                                        OnCommandSuccess success_callback)
            : base(parent, color, origin, LineWidth, success_callback)
        {
        }

        protected override void OnPathLoop(object sender, bool HasLoop)
        {
            //If the path loops it is not an open curve and we are in an invalid state
            this.PathView.Color = HasLoop ? Microsoft.Xna.Framework.Color.Magenta : this.OriginalColor;
            return;
        }

        protected override void OnMouseDown(object sender, MouseEventArgs e)
        {
            PenInput.Points.Clear();
            PenInput.Push(Parent.ScreenToWorld(e.X, e.Y));
            base.OnMouseDown(sender, e);
        }

        protected override void OnMouseUp(object sender, MouseEventArgs e)
        {
            if (PenInput.Points.Count < 2)
            {
                return;
            }

            //Simplify the curve and execute the command
            Execute(PenInput.SimplifiedPath);
            base.OnMouseUp(sender, e);
        }

        protected override void OnPenProposedNextSegmentChanged(object sender, GridLineSegment? segment)
        {

        }

        protected override void OnPenPathComplete(object sender, GridVector2[] Path)
        {
        }

        /// <summary>
        /// Can the command be completed by clicking this point?
        /// </summary>
        /// <param name="WorldPos"></param>
        /// <returns></returns>
        protected override bool CanCommandComplete()
        {
            //We cannot create an open curve if the path has a self-intersection
            return PenInput.HasSelfIntersection == false;
        }


        protected override bool ShapeIsValid()
        {
            if (this.PenInput.Points.Count < 2)
                return false;

            if (PenInput.HasSelfIntersection)
                return false;

            return this.PenInput.Points.ToSqlGeometry().STIsValid().IsTrue;
        }
    }



    /// <summary>
    /// Left-click once to create a new vertex in the poly line
    /// Left-click an existing vertex to complete polyline creation
    /// Double left-click to complete polyline creation
    /// Right-click to remove the last polyline vertex
    /// </summary> 
    abstract class PlaceGeometryWithPenCommandBase : LineGeometryCommandBase, Viking.Common.IHelpStrings, Viking.Common.IObservableHelpStrings
    {
        public abstract uint NumCurveInterpolations
        {
            get;
        }

        public override double LineWidth
        {
            get
            {
                // return this.PathView == null ? Global.DefaultClosedLineWidth : this.PenInput.Points.MinDistanceBetweenSequentialPoints(out int FirstIndex);
                return this.PathView.LineWidth;
            }
        }

        /// <summary>
        /// Used for debugging when we want to show control points
        /// </summary>
        public virtual double ControlPointRadius
        {
            get
            {
                return this.LineWidth / 2.0;
            }
        }

        public ObservableCollection<string> ObservableHelpStrings
        {
            get
            {
                return new ObservableCollection<string>(this.HelpStrings);
            }
        }


        public string[] HelpStrings
        {
            get
            {
                List<string> s = new List<string>();

                s.AddRange(PlaceCurveCommand.DefaultMouseHelpStrings);
                s.AddRange(PlaceCurveCommand.DefaultKeyHelpStrings);

                return s.ToArray();
            }

        }

        public new static string[] DefaultMouseHelpStrings = new String[] {
            "Double Left Click: Place final control point, save and exit command",
            "Double Right Click: Pop last control point",
            "Left Click and Drag Control Point: Move existing control point",
            "Left Click last control point: Save and exit command",
            "No cursor: Command cannot be completed at this location due to invalid geometry. Typically crossed lines."
            };

        public new static string[] DefaultKeyHelpStrings = new String[] {
            "Escape Key: Cancel command",
            "Page up/down key: Change Magnification",
            "Arrow key: Move view",
            "Home key: Round magnification to whole number"
            };

        //protected List<GridVector2> vert_stack = new List<GridVector2>();
        public Viking.UI.PenInputHelper PenInput;

        protected PolyLineView PathView;

        public Path Path => PenInput.path;

        public PlaceGeometryWithPenCommandBase(Viking.UI.Controls.SectionViewerControl parent,
                                        Microsoft.Xna.Framework.Color color,
                                        double LineWidth,
                                        OnCommandSuccess success_callback)
            : base(parent, color, LineWidth, success_callback)
        {
            PathView = new PolyLineView(color, lineWidth: LineWidth, lineStyle: LineStyle.Tubular);
#if DEBUG
            PathView.ShowControlPoints = false;
#else
            PathView.ShowControlPoints = false;
#endif

            parent.Cursor = Cursors.Cross;
            PenInput = new Viking.UI.PenInputHelper(parent);
            //Ensure any pen subscriptions are released in the OnDeactivate call
            System.Diagnostics.Trace.WriteLine(string.Format("PlaceCurveWithPenCommand {0} Subscribed to events", this.ID));
            PenInput.OnPathChanged += this.OnPenPathChanged;
            PenInput.OnPathCompleted += this.OnPenPathComplete;
            PenInput.OnProposedNextSegmentChanged += this.OnPenProposedNextSegmentChanged;
            PenInput.OnPathLoop += this.OnPathLoop;
            this.success_callback = success_callback;

            SetPathViewForDownsample(Parent.Camera.Downsample);
        }

        /// <summary>
        /// Used to initialize the path for the command
        /// </summary>
        /// <param name="path"></param>
        public virtual void InitPath(IReadOnlyCollection<GridVector2> path)
        {
            if (PenInput.path.Points.Count > 0)
            {
                throw new ArgumentException("Path initialized with an existing path in place.");
            }

            foreach (GridVector2 p in path)
            {
                PenInput.Push(p);
                PathView.Add(p);
            }
        }

        public PlaceGeometryWithPenCommandBase(Viking.UI.Controls.SectionViewerControl parent,
                                        Microsoft.Xna.Framework.Color color,
                                        GridVector2 origin,
                                        double LineWidth,
                                        OnCommandSuccess success_callback)
            : this(parent, color, LineWidth, success_callback)
        {
            PenInput.Push(origin);
            PathView.Add(origin);
        }

        public PlaceGeometryWithPenCommandBase(Viking.UI.Controls.SectionViewerControl parent,
                                        System.Drawing.Color color,
                                        GridVector2 origin,
                                        double LineWidth,
                                        OnCommandSuccess success_callback)
            : this(parent,
                    color.ToXNAColor(),
                    origin,
                    LineWidth,
                    success_callback)
        {
        }

        protected override void Execute()
        {
            this.Execute(this.PenInput.SimplifiedPath);
        }

        protected override void OnDeactivate()
        {
            System.Diagnostics.Trace.WriteLine(string.Format("PlaceCurveWithPenCommand {0} Unubscribed to events", this.ID));
            PenInput.OnPathChanged -= this.OnPenPathChanged;
            PenInput.OnPathCompleted -= this.OnPenPathComplete;
            PenInput.OnProposedNextSegmentChanged -= this.OnPenProposedNextSegmentChanged;
            PenInput.OnPathLoop -= this.OnPathLoop;
            this.PenInput.UnsubscribeEvents();
            this.PenInput = null;
            base.OnDeactivate();
        }

        virtual protected void OnPenPathChanged(object sender, System.Collections.Specialized.NotifyCollectionChangedEventArgs e)
        {
            //Update the view of the path
            switch (e.Action)
            {
                case NotifyCollectionChangedAction.Add:
                    this.PathView.Add(this.PenInput.Peek());
                    break;
                case NotifyCollectionChangedAction.Remove:
                    //Pop off as many items that were removed
                    foreach (object p in e.OldItems)
                    {
                        this.PathView.Remove();
                        //System.Diagnostics.Debug.Assert(this.PathView.ControlPoints.Last() == PenInput.Points.First());
                    }

                    break;
                case NotifyCollectionChangedAction.Move:
                    this.PathView.Remove();
                    this.PathView.Add(this.PenInput.Peek());
                    break;
                case NotifyCollectionChangedAction.Reset:
                    this.PathView.ControlPoints = new GridVector2[0];
                    break;
                default:
                    this.PathView.ControlPoints = this.PenInput.Points;
                    break;
            }

            this.Parent.Invalidate();
        }

        private void SetPathViewForDownsample(double Downsample)
        {
            this.PathView.LineWidth = Downsample * PenInput.SimplifiedPathToleranceInPixels;
            this.PathView.ControlPointRadius = this.PathView.LineWidth / 2.0f;
            this.PathView.DashLength = (float)(Downsample * PenInput.SimplifiedPathToleranceInPixels * 2.0f);
        }

        protected override void OnCameraChanged(object sender, PropertyChangedEventArgs e)
        {
            if (e.PropertyName == "Downsample")
            {
                SetPathViewForDownsample(Parent.Camera.Downsample);
            }

            base.OnCameraChanged(sender, e);
        }

        abstract protected void OnPenPathComplete(object sender, GridVector2[] Path);

        abstract protected void OnPenProposedNextSegmentChanged(object sender, GridLineSegment? segment);

        abstract protected void OnPathLoop(object sender, bool HasLoop);

        protected abstract bool ShapeIsValid();

        protected abstract bool CanCommandComplete();

        protected override void OnPenMove(object sender, PenEventArgs e)
        {
            //Passing down erase move events will translate the view.  Make sure the pen was placed enough far away from the path that the pen input helper will not process the event.
            if (PenInput != null && PenInput.IgnoringThisPenContact)
            {
                base.OnPenMove(sender, e);
            }
            else
            {
                GridVector2 NewPosition = Parent.ScreenToWorld(e.X, e.Y);
                this.Parent.StatusPosition = NewPosition;
                SaveAsOldPenPosition(e);
            }
        }

        public override void OnDraw(Microsoft.Xna.Framework.Graphics.GraphicsDevice graphicsDevice, VikingXNA.Scene scene, Microsoft.Xna.Framework.Graphics.BasicEffect basicEffect)
        {
            PolyLineView.Draw(graphicsDevice, scene, OverlayStyle.Luma, new PolyLineView[] { this.PathView });

#if DEBUG
            if (PenInput.ProposedNextSegment.HasValue)
            {
                LineView unofficialPath = new LineView(PenInput.ProposedNextSegment.Value, width: this.LineWidth, color: this.LineColor, lineStyle: LineStyle.Standard);
                LineView.Draw(graphicsDevice, scene, Parent.LumaOverlayLineManager, new LineView[] { unofficialPath });
            }
#endif
        }
    }
}
