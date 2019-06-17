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
using Viking.UI.Commands;
using WebAnnotation.ViewModel;
using WebAnnotationModel;
using VikingXNAGraphics;
using VikingXNAWinForms;

using Viking.Common; 

namespace WebAnnotation.UI.Commands
{
    /// <summary>
    /// This command has two parts.  The first phase is when the user left-clicks the location for the structure. 
    /// If the structure type does not have a parent we create the structure.  Otherwise we create a link parent 
    /// command which requires the user to select the parent structure.  Once that is done the structure is created
    /// </summary>
    [Viking.Common.CommandAttribute(typeof(WebAnnotation.ViewModel.StructureType))]
    class PlaceStructureCommand : AnnotationCommandBase
    {
        StructureType Type = Viking.UI.State.SelectedObject as StructureType;
        Viking.VolumeModel.IVolumeToSectionTransform mapping;

        public PlaceStructureCommand(Viking.UI.Controls.SectionViewerControl parent)
            : base(parent)
        {
            this.Type = Viking.UI.State.SelectedObject as StructureType; 
            parent.Cursor = Cursors.Cross;
            mapping = parent.Section.ActiveSectionToVolumeTransform;
        }

        public PlaceStructureCommand(Viking.UI.Controls.SectionViewerControl parent, StructureType type)
            : base(parent)
        {
            this.Type = type;
            parent.Cursor = Cursors.Cross;
            mapping = parent.Section.ActiveSectionToVolumeTransform;
        }

        public override void OnDeactivate()
        {
           Parent.Cursor = Cursors.Default; 
           base.OnDeactivate();
        }

        protected override void OnMouseMove(object sender, MouseEventArgs e)
        {
            base.OnMouseMove(sender, e);

            Parent.Invalidate(); 
        }

        /// <summary>
        /// When the user left clicks the control we create a new structure at that location
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="e"></param>
        protected override void OnMouseDown(object sender, System.Windows.Forms.MouseEventArgs e)
        {
            //Create a new structure on left click
            if (e.Button.Left())
            {
            //  Debug.Assert(obj == null, "This command should be inactive if Selected Object isn't a StructureTypeObj"); 
                if (Type == null)
                    return;

                GridVector2 WorldPos = Parent.ScreenToWorld(e.X, e.Y);

                //Transform from volume space to section space if we need to
                GridVector2 SectionPos;

                bool Transformed= mapping.TryVolumeToSection(WorldPos, out SectionPos);

                if (!Transformed)
                {
                    this.Deactivated = true;
                    base.OnMouseClick(sender, e);
                }
                    

                StructureObj newStruct = new StructureObj(Type.modelObj);

                LocationObj newLocation = new LocationObj(newStruct,
                                                Parent.Section.Number,
                                                LocationType.CIRCLE);

                newLocation.MosaicShape = SqlGeometryUtils.Extensions.ToCircle(SectionPos.X, SectionPos.Y, Parent.Section.Number, 16.0);
                newLocation.VolumeShape = SqlGeometryUtils.Extensions.ToCircle(WorldPos.X, WorldPos.Y, Parent.Section.Number, 16.0);

                Parent.CommandQueue.EnqueueCommand(typeof(ResizeCircleCommand), new object[] { Parent, Type.Color, WorldPos,
                        new ResizeCircleCommand.OnCommandSuccess((double radius) => 
                        {
                            radius = radius < Global.MinRadius ? Global.MinRadius : radius;
                            WebAnnotation.View.LocationActions.UpdateCircleLocationNoSaveCallback(newLocation, WorldPos, SectionPos, radius);
                        }) });
                if (Type.Parent != null)
                {
                    //Enqueue extra command to select a parent
                    Parent.CommandQueue.EnqueueCommand(typeof(LinkStructureToParentCommand), new object[] { Parent, newStruct, newLocation });
                }

                Parent.CommandQueue.EnqueueCommand(typeof(CreateNewStructureCommand), new object[] { Parent, newStruct, newLocation });
            
                Execute();
            }
            else if (e.Button.Right())
            {
                this.Deactivated = true; 
            }

            base.OnMouseClick(sender, e);
        }

        public override void OnDraw(GraphicsDevice graphicsDevice, VikingXNA.Scene scene, BasicEffect basicEffect)
        {
            StructureType obj = Type; 
        //    Debug.Assert(obj == null, "This command should be inactive if Selected Object isn't a StructureTypeObj"); 
            if(obj == null)
                return;

            Parent.spriteBatch.Begin();

            string title = obj.Code; 

            if(this.Parent.spriteBatch != null && this.oldMouse != null)
            {
                
                Vector2 offset = Parent.fontArial.MeasureString(title);
                offset.X /= 2;
                offset.Y /= 2; 
                Parent.spriteBatch.DrawString(Parent.fontArial, 
                    title,
                    new Vector2((float)this.oldMouse.X - offset.X, (float)this.oldMouse.Y - offset.Y), 
                    new Microsoft.Xna.Framework.Color(obj.Color.R, obj.Color.G, obj.Color.B, 196));
                
            }

            Parent.spriteBatch.End(); 
            
            return;
        }
    }
}
