using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using Common.UI;
using WebAnnotation.Service;
using System.Windows.Forms;

namespace WebAnnotation.Objects
{
    public class StructureLinkObj : WCFObjBase<StructureLink>
    {
        [ColumnAttribute("Source ID")]
        public long SourceID
        {
            get
            {
                return Data.SourceID;
            }
            set
            {
                if (Data.SourceID != value)
                {
                    Data.SourceID = value;
                    ValueChangedEvent("SourceID");
                    SetDBActionForChange();
                }
            }
        }

        [ColumnAttribute("Target ID")]
        public long TargetID
        {
            get
            {
                return Data.TargetID;
            }
            set
            {
                if (Data.TargetID != value)
                {
                    Data.TargetID = value;
                    ValueChangedEvent("TargetID");
                    SetDBActionForChange();
                }
            }
        }

        [ColumnAttribute("Bidirectional")]
        public bool Bidirectional
        {
            get { return Data.Bidirectional; }
            set {
                if (Data.Bidirectional != value)
                {
                    Data.Bidirectional = value;
                    ValueChangedEvent("Bidirectional");
                    SetDBActionForChange();
                }
            }
        }

        /// <summary>
        /// LocationOnSection is the location on the section being viewed
        /// </summary>
        public LocationObj SourceLocation;

        /// <summary>
        /// LocationOnSection is the location on the reference section
        /// </summary>
        public LocationObj TargetLocation;

        public RoundLineCode.RoundLine lineGraphic;

        public Geometry.GridLineSegment lineSegment;

        public double Radius
        {
            get
            {
                return SourceLocation.Radius / 10.0f;
            }
        }

        public StructureLinkObj(long sourceID, long targetID, 
                                LocationObj sourceLoc, LocationObj targetLoc,
                                Geometry.GridLineSegment line) : base()
        {
            this.Data = new StructureLink(); 
            this.SourceID = sourceID;
            this.TargetID = targetID;
            this.SourceLocation = sourceLoc;
            this.TargetLocation = targetLoc;
            lineSegment = line;
            lineGraphic = new RoundLineCode.RoundLine((float)line.A.X,
                                                      (float)line.A.Y,
                                                      (float)line.B.X,
                                                      (float)line.B.Y);
        }

        public override System.Windows.Forms.ContextMenu ContextMenu
        {
            get
            {
                ContextMenu menu = new ContextMenu();

                MenuItem menuSeperator = new MenuItem();
                MenuItem menuDelete = new MenuItem("Delete", ContextMenu_OnDelete);

                menu.MenuItems.Add(menuSeperator);
                menu.MenuItems.Add(menuDelete);

                return menu;
            }
        }

        protected void ContextMenu_OnDelete(object sender, EventArgs e)
        {
            Delete();
        }

        public override void Delete()
        {
            this.DBAction = DBACTION.DELETE;

            Store.Structures.SaveLinks(new StructureLinkObj[] {this});
        }
    }
}
