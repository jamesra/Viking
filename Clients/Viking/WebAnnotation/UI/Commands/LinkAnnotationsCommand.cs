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
using SqlGeometryUtils;

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

        private static GridVector2 GetOriginForLocation(LocationObj obj)
        {
            switch(obj.TypeCode)
            {
                case LocationType.CIRCLE:
                    return obj.VolumePosition;
                case LocationType.POLYGON:
                    return obj.VolumePosition;
                case LocationType.OPENCURVE:
                    return Midpoint(obj.VolumeShape.ToPoints());
                case LocationType.POLYLINE:
                    return Midpoint(obj.VolumeShape.ToPoints());
                default:
                    return obj.VolumePosition;
            }
        }

        private static GridVector2 Midpoint(GridVector2[] array)
        {
            int i = array.Length / 2;
            return array[i];
        }

        protected IViewLocation NearestLocationToMouse(GridVector2 WorldPos)
        {
            List<HitTestResult> listHitTestResults = Overlay.GetAnnotationsAtPosition(WorldPos);

            listHitTestResults = listHitTestResults.ExpandICanvasViewContainers(WorldPos);

            //Find locations that are not equal to our origin location
            listHitTestResults = listHitTestResults.Where(hr =>
            {
                IViewLocation loc = hr.obj as IViewLocation;
                if (loc == null)
                    return false;
                 
                return loc.ID != OriginObj.ID && !OriginObj.Links.Contains(loc.ID);
            }).ToList();

            IViewLocation nearestVisible = null;
            HitTestResult BestMatch = listHitTestResults.NearestObjectOnCurrentSectionThenAdjacent((int)OriginObj.Z);
            if (BestMatch != null)
            {
                nearestVisible = BestMatch.obj as IViewLocation;
            }

            return nearestVisible;
        }

        protected override void OnMouseMove(object sender, MouseEventArgs e)
        {
            GridVector2 WorldPos = Parent.ScreenToWorld(e.X, e.Y);
    
            //Find if we are close enough to a location to "snap" the line to the target
            double distance;

            IViewLocation nearestVisible = NearestLocationToMouse(WorldPos);
            NearestTarget = nearestVisible != null ? TrySetTarget(Store.Locations[nearestVisible.ID]) : null;

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

            if (StructureLinkViewModelBase.IsValidStructureLinkTarget(nearest_target, OriginObj))
                return nearest_target;

            return nearest_target; 
        }

        protected override void OnMouseUp(object sender, MouseEventArgs e)
        {
            //Figure out if we've clicked another structure and create the structure
            if (e.Button.Left())
            {
                GridVector2 WorldPos = Parent.ScreenToWorld(e.X, e.Y);
                
                //Find if we are close enough to a location to "snap" the line to the target
                IViewLocation nearest = NearestLocationToMouse(WorldPos);
                NearestTarget = nearest != null ? Store.Locations[nearest.ID] : null;

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
                else if(StructureLinkViewModelBase.IsValidStructureLinkTarget(NearestTarget, OriginObj))
                {
                    try
                    {
                        bool Bidirectional = NearestTarget.Parent.Type.ID == OriginObj.Parent.Type.ID;
                        StructureLinkObj linkStruct = new StructureLinkObj(OriginObj.ParentID.Value, NearestTarget.ParentID.Value, Bidirectional);
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
            
            GridVector2 OriginPosition = GetOriginForLocation(OriginObj);

            Vector3 target;
            if (NearestTarget != null)
            {
                //Snap the line to a nearby target if it exists
                GridVector2 targetPos = GetOriginForLocation(NearestTarget);
                
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
                else if(StructureLinkViewModelBase.IsValidStructureLinkTarget(NearestTarget, OriginObj))
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
