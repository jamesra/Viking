using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Windows.Forms;
using WebAnnotationModel;
using WebAnnotation.View;
using Geometry;
using Microsoft.Xna.Framework.Graphics;
using Microsoft.Xna.Framework;
using VikingXNA;
using VikingXNAGraphics;
using SqlGeometryUtils;

namespace WebAnnotation.ViewModel
{
    abstract class StructureLinkViewModelBase : Viking.Objects.UIObjBase, ICanvasView
    {
        WebAnnotationModel.StructureLinkObj modelObj;

        /// <summary>
        /// LocationOnSection is the location on the section being viewed
        /// </summary>
        public LocationObj SourceLocation;

        /// <summary>
        /// LocationOnSection is the location on the reference section
        /// </summary>
        public LocationObj TargetLocation;

        public override string ToString()
        {
            return modelObj.ToString();
        }

        public override int GetHashCode()
        {
            return modelObj.GetHashCode();
        }

        public override bool Equals(object obj)
        {
            StructureLinkViewModelBase Obj = obj as StructureLinkViewModelBase;
            if (Obj != null)
            {
                return modelObj.Equals(Obj.modelObj);
            }

            StructureLinkObj Obj2 = obj as StructureLinkObj;
            if (Obj2 != null)
            {
                return modelObj.Equals(Obj2);
            }

            return false;
        }

        public long SourceID
        {
            get
            {
                return modelObj.SourceID;
            }
        }

        public long TargetID
        {
            get
            {
                return modelObj.TargetID;
            }
        }

        public bool Bidirectional
        {
            get { return modelObj.Bidirectional; }
            set {
                modelObj.Bidirectional = value; 
            }
        }

        

        /// <summary>
        /// Use this version only for searches
        /// </summary>
        /// <param name="linkObj"></param>
        public StructureLinkViewModelBase(StructureLinkObj linkObj)
            : base()
        {
            this.modelObj = linkObj;
        }

        public StructureLinkViewModelBase(StructureLinkObj linkObj, 
                             LocationObj sourceLoc,
                             LocationObj targetLoc) : base()
        {
            this.modelObj = linkObj; 
            this.SourceLocation = sourceLoc;
            this.TargetLocation = targetLoc;
            CreateView(linkObj,sourceLoc, targetLoc);
        }

        protected abstract void CreateView(StructureLinkObj link, LocationObj source, LocationObj target);

        public override System.Windows.Forms.ContextMenu ContextMenu
        {
            get
            {
                ContextMenu menu = new ContextMenu();
                MenuItem menuFlip = new MenuItem("Flip Direction", ContextMenu_OnFlip);

                MenuItem menuBidirectional = new MenuItem("Bidirectional", ContextMenu_OnBidirectional);
                menuBidirectional.Checked = this.modelObj.Bidirectional; 

                MenuItem menuSeperator = new MenuItem();
                MenuItem menuDelete = new MenuItem("Delete", ContextMenu_OnDelete);

                if(!Bidirectional)
                    menu.MenuItems.Add(menuFlip);

                menu.MenuItems.Add(menuBidirectional);
                menu.MenuItems.Add(menuSeperator);
                menu.MenuItems.Add(menuDelete);

                return menu;
            }
        }

        protected void ContextMenu_OnFlip(object sender, EventArgs e)
        {
            Store.StructureLinks.Remove(this.modelObj); 
            bool Success = Store.StructureLinks.Save();
            if (Success)
            {
                StructureLinkObj newLink = new StructureLinkObj(this.TargetID, this.SourceID, this.Bidirectional);
                Store.StructureLinks.Create(newLink);

                this.modelObj = newLink;
                LocationObj newSource = this.TargetLocation;
                this.TargetLocation = SourceLocation;
                this.SourceLocation = newSource;
                CreateView(newLink, SourceLocation, TargetLocation);
            }
        }

        protected void ContextMenu_OnBidirectional(object sender, EventArgs e)
        {
            this.modelObj.Bidirectional = !this.modelObj.Bidirectional;
            Store.StructureLinks.Save(); 
        }

        protected void ContextMenu_OnDelete(object sender, EventArgs e)
        {
            Delete();
        }

        public override void Delete()
        {
            Store.StructureLinks.Remove(this.modelObj);
            Store.StructureLinks.Save(); 
        }

        public override void Save()
        {
            Store.StructureLinks.Save();
        }


