using Geometry;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using System;
using System.Collections.Generic;
using System.Collections.Specialized;
using System.Diagnostics;
using System.Linq;
using Viking.UI.Controls;
using VikingXNA;
using VikingXNAGraphics;

namespace WebAnnotation.UI.Commands
{
    /// <summary>
    /// This class is active when the user begins drawing a path with the pen in an area there are no annotations to take action on. 
    /// The command may exit with no action, draw an open curve, or draw a closed curved polygon.  Once the geometry is placed the 
    /// user can complete the annotation
    /// </summary>
    class AnnotationOverlayPenFreeDrawCommandV2 : PlaceGeometryWithPenCommandBase
    {
        /// <summary>
        /// Renders any loops the user has created so they have feedback if that was the goal
        /// </summary>
        List<SolidPolygonView> LoopViews = new List<SolidPolygonView>();

        /*
        /// <summary>
        /// Look up the actions that were added when a given point was added to the path
        /// </summary>
        Dictionary<GridVector2, IAction> ActionAtPoint = new Dictionary<GridVector2, IAction>();*/

        public Dictionary<ICanvasView, List<IAction>> ActionsForCanvasItem = new Dictionary<ICanvasView, List<IAction>>();

        public IAction[] PossibleActions
        {
            get
            {
                List<IAction> listActions = new List<IAction>();
                foreach (var list in ActionsForCanvasItem.Values)
                {
                    listActions.AddRange(list);
                }

                return listActions.ToArray();
            }
        }


        public PathAnnotationInteractionLog InteractionsLog
        {
            get
            {
                return InteractionsLogger.Log;
            }
        }

        PathInteractionLogger InteractionsLogger;

        /// <summary>
        /// Prevent the user from making absurdly small annotations by accident
        /// </summary>
        private double MinAreaForClosedShape
        {
            get
            {
                return Parent.Downsample * 10 * 10;
            }
        }

        private double MinLengthForOpenShape
        {
            get
            {
                return Parent.Downsample * 10;
            }
        }


        public AnnotationOverlayPenFreeDrawCommandV2(SectionViewerControl parent, Color color, double LineWidth, OnCommandSuccess success_callback) : base(parent, color, LineWidth, success_callback)
        {
            InteractionsLogger = new PathInteractionLogger(base.PenInput.path, AnnotationOverlay.CurrentOverlay);
            InteractionsLogger.Log.OnLogChanged += this.OnInteractionLogChanged;
        }

        public AnnotationOverlayPenFreeDrawCommandV2(SectionViewerControl parent, Color color, GridVector2 origin, double LineWidth, OnCommandSuccess success_callback) : base(parent, color, origin, LineWidth, success_callback)
        {
            InteractionsLogger = new PathInteractionLogger(base.PenInput.path, AnnotationOverlay.CurrentOverlay);
            InteractionsLogger.Log.OnLogChanged += this.OnInteractionLogChanged;
        }

        public override uint NumCurveInterpolations => throw new NotImplementedException();

        protected override bool CanCommandComplete()
        {
            return true;
        }

        protected override void OnPenPathComplete(object sender, GridVector2[] Path)
        {
            //TODO: Prompt the user to create an open curve type if there is no curve
            //If we draw from one annotation to another we either create a location link (different sections) or a structure link (same sections).
            //If not we create a new open curve annotation.

            //TODO: For certain actions we need to update the path once it is done, for example changing the line of a synapse
            //Other commands we don't need to update... For now I update everything
            OnInteractionAdded(this.InteractionsLog.Entries);

            /*
            foreach (InteractionLogEvent e in this.InteractionsLog.Entries)
            {
                Trace.WriteLine(string.Format("{0} {1}", e.Interaction, e.Annotation == null ? "Empty region" : e.Annotation.ToString()));
            }

            // PossibleActions.Clear();

            var Annotations = this.InteractionsLogger.Log.Entries.Select(e => e.Annotation).Distinct();
            foreach (var annotation in Annotations)
            {
                IPenActionSupport pen_view = annotation as IPenActionSupport;
                if (pen_view == null)
                    continue;

                var actions = pen_view.GetPenActionsForShapeAnnotation(this.PenInput.path, this.InteractionsLogger.Log.Entries, Parent.Section.Number);
                ActionsForCanvasItem[annotation] = actions;
            }
            */

            this.Execute();
        }

