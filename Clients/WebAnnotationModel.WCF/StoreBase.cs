using System;
using System.Collections.Generic;
using System.Collections.Specialized;
using System.Diagnostics;
using WebAnnotationModel.Objects;

namespace WebAnnotationModel
{

    /// <summary>
    /// This base class implements the basic functionality to talk to a WCF Service
    /// </summary>
    public abstract class StoreBase<PROXY, INTERFACE, OBJECT, WCFOBJECT> : INotifyCollectionChanged
        where INTERFACE : class
        where PROXY : System.ServiceModel.ClientBase<INTERFACE>
        where WCFOBJECT : AnnotationService.Types.DataObject, new()
        where OBJECT : AnnotationModelObjBase<WCFOBJECT>, new()
    {
        //Perform any required initialization
        public abstract void Init();

        protected abstract PROXY CreateProxy();


        #region Public Creation/Removal methods


        /// <summary>
        /// Create a local instance of a new item in the store
        /// The item should already exist on the server
        /// Collection change notification events will be sent
        /// </summary>
        /// <param name="obj"></param>
        /// <returns></returns>
        public abstract OBJECT Add(OBJECT obj);


        /// <summary>
        /// Create a local instance of a new item in the store
        /// The item should already exist on the server
        /// Collection change notification events will be sent
        /// </summary>
        /// <param name="obj"></param>
        /// <returns></returns>
        public abstract ICollection<OBJECT> Add(ICollection<OBJECT> objs);


        /// <summary>
        /// Remove the passed object from the local store and server.
        /// </summary>
        /// <param name="obj"></param>
        /// <returns></returns>
        public abstract bool Remove(OBJECT obj);

        #endregion

        #region Events

        protected void InvokeEventAction(Action a, [System.Runtime.CompilerServices.CallerMemberName] string memberName = "")
        {
#if DEBUG
            //System.Diagnostics.Trace.WriteLine(string.Format("{0}.{1} Invoking Event Action", this.GetType().FullName, memberName));
#endif
            if (State.UseAsynchEvents)
            {
                System.Threading.Tasks.Task.Run(a);
            }
            else
            {
                a.Invoke();
            }
        }

        internal void CallOnCollectionChanged(ChangeInventory<OBJECT> inventory)
        {
            //Action a = new Action(() =>
            //    {
            CallOnCollectionChangedForDelete(inventory.DeletedObjects);
            CallOnCollectionChangedForReplace(inventory.OldObjectsReplaced, inventory.NewObjectReplacements);
            CallOnCollectionChangedForAdd(inventory.AddedObjects);
            //    });
            //InvokeEventAction(a); 

        }

        /// <summary>
        /// This is fired when all objects retrieved from a call to the database have been added/updated/removed
        /// It needs to be called on the main UI thread
        /// </summary>
      //  public event OnAllUpdatesCompletedEventHandler OnAllUpdatesCompleted; 

        protected void CallOnCollectionChangedForAdd(ICollection<OBJECT> listAddedObj)
        {
            //InternalUpdate will send its own notification for the updated objects
            if (listAddedObj.Count > 0)
            {
                Action a = new Action(() =>
                {
                    OBJECT[] listCopy = new OBJECT[listAddedObj.Count];
                    listAddedObj.CopyTo(listCopy, 0);
                    //CallOnCollectionChanged(new NotifyCollectionChangedEventArgs(NotifyCollectionChangedAction.Add, listAddedObj));
                    CallOnCollectionChanged(new NotifyCollectionChangedEventArgs(NotifyCollectionChangedAction.Add, listCopy));
                });

                InvokeEventAction(a);
            }
        }

        protected void CallOnCollectionChangedForDelete(ICollection<OBJECT> listObj)
        {
            //InternalUpdate will send its own notification for the updated objects
            if (listObj.Count > 0)
            {
                Action a = new Action(() =>
                {
                    OBJECT[] listCopy = new OBJECT[listObj.Count];
                    listObj.CopyTo(listCopy, 0);
                    //CallOnCollectionChanged(new NotifyCollectionChangedEventArgs(NotifyCollectionChangedAction.Add, listAddedObj));
                    CallOnCollectionChanged(new NotifyCollectionChangedEventArgs(NotifyCollectionChangedAction.Remove, listCopy));
                });

                InvokeEventAction(a);
            }
        }


        protected void CallOnCollectionChangedForReplace(ICollection<OBJECT> listOldObjects, ICollection<OBJECT> listNewObjects)
        {
            Debug.Assert(listOldObjects.Count == listNewObjects.Count);
            if (listNewObjects.Count > 0)
            {
                Action a = new Action(() =>
                {
                    OBJECT[] listOldObjectsCopy = new OBJECT[listOldObjects.Count];
                    OBJECT[] listNewObjectsCopy = new OBJECT[listNewObjects.Count];
                    listOldObjects.CopyTo(listOldObjectsCopy, 0);
                    listNewObjects.CopyTo(listNewObjectsCopy, 0);
                    NotifyCollectionChangedEventArgs e = new NotifyCollectionChangedEventArgs(NotifyCollectionChangedAction.Replace,
                                                                                              listNewObjectsCopy, listOldObjectsCopy);
                    CallOnCollectionChanged(e);
                });

                InvokeEventAction(a);
            }
        }


        private void CallOnCollectionChanged(NotifyCollectionChangedEventArgs e)
        {
            OnCollectionChanged?.Invoke(this, e);
            /*
             if (OnCollectionChanged != null)
            {
                OnCollectionChanged(this, e);
                //System.Threading.Tasks.Task.Factory.StartNew(() => OnCollectionChanged(this, e));
                //Action a = new Action(() => OnCollectionChanged(this, e));
                //a.BeginInvoke(null, null);

                //Because we are handling collection changes these events need to appear in order, however there are
                //too many cascading events...  RIght now the worst case is a location doesn't show in the UI as expected.
                //This can be fixed by implementing the replaced collection change action for delete instead of using
                //remove and then add.  When we separate the operation the order can be flipped.
                /*
                Action a = new Action(() => OnCollectionChanged(this, e));
                if (State.UseAsynchEvents)
                {
                    a.BeginInvoke(null, null);
                }
                else
                {
                    a.Invoke(); 
                }
                */
            
            //}
        }

        /*
        protected void CallOnAllUpdatesCompleted(OnAllUpdatesCompletedEventArgs e)
        {            
            if (OnAllUpdatesCompleted != null)
            {
                OnAllUpdatesCompleted(this, e);
            }
        }*/


        #endregion

        protected void ShowStandardExceptionMessage(Exception e)
        {
            Trace.WriteLine(e.ToString());
            Trace.WriteLine(e.Message);
            //System.Windows.Forms.MessageBox.Show("An error occurred:\n" + e.Message, "WebAnnotation");
        }

        #region Proxy Calls


        #endregion

        #region INotifyCollectionChanged Members


        public event NotifyCollectionChangedEventHandler OnCollectionChanged;
        event NotifyCollectionChangedEventHandler INotifyCollectionChanged.CollectionChanged
        {
            add { OnCollectionChanged += value; }
            remove { OnCollectionChanged -= value; }
        }

        #endregion


    }
}
