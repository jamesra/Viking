using Geometry;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using System;
using System.Diagnostics;
using System.Windows.Forms;
using VikingXNAGraphics;
using VikingXNAWinForms;
using WebAnnotation.View;
using WebAnnotationModel;

namespace WebAnnotation.UI.Commands
{
    [Viking.Common.CommandAttribute(typeof(LocationCanvasView))]
    class LocationObjCommand : AnnotationCommandBase
    {
        LocationObj selected;
        StructureTypeObj _LocType = null;

        StructureTypeObj LocType
        {
            get
            {
                if (selected.ParentID.HasValue)
                {
                    StructureObj structure = Store.Structures.GetObjectByID(selected.ParentID.Value, true);
                    _LocType = Store.StructureTypes.GetObjectByID(structure.TypeID, true);
                }

                return _LocType;
            }
        }

        public LocationObjCommand(Viking.UI.Controls.SectionViewerControl parent)
            : base(parent)
        {
            LocationCanvasView select_ViewObj = Viking.UI.State.SelectedObject as LocationCanvasView;
            selected = Store.Locations[select_ViewObj.ID];
            Debug.Assert(selected != null);

            //Figure out if we've selected a location on the same section or different
            if (selected.Section != this.Parent.Section.Number)
            {
                parent.Cursor = Cursors.Cross;
            }
            else
            {
                parent.Cursor = Cursors.Hand;
            }
        }

        protected override void OnMouseMove(object sender, MouseEventArgs e)
        {
            //Redraw if we are dragging a location
            if (this.oldMouse != null)
            {
                if (this.oldMouse.Button.Left())
                    Parent.Invalidate();
            }

            base.OnMouseMove(sender, e);
        }

        protected override void OnDeactivate()
        {
            Viking.UI.State.SelectedObject = null;

            base.OnDeactivate();
        }

        protected override void OnMouseUp(object sender, MouseEventArgs e)
        {
            if (e.Button != MouseButtons.Right)
            {
                //Any other button other than the right button cancels the command
                this.Deactivated = true;
            }


            base.OnMouseUp(sender, e);
        }


        public override void OnDraw(Microsoft.Xna.Framework.Graphics.GraphicsDevice graphicsDevice,
                                    VikingXNA.Scene scene,
                                    Microsoft.Xna.Framework.Graphics.BasicEffect basicEffect)
        {
            if (this.oldMouse == null)
                return;

            if (basicEffect == null)
                throw new ArgumentNullException("basicEffect");

            if (scene == null)
                throw new ArgumentNullException("scene");

            //Draw a line from the selected location to the new location if we are holding left button down
            if (this.oldMouse.Button == MouseButtons.Left)
            {
                GridVector2 selectedPos = selected.VolumePosition;
                /*bool found = sectionAnnotations.TryGetPositionForLocation(selected, out selectedPos);
                if (found == false)
                    return; 
                */

                basicEffect.Texture = null;
                basicEffect.TextureEnabled = false;
                basicEffect.VertexColorEnabled = true;

                //Draw the new location
                if (LocType != null && selected.Section == Parent.Section.Number)
                {
                    Microsoft.Xna.Framework.Color color = LocType.Color.ToXNAColor(0.5f);

                    GlobalPrimitives.DrawCircle(graphicsDevice, basicEffect, this.oldWorldPosition, selected.Radius, color);
                }
                else
                {

                    VertexPositionColor[] verts = new VertexPositionColor[] {
                                                        new VertexPositionColor(new Vector3((float)selectedPos.X, (float)selectedPos.Y, 0f), Color.Gold),
                                                        new VertexPositionColor(new Vector3((float)this.oldWorldPosition.X, (float)oldWorldPosition.Y, 0f), Color.Gold)};

                    int[] indicies = new int[] { 0, 1 };

                    foreach (EffectPass pass in basicEffect.CurrentTechnique.Passes)
                    {
                        pass.Apply();

                        if (verts != null && verts.Length > 0)
                            graphicsDevice.DrawUserIndexedPrimitives<VertexPositionColor>(PrimitiveType.LineList, verts, 0, verts.Length, indicies, 0, indicies.Length / 2);

                    }
                }
            }

            basicEffect.VertexColorEnabled = false;

            base.OnDraw(graphicsDevice, scene, basicEffect);
        }
    }
}
