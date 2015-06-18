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
    class ResizeCircleCommand : AnnotationCommandBase
    {
        public double radius;
         
        GridVector2 Origin;
        System.Drawing.Color CircleColor; 
 
        //DateTime CreationTime; 
        private bool HasMouseMoved = false;

        public delegate void OnCommandSuccess(double radius);
        OnCommandSuccess success_callback;
         
        /*
        public ResizeCircleCommand(Viking.UI.Controls.SectionViewerControl parent)
            : base(parent)
        {
            selected = Viking.UI.State.SelectedObject as Location_CanvasViewModel; 
            Debug.Assert(selected != null);

            CircleColor = selected.Parent.Type.Color;

            SectionLocationsViewModel sectionAnnotations = AnnotationOverlay.GetAnnotationsForSection(Parent.Section.Number);
            if (sectionAnnotations == null)
                return;

            Origin = sectionAnnotations.GetPositionForLocation(selected);
            parent.Cursor = Cursors.SizeAll;
            SaveToDB = true; 
        }
        */

        public ResizeCircleCommand(Viking.UI.Controls.SectionViewerControl parent, 
                                     System.Drawing.Color color,  
                                     GridVector2 origin,  
                                     OnCommandSuccess success_callback)
            : base(parent)
        { 
            CircleColor = color; 
            Origin = origin;
            parent.Cursor = Cursors.SizeAll;
            this.success_callback = success_callback;
        }

        protected override void OnMouseMove(object sender, System.Windows.Forms.MouseEventArgs e)
        {
            GridVector2 WorldPos = Parent.ScreenToWorld(e.X, e.Y);

            this.radius = GridVector2.Distance(Origin, WorldPos); 

            HasMouseMoved=true;

            Parent.Invalidate(); 

            base.OnMouseMove(sender, e);
        }

        protected override void Execute()
        {
            if(this.success_callback != null)
                this.success_callback(this.radius);

            base.Execute();
        }

        protected override void OnMouseUp(object sender, MouseEventArgs e)
        {
            if (e.Button == MouseButtons.Left)
            {
            //    TimeSpan Elapsed = new TimeSpan(DateTime.Now.Ticks - CreationTime.Ticks);
                GridVector2 WorldPos = Parent.ScreenToWorld(e.X, e.Y);

                this.radius = GridVector2.Distance(Origin, WorldPos); 

                /*
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
                */

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

            GridVector2 Pos = Origin;

            /*
            bool found = sectionAnnotations.TryGetPositionForLocation(selected, out Pos);
            if (found == false)
                return; 
            */
            Microsoft.Xna.Framework.Color color = new Microsoft.Xna.Framework.Color(CircleColor.R,
                CircleColor.G,
                CircleColor.B,
                128);

            GlobalPrimitives.DrawCircle(graphicsDevice, basicEffect, Pos, this.radius, color); 
              

            base.OnDraw(graphicsDevice, scene, basicEffect);
        }
    }
}
