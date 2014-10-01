using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Diagnostics; 
using WebAnnotation.Service;
using System.Drawing;
using System.Windows.Forms;
using Viking.Common; 

using Common.UI; 

namespace WebAnnotation.Objects
{
    [Common.UI.TreeViewVisible]
    public class StructureTypeObj : WCFObjBaseWithParent<StructureType, StructureTypeObj>
    {
        public override string ToString()
        {
            return this.Name; 
        }

        [Column("Name")]
        public string Name
        {
            get { return Data.Name; }
            set { Data.Name = value; 
                  SetDBActionForChange();
                  ValueChangedEvent("Name");
            }
        }

        [Column("Notes")]
        public string Notes
        {
            get { return Data.Notes; }
            set
            {
                Data.Notes = value;
                SetDBActionForChange();
                ValueChangedEvent("Notes");
            }
        }

        [Column("Color")]
        public System.Drawing.Color Color
        {
            get { return Color.FromArgb(Data.Color); }
            set
            {
                Data.Color = value.ToArgb(); 
                SetDBActionForChange();
                ValueChangedEvent("Color");
            }
        }

        [Column("Code")]
        public string Code
        {
            get { return Data.Code; }
            set
            {
                Data.Code = value;
                SetDBActionForChange();
                ValueChangedEvent("Code");
            }
        }
        
        public StructureTypeObj()
        {
            if(this.Data == null)
                this.Data = new StructureType();

            this.Data.DBAction = DBACTION.INSERT;
            this.Data.Name = "New Structure Type";
            this.Data.MarkupType = "Point";
            this.Data.ID = Store.StructureTypes.GetTempID();
            this.Data.Tags = new String[0];
            this.Data.StructureTags = new String[0];
            this.Data.Code = "NoCode";

            Store.StructureTypes.Add(this); 
        }

        public StructureTypeObj(StructureType data)
        {
            this.Data = data;
            this.Data.Code = this.Data.Code.Trim();
        }

        public StructureTypeObj(StructureTypeObj parent) : this()
        {
            if(this.Data == null)
                this.Data = new StructureType();

            if (parent != null)
            {
                this.Data.ParentID = parent.ID;
            }
        }

        protected override StructureTypeObj OnMissingParent()
        {
            return Store.StructureTypes.GetObjectByID(this.ParentID.Value, true); 
        }

        #region IUIObject Members

        public override System.Windows.Forms.ContextMenu ContextMenu
        {
            get 
            {
                ContextMenu menu = new ContextMenu();

                MenuItem newMenuItem = new MenuItem("New");
                menu.MenuItems.Add(newMenuItem); 

                newMenuItem.MenuItems.Add("Structure Type", ContextMenu_OnNewStructureType);

                if(this.Children.Length == 0)
                    menu.MenuItems.Add("Delete", ContextMenu_OnDelete); 

                menu.MenuItems.Add("Properties", ContextMenu_OnProperties);

                

                return menu;
            }
        }

        public override System.Drawing.Image SmallThumbnail
        {
            get { return null; }
        }

        public override string ToolTip
        {
            get { return this.Name; }
        }

        public override void Save()
        {
            Store.StructureTypes.Save();
        }

        public override Viking.UI.Controls.GenericTreeNode CreateNode()
        {
            return new Viking.UI.Controls.GenericTreeNode(this); 
        }

        public override int TreeImageIndex
        {
            get { return 0; }
        }

        public override int TreeSelectedImageIndex
        {
            get { return 0;  }
        }

        public override Type[] AssignableParentTypes
        {
            get { return new Type[] { typeof(StructureTypeObj) }; }
        }

        public override void SetParent(IUIObject parent)
        {
            this.Parent = (StructureTypeObj)parent; 

        }

        #endregion

        protected void ContextMenu_OnNewStructureType(object sender, EventArgs e)
        {
            StructureTypeObj newType = new StructureTypeObj(this);
            Viking.UI.Forms.PropertySheetForm.Show(newType);
        }


        protected void ContextMenu_OnProperties(object sender, EventArgs e)
        {
            Viking.UI.Forms.PropertySheetForm.Show(this);
        }

        protected void ContextMenu_OnDelete(object sender, EventArgs e)
        {
            Delete();  
        }

        public override void Delete()
        {
            StructureTypeObj OriginalParent = this.Parent;
            this.Parent = null;

            DBACTION originalAction = this.DBAction;
            this.DBAction = DBACTION.DELETE;

            bool success = Store.StructureTypes.Save();
            if (!success)
            {
                //Write straight to data since we have an assert to check whether an object is being deleted, but
                //in this case we know it is ok
                this.Data.DBAction = originalAction;
                this.Parent = OriginalParent;
            }
            
            Viking.UI.State.SelectedObject = null;             
        }

    }
}
