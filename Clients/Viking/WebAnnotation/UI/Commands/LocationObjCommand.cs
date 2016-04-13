using System;
using System.Collections.Generic;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using System.Windows.Forms;
using System.Diagnostics; 
using System.Linq;
using System.Text;
using WebAnnotationModel; 
using Geometry;
using WebAnnotation.View;
using SqlGeometryUtils;
using VikingXNAGraphics;

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
                if (this.oldMouse.Button == MouseButtons.Left)
                    Parent.Invalidate();
            }

            base.OnMouseMove(sender, e);
        }

        public override void OnDeactivate()
        {
            Viking.UI.State.SelectedObject = null; 

            base.OnDeactivate();
        }
        
        protected override void OnMouseUp(object sender, MouseEventArgs e)
        {
            if (e.Button == MouseButtons.Left)
            {
                //If we clicked a location from another section then create a new linked section
                /*
                if (selected.Section != this.Parent.Section.Number)
                {
                    Debug.Assert(selected != null);
                    GridVector2 WorldPos = Parent.ScreenToWorld(e.X, e.Y);

                    //Transform from volume space to section space if we can
                    GridVector2 SectionPos;
                    try
                    {
                        SectionPos = Parent.VolumeToSection(WorldPos);
                    }
                    catch (ArgumentOutOfRangeException )
                    {
                        Trace.WriteLine("Could not map world point OnMouseUp: " + WorldPos.ToString(), "Command");
                        return;
                    }

                    LocationObj newLoc = new LocationObj(selected.Parent,
                            SectionPos,
                            WorldPos,
                            Parent.Section.Number,
                            selected.TypeCode);

                    LocationCanvasView newLocView = AnnotationViewFactory.Create(newLoc);

                    Viking.UI.Commands.Command.EnqueueCommand(typeof(ResizeCircleCommand), new object[] { Parent, selected.Parent.Type.Color, WorldPos, new ResizeCircleCommand.OnCommandSuccess((double radius) => { newLocView.modelObj.Radius = radius; }) });
                    Viking.UI.Commands.Command.EnqueueCommand(typeof(CreateNewLinkedLocationCommand), new object[] { Parent, selected, newLocView });

                    Viking.UI.State.SelectedObject = null;
                    this.Execute(); 


                }
                else
                {
                    //If we've been dragging a location on the same section then relocate the section
                    GridVector2 WorldPos = Parent.ScreenToWorld(e.X, e.Y);
                    //Transform from volume space to section space if we need to
                    GridVector2 SectionPos;

                    try
                    {
                        SectionPos = Parent.VolumeToSection(WorldPos);
                    }
                    catch (ArgumentOutOfRangeException )
                    {
                        Trace.WriteLine("Could not map world point OnMouseUp: " + WorldPos.ToString(), "Command");
                        return;
                    }

                    //selected.SectionPosition = SectionPos;
                    selected.MosaicShape = selected.MosaicShape.MoveTo(SectionPos);
                    
                    //Send changes to DB
                    Store.Locations.Save();

                    this.Deactivated = true; 
                }*/
            }
            else if (e.Button != MouseButtons.Right)
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