        /// <summary>
        /// Return true if two annotations can be joined with a structure link
        /// </summary>
        /// <param name="TargetObj"></param>
        /// <param name="OriginObj"></param>
        /// <returns></returns>
        public static bool IsValidStructureLinkTarget(LocationObj TargetObj, LocationObj OriginObj)
        {
            if (TargetObj == null || OriginObj == null)
                return false;

            //Cannot link a location object to itself
            if (TargetObj.ID == OriginObj.ID)
                return false;

            return IsValidStructureLinkTarget(TargetObj.Parent, OriginObj.Parent);
        }

        private static bool IsExistingLink(StructureObj TargetObj, StructureObj OriginObj)
        {
            //Do not recreate existing link
            if (TargetObj.LinksCopy.Any(link => (link.SourceID == TargetObj.ID && link.TargetID == OriginObj.ID) ||
                                                (link.SourceID == OriginObj.ID && link.TargetID == TargetObj.ID)))
                return true;

            //Do not recreate existing link
            if (OriginObj.LinksCopy.Any(link => (link.SourceID == TargetObj.ID && link.TargetID == OriginObj.ID) ||
                                                (link.SourceID == OriginObj.ID && link.TargetID == TargetObj.ID)))
                return true;

            return false; 
        }
        
        /// <summary>
        /// Return true if two annotations can be joined with a structure link
        /// </summary>
        /// <param name="TargetObj"></param>
        /// <param name="OriginObj"></param>
        /// <returns></returns>
        public static bool IsValidStructureLinkTarget(StructureObj TargetObj, StructureObj OriginObj)
        {
            if (TargetObj == null || OriginObj == null)
                return false; 

            //Cannot link a structure to itself
            if (TargetObj.ID == OriginObj.ID)
                return false;

            if (IsExistingLink(TargetObj, OriginObj))
                return false;

            //Can link synapses with the same parent
            if (TargetObj.ParentID == OriginObj.ParentID)
                return true;

            //Cannot link to higher levels in our parent heirarchy
            if (OriginObj.ParentID.HasValue && !IsValidStructureLinkTarget(TargetObj, OriginObj.Parent))
            {
                return false;
            }

            if (TargetObj.ParentID.HasValue && !IsValidStructureLinkTarget(TargetObj.Parent, OriginObj))
            {
                return false;
            }

            return true;
        }

        public abstract bool IsVisible(Scene scene);
        public abstract bool Intersects(GridVector2 Position);
        public abstract double Distance(GridVector2 Position);
        public abstract double DistanceFromCenterNormalized(GridVector2 Position);

        public abstract Geometry.GridRectangle BoundingBox
        {
            get;
        } 
    }

    class StructureLinkCirclesView : StructureLinkViewModelBase
    {
        public LineView lineView;  
        public Geometry.GridLineSegment lineSegment;

        public double LineWidth
        {
            get
            {
                return ((SourceLocation.Radius + TargetLocation.Radius));
            }
        }

        public double Radius
        {
            get
            {
                return this.LineWidth / 2.0;
            }
        }

        public float alpha
        {
            get { return (float)color.A / 255.0f; }
            set {
                lineView.Color = new Microsoft.Xna.Framework.Color((int)lineView.Color.R,
                                                                   (int)lineView.Color.G,
                                                                   (int)lineView.Color.B,
                                                                   (int)(value * 255.0f));
            }
        }

        public Microsoft.Xna.Framework.Color color
        {
            get { return lineView.Color; }
            set { lineView.Color = value; }
        }


        public static Microsoft.Xna.Framework.Color DefaultColor = new Microsoft.Xna.Framework.Color((byte)(255),
                (byte)(255),
                (byte)(255),
                (byte)(128));

        public StructureLinkCirclesView(StructureLinkObj linkObj,
                             LocationObj sourceLoc,
                             LocationObj targetLoc) : base(linkObj, sourceLoc, targetLoc)
        {

        }


        public override double Distance(GridVector2 Position)
        {
            return lineSegment.DistanceToPoint(Position) - this.Radius;
        }

        public override double DistanceFromCenterNormalized(GridVector2 Position)
        {
            return lineSegment.DistanceToPoint(Position) / (this.LineWidth / 2.0);
        }

        public override bool Intersects(GridVector2 Position)
        {
            return lineSegment.DistanceToPoint(Position) < this.LineWidth;
        }

        public override bool IsVisible(Scene scene)
        {
            //Do not draw unless the line is at least four pixels wide
            return this.LineWidth >= Math.Max(scene.DevicePixelWidth, scene.DevicePixelHeight) * 4;
        }

        public override Geometry.GridRectangle BoundingBox
        {
            get
            {
                return lineSegment.BoundingBox.Pad(this.LineWidth);
            }
        }

