using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using WebAnnotationModel;
using Geometry;
using WebAnnotation.View;
using SqlGeometryUtils;
using VikingXNAGraphics;
using System.Windows.Forms;
using System.Diagnostics;

namespace WebAnnotation.UI.Commands
{
    class TranslateLocationCommand : AnnotationCommandBase
    {
        LocationCanvasView Loc;

        public delegate void OnCommandSuccess(LocationObj loc, GridVector2 VolumePosition, GridVector2 MosaicPosition);
        OnCommandSuccess success_callback;

        /// <summary>
        /// Translated position in volume space
        /// </summary>
        GridVector2 TranslatedPosition;

        public TranslateLocationCommand(Viking.UI.Controls.SectionViewerControl parent,
                                        LocationCanvasView selectedObj,
                                        OnCommandSuccess success_callback) : base(parent)
        {
            Loc = selectedObj;
            TranslatedPosition = selectedObj.VolumePosition;
            this.success_callback = success_callback;
        }

        protected override void Execute()
        {
            if (this.success_callback != null)
            {
                GridVector2 MosaicPosition; 

                try
                {
                    MosaicPosition = Parent.VolumeToSection(TranslatedPosition);
                }
                catch (ArgumentOutOfRangeException)
                {
                    Trace.WriteLine("TranslateLocationCommand: Could not map world point on Execute: " + TranslatedPosition.ToString(), "Command");
                    return;
                }

                this.success_callback(Loc.modelObj, this.TranslatedPosition, MosaicPosition);
            }
                

            base.Execute();
        }

        public static void DefaultSuccessCallback(LocationObj loc, GridVector2 WorldPosition, GridVector2 MosaicPosition)
        {
            DefaultSuccessNoSaveCallback(loc, WorldPosition, MosaicPosition);
            Store.Locations.Save();
        }

        public static void DefaultSuccessNoSaveCallback(LocationObj loc, GridVector2 WorldPosition, GridVector2 MosaicPosition)
        {
            loc.MosaicShape = loc.MosaicShape.MoveTo(MosaicPosition);
            loc.VolumeShape = loc.VolumeShape.MoveTo(WorldPosition); 
        }

        public override void OnDeactivate()
        {
            Viking.UI.State.SelectedObject = null;

            base.OnDeactivate();
        }

        protected override void OnMouseMove(object sender, MouseEventArgs e)
        {
            //Redraw if we are dragging a location
            if (this.oldMouse != null)
            {
                if (this.oldMouse.Button == MouseButtons.Left)
                {
                    this.TranslatedPosition = Parent.ScreenToWorld(e.X, e.Y);
                    Parent.Invalidate();
                }
            }

            base.OnMouseMove(sender, e);
        }

        protected override void OnMouseUp(object sender, MouseEventArgs e)
        {
            base.OnMouseUp(sender, e);

            this.Execute();            
        }
        
        public override void OnDraw(Microsoft.Xna.Framework.Graphics.GraphicsDevice graphicsDevice,
                                    VikingXNA.Scene scene,
                                    Microsoft.Xna.Framework.Graphics.BasicEffect basicEffect)
        {
            //TODO: Translate the LocationCanvasView before it is drawn
            List<LocationCanvasView> items = new List<LocationCanvasView>();

            items.Add(Loc);
            LocationObjRenderer.DrawBackgrounds(items, graphicsDevice, basicEffect, Parent.annotationOverlayEffect, Parent.LumaOverlayLineManager, scene, Parent.Section.Number);            
        }
    }
}
