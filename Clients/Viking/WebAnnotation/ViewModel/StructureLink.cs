using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Windows.Forms; 
using WebAnnotationModel;

namespace WebAnnotation.ViewModel
{
    public class StructureLink : Viking.Objects.UIObjBase
    {

        WebAnnotationModel.StructureLinkObj modelObj;

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
            StructureLink Obj = obj as StructureLink;
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
        /// LocationOnSection is the location on the section being viewed
        /// </summary>
        public Location_CanvasViewModel SourceLocation;

        /// <summary>
        /// LocationOnSection is the location on the reference section
        /// </summary>
        public Location_CanvasViewModel TargetLocation;

        public RoundLineCode.RoundLine lineGraphic;
        public Geometry.GridLineSegment lineSegment;

        public double Radius
        {
            get
            {
             //   return SourceLocation.Radius / 8.0f;
                return SourceLocation.Radius;
            }
        }

        /// <summary>
        /// Use this version only for searches
        /// </summary>
        /// <param name="linkObj"></param>
        public StructureLink(StructureLinkObj linkObj)
            : base()
        {
            this.modelObj = linkObj;
        }

        public StructureLink(StructureLinkObj linkObj, 
                             Location_CanvasViewModel sourceLoc,
                             Location_CanvasViewModel targetLoc) : base()
        {
            this.modelObj = linkObj; 
            this.SourceLocation = sourceLoc;
            this.TargetLocation = targetLoc;
            lineSegment = new Geometry.GridLineSegment(sourceLoc.VolumePosition,
                                                       targetLoc.VolumePosition);
            lineGraphic = new RoundLineCode.RoundLine((float)lineSegment.A.X,
                                                      (float)lineSegment.A.Y,
                                                      (float)lineSegment.B.X,
                                                      (float)lineSegment.B.Y);
        }

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
            StructureLinkObj newLink = new StructureLinkObj(this.TargetID, this.SourceID, this.Bidirectional);
            Store.StructureLinks.Add(newLink);
            Store.StructureLinks.Remove(this.modelObj);

            

            bool Success = Store.StructureLinks.Save();
            if (Success)
            {
                this.modelObj = newLink;
                Location_CanvasViewModel newSource = this.TargetLocation;
                this.TargetLocation = SourceLocation;
                this.SourceLocation = newSource;
                lineSegment = new Geometry.GridLineSegment(SourceLocation.VolumePosition,
                                                           TargetLocation.VolumePosition);
                lineGraphic = new RoundLineCode.RoundLine((float)lineSegment.A.X,
                                                          (float)lineSegment.A.Y,
                                                          (float)lineSegment.B.X,
                                                          (float)lineSegment.B.Y);
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
    }
}
