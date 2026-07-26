using System;
using System.Collections.Specialized;
using System.Windows.Forms;
using Viking.Common;
using Viking.Common.UI;
using WebAnnotationModel;

namespace WebAnnotation.ViewModel
{
    [Viking.Common.UI.TreeViewVisible]
    public class StructureType(StructureTypeObj data) : Viking.Objects.UIObjBase, IViewStructureType, IContextMenu
    {
        public StructureTypeObj modelObj = data;

        public override int GetHashCode() => modelObj.GetHashCode();

        public override bool Equals(object obj)
        {
            if (obj is StructureType Obj)
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

        public override string ToString() => modelObj.Name;

        public StructureType Parent
        {
            get
            {
                if (modelObj.ParentID.HasValue == false)
                {
                    return null;
                }

                return new StructureType(modelObj.Parent);
            }
        }

        [Viking.Common.UI.ThisToManyRelationAttribute()]
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
            add => modelObj.ChildChanged += value;
            remove => modelObj.ChildChanged += value;
        }

        [Column("ID")]
        public long ID => modelObj.ID;


        [Column("ParentID")]
        public long? ParentID => modelObj.ParentID;

        [Column("Name")]
        public string Name
        {
            get => modelObj.Name;
            set => modelObj.Name = value;
        }

        [Column("Notes")]
        public string Notes
        {
            get => modelObj.Notes;
            set => modelObj.Notes = value;
        }

        [Column("Color")]
        public System.Drawing.Color Color
        {
            get => System.Drawing.Color.FromArgb((int)modelObj.Color);
            set => modelObj.Color = (uint)value.ToArgb();
        }

        [Column("Code")]
        public string Code
        {
            get => modelObj.Code;
            set => modelObj.Code = value;
        }

        #region IUIObject Members

        public override System.Windows.Forms.ContextMenuStrip ContextMenu
        {
            get
            {
                ContextMenuStrip menu = new();

                ToolStripMenuItem newMenuItem = new("New");
                menu.Items.Add(newMenuItem);

                ToolStripMenuItem structureTypeItem = new("Structure Type");
                structureTypeItem.Click += ContextMenu_OnNewStructureType;
                newMenuItem.DropDownItems.Add(structureTypeItem);

                if (modelObj.Children.Length == 0)
                {
                    ToolStripMenuItem deleteItem = new("Delete");
                    deleteItem.Click += ContextMenu_OnDelete;
                    menu.Items.Add(deleteItem);
                }

                ToolStripMenuItem propertiesItem = new("Properties");
                propertiesItem.Click += ContextMenu_OnProperties;
                menu.Items.Add(propertiesItem);

                return menu;
            }
        }

        public override System.Drawing.Image SmallThumbnail => null;

        public override string ToolTip => Name;

        public override void Save()
        {
            try
            {
                Store.StructureTypes.Save();
            }
            catch (System.ServiceModel.FaultException ex)
            {
                AnnotationOverlay.ShowFaultExceptionMsgBox(ex);
            }
        }

        public override Viking.UI.Controls.GenericTreeNode CreateNode() => new Viking.UI.Controls.GenericTreeNode(this);

        public override int TreeImageIndex => 0;

        public override int TreeSelectedImageIndex => 0;

        public override Type[] AssignableParentTypes => [typeof(StructureType)];

        public override void SetParent(IUIObject parent)
        {
            StructureType newParent = (StructureType)parent;
            if (parent != Parent)
            {
                //      this.Parent.CallOnChildChanged(new ChildChangeEventArgs(this, CHANGEACTION.BEFOREADD)); 
                modelObj.Parent = newParent.modelObj;
                //      this.Parent.CallOnChildChanged(new ChildChangeEventArgs(this, CHANGEACTION.ADD));

                //  Store.StructureTypes.Save(); 
            }
        }

        #endregion

        protected void ContextMenu_OnNewStructureType(object sender, EventArgs e)
        {
            StructureTypeObj newType = new(modelObj);
            StructureType newTypeView = new(newType);
            DialogResult result = Viking.UI.Forms.PropertySheetForm.ShowDialog(newTypeView, null);

            if (result != DialogResult.Cancel)
            {
                try
                {
                    newType = Store.StructureTypes.Create(newType);
                    Store.StructureTypes.Save();
                }
                catch (System.ServiceModel.FaultException ex)
                {
                    AnnotationOverlay.ShowFaultExceptionMsgBox(ex);
                }
            }

        }


        protected void ContextMenu_OnProperties(object sender, EventArgs e) => Viking.UI.Forms.PropertySheetForm.Show(this);

        protected void ContextMenu_OnDelete(object sender, EventArgs e) => Delete();

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
            CallBeforeDelete();

            Store.StructureTypes.Remove(modelObj);
            Store.StructureTypes.Save();

            CallAfterDelete();

            Viking.UI.State.SelectedObject = null;

        }
    }
}
