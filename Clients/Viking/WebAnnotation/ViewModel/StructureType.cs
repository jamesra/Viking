using System;
using System.Collections.Specialized; 
using System.Collections.Generic;
using System.Linq;
using System.Text;
using Viking.Common;
using Common.UI; 
using System.Windows; 
using System.Windows.Forms;
using WebAnnotationModel;

namespace WebAnnotation.ViewModel
{
    [Common.UI.TreeViewVisible]
    public class StructureType : Viking.Objects.UIObjBase
    {
        public StructureTypeObj modelObj;

        public override int GetHashCode()
        {
            return modelObj.GetHashCode();
        }

        public override bool Equals(object obj)
        {
            StructureType Obj = obj as StructureType;
            if (Obj != null)
            {
                return modelObj.Equals(Obj.modelObj);
            }

            StructureTypeObj Obj2 = obj as StructureTypeObj;
            if (Obj2 != null)
            {
                return modelObj.Equals(Obj2);
            }

            return false;
        }

        public override string ToString()
        {
            return modelObj.Name; 
        }

        public StructureType Parent
        {
            get
            {
                if (modelObj.ParentID.HasValue == false)
                    return null;

                return new StructureType(modelObj.Parent); 
            }
        }

        [Common.UI.ThisToManyRelationAttribute()]
        public StructureType[] Children
        {
            get
            {
                StructureType[] children = new StructureType[modelObj.Children.Length];
                for (int i = 0; i < modelObj.Children.Length; i++)
                {
                    children[i] = new StructureType(modelObj.Children[i]); 
                }

                return children;
            }
        }

        public override event NotifyCollectionChangedEventHandler ChildChanged
        {
            add { modelObj.ChildChanged += value; }
            remove { modelObj.ChildChanged += value; }
        }

        [Column("ID")]
        public long ID
        {
            get { return modelObj.ID; }
        }


        [Column("ParentID")]
        public long? ParentID
        {
            get { return modelObj.ParentID;}
        }

       [Column("Name")]
        public string Name
        {
            get { return modelObj.Name; }
            set { modelObj.Name = value; 
            }
        }

        [Column("Notes")]
        public string Notes
        {
            get { return modelObj.Notes; }
            set
            {
                modelObj.Notes = value;
            }
        }

        [Column("Color")]
        public System.Drawing.Color Color
        {
            get { return System.Drawing.Color.FromArgb(modelObj.Color); }
            set
            {
                modelObj.Color = value.ToArgb();
            }
        }

        [Column("Code")]
        public string Code
        {
            get { return modelObj.Code; }
            set
            {
                modelObj.Code = value;
            }
        }
        
        public StructureType(StructureTypeObj data)
        {
            this.modelObj = data; 
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

                if(modelObj.Children.Length == 0)
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
            get { return new Type[] { typeof(StructureType) }; }
        }

        public override void SetParent(IUIObject parent)
        {
            StructureType newParent = (StructureType)parent;
            if(parent != this.Parent)
            {
          //      this.Parent.CallOnChildChanged(new ChildChangeEventArgs(this, CHANGEACTION.BEFOREADD)); 
                this.modelObj.Parent = newParent.modelObj;
          //      this.Parent.CallOnChildChanged(new ChildChangeEventArgs(this, CHANGEACTION.ADD));

              //  Store.StructureTypes.Save(); 
            }
        }

        #endregion

        protected void ContextMenu_OnNewStructureType(object sender, EventArgs e)
        {
            StructureTypeObj newType = new StructureTypeObj(this.modelObj);
            StructureType newTypeView = new StructureType(newType);
            DialogResult result = Viking.UI.Forms.PropertySheetForm.ShowDialog(newTypeView, null);

            if (result != DialogResult.Cancel)
            {
                newType = Store.StructureTypes.Add(newType);
                Store.StructureTypes.Save(); 
            }

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
//            StructureTypeObj OriginalParent = this.Parent;
//            this.Parent = null;

            /*
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
             */

            //This is a hack because not every control may be subscribing to the same object, but the 
            //alternative is a huge rewrite which I am doing with Jotunn
            this.CallBeforeDelete(); 

            Store.StructureTypes.Remove(this.modelObj);
            Store.StructureTypes.Save();

            this.CallAfterDelete();
            
            Viking.UI.State.SelectedObject = null;
             
        }
    }
}
