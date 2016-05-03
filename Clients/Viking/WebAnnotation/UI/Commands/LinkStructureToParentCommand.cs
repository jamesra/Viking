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

namespace WebAnnotation.UI.Commands
{
    /// <summary>
    /// Created after a location for a new structure has been determined, but we
    /// have to choose a parent for the new structure. 
    /// </summary>
    class LinkStructureToParentCommand : AnnotationCommandBase
    {
        /// <summary>
        /// New Locations position in world space
        /// </summary>
        GridVector2 transformedPos; 

        StructureObj putativeStruct;
        LocationObj putativeLoc;

        LocationObj nearestParent;

        Microsoft.Xna.Framework.Color linecolor;

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
        }

        protected override void OnMouseMove(object sender, MouseEventArgs e)
        {
            GridVector2 WorldPos = Parent.ScreenToWorld(e.X, e.Y);
    
            //Find if we are close enough to a location to "snap" the line to the target
            double distance; 
            LocationCanvasView nearest = Overlay.GetNearestLocation(WorldPos, out distance);
            if (nearest != null)
            {
                nearestParent = Store.Locations[nearest.ID];
            }
           
            base.OnMouseMove(sender, e);

            Parent.Invalidate(); 
        }

        protected override void OnMouseDown(object sender, MouseEventArgs e)
        {
            //Figure out if we've clicked another structure and create the structure
            if (e.Button == MouseButtons.Left)
            {
                GridVector2 WorldPos = Parent.ScreenToWorld(e.X, e.Y);

                /*Check to see if we clicked a location*/
                double distance; 
                LocationCanvasView loc = Overlay.GetNearestLocation(WorldPos, out distance);
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

            LineView line = new LineView(transformedPos, target, 16.0, linecolor, LineStyle.Standard);
            
            RoundLineManager lineManager = VikingXNA.DeviceEffectsStore<LumaOverlayRoundLineManager>.TryGet(graphicsDevice);
            if (lineManager == null)
                return;

            LineView.Draw(graphicsDevice, scene, lineManager, new LineView[] { line });

            base.OnDraw(graphicsDevice, scene, basicEffect);
        }
        
    }
}
