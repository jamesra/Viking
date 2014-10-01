using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Windows.Forms;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using Util;
using System.Drawing;
using System.Diagnostics;
using WebAnnotation.Objects; 

using Viking.Common; 

namespace WebAnnotation.UI.Commands
{
    /// <summary>
    /// This command has two parts.  The first phase is when the user left-clicks the location for the structure. 
    /// If the structure type does not have a parent we create the structure.  Otherwise we create a link parent 
    /// command which requires the user to select the parent structure.  Once that is done the structure is created
    /// </summary>
    [Viking.Common.CommandAttribute(typeof(WebAnnotation.Objects.StructureTypeObj))]
    class CreateStructureCommand : Viking.UI.Commands.Command
    {
        public CreateStructureCommand(Viking.UI.Controls.SectionViewerControl parent)
            : base(parent)
        {
            parent.Cursor = Cursors.Cross;
        }

        public override void OnDeactivate()
        {
           Parent.Cursor = Cursors.Default; 
           base.OnDeactivate();
        }

        public override void OnMouseMove(object sender, MouseEventArgs e)
        {
            base.OnMouseMove(sender, e);

            Parent.Invalidate(); 
        }

        /// <summary>
        /// When the user left clicks the control we create a new structure at that location
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="e"></param>
        public override void OnMouseClick(object sender, System.Windows.Forms.MouseEventArgs e)
        {
            //Create a new structure on left click
            if (e.Button == MouseButtons.Left)
            {
                WebAnnotation.Objects.StructureTypeObj obj = Viking.UI.State.SelectedObject as WebAnnotation.Objects.StructureTypeObj; 
            //  Debug.Assert(obj == null, "This command should be inactive if Selected Object isn't a StructureTypeObj"); 
                if(obj == null)
                    return;

                GridVector2 WorldPos = Parent.ScreenToWorld(e.X, e.Y);

                //Transform from volume space to section space if we need to
                GridVector2 SectionPos = Parent.Section.VolumeToSection(WorldPos); 

                StructureObj newStruct = new StructureObj(obj);

                LocationObj newLocation = new LocationObj(newStruct,
                                                SectionPos,
                                                WorldPos,
                                                Parent.Section.Number); 

                if (obj.Parent == null)
                {
                    StructureStore.CreateStructure(newStruct, newLocation);
                }
                else
                {
                    Parent.CurrentCommand = new LinkStructureToParentCommand(Parent, newStruct, newLocation); 
                }

            }
            else if (e.Button == MouseButtons.Right)
            {
                this.Deactivated = true; 
            }

            base.OnMouseClick(sender, e);
        }

        public override void OnDraw(GraphicsDevice graphicsDevice, GridQuad bounds, double DownSample, BasicEffect basicEffect)
        {
            WebAnnotation.Objects.StructureTypeObj obj = Viking.UI.State.SelectedObject as WebAnnotation.Objects.StructureTypeObj; 
        //    Debug.Assert(obj == null, "This command should be inactive if Selected Object isn't a StructureTypeObj"); 
            if(obj == null)
                return;

            Parent.fonts.Begin();

            string title = obj.Code; 

            if(this.Parent.fonts != null && this.oldMouse != null)
            {
                
                Vector2 offset = Parent.fontArial.MeasureString(title);
                offset.X /= 2;
                offset.Y /= 2; 
                Parent.fonts.DrawString(Parent.fontArial, 
                    title,
                    new Vector2((float)this.oldMouse.X - offset.X, (float)this.oldMouse.Y - offset.Y), 
                    new Microsoft.Xna.Framework.Graphics.Color(obj.Color.R, obj.Color.G, obj.Color.B, 196));
                
            }

            Parent.fonts.End(); 
            
            return;
        }
    }
}
