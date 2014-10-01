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
using WebAnnotationModel; 


using Viking.Common; 

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

        Structure putativeStruct; 
        Location_CanvasViewModel putativeLoc;

        Location_CanvasViewModel nearestParent;

        Microsoft.Xna.Framework.Color linecolor;

        public LinkStructureToParentCommand(Viking.UI.Controls.SectionViewerControl parent,
                                               Structure structure, 
                                               Location_CanvasViewModel location)
            : base(parent)
        {

            this.putativeStruct = structure;
            this.putativeLoc = location;

            StructureType LocType = this.putativeStruct.Type;
            if (LocType != null)
            {
                linecolor = new Microsoft.Xna.Framework.Color(LocType.Color.R,
                    LocType.Color.G,
                    LocType.Color.B,
                    128);
            }
            else
            {
                linecolor = Microsoft.Xna.Framework.Color.Green;
            }

            //Transform the location position to the correct coordinates
            transformedPos = parent.SectionToVolume(new GridVector2(putativeLoc.X, putativeLoc.Y));

            parent.Cursor = Cursors.Cross; 
        }

        protected override void OnMouseMove(object sender, MouseEventArgs e)
        {
            GridVector2 WorldPos = Parent.ScreenToWorld(e.X, e.Y);
    //        GridVector2 locPosition;

            SectionLocationsViewModel sectionAnnotations = AnnotationOverlay.GetAnnotationsForSection(Parent.Section.Number);
            if (sectionAnnotations == null)
                return;

            //Find if we are close enough to a location to "snap" the line to the target
            double distance; 
            nearestParent = Overlay.GetNearestLocation(WorldPos, out distance);
           
            base.OnMouseMove(sender, e);

            Parent.Invalidate(); 
        }

        protected override void OnMouseDown(object sender, MouseEventArgs e)
        {
            //Figure out if we've clicked another structure and create the structure
            if (e.Button == MouseButtons.Left)
            {
                SectionLocationsViewModel sectionAnnotations = AnnotationOverlay.GetAnnotationsForSection(Parent.Section.Number);
                if (sectionAnnotations == null)
                    return;

                GridVector2 WorldPos = Parent.ScreenToWorld(e.X, e.Y);

                /*Check to see if we clicked a location*/
                double distance; 
                Location_CanvasViewModel loc = Overlay.GetNearestLocation(WorldPos, out distance);
                if (loc == null)
                    return;

                this.putativeStruct.modelObj.Parent = loc.Parent.modelObj; 

                this.Deactivated = true; 
            }

            base.OnMouseDown(sender, e);
        }

        public override void OnDraw(GraphicsDevice graphicsDevice, VikingXNA.Scene scene, BasicEffect basicEffect)
        {
            if (this.oldMouse == null)
                return;

            GlobalPrimitives.DrawCircle(graphicsDevice, basicEffect, transformedPos, putativeLoc.Radius, linecolor); 

            Vector3 target;
            if (nearestParent != null)
            {
                SectionLocationsViewModel sectionAnnotations = AnnotationOverlay.GetAnnotationsForSection(Parent.Section.Number);
                if (sectionAnnotations == null)
                    return;

                //Snap the line to a nearby target if it exists
                GridVector2 targetPos = nearestParent.VolumePosition; 

                /*
                bool success = sectionAnnotations.TryGetPositionForLocation(nearestParent, out targetPos);
                if (!success)
                    return; 
                */

                target = new Vector3((float)targetPos.X, (float)targetPos.Y, 0f);
            }
            else
            {
                //Otherwise use the old mouse position
                target = new Vector3((float)this.oldWorldPosition.X, (float)oldWorldPosition.Y, 0f);
            }

            RoundLine lineToParent = new RoundLine((float)transformedPos.X, (float)transformedPos.Y, (float)target.X, (float)target.Y);

            Parent.LineManager.Draw(lineToParent, (float)(putativeLoc.Radius / 6.0), linecolor, basicEffect.View * basicEffect.Projection, 1, null); 

            base.OnDraw(graphicsDevice, scene, basicEffect);
        }
        
    }
}
