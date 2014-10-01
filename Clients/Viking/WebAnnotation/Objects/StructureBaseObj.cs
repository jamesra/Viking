using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Diagnostics; 

using WebAnnotation.WCFService.Annotation;
using System.Drawing;
using System.Windows.Forms;

using Viking.Common; 

namespace WebAnnotation.Objects
{
    class StructureBaseObj : IUIObject
    {
        public override string ToString()
        {
            if (Data != null)
                return Data.ID.ToString();

            return "Uninitialized " + base.ToString();
        }

        public override bool Equals(object obj)
        {
            StructureBaseObj structObj = obj as StructureBaseObj;
            if (structObj != null)
            {
                return this.ID == structObj.ID; 
            }
            else
                return base.Equals(obj);
        }

        /// <summary>
        /// Pointer to the data which is sent to the web server
        /// </summary>
        protected object _Data; //This is a structureBase object

        private StructureBase Data
        {
            get { return (StructureBase)_Data; }
            set { _Data = value; }
        }

        public DBACTION DBAction
        {
            get
            {
                return ((StructureBase)Data).DBAction;
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

        [Column("Verified")]
        public bool Verified
        {
            get { return Data.Verified; }
            set
            {
                Data.Verified = value;
                SetDBActionForChange();
                ValueChangedEvent("Verified");
            }
        }

        [Column("Confidence")]
        public double Confidence
        {
            get { return Data.Confidence; }
            set
            {
                Data.Confidence = value;
                SetDBActionForChange();
                ValueChangedEvent("Confidence");
            }
        }

        [Column("Tags")]
        public string[] Tags
        {
            get { return Data.Tags; }
            set
            {
                Data.Tags = value;
                SetDBActionForChange();
                ValueChangedEvent("Tags");
            }
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

        protected StructureBaseObj()
        {
        }

        protected virtual void InitNewData(StructureTypeObj type)
        {
            this.Data.DBAction = DBACTION.INSERT;
            
            this.Data.ID = StructureStore.GetTempID();
            this.Data.TypeID = type.ID;
            Debug.Assert(type.ID >= 0);
            this.Data.Notes = "";
            this.Data.Tags = new String[0]; 
            this.Data.Confidence = 0.5; 
        }

        internal virtual StructureBase GetData()
        {
            return Data;
        }

        private StructureTypeObj _Type = null;
        public StructureTypeObj Type
        {
            get
            {
                if (_Type == null)
                {
                    _Type = StructureTypeStore.GetStructureType(Data.TypeID);
                }
                return _Type; 
            }
            set
            {
                Debug.Assert(value != null);
                if (value != null)
                {
                    Data.TypeID = value.ID;
                    _Type = value;

                    SetDBActionForChange();

                    ValueChangedEvent("Type");
                }
            }
        }

        protected void FireOnChildChanged(ChildChangeEventArgs args)
        {
            if(OnChildChanged != null)
                OnChildChanged(this, args); 
        }

        protected event ValueChangedEventHandler OnValueChanged;
        protected event EventHandler OnBeforeDelete;
        protected event EventHandler OnAfterDelete;
        protected event EventHandler OnBeforeSave;
        protected event EventHandler OnAfterSave;
        protected event ChildChangedEventHandler OnChildChanged;

        #region IUIObject Members

        event ValueChangedEventHandler IUIObject.ValueChanged
        {
            add { OnValueChanged += value; }
            remove { OnValueChanged -= value; }
        }

        event EventHandler IUIObject.BeforeDelete
        {
            add { OnBeforeDelete += value; }
            remove { OnBeforeDelete -= value; }
        }

        event EventHandler IUIObject.AfterDelete
        {
            add { OnAfterDelete += value; }
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

        ContextMenu IUIObject.ContextMenu
        {
            get
            {
                ContextMenu menu = new ContextMenu();

                menu.MenuItems.Add("Delete", ContextMenu_OnDelete);
                menu.MenuItems.Add("Properties", ContextMenu_OnProperties);

                return menu;
            }
        }

        Image IUIObject.SmallThumbnail
        {
            get { throw new NotImplementedException(); }
        }

        string IUIObject.ToolTip
        {
            get { throw new NotImplementedException(); }
        }

        public virtual void Save()
        {
            throw new NotImplementedException();
        }

        Viking.UI.Controls.GenericTreeNode IUIObject.CreateNode()
        {
            return new Viking.UI.Controls.GenericTreeNode(this); 
        }

        int IUIObject.TreeImageIndex
        {
            get { throw new NotImplementedException(); }
        }

        int IUIObject.TreeSelectedImageIndex
        {
            get { throw new NotImplementedException(); }
        }

        event ChildChangedEventHandler IUIObject.ChildChanged
        {
            add { OnChildChanged += value; }
            remove { OnChildChanged -= value; }
        }

        Type[] IUIObject.AssignableParentTypes
        {
            get { return new System.Type[] { typeof(StructureObj) }; }
        }

        public virtual void SetParent(IUIObject parent)
        {
            return;
        }

        #endregion

        protected void ContextMenu_OnProperties(object sender, EventArgs e)
        {
            Viking.UI.Forms.PropertySheetForm.Show(this);
        }

        protected void ContextMenu_OnDelete(object sender, EventArgs e)
        {
            Delete();
        }

        public void Delete()
        {
            if (OnBeforeDelete != null)
                OnBeforeDelete(this, null);

            this.DBAction = DBACTION.DELETE;

            StructureStore.Save();

            if (OnAfterDelete != null)
                OnAfterDelete(this, null);
        }
    }
}
