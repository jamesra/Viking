using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Windows.Forms;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using Geometry;
using System.Drawing;
using System.Diagnostics;
using RoundLineCode;
using WebAnnotation.ViewModel;
using WebAnnotation.View;
using WebAnnotationModel;
using VikingXNAGraphics;
using System.Collections.ObjectModel;
using VikingXNAWinForms;

namespace WebAnnotation.UI.Commands
{
    /// <summary>
    /// Created after a location for a new structure has been determined, but we
    /// have to choose a parent for the new structure. 
    /// </summary>
    class LinkStructureToParentCommand : AnnotationCommandBase, Viking.Common.IObservableHelpStrings, Viking.Common.IHelpStrings
    {
        /// <summary>
        /// New Locations position in world space
        /// </summary>
        GridVector2 transformedPos; 

        StructureObj putativeStruct;
        LocationObj putativeLoc;

        LocationObj nearestParent;

        LocationCanvasView locView;

        Microsoft.Xna.Framework.Color linecolor;

        CurveLabel labelView = null;

        public string[] HelpStrings
        {
            get
            {
                return new string[] { "Left Mouse Button Release over parent structure annotation: Set annotation's parent structure",
                                      "Escape: Cancel command"};
            }
        }

        public ObservableCollection<string> ObservableHelpStrings
        {
            get
            {
                return new ObservableCollection<string>(this.HelpStrings);
            }
        }

        public LinkStructureToParentCommand(Viking.UI.Controls.SectionViewerControl parent,
                                               StructureObj structure,
                                               LocationObj location)
            : base(parent)
        {

            this.putativeStruct = structure;
            this.putativeLoc = location;

            StructureTypeObj LocType = this.putativeStruct.Type;
            if (LocType != null)
            {
                linecolor = LocType.Color.ToXNAColor(0.5f);
            }
            else
            {
                linecolor = Microsoft.Xna.Framework.Color.Green;
            }

            //Transform the location position to the correct coordinates
            transformedPos = parent.Section.ActiveSectionToVolumeTransform.SectionToVolume(new GridVector2(putativeLoc.Position.X, putativeLoc.Position.Y));

            parent.Cursor = Cursors.Cross;

            double textHeight = location.Radius * 2;

            locView = AnnotationViewFactory.Create(putativeLoc, parent.Section.ActiveSectionToVolumeTransform);
            
        }

        protected LocationCanvasView NearestLocationToMouse(GridVector2 WorldPos)
        {
            List<HitTestResult> listHitTestResults = Overlay.GetAnnotationsAtPosition(WorldPos);

            //Find locations that are not equal to our origin location
            listHitTestResults = listHitTestResults.Where(hr =>
            {
                LocationCanvasView loc = hr.obj as LocationCanvasView;
                if (loc == null)
                    return false;

                return loc.ID != putativeLoc.ID && loc.ParentID != putativeStruct.ID;
            }).ToList();

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
            GridVector2 WorldPos = Parent.ScreenToWorld(e.X, e.Y);
    
            //Find if we are close enough to a location to "snap" the line to the target
            double distance;
            LocationCanvasView nearest = NearestLocationToMouse(WorldPos);
            if (nearest != null)
            {
                nearestParent = Store.Locations[nearest.ID];
            }
            else
            {
                nearestParent = null; 
            }
           
            base.OnMouseMove(sender, e);

            Parent.Invalidate(); 
        }

        protected override void OnMouseDown(object sender, MouseEventArgs e)
        {
            //Figure out if we've clicked another structure and create the structure
            if (e.Button.Left())
            {
                GridVector2 WorldPos = Parent.ScreenToWorld(e.X, e.Y);

                /*Check to see if we clicked a location*/
                double distance; 
                LocationCanvasView loc = NearestLocationToMouse(WorldPos);
                if (loc == null)
                    return;

                this.putativeStruct.Parent = loc.Parent.modelObj; 

                this.Deactivated = true; 
            }

            base.OnMouseDown(sender, e);
        }

        public override void OnDraw(GraphicsDevice graphicsDevice, VikingXNA.Scene scene, BasicEffect basicEffect)
        {
            if (this.oldMouse == null)
                return;

            if (locView != null)
                LocationObjRenderer.DrawCanvasView(new LocationCanvasView[] { locView }, graphicsDevice, basicEffect, Parent.AnnotationOverlayEffect, Parent.LumaOverlayLineManager, Parent.LumaOverlayCurveManager, scene, (int)locView.Z);
            else
                GlobalPrimitives.DrawCircle(graphicsDevice, basicEffect, transformedPos, putativeLoc.Radius, linecolor);

            GridVector2 target;
            if (nearestParent != null)
            {
                //Snap the line to a nearby target if it exists
                target = nearestParent.VolumePosition;
            }
            else
            {
                //Otherwise use the old mouse position
                target = this.oldWorldPosition;
            }
            
            LineView line = new LineView(transformedPos, target, 16.0, Microsoft.Xna.Framework.Color.White, LineStyle.Tubular);
            
            RoundLineManager lineManager = VikingXNAGraphics.DeviceEffectsStore<LumaOverlayRoundLineManager>.TryGet(graphicsDevice);
            if (lineManager == null)
                return;

            if(labelView == null)
            {
                labelView = new CurveLabel("Select Parent Structure", new GridVector2[] { transformedPos, target }, Microsoft.Xna.Framework.Color.Black, false, lineWidth: line.LineWidth, numInterpolations: 0);
            }
            else
            {
                labelView.ControlPoints = transformedPos.X < target.X ? new GridVector2[] { transformedPos, target } : new GridVector2[] { target, transformedPos };
            }

            labelView.Draw(graphicsDevice, scene.ViewProj, Parent.spriteBatch, Parent.fontArial, Parent.CurveManager);
            
            LineView.Draw(graphicsDevice, scene, lineManager, new LineView[] { line });

            base.OnDraw(graphicsDevice, scene, basicEffect);
        }
        
    }
}
