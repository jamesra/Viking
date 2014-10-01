using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Diagnostics; 
using WebAnnotation.WCFService.Annotation;
using System.Drawing;
using System.Windows.Forms; 

using Viking.Common; 

namespace WebAnnotation.AnnotationObjects
{
    [TreeViewVisible]
    class StructureTypeObj : IUIObject
    {
        public override string ToString()
        {
            if(Data != null)
                return Data.Name;

            return "Uninitialized " + base.ToString(); 
        }
        /// <summary>
        /// Pointer to the data which is sent to the web server
        /// </summary>
        StructureType Data;

        public DBACTION DBAction
        {
            get
            {
                return Data.DBAction;
            }
            set
            {
                if (Data.DBAction == DBACTION.INSERT && value == DBACTION.UPDATE)
                    return;

                //Just a precaution. I haven't thought whether I could undelete an object
                Debug.Assert(false == (Data.DBAction == DBACTION.DELETE && value != DBACTION.DELETE));

                Data.DBAction = value;
            }
        }

        protected void SetDBActionForChange()
        {
            DBAction = DBACTION.UPDATE;
        }

        protected void ValueChangedEvent(string Column)
        {
            if (OnValueChanged != null)
            {
                OnValueChanged(this, new ValueChangedEventArgs(Column));
            }
        }

        public long ID
        {
            get { return Data.ID; }
        }

        public long? ParentID
        {
            get { return Data.ParentID; }
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


        private StructureTypeObj _Parent = null; 
        public StructureTypeObj Parent
        {
            get
            {
                return _Parent;
            }
            set
            {
                //Do nothing if the parent isn't changed
                if (_Parent == value)
                    return;
 
                //Make sure we aren't being assigned to a parent who hasn't been saved to the database yet, because we don't know how to 
                //write that insert statement
                if (value != null)
                {
                    Debug.Assert(value.DBAction != DBACTION.INSERT);
                    if(value.DBAction == DBACTION.INSERT) 
                        throw new ArgumentException("Cannot set StructureType.Parent to a parent who is not yet in the database.");
                }

                //Remove ourselves from our old parent's list of children
                if (_Parent != null)
                {
                    _Parent.RemoveChild(this); 
                }

                _Parent = value;

                //Need to update the underlying type so we persist the change if asked
                if (_Parent != null)
                {
                    Data.ParentID  = new long?(value.ID);
                    _Parent.AddChild(this); 
                }
                else
                {
                    Data.ParentID = new long?(); 
                }

                ValueChangedEvent("Parent");
            }
        }

        List<StructureTypeObj> _Children = new List<StructureTypeObj>(); 
        [ThisToManyRelation]
        public StructureTypeObj[] Children
        {
            get { return _Children.ToArray(); }
        }

        private void AddChild(StructureTypeObj child)
        {
            _Children.Add(child);
//            SetDBActionForChange(); Don't do this, the database doesn't care if the child changes, tables only carry a parent field
            if(OnChildChanged != null)
                OnChildChanged(this, new ChildChangeEventArgs(child, CHANGEACTION.ADD)); 
        }

        private void RemoveChild(StructureTypeObj child)
        {
            _Children.Remove(child);
//            SetDBActionForChange(); Don't do this, the database doesn't care if the child changes, tables only carry a parent field
            if (OnChildChanged != null)
                OnChildChanged(this, new ChildChangeEventArgs(child, CHANGEACTION.REMOVE)); 
        }

        public StructureTypeObj(StructureType data)
        {
            this.Data = data; 
        }

        public StructureTypeObj(StructureTypeObj parent)
        {
            this.Data = new StructureType();
            this.Data.DBAction = DBACTION.INSERT;
            this.Data.Name = "New Structure Type";
            this.Data.MarkupType = "Point"; 
            this.Data.ID = StructureTypeStore.GetTempID();
            this.Data.Tags = new String[0];
            this.Data.StructureTags = new String[0];

            if (parent != null)
            {
                this.Data.ParentID = parent.ID;
            }

            StructureTypeStore.AddType(this); 
        }

        internal StructureType GetData()
        {
            return Data;
        }

        private event ValueChangedEventHandler OnValueChanged;
        internal event EventHandler OnBeforeDelete;
        internal event EventHandler OnAfterDelete;
        internal event EventHandler OnBeforeSave;
        internal event EventHandler OnAfterSave;
        private event ChildChangedEventHandler OnChildChanged;

        #region IUIObject Members

        event ValueChangedEventHandler IUIObject.ValueChanged
        {
            add { OnValueChanged += value; }
            remove { OnValueChanged -= value; }
        }

        event EventHandler IUIObject.BeforeDelete
        {
            add { OnBeforeDelete += value;  }
            remove { OnBeforeDelete -= value; }
        }

        event EventHandler IUIObject.AfterDelete
        {
            add { OnAfterDelete += value;  }
            remove { OnAfterDelete -= value; }
        }

        event EventHandler IUIObject.BeforeSave
        {
            add { OnBeforeSave += value; }
            remove { OnBeforeSave -= value; }
        }

        event EventHandler IUIObject.AfterSave
        {
            add { OnAfterSave += value; }
            remove { OnAfterSave -= value; }
        }

        void IUIObject.ShowProperties()
        {
            Viking.UI.Forms.PropertySheetForm.Show(this); 
        }

        System.Windows.Forms.ContextMenu IUIObject.ContextMenu
        {
            get 
            {
                ContextMenu menu = new ContextMenu();

                MenuItem newMenuItem = new MenuItem("New");
                menu.MenuItems.Add(newMenuItem); 

                newMenuItem.MenuItems.Add("Schematic Type", ContextMenu_OnNewStructureType);

                if(this.Children.Length == 0)
                    menu.MenuItems.Add("Delete", ContextMenu_OnDelete); 

                menu.MenuItems.Add("Properties", ContextMenu_OnProperties);

                

                return menu;
            }
        }

        System.Drawing.Image IUIObject.SmallThumbnail
        {
            get { return null; }
        }

        string IUIObject.ToolTip
        {
            get { return this.Name; }
        }

        void IUIObject.Save()
        {
            if (OnBeforeSave != null)
                OnBeforeSave(this, null); 

            StructureTypeStore.Save();

            if (OnAfterSave != null)
                OnAfterSave(this, null); 
        }

        Viking.UI.Controls.GenericTreeNode IUIObject.CreateNode()
        {
            return new Viking.UI.Controls.GenericTreeNode(this); 
        }

        int IUIObject.TreeImageIndex
        {
            get { return 0; }
        }

        int IUIObject.TreeSelectedImageIndex
        {
            get { return 0;  }
        }

        event ChildChangedEventHandler IUIObject.ChildChanged
        {
            add { OnChildChanged += value;  }
            remove { OnChildChanged -= value;  }
        }

        Type[] IUIObject.AssignableParentTypes
        {
            get { return new Type[] { typeof(StructureTypeObj) }; }
        }

        void IUIObject.SetParent(IUIObject parent)
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

        protected void Delete()
        {
            if (OnBeforeDelete != null)
                OnBeforeDelete(this, null);

            this.DBAction = DBACTION.DELETE;
            StructureTypeStore.Save();

            if (OnAfterDelete != null)
                OnAfterDelete(this, null); 
        }

    }
}
