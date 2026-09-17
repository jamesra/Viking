using System;
using System.Collections.Generic;
using System.Collections.Specialized;
using System.Diagnostics;
using System.Threading;
using System.Threading.Tasks;
using WebAnnotationModel.Objects;
using WebAnnotationModel;

namespace WebAnnotationModel.gRPC
{

    /// <summary>
    /// gRPC client-side store: local cache plus CollectionChanged for UI.
    /// </summary>
    public abstract class StoreBase<OBJECT> : INotifyCollectionChanged, IStore<OBJECT>
        where OBJECT : IEquatable<OBJECT> 
    {
        //Perform any required initialization
        protected virtual Task Init() => Task.CompletedTask;

        /// <summary>
        /// Public entry point so the composition root can warm caches (structure types, permitted links, …).
        /// </summary>
        public Task InitializeAsync() => Init();

        #region Public Creation/Removal methods
        
        /// <summary>
        /// Create a local instance of a new item in the store
        /// This item should already exist on the store
        /// Collection change notification events will be sent
        /// </summary>
        /// <param name="obj"></param>
        /// <returns></returns>
        public abstract Task<OBJECT> Add(OBJECT obj);


        /// <summary>
        /// Create a local instance of a new item in the store
        /// This item should already exist on the store
        /// Collection change notification events will be sent
        /// </summary>
        /// <param name="obj"></param>
        /// <returns></returns>
        public abstract Task<ICollection<OBJECT>> Add(ICollection<OBJECT> objs);


        /// <summary>
        /// Remove the passed object from the local store and server.
        /// </summary>
        /// <param name="obj"></param>
        /// <returns></returns>
        public abstract Task<bool> Remove(OBJECT obj);

        /// <summary>
        /// Push every locally changed (added/updated/deleted) object in the store to the server.
        /// </summary>
        public abstract Task<bool> Save(CancellationToken token);

        /// <summary>
        /// Synchronous convenience overload equivalent to Save(CancellationToken.None).
        /// </summary>
        public Task<bool> Save() => Save(CancellationToken.None);

        #endregion

        #region Events

        /// <summary>
        /// Runs store UI events. When UseAsynchEvents is true this is Task.Run; callers of
        /// CallOnCollectionChanged await so ingest does not race the next chunk.
        /// </summary>
        protected Task InvokeEventAction(Action a, [System.Runtime.CompilerServices.CallerMemberName] string memberName = "")
        {
#if DEBUG
            System.Diagnostics.Trace.WriteLine($"{GetType().FullName}.{memberName} Invoking Event Action");
#endif
            if (State.UseAsynchEvents)
                return Task.Run(a);

            a.Invoke();
            return Task.CompletedTask;
        }

        /// <summary>
        /// Notify listeners after a server batch is already in IDToObject.
        /// StoreBaseWithKeyAndParent overrides this to wire RootObjects / Parent.Children
        /// before the event. IStoreEditor.EndBatch calls this method on StoreBase directly
        /// and skips that override — use this virtual on the concrete store for parented types.
        /// </summary>
        internal virtual async Task CallOnCollectionChanged(ChangeInventory<OBJECT> inventory)
        {
            await CallOnCollectionChangedForDelete(inventory.DeletedObjects).ConfigureAwait(false);
            await CallOnCollectionChangedForReplace(inventory.OldObjectsReplaced, inventory.NewObjectReplacements).ConfigureAwait(false);
            await CallOnCollectionChangedForAdd(inventory.AddedObjects).ConfigureAwait(false);
        }

        protected Task CallOnCollectionChangedForAdd(OBJECT addedObj)
        {
            return InvokeEventAction(() =>
            {
                OBJECT[] listCopy = new OBJECT[1];
                listCopy[0] = addedObj;
                CallOnCollectionChanged(new NotifyCollectionChangedEventArgs(NotifyCollectionChangedAction.Add, listCopy));
            });
        }

        /// <summary>
        /// This is fired when all objects retrieved from a call to the database have been added/updated/removed
        /// It needs to be called on the main UI thread
        /// </summary>
      //  public event OnAllUpdatesCompletedEventHandler OnAllUpdatesCompleted; 

        protected Task CallOnCollectionChangedForAdd(ICollection<OBJECT> listAddedObj)
        {
            if (listAddedObj == null || listAddedObj.Count == 0)
                return Task.CompletedTask;

            return InvokeEventAction(() =>
            {
                OBJECT[] listCopy = new OBJECT[listAddedObj.Count];
                listAddedObj.CopyTo(listCopy, 0);
                CallOnCollectionChanged(new NotifyCollectionChangedEventArgs(NotifyCollectionChangedAction.Add, listCopy));
            });
        }

        protected Task CallOnCollectionChangedForDelete(OBJECT deletedObj)
        {
            return InvokeEventAction(() =>
            {
                OBJECT[] listCopy = new OBJECT[1];
                listCopy[0] = deletedObj;
                CallOnCollectionChanged(new NotifyCollectionChangedEventArgs(NotifyCollectionChangedAction.Remove, listCopy));
            });
        }

        protected Task CallOnCollectionChangedForDelete(ICollection<OBJECT> listObj)
        {
            if (listObj == null || listObj.Count == 0)
                return Task.CompletedTask;

            return InvokeEventAction(() =>
            {
                OBJECT[] listCopy = new OBJECT[listObj.Count];
                listObj.CopyTo(listCopy, 0);
                CallOnCollectionChanged(new NotifyCollectionChangedEventArgs(NotifyCollectionChangedAction.Remove, listCopy));
            });
        }


        protected Task CallOnCollectionChangedForReplace(ICollection<OBJECT> listOldObjects, ICollection<OBJECT> listNewObjects)
        {
            Debug.Assert(listOldObjects.Count == listNewObjects.Count);
            if (listNewObjects == null || listNewObjects.Count == 0)
                return Task.CompletedTask;

            return InvokeEventAction(() =>
            {
                OBJECT[] listOldObjectsCopy = new OBJECT[listOldObjects.Count];
                OBJECT[] listNewObjectsCopy = new OBJECT[listNewObjects.Count];
                listOldObjects.CopyTo(listOldObjectsCopy, 0);
                listNewObjects.CopyTo(listNewObjectsCopy, 0);
                NotifyCollectionChangedEventArgs e = new NotifyCollectionChangedEventArgs(NotifyCollectionChangedAction.Replace,
                                                                                          listNewObjectsCopy, listOldObjectsCopy);
                CallOnCollectionChanged(e);
            });
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


        /// <summary>
        /// Raised after a batch is in IDToObject. May run on a thread-pool thread when UseAsynchEvents is true.
        /// </summary>
        public event NotifyCollectionChangedEventHandler OnCollectionChanged;
        event NotifyCollectionChangedEventHandler INotifyCollectionChanged.CollectionChanged
        {
            add { OnCollectionChanged += value; }
            remove { OnCollectionChanged -= value; }
        }

        #endregion


    }
}
