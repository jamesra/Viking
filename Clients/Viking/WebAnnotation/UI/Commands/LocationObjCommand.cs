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
using WebAnnotation.ViewModel;
using SqlGeometryUtils;

namespace WebAnnotation.UI.Commands
{
    [Viking.Common.CommandAttribute(typeof(Location_CanvasViewModel))]
    class LocationObjCommand : AnnotationCommandBase
    {
        Location_CanvasViewModel selected;
        StructureType LocType;

        public LocationObjCommand(Viking.UI.Controls.SectionViewerControl parent)
            : base(parent)
        {
            selected = Viking.UI.State.SelectedObject as Location_CanvasViewModel;
            Debug.Assert(selected != null);

            if (selected.Parent != null)
            {
                LocType = selected.Parent.Type;
            }
            
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

                    LocationObj newLoc = new LocationObj(selected.Parent.modelObj,
                            SectionPos,
                            WorldPos,
                            Parent.Section.Number,
                            selected.TypeCode);

                    Location_CanvasViewModel newLocView = new Location_CanvasViewModel(newLoc);

                    Viking.UI.Commands.Command.EnqueueCommand(typeof(ResizeCircleCommand), new object[] { Parent, selected.Parent.Type.Color, WorldPos, new ResizeCircleCommand.OnCommandSuccess((double radius) => { newLocView.Radius = radius; }) });
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

                    selected.MosaicShape = selected.MosaicShape.MoveTo(SectionPos);
                    
                    //Send changes to DB
                    Store.Locations.Save();

                    this.Deactivated = true; 
                }
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
                SectionLocationsViewModel sectionAnnotations = AnnotationOverlay.GetAnnotationsForSection(Parent.Section.Number);
                if (sectionAnnotations == null)
                    return;

                GridVector2 selectedPos = selected.VolumePosition; 
                /*bool found = sectionAnnotations.TryGetPositionForLocation(selected, out selectedPos);
                if (found == false)
                    return; 
                */

                //PORT XNA 4
                /*
                VertexDeclaration oldVertexDeclaration = graphicsDevice.VertexDeclaration;
                graphicsDevice.VertexDeclaration = Parent.VertexPositionColorDeclaration; 
                graphicsDevice.RenderState.PointSize = 5.0f;
                */

                basicEffect.Texture = null;
                basicEffect.TextureEnabled = false;
                basicEffect.VertexColorEnabled = true;

                //PORT XNA 4
                //basicEffect.CommitChanges();

                //Draw the new location
                if (LocType != null && selected.Section == Parent.Section.Number)
                {
                    Microsoft.Xna.Framework.Color color = new Microsoft.Xna.Framework.Color(LocType.Color.R,
                        LocType.Color.G,
                        LocType.Color.B,
                        128);
                    
                    GlobalPrimitives.DrawCircle(graphicsDevice, basicEffect, this.oldWorldPosition, selected.Radius, color);
                }
                else
                {

                    VertexPositionColor[] verts = new VertexPositionColor[] {
                                                        new VertexPositionColor(new Vector3((float)selectedPos.X, (float)selectedPos.Y, 0f), Color.Gold),
                                                        new VertexPositionColor(new Vector3((float)this.oldWorldPosition.X, (float)oldWorldPosition.Y, 0f), Color.Gold)};

                    int[] indicies = new int[] { 0, 1 };


                    //PORT XNA 4
                    //basicEffect.Begin();

                    foreach (EffectPass pass in basicEffect.CurrentTechnique.Passes)
                    {
                        //PORT XNA 4
                        //pass.Begin();
                        pass.Apply();

                        if (verts != null && verts.Length > 0)
                            graphicsDevice.DrawUserIndexedPrimitives<VertexPositionColor>(PrimitiveType.LineList, verts, 0, verts.Length, indicies, 0, indicies.Length / 2);

                        //PORT XNA 4
                        //pass.End();
                    }

                    //PORT XNA 4
                    //basicEffect.End();
                }

                //PORT XNA 4
                //graphicsDevice.VertexDeclaration = oldVertexDeclaration; 
            }

            basicEffect.VertexColorEnabled = false;
            //PORT XNA 4
            //basicEffect.CommitChanges();

            base.OnDraw(graphicsDevice, scene, basicEffect);
        }
    }
}
