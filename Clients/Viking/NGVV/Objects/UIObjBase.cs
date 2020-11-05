using System;
using System.ComponentModel;
using Viking.Common;

namespace Viking.Objects
{
    public abstract class UIObjBase : IUIObject
    {
        public abstract void Delete();

        #region Event Code

        //Create is no longer in this class because if a static event is placed here
        //all objects derived from the base class fire the same event.  Each derived
        //class has to declate the static event themselves.
        protected event System.ComponentModel.PropertyChangedEventHandler OnValueChanged;
        private event EventHandler OnBeforeDelete;
        private event EventHandler OnAfterDelete;
        private event EventHandler OnBeforeSave;
        private event EventHandler OnAfterSave;
        private event System.Collections.Specialized.NotifyCollectionChangedEventHandler OnChildChanged;

        protected void ValueChangedEvent(string Column)
        {
            if (OnValueChanged != null)
            {
                //We need to ensure these events are invoked on the main thread since UI controls listen to them and they can only 
                //change state on the main thread 
                Viking.UI.State.MainThreadDispatcher.BeginInvoke(OnValueChanged, new object[] { this, new PropertyChangedEventArgs(Column) });
            }
        }


        protected void CallBeforeSave()
        {
            if (OnBeforeSave != null)
            {
                Viking.UI.State.MainThreadDispatcher.BeginInvoke(OnBeforeSave, new object[] { this, null });
            }
        }

        protected void CallAfterSave()
        {
            if (OnAfterSave != null)
            {
                Viking.UI.State.MainThreadDispatcher.BeginInvoke(OnAfterSave, new object[] { this, null });
            }
        }

        protected void CallBeforeDelete()
        {
            if (OnBeforeDelete != null)
            {
                Viking.UI.State.MainThreadDispatcher.BeginInvoke(OnBeforeDelete, new object[] { this, null });
            }
        }

        protected void CallAfterDelete()
        {
            if (OnAfterDelete != null)
            {
                Viking.UI.State.MainThreadDispatcher.BeginInvoke(OnAfterDelete, new object[] { this, null });
            }
        }

        protected void CallOnChildChanged(System.Collections.Specialized.NotifyCollectionChangedEventArgs args)
        {
            if (OnChildChanged != null)
            {
                Viking.UI.State.MainThreadDispatcher.BeginInvoke(OnChildChanged, new object[] { this, args });
            }
        }

        #endregion

        #region IUIObject Members


        public event PropertyChangedEventHandler ValueChanged
        {
            add { OnValueChanged += value; }
            remove { OnValueChanged -= value; }
        }

        public event EventHandler BeforeDelete
        {
            add { OnBeforeDelete += value; }
            remove { OnBeforeDelete -= value; }
        }

        public event EventHandler AfterDelete
        {
            add { OnAfterDelete += value; }
            remove { OnAfterDelete -= value; }
        }

        public event EventHandler BeforeSave
        {
            add { OnBeforeSave += value; }
            remove { OnBeforeSave -= value; }
        }

        event EventHandler IUIObject.AfterSave
        {
            add { OnAfterSave += value; }
            remove { OnAfterSave -= value; }
        }

        public virtual event System.Collections.Specialized.NotifyCollectionChangedEventHandler ChildChanged
        {
            add { OnChildChanged += value; }
            remove { OnChildChanged -= value; }
        }

        public virtual void ShowProperties()
        {
            Viking.UI.Forms.PropertySheetForm.Show(this);
        }

        public virtual System.Windows.Forms.DialogResult ShowPropertiesDialog(System.Windows.Forms.Form ParentForm)
        {
            return Viking.UI.Forms.PropertySheetForm.ShowDialog(this, ParentForm);
        }

        public virtual System.Windows.Forms.ContextMenu ContextMenu
        {
            get { throw new NotImplementedException(); }
        }

        public virtual System.Drawing.Image SmallThumbnail
        {
            get { throw new NotImplementedException(); }
        }

        public virtual string ToolTip
        {
            get { return this.ToString(); }
        }

        public virtual void Save()
        {
            throw new NotImplementedException();
        }

        public virtual Type[] AssignableParentTypes
        {
            get { throw new NotImplementedException(); }
        }

        public virtual void SetParent(IUIObject parent)
        {
            throw new NotImplementedException();
        }

        public virtual Viking.UI.Controls.GenericTreeNode CreateNode()
        {
            throw new NotImplementedException();
        }

        public virtual int TreeImageIndex
        {
            get { return 0; }
        }

        public virtual int TreeSelectedImageIndex
        {
            get { return 1; }
        }

        #endregion
    }
}
