using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Diagnostics;
using System.Linq;
using System.Threading;
using WebAnnotationModel.Objects;

namespace WebAnnotationModel
{
    /// <summary>
    /// This base class implements the basic functionality to talk to a WCF Service
    /// </summary>
    public abstract class StoreBaseWithIndexKeyAndParent<PROXY, INTERFACE, KEY, KEYGEN, OBJECT, WCFOBJECT> : StoreBaseWithIndexKey<PROXY, INTERFACE, KEY, KEYGEN, OBJECT, WCFOBJECT>,
        INotifyPropertyChanged
        where INTERFACE : class
        where KEY : struct, IEquatable<KEY>
        where KEYGEN : IKeyGenerator<KEY>, new()
        where PROXY : System.ServiceModel.ClientBase<INTERFACE>
        where OBJECT : AnnotationModelObjBaseWithParent<KEY, WCFOBJECT, OBJECT>, new()
        where WCFOBJECT : AnnotationService.Types.DataObjectWithParentOfLong, new()
    {

        protected ReaderWriterLockSlim _rwLockRootObjects = new ReaderWriterLockSlim();
        /// <summary>
        /// Known objects with no parent object
        /// </summary>
        private readonly ObservableCollection<KEY> _rootObjects = new ObservableCollection<KEY>();
        private KEY[] _readOnlyRootObjects;

        public event PropertyChangedEventHandler PropertyChanged;

        public KEY[] RootObjects
        {
            get
            {
                try
                {
                    _rwLockRootObjects.EnterUpgradeableReadLock();
                    if (_readOnlyRootObjects != null)
                    {
                        return _readOnlyRootObjects;
                    }

                    try
                    {
                        _rwLockRootObjects.EnterWriteLock();
                        if (_readOnlyRootObjects != null)
                        {
                            return _readOnlyRootObjects;
                        }

                        _readOnlyRootObjects = _rootObjects.ToArray();
                        return _readOnlyRootObjects;
                    }
                    finally
                    {
                        _rwLockRootObjects.ExitWriteLock();
                    }
                }
                finally
                {
                    _rwLockRootObjects.ExitUpgradeableReadLock();
                }

            }
        }

        protected bool TryAddRootObject(KEY key)
        {
            bool added = PrivateTryAddRootObject(key);
            if (added && PropertyChanged != null)
            {
                _readOnlyRootObjects = null;
                PropertyChanged.RaiseEventOnUIThread(new PropertyChangedEventArgs("RootObjects"));
            }

            return added;
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
                            _readOnlyRootObjects = null; //Blank out the visible array.  Our caller should send a notification
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
                _readOnlyRootObjects = null;
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
                        if (removed)
                            _readOnlyRootObjects = null; //Blank out the visible array.  Our caller should send a notification
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

        /// <summary>
        /// Used to populate cache when a call returns from the server
        /// </summary>
        /// <param name="updateObj"></param>
        /// <returns></returns>
        protected override ChangeInventory<OBJECT> InternalAdd(OBJECT[] newType)
        {
            return InternalAdd(newType, false);
        }

        /// <summary>
        /// Used to populate cache when a call returns from the server
        /// </summary>
        /// <param name="updateObj"></param>
        /// <returns></returns>
        protected virtual ChangeInventory<OBJECT> InternalAdd(OBJECT[] addObjs, bool LoadParents)
        {
            List<OBJECT> listAddedObj = new List<OBJECT>(addObjs.Length);

            //This list records objects we can't add which must be updated instead
            List<OBJECT> listUpdateObj = new List<OBJECT>(addObjs.Length);

            //List of all parent objects which are missing.  These need to be loaded.
            List<KEY> listMissingParents = new List<KEY>(addObjs.Length);

            List<OBJECT> listObjNeedingParents = new List<OBJECT>(addObjs.Length);

            for (int iObj = 0; iObj < addObjs.Length; iObj++)
            {
                OBJECT newObj = addObjs[iObj];

                bool added = TryAddObject(newObj);

                if (false == added)
                {
                    listUpdateObj.Add(newObj);
                }
                else
                {
                    if (newObj.ParentID.HasValue == false)
                    {
                        TryAddRootObject(newObj.ID);
                    }
                    else
                    {
                        //Added the false for dynamic structure loading change, may cause bugs
                        OBJECT parent = GetObjectByID(newObj.ParentID.Value, false);
                        //Don't use newObj.Parent in if test because get method will fetch parent
                        if (parent == null)
                        {
                            //If it is a new parentID then add it
                            if (listMissingParents.Contains(newObj.ParentID.Value) == false)
                                listMissingParents.Add(newObj.ParentID.Value);

                            listObjNeedingParents.Add(newObj);
                        }
                        else
                        {
                            newObj.Parent = parent;
                        }

                        //If it returns null we couldn't find the parent on the server, what the hell?
                        //Debug.Assert(newObj.Parent != null, "Couldn't locate parent of the structureType, Hit continue to reload all structure types in a panic");
                    }

                    listAddedObj.Add(newObj);
                }
            }

            ChangeInventory<OBJECT> inventory = new ChangeInventory<OBJECT>();
            inventory.AddedObjects.AddRange(listAddedObj);

            //Go find all of the missing parent objects and make sure they have been downloaded
            if (listMissingParents.Count > 0)
            {
                ChangeInventory<OBJECT> parent_inventory = InternalGetObjectsByIDs(listMissingParents.ToArray(), true);
                inventory.Add(parent_inventory);
            }


            if (listUpdateObj.Count > 0)
            {
                OBJECT[] updatedObjs = InternalUpdate(listUpdateObj.ToArray());
                inventory.UpdatedObjects.AddRange(updatedObjs);
            }

            //OK, now go through and make sure every object is correctly assigned to its parent.
            foreach (OBJECT newObj in listObjNeedingParents)
            {
                //Added the false for dynamic structure loading change, may cause bugs
                newObj.Parent = GetObjectByID(newObj.ParentID.Value, false);
                if (newObj.Parent != null)
                {
                    //TODO: This shouldn't happen unless the parent was somehow deleted from the server...
                    //InternalDelete(newObj.ID);
                }
            }

            return inventory;
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
                        existingObj.Parent = GetObjectByID(existingObj.ParentID.Value);

                        //If it returns null we couldn't find the parent on the server, what the hell?
                        Debug.Assert(existingObj.Parent != null, "Couldn't locate parent of the structureType, Hit continue to reload all structure types in a panic");
                    }
                }
            }

            return listUpdatedObjs.ToArray();
        }



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
    }
}
