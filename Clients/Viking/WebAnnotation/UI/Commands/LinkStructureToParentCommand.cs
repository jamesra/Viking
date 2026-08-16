using Geometry;
using Microsoft.Xna.Framework.Graphics;
using RoundLineCode;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using System.Windows.Forms;
using Viking.UI;
using VikingXNAGraphics;
using VikingXNAWinForms;
using WebAnnotation.View;
using WebAnnotationModel;
using WebAnnotationModel.Objects;
using Vector2 = Microsoft.Xna.Framework.Vector2;
using Vector3 = Microsoft.Xna.Framework.Vector3;

namespace WebAnnotation.UI.Commands
{
    /// <summary>
    /// Created after a location for a new structure has been determined, but we
    /// have to choose a parent for the new structure. 
    /// </summary>
    internal class LinkStructureToParentCommand : AnnotationCommandBase, Viking.Common.IObservableHelpStrings, Viking.Common.IHelpStrings
    {
        /// <summary>
        /// New Locations position in world space
        /// </summary>
        private Geometry.Vector2 transformedPos;
        private readonly StructureObj putativeStruct;
        private readonly LocationObj putativeLoc;
        private LocationObj nearestParent;
        private readonly LocationCanvasView locView;
        private Microsoft.Xna.Framework.Color linecolor;
        private CurveLabel? labelView = null;

        public string[] HelpStrings => [ "Left Mouse Button Release over parent structure annotation: Set annotation's parent structure",
                                      "Escape: Cancel command"];

        public ObservableCollection<string> ObservableHelpStrings => new(HelpStrings);

        public LinkStructureToParentCommand(Viking.UI.Controls.SectionViewerControl parent,
                                               StructureObj structure,
                                               LocationObj location)
            : base(parent)
        {

            putativeStruct = structure;
            putativeLoc = location;

            StructureTypeObj LocType = putativeStruct.Type;
            linecolor = LocType != null ? LocType.Color.ToXNAColor(0.5f) : Microsoft.Xna.Framework.Color.Green;

            //Transform the location position to the correct coordinates
            transformedPos = parent.Section.ActiveSectionToVolumeTransform.SectionToVolume(new Geometry.Vector2(putativeLoc.Position.X, putativeLoc.Position.Y));

            parent.Cursor = Cursors.Cross;

            double textHeight = location.Radius * 2;

            locView = AnnotationViewFactory.Create(putativeLoc, parent.Section.ActiveSectionToVolumeTransform);

        }

        protected LocationCanvasView NearestLocationToMouse(Geometry.Vector2 WorldPos)
        {
            List<HitTestResult> listHitTestResults = Overlay.GetAnnotations(WorldPos);

            //Find locations that are not equal to our origin location
            listHitTestResults = [.. listHitTestResults.Where(hr =>
            {
                if (hr.obj is not LocationCanvasView loc)
                {
                    return false;
                }

                return loc.ID != putativeLoc.ID && loc.ParentID != putativeStruct.ID;
            })];

            LocationCanvasView nearestVisible = null;
            HitTestResult BestMatch = listHitTestResults.NearestObjectOnCurrentSectionThenAdjacent((int)putativeLoc.Z);
            if (BestMatch != null)
            {
                nearestVisible = BestMatch.obj as LocationCanvasView;
            }

            return nearestVisible;
        }

        protected override void OnMouseMove(object sender, MouseEventArgs e)
        {
            HandleInputMovement(e.X, e.Y);

            base.OnMouseMove(sender, e);

            Parent.Invalidate();
        }

        protected override void OnMouseDown(object sender, MouseEventArgs e)
        {
            //Figure out if we've clicked another structure and create the structure
            if (e.Button.Left())
            {
                if (HandleInputSelection(e.X, e.Y) == false)
                {
                    return;
                }
            }

            base.OnMouseDown(sender, e);
        }

        protected override void OnPenMove(object sender, PenEventArgs e)
        {
            HandleInputMovement(e.X, e.Y);

            base.OnPenMove(sender, e);

            Parent.Invalidate();
        }

        protected override void OnPenContact(object sender, PenEventArgs e)
        {
            //Figure out if we've clicked another structure and create the structure
            if (e.Erase == false)
            {
                if (HandleInputSelection(e.X, e.Y) == false)
                {
                    return;
                }
            }

            base.OnPenContact(sender, e);
        }

        protected void HandleInputMovement(int X, int Y)
        {
            Geometry.Vector2 WorldPos = Parent.ScreenToWorld(X, Y);
            LocationCanvasView nearest = NearestLocationToMouse(WorldPos);
            nearestParent = nearest != null ? Store.Locations[nearest.ID] : null;
        }

        protected bool HandleInputSelection(int X, int Y)
        {
            Geometry.Vector2 WorldPos = Parent.ScreenToWorld(X, Y);

            /*Check to see if we clicked a location*/
            LocationCanvasView loc = NearestLocationToMouse(WorldPos);
            if (loc is null)
            {
                return false;
            }

            putativeStruct.Parent = loc.Parent.modelObj;

            Deactivated = true;
            return true;
        }

        public override void OnDraw(GraphicsDevice graphicsDevice, VikingXNA.Scene scene, BasicEffect basicEffect)
        {
            if (oldMouse is null)
            {
                return;
            }

            if (locView != null)
            {
                LocationObjRenderer.DrawCanvasView(new LocationCanvasView[] { locView }, graphicsDevice, basicEffect, Parent.AnnotationOverlayEffect, Parent.LumaOverlayLineManager, Parent.LumaOverlayCurveManager, scene, (int)locView.Z);
            }
            else
            {
                GlobalPrimitives.DrawCircle(graphicsDevice, basicEffect, transformedPos, putativeLoc.Radius, linecolor);
            }

            Geometry.Vector2 target;
            if (nearestParent != null)
            {
                //Snap the line to a nearby target if it exists
                target = nearestParent.VolumePosition;
            }
            else
            {
                //Otherwise use the old mouse position
                target = oldWorldPosition;
            }

            LineView line = new(transformedPos, target, 16.0, Microsoft.Xna.Framework.Color.White, LineStyle.Tubular);

            RoundLineManager lineManager = VikingXNAGraphics.DeviceEffectsStore<LumaOverlayRoundLineManager>.TryGet(graphicsDevice);
            if (lineManager is null)
            {
                return;
            }

            if (labelView is null)
            {
                labelView = new CurveLabel("Select Parent Structure", new Geometry.Vector2[] { transformedPos, target }, Microsoft.Xna.Framework.Color.Black, false, lineWidth: line.LineWidth, numInterpolations: 0);
            }
            else
            {
                labelView.ControlPoints = transformedPos.X < target.X ? [transformedPos, target] : [target, transformedPos];
            }

            labelView.Draw(graphicsDevice, scene.ViewProj, Parent.spriteBatch, Parent.fontArial, Parent.CurveManager);

            LineView.Draw(graphicsDevice, scene, lineManager, [line]);

            base.OnDraw(graphicsDevice, scene, basicEffect);
        }
    }
}
