using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Diagnostics;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Viking.AnnotationServiceTypes.Interfaces;
using WebAnnotationModel.Objects;
using WebAnnotationModel;
using WebAnnotationModel.ServerInterface;

namespace WebAnnotationModel.gRPC
{
    /// <summary>
    /// Keyed store whose objects form a parent/child tree (structure types, structures).
    /// </summary>
    public abstract class StoreBaseWithKeyAndParent<KEY, OBJECT, SERVER_OBJECT, CREATION_DATA_PARAM, CREATION_RESULT> : StoreBaseWithKey<KEY, OBJECT, SERVER_OBJECT, CREATION_DATA_PARAM, CREATION_RESULT>,
        INotifyPropertyChanged, IStoreWithParent<KEY, OBJECT>
        where KEY : struct, IEquatable<KEY>, IComparable<KEY>
        where OBJECT : AnnotationModelObjBaseWithParent<KEY, SERVER_OBJECT, OBJECT>, IDataObjectWithParent<KEY>, IEquatable<OBJECT>
        where SERVER_OBJECT : IEquatable<SERVER_OBJECT>, IDataObjectWithParent<KEY>
    {

        private readonly ReaderWriterLockSlim _rwLockRootObjects = new ReaderWriterLockSlim();

        private readonly ObservableCollection<KEY> _rootObjects = new ObservableCollection<KEY>();

        /// <summary>
        /// IDs of objects with no parent. Filled by WireParentsAndRoots from CallOnCollectionChanged,
        /// not by IStoreEditor.GetOrAdd. Empty RootObjects with a full IDToObject means the notify step was skipped.
        /// </summary>
        public readonly ReadOnlyObservableCollection<KEY> RootObjects;

        ReadOnlyObservableCollection<KEY> IStoreWithParent<KEY, OBJECT>.RootObjects => RootObjects;
          
        public event PropertyChangedEventHandler PropertyChanged;

        protected StoreBaseWithKeyAndParent(IServerAnnotationsClientFactory<IServerAnnotationsClient<KEY, SERVER_OBJECT, CREATION_DATA_PARAM, CREATION_RESULT>> clientFactory,
            IStoreServerQueryResultsHandler<KEY, OBJECT, SERVER_OBJECT> serverQueryResultsHandler,
            IObjectConverter<OBJECT, SERVER_OBJECT> objToServerObjConverter,
            IObjectConverter<SERVER_OBJECT, OBJECT> serverObjToObjConverter) : base(clientFactory, serverQueryResultsHandler, objToServerObjConverter,
            serverObjToObjConverter)
        {
            RootObjects = new ReadOnlyObservableCollection<KEY>(_rootObjects);
        }

        /// <summary>
        /// gRPC GetAll uses IStoreEditor.GetOrAdd, which never called the old InternalAdd
        /// that populated RootObjects and Parent.Children. Wire that here when the batch is applied.
        /// </summary>
        internal override async Task CallOnCollectionChanged(ChangeInventory<OBJECT> inventory)
        {
            if (inventory?.AddedObjects != null && inventory.AddedObjects.Count > 0)
                WireParentsAndRoots(inventory.AddedObjects);

            await base.CallOnCollectionChanged(inventory).ConfigureAwait(false);
        }

        /// <summary>
        /// ParentID == null → RootObjects. Otherwise set Parent (which AddChilds).
        /// Parents must already be in IDToObject; GetAll loads the full table first so one pass is enough.
        /// Setting Parent when ParentID already matches does not mark DBAction.UPDATE.
        /// </summary>
        void WireParentsAndRoots(IEnumerable<OBJECT> objects)
        {
            foreach (OBJECT obj in objects)
            {
                if (obj == null)
                    continue;

                if (!obj.ParentID.HasValue)
                {
                    TryAddRootObject(obj.ID);
                    continue;
                }

                if (IDToObject.TryGetValue(obj.ParentID.Value, out OBJECT parent))
                    obj.Parent = parent;
            }
        }

        protected bool TryAddRootObject(KEY key)
        {
            bool added = PrivateTryAddRootObject(key);
            if (added && PropertyChanged != null)
            {  
                PropertyChanged.RaiseEventOnUIThread(new PropertyChangedEventArgs(nameof(RootObjects)));
            }

            return added;
        }

        /// <summary>
        /// Adds several keys to RootObjects under one lock, then raises PropertyChanged once if any were new.
        /// </summary>
        protected bool[] TryAddRootObjects(KEY[] keys)
        {
            bool[] results = new bool[keys.Length];

            for(int i = 0; i < keys.Length; i++)
            { 
                results[i] = PrivateTryAddRootObject(keys[i]);
            }
            
            if (results.Any(added => added) && PropertyChanged != null)
            { 
                PropertyChanged.RaiseEventOnUIThread(new PropertyChangedEventArgs(nameof(RootObjects)));
            }

            return results;
        }

        private bool PrivateTryAddRootObject(KEY key)
        {
            try
            {
                _rwLockRootObjects.TryEnterUpgradeableReadLock(-1);
                if (_rootObjects.Contains(key))
                    return false;
                else
                {
                    try
                    {
                        _rwLockRootObjects.TryEnterWriteLock(-1);
                        if (_rootObjects.Contains(key) == false)
                        {
                            _rootObjects.Add(key);
                            return true;
                        }

                        return false;
                    }
                    finally
                    {
                        _rwLockRootObjects.ExitWriteLock();
                    }
                }
            }
            finally
            {
                _rwLockRootObjects.ExitUpgradeableReadLock();
            }
        } 

        protected bool TryRemoveRootObject(KEY key)
        {
            bool added = PrivateTryRemoveRootObject(key);
            if (added && PropertyChanged != null)
            { 
                PropertyChanged.RaiseEventOnUIThread(new PropertyChangedEventArgs("RootObjects"));
            }

            return added;
        }

        private bool PrivateTryRemoveRootObject(KEY key)
        {
            try
            {
                _rwLockRootObjects.TryEnterUpgradeableReadLock(-1);
                if (false == _rootObjects.Contains(key))
                    return false;
                else
                {
                    try
                    {
                        _rwLockRootObjects.TryEnterWriteLock(-1);
                        bool removed = _rootObjects.Remove(key);
                        return removed;
                    }
                    finally
                    {
                        _rwLockRootObjects.ExitWriteLock();
                    }
                }
            }
            finally
            {
                _rwLockRootObjects.ExitUpgradeableReadLock();
            }
        }
        /*
        DEAD WCF-era path. Server ingest uses IStoreEditor.GetOrAdd, not InternalAdd.
        Hierarchy wiring now lives in CallOnCollectionChanged → WireParentsAndRoots.
        Do not uncomment without also routing GetAll through InternalAdd, or you will
        double-wire and fight the live path.

        /// <summary>
        /// Used to populate cache when a call returns from the server
        /// </summary>
        /// <param name="updateObj"></param>
        /// <returns></returns>
        protected override ChangeInventory<OBJECT> InternalAdd(OBJECT[] newType)
        {
            return InternalAdd(newType, false).Result;
        }
        
        /// <summary>
        /// Used to populate cache when a call returns from the server
        /// </summary>
        /// <param name="updateObj"></param>
        /// <returns></returns>
        protected virtual async Task<ChangeInventory<OBJECT>> InternalAdd(OBJECT[] addObjs, bool LoadParents)
        {
            var changeInventory = base.InternalAdd(addObjs);
            
            //List of all parent objects which are missing.  These need to be loaded.
            List<KEY> listMissingParents = new List<KEY>(addObjs.Length);
            List<OBJECT> listObjNeedingParents = new List<OBJECT>(addObjs.Length);

            foreach (var newObj in changeInventory.AddedObjects)
            {
                if (newObj.ParentID.HasValue == false) 
                    TryAddRootObject(newObj.ID);
                else
                {  
                    //Don't use newObj.Parent in if test because get method will fetch parent
                    if (IDToObject.TryGetValue(newObj.ParentID.Value, out var parent))
                    {
                        newObj.Parent = parent;
                    }
                    else
                    {
                        //If it is a new parentID then add it
                        if (listMissingParents.Contains(newObj.ParentID.Value) == false)
                            listMissingParents.Add(newObj.ParentID.Value);

                        listObjNeedingParents.Add(newObj); 
                    }
                }
            }

            //Go find all of the missing parent objects and make sure they are downloadedfs
            if (listMissingParents.Count > 0)
            {
                var parentInventory = await InternalGetObjectsByIDs(listMissingParents.ToArray(), CancellationToken.None);
                changeInventory.Add(parentInventory);
            }
            

            return changeInventory;
        }
        
        protected override OBJECT[] InternalUpdate(OBJECT[] newObjs)
        {
            return InternalUpdate(newObjs, false);
        }

        /// <summary>
        /// Used to populate cache when a call returns from the server
        /// </summary>
        /// <param name="updateObj"></param>
        /// <returns></returns>
        internal virtual OBJECT[] InternalUpdate(OBJECT[] updateObjs, bool LoadParent)
        {
            List<OBJECT> listUpdatedObjs = new List<OBJECT>(updateObjs.Length);
            List<OBJECT> listOldObjs = new List<OBJECT>(updateObjs.Length);

            for (int iObj = 0; iObj < updateObjs.Length; iObj++)
            {
                OBJECT existingObj;
                OBJECT updateObj = updateObjs[iObj];
                bool Success = IDToObject.TryGetValue(updateObj.ID, out existingObj);

                if (Success)
                {
                    OBJECT oldObj = existingObj.Clone() as OBJECT;
                    Debug.Assert(oldObj != null);

                    listOldObjs.Add(oldObj);

                    //Remove ourselves from the root list if we have a ParentID
                    if (false == existingObj.ParentID.Equals(updateObj.ParentID))
                    {
                        if (existingObj.ParentID.HasValue)
                        {
                            TryRemoveRootObject(existingObj.ID);
                        }
                        else
                        {
                            //Remove ourselves from our parent object
                            existingObj.Parent = null;
                        }
                    }

                    existingObj.Update(updateObj.GetData());

                    listUpdatedObjs.Add(existingObj);

                    //Add ourselves from the root list if we do not have a ParentID
                    if (!existingObj.ParentID.HasValue)
                    {
                        TryAddRootObject(existingObj.ID);
                    }
                    else if (LoadParent)
                    {
                        //Make sure the structure object points to the correct parent
                        existingObj.Parent = await GetObjectByID(existingObj.ParentID.Value, CancellationToken.None);

                        //If it returns null we couldn't find the parent on the server, what the hell?
                        Debug.Assert(existingObj.Parent != null, "Couldn't locate parent of the structureType, Hit continue to reload all structure types in a panic");
                    }
                }
            }

            return listUpdatedObjs.ToArray();
        }

        */

        /// <summary>
        /// Used to populate cache when a call returns from the server
        /// </summary>
        /// <param name="updateObj"></param>
        /// <returns></returns>
        protected override List<OBJECT> InternalDelete(KEY[] IDs)
        {
            List<OBJECT> listDeleted = new List<OBJECT>(IDs.Length);

            for (int iObj = 0; iObj < IDs.Length; iObj++)
            {
                KEY ID = IDs[iObj];
                OBJECT obj = TryRemoveObject(ID);
                if (obj != null)
                {
                    listDeleted.Add(obj);
                }
            }

            return listDeleted;
        }

        /// <summary>
        /// Remove our local cache for an object.  Delete event subscriptions on the object.
        /// Return object reference if the object was found an removed.
        /// </summary> 
        //OBJECT IStoreEditor<KEY, OBJECT>.TryRemoveObject(KEY key)
        //{
        //    return TryRemoveObject(key);
        //}


        protected override OBJECT TryRemoveObject(KEY key)
        {
            OBJECT existingObj;
            bool success = IDToObject.TryRemove(key, out existingObj);
            if (success)
            {
                existingObj.PropertyChanged -= this.OnOBJECTPropertyChangedEventHandler;
                //existingObj.Dispose(); 

                TryRemoveFromParent(existingObj);
            }
            else
            {
                existingObj = null;
            }

            return existingObj;
        }

        private void TryRemoveFromParent(OBJECT obj)
        {
            if (obj.ParentID.HasValue == false)
            {
                TryRemoveRootObject(obj.ID);
                //                _rootObjects.TryRemove(obj.ID, out OBJECT outVal);
            }
            else
            {
                //Long winded way of removing ourselves from our parents list
                if (obj.Parent != null)
                    obj.Parent.RemoveChild(obj);
            }

        }

        Task<OBJECT> IStoreWithKey<KEY, OBJECT>.GetOrAdd(KEY key, Func<KEY, OBJECT> createFunc, out bool added)
        {
            return Task.FromResult(GetOrAdd(key, createFunc, out added));
        }

        Task<OBJECT> IStoreWithKey<KEY, OBJECT>.Remove(KEY key)
        {
            return Remove(key);
        }

        OBJECT IStoreWithKey<KEY, OBJECT>.ForgetLocally(KEY key)
        {
            return ForgetLocally(key);
        }

        List<OBJECT> IStoreWithKey<KEY, OBJECT>.ForgetLocally(KEY[] keys)
        {
            return ForgetLocally(keys);
        }

        public KEY NextKey()
        {
            // Temporary client-side IDs for unsaved objects. long keys use a shared negative counter;
            // other key types are not supported for client-generated IDs.
            if (typeof(KEY) == typeof(long))
            {
                long id = ((IKeyGenerator<long>)LongKeyGenerator).NextKey();
                return (KEY)(object)id;
            }

            throw new NotSupportedException($"NextKey is not supported for key type {typeof(KEY).Name}.");
        }

        private static readonly LongIndexGenerator LongKeyGenerator = new LongIndexGenerator();
    }
}
