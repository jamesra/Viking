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
    internal class PlaceClosedCurveWithPenCommand : PlaceGeometryWithPenCommandBase
    {
        public override LineStyle Style => LineStyle.HalfTube;

        public override uint NumCurveInterpolations => Geometry.Global.NumClosedCurveInterpolationPoints;

        public float PointIntervalOnDrag => 90;

        public float PenAngleThreshold => .3f;

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
                    Execute(PenInput.SimplifiedFirstLoop);
                }
            }
        }



        protected virtual bool IsProposedClosedLoopValid(IReadOnlyCollection<GridVector2> proposed_curve) => true;


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
        protected override bool CanCommandComplete() => PenInput.HasSelfIntersection;

        protected override bool ShapeIsValid()
        {
            if (PenInput.Points.Count < 3 || PenInput.HasSelfIntersection == false)
            {
                return false;
            }

            try
            {
                return PenInput.Loop.ToPolygon().STIsValid().IsTrue;
            }
            catch (ArgumentException)
            {
                return false;
            }
        }
    }

    internal class PlaceOpenCurveWithPenCommand : PlaceGeometryWithPenCommandBase
    {
        public override LineStyle Style => LineStyle.Tubular;

        public override uint NumCurveInterpolations => Geometry.Global.NumOpenCurveInterpolationPoints;

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
            PathView.Color = HasLoop ? Microsoft.Xna.Framework.Color.Magenta : OriginalColor;
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
        protected override bool CanCommandComplete() =>
            //We cannot create an open curve if the path has a self-intersection
            PenInput.HasSelfIntersection == false;


        protected override bool ShapeIsValid()
        {
            if (PenInput.Points.Count < 2)
            {
                return false;
            }

            if (PenInput.HasSelfIntersection)
            {
                return false;
            }

            return PenInput.Points.ToSqlGeometry().STIsValid().IsTrue;
        }
    }



    /// <summary>
    /// Left-click once to create a new vertex in the poly line
    /// Left-click an existing vertex to complete polyline creation
    /// Double left-click to complete polyline creation
    /// Right-click to remove the last polyline vertex
    /// </summary> 
    internal abstract class PlaceGeometryWithPenCommandBase : LineGeometryCommandBase, Viking.Common.IHelpStrings, Viking.Common.IObservableHelpStrings
    {
        public abstract uint NumCurveInterpolations
        {
            get;
        }

        public override double LineWidth =>
                // return this.PathView is null ? Global.DefaultClosedLineWidth : this.PenInput.Points.MinDistanceBetweenSequentialPoints(out int FirstIndex);
                PathView.LineWidth;

        /// <summary>
        /// Used for debugging when we want to show control points
        /// </summary>
        public virtual double ControlPointRadius => LineWidth / 2.0;

        public ObservableCollection<string> ObservableHelpStrings => new(HelpStrings);


        public string[] HelpStrings
        {
            get
            {
                List<string> s = [.. PlaceCurveCommand.DefaultMouseHelpStrings, .. PlaceCurveCommand.DefaultKeyHelpStrings];

                return [.. s];
            }

        }

        public new static string[] DefaultMouseHelpStrings = [
            "Double Left Click: Place final control point, save and exit command",
            "Double Right Click: Pop last control point",
            "Left Click and Drag Control Point: Move existing control point",
            "Left Click last control point: Save and exit command",
            "No cursor: Command cannot be completed at this location due to invalid geometry. Typically crossed lines."
            ];

        public new static string[] DefaultKeyHelpStrings = [
            "Escape Key: Cancel command",
            "Page up/down key: Change Magnification",
            "Arrow key: Move view",
            "Home key: Round magnification to whole number"
            ];

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
            System.Diagnostics.Trace.WriteLine($"PlaceCurveWithPenCommand {ID} Subscribed to events");
            PenInput.OnPathChanged += OnPenPathChanged;
            PenInput.OnPathCompleted += OnPenPathComplete;
            PenInput.OnProposedNextSegmentChanged += OnPenProposedNextSegmentChanged;
            PenInput.OnPathLoop += OnPathLoop;
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

        protected override void Execute() => Execute(PenInput.SimplifiedPath);

        protected override void OnDeactivate()
        {
            System.Diagnostics.Trace.WriteLine($"PlaceCurveWithPenCommand {ID} Unubscribed to events");
            PenInput.OnPathChanged -= OnPenPathChanged;
            PenInput.OnPathCompleted -= OnPenPathComplete;
            PenInput.OnProposedNextSegmentChanged -= OnPenProposedNextSegmentChanged;
            PenInput.OnPathLoop -= OnPathLoop;
            PenInput.UnsubscribeEvents();
            PenInput = null;
            base.OnDeactivate();
        }

        protected virtual void OnPenPathChanged(object sender, System.Collections.Specialized.NotifyCollectionChangedEventArgs e)
        {
            //Update the view of the path
            switch (e.Action)
            {
                case NotifyCollectionChangedAction.Add:
                    PathView.Add(PenInput.Peek());
                    break;
                case NotifyCollectionChangedAction.Remove:
                    //Pop off as many items that were removed
                    foreach (object p in e.OldItems)
                    {
                        PathView.Remove();
                        //System.Diagnostics.Debug.Assert(this.PathView.ControlPoints.Last() == PenInput.Points.First());
                    }

                    break;
                case NotifyCollectionChangedAction.Move:
                    PathView.Remove();
                    PathView.Add(PenInput.Peek());
                    break;
                case NotifyCollectionChangedAction.Reset:
                    PathView.ControlPoints = new GridVector2[0];
                    break;
                default:
                    PathView.ControlPoints = PenInput.Points;
                    break;
            }

            Parent.Invalidate();
        }

        private void SetPathViewForDownsample(double Downsample)
        {
            PathView.LineWidth = Downsample * PenInput.SimplifiedPathToleranceInPixels;
            PathView.ControlPointRadius = PathView.LineWidth / 2.0f;
            PathView.DashLength = (float)(Downsample * PenInput.SimplifiedPathToleranceInPixels * 2.0f);
        }

        protected override void OnCameraChanged(object sender, PropertyChangedEventArgs e)
        {
            if (e.PropertyName == "Downsample")
            {
                SetPathViewForDownsample(Parent.Camera.Downsample);
            }

            base.OnCameraChanged(sender, e);
        }

        protected abstract void OnPenPathComplete(object sender, GridVector2[] Path);

        protected abstract void OnPenProposedNextSegmentChanged(object sender, GridLineSegment? segment);

        protected abstract void OnPathLoop(object sender, bool HasLoop);

        protected abstract bool ShapeIsValid();

        protected abstract bool CanCommandComplete();

        protected override void OnPenMove(object sender, PenEventArgs e)
        {
            //Passing down erase move events will translate the view.  Make sure the pen was placed enough far away from the path that the pen input helper will not process the event.
            if (PenInput != null && PenInput.IgnoringThisPenContact)
            {
                base.OnPenMove(sender, e);
            }
            else if (PenInput != null && e.Barrel)
            {
                CancelCommand();
                return;
            }
            else
            {
                GridVector2 NewPosition = Parent.ScreenToWorld(e.X, e.Y);
                Parent.StatusPosition = NewPosition;
                SaveAsOldPenPosition(e);
            }
        }

        public override void OnDraw(Microsoft.Xna.Framework.Graphics.GraphicsDevice graphicsDevice, VikingXNA.Scene scene, Microsoft.Xna.Framework.Graphics.BasicEffect basicEffect)
        {
            PolyLineView.Draw(graphicsDevice, scene, OverlayStyle.Luma, [PathView]);

#if DEBUG
            if (PenInput.ProposedNextSegment.HasValue)
            {
                LineView unofficialPath = new(PenInput.ProposedNextSegment.Value, width: LineWidth, color: LineColor, lineStyle: LineStyle.Standard);
                LineView.Draw(graphicsDevice, scene, Parent.LumaOverlayLineManager, [unofficialPath]);
            }
#endif
        }
    }
}
