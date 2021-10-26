using System;
using System.Collections.Generic;
using System.Collections.Specialized;
using System.Diagnostics;
using System.ServiceModel;
using WebAnnotationModel.Objects;

namespace WebAnnotationModel
{
    /// <summary>
    /// Returned type used by queries which return a local cache immediately and a handle on the server request for updates
    /// </summary>
    /// <typeparam name="KEY"></typeparam>
    /// <typeparam name="OBJECT"></typeparam>
    public class MixedLocalAndRemoteQueryResults<KEY, OBJECT>
    {
        public readonly IAsyncResult ServerRequestResult = null;
        public readonly ICollection<OBJECT> KnownObjects = null;

        public MixedLocalAndRemoteQueryResults(IAsyncResult result, ICollection<OBJECT> known_objects)
        {
            this.ServerRequestResult = result;
            this.KnownObjects = known_objects;
        }
    }

    /// <summary>
    /// Groups the objects according to how the store saw them during an operation.  Used to 
    /// store up all changes so a single set of collection changed events can be sent
    /// </summary>
    /// <typeparam name="OBJECT"></typeparam>
    public class ChangeInventory<OBJECT>
    {
        /// <summary>
        /// Objects freshly added to the store
        /// </summary>
        public List<OBJECT> AddedObjects;

        /// <summary>
        /// Objects that existed in the store but had some properties updated
        /// </summary>
        public List<OBJECT> UpdatedObjects;

        /// <summary>
        /// Objects we deleted from the store
        /// </summary>
        public List<OBJECT> DeletedObjects;

        /// <summary>
        /// An object that was in the store but was removed and replaced with a new object
        /// </summary>
        public List<OBJECT> OldObjectsReplaced;

        /// <summary>
        /// Objects that are now in the store and replaced an object that existed previously, common when server sends a new ID on update
        /// </summary>
        public List<OBJECT> NewObjectReplacements;

        /// <summary>
        /// Objects that were found already existing in the store and required no updates
        /// </summary>
        public List<OBJECT> UnchangedObjects;

        public ChangeInventory()
        {
            AddedObjects = new List<OBJECT>();
            UpdatedObjects = new List<OBJECT>();
            DeletedObjects = new List<OBJECT>();
            OldObjectsReplaced = new List<OBJECT>();
            NewObjectReplacements = new List<OBJECT>();
            UnchangedObjects = new List<OBJECT>();

        }

        public ChangeInventory(int numObjects)
        {
            AddedObjects = new List<OBJECT>(numObjects);
            UpdatedObjects = new List<OBJECT>(numObjects);
            DeletedObjects = new List<OBJECT>(numObjects);
            OldObjectsReplaced = new List<OBJECT>(numObjects);
            NewObjectReplacements = new List<OBJECT>(numObjects);
            UnchangedObjects = new List<OBJECT>(numObjects);
        }

        /// <summary>
        /// Return a concatenation of all affected objects minus objects that were deleted
        /// </summary>
        /// <returns></returns>
        public List<OBJECT> ObjectsInStore
        {
            get
            {
                List<OBJECT> listObjects = new List<OBJECT>(AddedObjects.Count + UpdatedObjects.Count + NewObjectReplacements.Count);
                listObjects.AddRange(AddedObjects);
                listObjects.AddRange(UpdatedObjects);
                listObjects.AddRange(NewObjectReplacements);
                listObjects.AddRange(UnchangedObjects);
                return listObjects;
            }
        }

        /// <summary>
        /// Add all elements from another inventory to our own
        /// </summary>
        /// <param name="inventory"></param>
        public void Add(ChangeInventory<OBJECT> inventory)
        {
            AddedObjects.AddRange(inventory.AddedObjects);
            UpdatedObjects.AddRange(inventory.UpdatedObjects);
            DeletedObjects.AddRange(inventory.DeletedObjects);
            OldObjectsReplaced.AddRange(inventory.OldObjectsReplaced);
            NewObjectReplacements.AddRange(inventory.NewObjectReplacements);
            UnchangedObjects.AddRange(inventory.UnchangedObjects);
        }
    }

    /// <summary>
    /// This base class implements the basic functionality to talk to a WCF Service
    /// </summary>
    public abstract class StoreBase<PROXY, INTERFACE, OBJECT, WCFOBJECT> : INotifyCollectionChanged
        where INTERFACE : class
        where PROXY : System.ServiceModel.ClientBase<INTERFACE>
        where WCFOBJECT : AnnotationService.Types.DataObject, new()
        where OBJECT : WCFObjBase<WCFOBJECT>, new()
    {

        protected ChannelFactory<INTERFACE> channelFactory;

        //Perform any required initialization
        public abstract void Init();

        protected virtual IClientChannel CreateProxy()
        {
            return (IClientChannel)channelFactory.CreateChannel(State.EndpointAddress);
        } 

        #region Public Creation/Removal methods


        /// <summary>
        /// Create a local instance of a new item in the store
        /// This item should already exist on the store
        /// Collection change notification events will be sent
        /// </summary>
        /// <param name="obj"></param>
        /// <returns></returns>
        public abstract OBJECT Add(OBJECT obj);


        /// <summary>
        /// Create a local instance of a new item in the store
        /// This item should already exist on the store
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
            System.Diagnostics.Trace.WriteLine(string.Format("{0}.{1} Invoking Event Action", this.GetType().FullName, memberName));
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