        protected override void CreateView(StructureLinkObj link, LocationObj source, LocationObj target)
        {
            lineSegment = new Geometry.GridLineSegment(source.VolumePosition,
                                                       target.VolumePosition);

            lineView = new LineView(source.VolumePosition, target.VolumePosition, Math.Min(source.Radius, target.Radius), DefaultColor,
                                    link.Bidirectional ? LineStyle.AnimatedBidirectional : LineStyle.AnimatedLinear); 
        }

        public static void Draw(GraphicsDevice device,
                          VikingXNA.Scene scene,
                          RoundLineCode.RoundLineManager lineManager,
                          StructureLinkCirclesView[] listToDraw)
        {
            LineView[] linesToDraw = listToDraw.Select(l => l.lineView).ToArray();

            LineView.Draw(device, scene, lineManager, linesToDraw);
        }
    }

    /// <summary>
    /// Link structures represented by curves
    /// </summary>
    class StructureLinkCurvesView : StructureLinkViewModelBase
    {
        public LinkedPolyLineSimpleView lineView; 
        public Geometry.GridLineSegment[] lineSegments;
        public static float DefaultLineWidth = 16.0f;
        
        public double LineWidth
        {
            get
            {
                return ((SourceLocation.Radius + TargetLocation.Radius));
            }
        }

        public double Radius
        {
            get
            {
                return this.LineWidth / 2.0;
            }
        }

        public float alpha
        {
            get { return (float)color.A / 255.0f; }
            set
            {
                lineView.Color = new Microsoft.Xna.Framework.Color((int)lineView.Color.R,
                                                                   (int)lineView.Color.G,
                                                                   (int)lineView.Color.B,
                                                                   (int)(value * 255.0f));
            }
        }

        public Microsoft.Xna.Framework.Color color
        {
            get { return lineView.Color; }
            set { lineView.Color = value; }
        }


        public static Microsoft.Xna.Framework.Color DefaultColor = new Microsoft.Xna.Framework.Color((byte)(255),
                (byte)(255),
                (byte)(255),
                (byte)(192));

        public StructureLinkCurvesView(StructureLinkObj linkObj,
                             LocationObj sourceLoc,
                             LocationObj targetLoc) : base(linkObj, sourceLoc, targetLoc)
        {
            CreateLineSegments();
        }

        private void CreateLineSegments()
        {
            this.lineSegments = lineView.Lines.Select(l => new GridLineSegment(l.Source, l.Destination)).ToArray();
        }


        public override double Distance(GridVector2 Position)
        {
            return lineSegments.Select(l => l.DistanceToPoint(Position) - this.Radius).Min();
        }

        public override double DistanceFromCenterNormalized(GridVector2 Position)
        {
            return lineSegments.Select(l => l.DistanceToPoint(Position) / (this.LineWidth / 2.0)).Min();
        }

        public override bool Intersects(GridVector2 Position)
        {
            return lineSegments.Any(l => l.DistanceToPoint(Position) < this.LineWidth);
        }

        public override bool IsVisible(Scene scene)
        {
            //Do not draw unless the line is at least four pixels wide
            return this.LineWidth >= Math.Max(scene.DevicePixelWidth, scene.DevicePixelHeight) * 4;
        }

        public override Geometry.GridRectangle BoundingBox
        {
            get
            {
                GridRectangle bbox = lineSegments[0].BoundingBox;
                foreach(GridLineSegment l in lineSegments)
                {
                    bbox.Union(l.BoundingBox);
                }

                bbox.Union(bbox.LowerLeft - new GridVector2(this.Radius, this.Radius));
                bbox.Union(bbox.UpperRight + new GridVector2(this.Radius, this.Radius));

                return bbox;
            }
        }

        protected override void CreateView(StructureLinkObj link, LocationObj source, LocationObj target)
        {
            lineView = new LinkedPolyLineSimpleView(source.VolumeShape.ToPoints(), target.VolumeShape.ToPoints(), DefaultLineWidth, DefaultColor, link.Bidirectional ? LineStyle.AnimatedBidirectional : LineStyle.AnimatedLinear);          
        }

        public static void Draw(GraphicsDevice device,
                          VikingXNA.Scene scene,
                          RoundLineCode.RoundLineManager lineManager,
                          StructureLinkCurvesView[] listToDraw)
        {
            LinkedPolyLineSimpleView[] linesToDraw = listToDraw.Select(l => l.lineView).ToArray();

            LinkedPolyLineSimpleView.Draw(device, scene, lineManager, linesToDraw);
        }
    }
}
