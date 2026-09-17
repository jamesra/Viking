using System;
using System.ComponentModel;
using System.Windows.Threading;
using Viking.Common; 

namespace Viking.Objects
{
    public abstract class UIObjBase : IUIObject, IContextMenu, IToolTip
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
                Dispatcher.CurrentDispatcher.BeginInvoke(OnValueChanged, new object[] { this, new PropertyChangedEventArgs(Column) });
            }
        }


        protected void CallBeforeSave()
        {
            if (OnBeforeSave != null)
            {
                Dispatcher.CurrentDispatcher.BeginInvoke(OnBeforeSave, new object[] { this, null });
            }
        }

        protected void CallAfterSave()
        {
            if (OnAfterSave != null)
            {
                Dispatcher.CurrentDispatcher.BeginInvoke(OnAfterSave, new object[] { this, null });
            }
        }

        protected void CallBeforeDelete()
        {
            if (OnBeforeDelete != null)
            {
                Dispatcher.CurrentDispatcher.BeginInvoke(OnBeforeDelete, new object[] { this, null });
            }
        }

        protected void CallAfterDelete()
        {
            if (OnAfterDelete != null)
            {
                Dispatcher.CurrentDispatcher.BeginInvoke(OnAfterDelete, new object[] { this, null });
            }
        }

        protected void CallOnChildChanged(System.Collections.Specialized.NotifyCollectionChangedEventArgs args)
        {
            if (OnChildChanged != null)
            {
                Dispatcher.CurrentDispatcher.BeginInvoke(OnChildChanged, new object[] { this, args });
            }
        }

        #endregion

        #region IUIObject Members


        public event PropertyChangedEventHandler ValueChanged
        {
            add => OnValueChanged += value;
            remove => OnValueChanged -= value;
        }

        public event EventHandler BeforeDelete
        {
            add => OnBeforeDelete += value;
            remove => OnBeforeDelete -= value;
        }

        public event EventHandler AfterDelete
        {
            add => OnAfterDelete += value;
            remove => OnAfterDelete -= value;
        }

        public event EventHandler BeforeSave
        {
            add => OnBeforeSave += value;
            remove => OnBeforeSave -= value;
        }

        event EventHandler IUIObject.AfterSave
        {
            add => OnAfterSave += value;
            remove => OnAfterSave -= value;
        }

        public virtual event System.Collections.Specialized.NotifyCollectionChangedEventHandler ChildChanged
        {
            add => OnChildChanged += value;
            remove => OnChildChanged -= value;
        }
         
        public virtual System.Windows.Forms.ContextMenu ContextMenu => throw new NotImplementedException();

        public virtual System.Drawing.Image SmallThumbnail => throw new NotImplementedException();

        public virtual string ToolTip => this.ToString();

        public virtual void Save()
        {
            throw new NotImplementedException();
        }
         

        #endregion
    }
}