        protected void OnInteractionLogChanged(object sender, NotifyCollectionChangedEventArgs e)
        {
            switch (e.Action)
            {
                case NotifyCollectionChangedAction.Add:
                    //OnInteractionAdded(e.NewItems.OfType<InteractionLogEvent>());
                    break;
                case NotifyCollectionChangedAction.Move:
                    throw new NotImplementedException("Move is not implemented for PathInteractionLogger");
                case NotifyCollectionChangedAction.Remove:
                    //OnInteractionRemove(e.OldItems.OfType<InteractionLogEvent>());
                    break;
                case NotifyCollectionChangedAction.Replace:
                    throw new NotImplementedException("Replace is not implemented for PathInteractionLogger");
                case NotifyCollectionChangedAction.Reset:
                    break;
            }
        }

        /// <summary>
        /// Collect actions from specified views when an interaction event is added related to those views
        /// </summary>
        /// <param name="views"></param>
        private void OnInteractionAdded(IEnumerable<InteractionLogEvent> views)
        {
            foreach (InteractionLogEvent e in views)
            {
                Trace.WriteLine(string.Format("{0} {1}", e.Interaction, e.Annotation == null ? "Empty region" : e.Annotation.ToString()));
            }

            // PossibleActions.Clear();

            var Annotations = this.InteractionsLogger.Log.Entries.Select(e => e.Annotation).Distinct();
            foreach (var annotation in Annotations)
            {
                IPenActionSupport pen_view = annotation as IPenActionSupport;
                if (pen_view == null)
                    continue;

                var actions = pen_view.GetPenActionsForShapeAnnotation(this.PenInput.path, this.InteractionsLogger.Log.Entries, Parent.Section.Number);
                ActionsForCanvasItem[annotation] = actions;
            }
        }

        /// <summary>
        /// Collect actions from specified views when an interaction event is added related to those views
        /// </summary>
        /// <param name="views"></param>
        private void OnInteractionRemove(IEnumerable<InteractionLogEvent> views)
        {

            foreach (InteractionLogEvent e in views)
            {
                Trace.WriteLine(string.Format("Remove {0} {1}", e.Interaction, e.Annotation == null ? "Empty region" : e.Annotation.ToString()));
            }

            ActionsForCanvasItem.Clear();

            var Annotations = this.InteractionsLogger.Log.Entries.Select(e => e.Annotation).Distinct();
            foreach (var annotation in Annotations)
            {
                IPenActionSupport pen_view = annotation as IPenActionSupport;
                if (pen_view == null)
                    continue;

                var actions = pen_view.GetPenActionsForShapeAnnotation(this.PenInput.path, this.InteractionsLogger.Log.Entries, Parent.Section.Number);
                ActionsForCanvasItem[annotation] = actions;
            }

        }


        public override void OnDraw(GraphicsDevice graphicsDevice, Scene scene, BasicEffect basicEffect)
        {
            base.OnDraw(graphicsDevice, scene, basicEffect);

            if (LoopViews.Count > 0)
                SolidPolygonView.Draw(graphicsDevice, scene, OverlayStyle.Luma, this.LoopViews);
        }

        protected override void OnPenProposedNextSegmentChanged(object sender, GridLineSegment? segment)
        {
            return;
        }

        protected override void OnPathLoop(object sender, bool HasLoop)
        {
            this.PathView.Color = HasLoop ? Color.DarkOrange : Color.DarkGreen;
            return;
        }

        protected override bool ShapeIsValid()
        {
            return true;
        }
    }
}
