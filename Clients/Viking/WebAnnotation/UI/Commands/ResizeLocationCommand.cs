using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using Geometry;
using System.Diagnostics;
using System.Windows.Forms;
using Microsoft.Xna.Framework.Graphics;
using WebAnnotation.ViewModel;
using WebAnnotationModel; 

namespace WebAnnotation.UI.Commands
{
    class ResizeLocationCommand : AnnotationCommandBase
    {
        Location_CanvasViewModel selected;
        GridVector2 Origin;
        StructureType LocType; 
        double radius;
        /// <summary>
        /// Set to true if we save our changes to DB
        /// If False we are part of a command queue and it will be done later
        /// </summary>
        bool SaveToDB;

        //DateTime CreationTime; 
        private bool HasMouseMoved = false;

        public ResizeLocationCommand(Viking.UI.Controls.SectionViewerControl parent)
            : base(parent)
        {
            selected = Viking.UI.State.SelectedObject as Location_CanvasViewModel; 
            Debug.Assert(selected != null);

            LocType = selected.Parent.Type;

            SectionLocationsViewModel sectionAnnotations = AnnotationOverlay.GetAnnotationsForSection(Parent.Section.Number);
            if (sectionAnnotations == null)
                return;

            Origin = sectionAnnotations.GetPositionForLocation(selected);
            parent.Cursor = Cursors.SizeAll;
            SaveToDB = true; 
        }

        public ResizeLocationCommand(Viking.UI.Controls.SectionViewerControl parent, 
                                     StructureType type, 
                                     Location_CanvasViewModel loc)
            : base(parent)
        {
            selected = loc;
            LocType = type;
            this.SaveToDB = false;

            SectionLocationsViewModel sectionAnnotations = AnnotationOverlay.GetAnnotationsForSection(Parent.Section.Number);
            if (sectionAnnotations == null)
                return;

            //Origin = sectionAnnotations.GetPositionForLocation(selected);
            Origin = selected.VolumePosition;
            parent.Cursor = Cursors.SizeAll;
 //           CreationTime = DateTime.Now;
        }

        protected override void OnMouseMove(object sender, System.Windows.Forms.MouseEventArgs e)
        {
            GridVector2 WorldPos = Parent.ScreenToWorld(e.X, e.Y);

            this.radius = GridVector2.Distance(Origin, WorldPos); 

            HasMouseMoved=true;

            Parent.Invalidate(); 

            base.OnMouseMove(sender, e);
        }

        protected override void OnMouseUp(object sender, MouseEventArgs e)
        {
            if (e.Button == MouseButtons.Left)
            {
            //    TimeSpan Elapsed = new TimeSpan(DateTime.Now.Ticks - CreationTime.Ticks);
                GridVector2 WorldPos = Parent.ScreenToWorld(e.X, e.Y);

                radius = GridVector2.Distance(Origin, WorldPos); 

                //Send changes to DB if we aren't in a queue of events
                if (SaveToDB)
                {
                    selected.Radius = radius;
                    Store.Locations.Save();
                    Viking.UI.State.SelectedObject = null;
                }
                else
                {
                    //If we are in a chain of events make sure a reasonable amount of time has elapsed, this tells us whether the 
                    //user had time to size the location or 

                    if (HasMouseMoved && radius > 0)
                    {
                        selected.Radius = radius;
                    }
                    else
                    {
                        selected.Radius = 128f; 
                    }

                }

                this.Execute(); 
            }

            base.OnMouseDown(sender, e);
        }

        public override void OnDraw(Microsoft.Xna.Framework.Graphics.GraphicsDevice graphicsDevice, VikingXNA.Scene scene, Microsoft.Xna.Framework.Graphics.BasicEffect basicEffect)
        {
//            double OldRadius = selected.Radius;
            SectionLocationsViewModel sectionAnnotations = AnnotationOverlay.GetAnnotationsForSection(Parent.Section.Number);
            if (sectionAnnotations == null)
                return;

            GridVector2 Pos = selected.VolumePosition; 

            /*
            bool found = sectionAnnotations.TryGetPositionForLocation(selected, out Pos);
            if (found == false)
                return; 
            */
            Microsoft.Xna.Framework.Color color = new Microsoft.Xna.Framework.Color(LocType.Color.R,
                LocType.Color.G,
                LocType.Color.B,
                128);

            GlobalPrimitives.DrawCircle(graphicsDevice, basicEffect, Pos, this.radius, color); 
              

            base.OnDraw(graphicsDevice, scene, basicEffect);
        }
    }
}
