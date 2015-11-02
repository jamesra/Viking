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
using WebAnnotation.View;
using WebAnnotation.ViewModel;
using WebAnnotationModel; 
using VikingXNAGraphics;

namespace WebAnnotation.UI.Commands
{
    class LinkAnnotationsCommand : AnnotationCommandBase
    {
        LocationObj OriginObj;
        LocationObj NearestTarget = null;

        public LinkAnnotationsCommand(Viking.UI.Controls.SectionViewerControl parent,
                                               LocationObj existingLoc)
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

            LocationCanvasView nearestVisible = Overlay.GetNearestLocation(WorldPos, out distance);
            if (nearestVisible != null)
            {
                NearestTarget = TrySetTarget(nearestVisible.modelObj);
            }

            base.OnMouseMove(sender, e);

            Parent.Invalidate();
        }


        /// <summary>
        /// Returns the same object if it is a valid target to create a link against.  Otherwise NULL
        /// </summary>
        /// <param name="NearestTarget"></param>
        /// <returns></returns>
        protected LocationObj TrySetTarget(LocationObj nearest_target)
        {
            if (nearest_target == null)
                return null; 

            if (LocationLinkView.IsValidLocationLinkTarget(nearest_target, OriginObj))
                return nearest_target;

            if (StructureLink.IsValidStructureLinkTarget(nearest_target, OriginObj))
                return nearest_target;

            return nearest_target; 
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
                LocationCanvasView nearest = Overlay.GetNearestLocation(WorldPos, out distance);
                NearestTarget = nearest != null ? nearest.modelObj : null;

                TrySetTarget(NearestTarget);

                if (NearestTarget == null)
                {
                    this.Deactivated = true; 
                    return; 
                }

                if(LocationLinkView.IsValidLocationLinkTarget(NearestTarget, OriginObj))
                {
                    try
                    {
                        Store.LocationLinks.CreateLink(OriginObj.ID, NearestTarget.ID);
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
                else if(StructureLink.IsValidStructureLinkTarget(NearestTarget, OriginObj))
                {
                    try
                    {
                        StructureLinkObj linkStruct = new StructureLinkObj(OriginObj.ParentID.Value, NearestTarget.ParentID.Value, false);
                        linkStruct = Store.StructureLinks.Create(linkStruct);
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

        static readonly Color invalidTarget = new Color((byte)255,
                                            (byte)0,
                                            (byte)64,
                                            0.5f);

        static readonly Color validTarget = new Microsoft.Xna.Framework.Color((byte)0,
                                (byte)255,
                                (byte)0,
                                (byte)128);

        static readonly Color noTarget = new Color(Color.White.R,
                                    Color.White.G,
                                    Color.White.B,
                                    0.5f);

        static readonly string InvalidTargetStyle = null;
        static readonly string LocationLinkStyle = null;
        static readonly string StructureLinkStyle = "AnimatedLinear";

        private double LineRadiusForLocationLink() { return OriginObj.Radius / 6.0; }
        private double LineRadiusForStructureLink()
        {
            if (NearestTarget == null)
                return OriginObj.Radius;

            return Math.Min(OriginObj.Radius, NearestTarget.Radius);
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

            Color lineColor = noTarget;
            String lineStyle = null;
            double lineRadius = LineRadiusForLocationLink();
            bool UseLumaLineManager = false;

            if (NearestTarget != null)
            {
                if(LocationLinkView.IsValidLocationLinkTarget(NearestTarget, OriginObj))
                {
                    lineColor = validTarget;
                    lineStyle = LocationLinkStyle;
                    lineRadius = LineRadiusForLocationLink();
                    UseLumaLineManager = true;
                }
                else if(StructureLink.IsValidStructureLinkTarget(NearestTarget, OriginObj))
                {
                    lineColor = validTarget;
                    lineStyle = StructureLinkStyle;
                    lineRadius = LineRadiusForStructureLink();
                    UseLumaLineManager = false;
                } 
                else
                {
                    lineColor = invalidTarget;
                    lineStyle = InvalidTargetStyle;
                    lineRadius = LineRadiusForLocationLink();
                    UseLumaLineManager = true;
                }
            }

            RoundLine lineToParent = new RoundLine((float)OriginPosition.X,
                                                   (float)OriginPosition.Y,
                                                   (float)target.X,
                                                   (float)target.Y);
               
            float Time = (float)TimeSpan.FromTicks(DateTime.Now.Ticks - DateTime.Today.Ticks).TotalSeconds;
            RoundLineManager lineManager = UseLumaLineManager ? Parent.LumaOverlayLineManager : Parent.LineManager;
            lineColor = UseLumaLineManager ? lineColor.ConvertToHSL() : lineColor;
            lineManager.Draw(lineToParent,
                                    (float)(lineRadius),
                                    lineColor,
                                    basicEffect.View * basicEffect.Projection,
                                    Time,
                                    lineStyle);


            base.OnDraw(graphicsDevice, scene, basicEffect);
        }
    }
}
