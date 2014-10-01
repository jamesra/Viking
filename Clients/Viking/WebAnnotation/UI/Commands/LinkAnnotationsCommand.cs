using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Windows.Forms;

using System.Diagnostics;
using WebAnnotation;
using Geometry;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using RoundLineCode;
using WebAnnotation.ViewModel;
using WebAnnotationModel;

namespace WebAnnotation.UI.Commands
{
    class LinkAnnotationsCommand : AnnotationCommandBase
    {
        Location_CanvasViewModel OriginObj;
        Location_CanvasViewModel NearestTarget = null;

        public LinkAnnotationsCommand(Viking.UI.Controls.SectionViewerControl parent,
                                               Location_CanvasViewModel existingLoc)
            : base(parent)
        {
            OriginObj = existingLoc;
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
            NearestTarget = Overlay.GetNearestLocation(WorldPos, out distance);
            NearestTarget = ValidateTarget(NearestTarget); 

            base.OnMouseMove(sender, e);

            Parent.Invalidate();
        }


        /// <summary>
        /// Returns the same object if it is a valid target to create a link against.  Otherwise NULL
        /// </summary>
        /// <param name="NearestTarget"></param>
        /// <returns></returns>
        protected Location_CanvasViewModel ValidateTarget(Location_CanvasViewModel NearestTarget)
        {
            if (NearestTarget != null)
            {
                //Check to make sure it isn't the same structure on the same section
                if (NearestTarget.ParentID == OriginObj.ParentID)
                {
                    if (NearestTarget.Z == OriginObj.Z)
                    {
                        //Not a valid target for a link
                        NearestTarget = null;
                    }
                    else
                    {
                        //Make sure the locations aren't already linked
                        foreach (long linkID in OriginObj.Links)
                        {
                            if (linkID == NearestTarget.ID)
                            {
                                //They are already linked, so not a valid target
                                NearestTarget = null;
                                break;
                            }
                        }
                    }
                }
            }

            return NearestTarget; 
        }

        protected override void OnMouseUp(object sender, MouseEventArgs e)
        {
            //Figure out if we've clicked another structure and create the structure
            if (e.Button == MouseButtons.Left)
            {
                GridVector2 WorldPos = Parent.ScreenToWorld(e.X, e.Y);

                SectionLocationsViewModel sectionAnnotations = AnnotationOverlay.GetAnnotationsForSection(Parent.Section.Number);
                if (sectionAnnotations == null)
                    return; 

                //Find if we are close enough to a location to "snap" the line to the target
                double distance;
                NearestTarget = Overlay.GetNearestLocation(WorldPos, out distance);
                NearestTarget = ValidateTarget(NearestTarget); 

                if(NearestTarget == null)
                {
                    this.Deactivated = true; 
                    return; 
                }

                if(NearestTarget.ParentID == OriginObj.ParentID)
                {
                    try
                    {
                        Store.LocationLinks.CreateLink(OriginObj.modelObj.ID, NearestTarget.modelObj.ID);
                    }
                    catch (Exception except)
                    {
                        MessageBox.Show("Could not create link between locations: " + except.Message, "Recoverable Error");
                    }
                    finally
                    {
                        this.Deactivated = true;
                    }
                }
                else
                {
                    try
                    {
                        StructureLinkObj linkStruct = new StructureLinkObj(OriginObj.ParentID.Value, NearestTarget.ParentID.Value, false);
                        Store.StructureLinks.Add(linkStruct);
                        Store.StructureLinks.Save();
                    }
                    catch (Exception except)
                    {
                        MessageBox.Show("Could not create link between structures: " + except.Message, "Recoverable Error");
                    }
                    finally
                    {
                        this.Deactivated = true;
                    }

                    //HACK: This updates the UI to show the new structure link.  It should be automatic, but force it for now...
                    //sectionAnnotations.AddStructureLinks(OriginObj.Parent);
                    //sectionAnnotations.AddStructureLinks(NearestTarget.Parent);
                }

                this.Execute(); 
            }

            base.OnMouseDown(sender, e);
        }

        protected override void Execute()
        {
            try
            {
            }
            catch (ArgumentOutOfRangeException )
            {
                MessageBox.Show("The chosen point is outside mappable volume space, location not created", "Recoverable Error");
            }

            base.Execute();
        }

        public override void OnDraw(GraphicsDevice graphicsDevice, VikingXNA.Scene scene, BasicEffect basicEffect)
        {
            if (this.oldMouse == null)
                return;

            WebAnnotation.ViewModel.SectionLocationsViewModel sectionAnnotations = AnnotationOverlay.GetAnnotationsForSection(OriginObj.Section);
            if (sectionAnnotations == null)
                return;

            GridVector2 OriginPosition = OriginObj.VolumePosition; 

            Vector3 target;
            if (NearestTarget != null)
            {
                //Snap the line to a nearby target if it exists
                GridVector2 targetPos = NearestTarget.VolumePosition; 
                
                /*bool success = sectionAnnotations.TryGetPositionForLocation(NearestTarget, out targetPos);
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

            Color lineColor = new Color(Color.Black.R,
                                        Color.Black.G,
                                        Color.Black.B, 
                                        0.5f);
            
            if (NearestTarget != null)
            {
                Structure TargetStruct = NearestTarget.Parent;
                if (TargetStruct != null)
                {
                    //If they are the same structure on different sections use the color for a location link, otherwise white
                    if (TargetStruct == OriginObj.Parent)
                    {
                        if (NearestTarget.Z != OriginObj.Z)
                        {
                            //Make sure the locations aren't already linked
                            bool ValidTarget = true;
                            foreach (long linkID in OriginObj.Links)
                            {
                                if (linkID == NearestTarget.ID)
                                {
                                    ValidTarget = false;
                                    break; 
                                }
                            }

                            if (ValidTarget)
                            {
                                StructureType type = OriginObj.Parent.Type;
                                //If you don't cast to byte the wrong constructor is used and the alpha value is wrong
                                lineColor = new Microsoft.Xna.Framework.Color((byte)(255 - type.Color.R),
                                    (byte)(255 - type.Color.G),
                                    (byte)(255 - type.Color.B),
                                    (byte)128);
                            }
                        }
                    }
                    else
                    {
                        lineColor = new Color(Color.White.R,
                                              Color.White.G,
                                              Color.White.B, 
                                              0.5f);
                    }
                }
            }

            RoundLine lineToParent = new RoundLine((float)OriginPosition.X,
                                                   (float)OriginPosition.Y,
                                                   (float)target.X,
                                                   (float)target.Y);

            Parent.LineManager.Draw(lineToParent,
                                    (float)(OriginObj.Radius / 6.0),
                                    lineColor,
                                    basicEffect.View * basicEffect.Projection,
                                    1,
                                    null);
        

            base.OnDraw(graphicsDevice, scene, basicEffect);
        }
    }
}
