using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.ServiceModel;
using WebAnnotationModel.Service;
using WebAnnotationModel.Objects;
using System.Diagnostics;
using System.Collections.Concurrent;
using System.Collections.Specialized;

namespace WebAnnotationModel
{
    /// <summary>
    /// This base class implements the basic functionality to talk to a WCF Service
    /// </summary>
    public abstract class StoreBaseWithIndexKeyAndParent<PROXY, INTERFACE, KEY, KEYGEN, OBJECT, WCFOBJECT> : StoreBaseWithIndexKey<PROXY, INTERFACE, KEY, KEYGEN, OBJECT, WCFOBJECT>
        where INTERFACE : class
        where KEY : struct
        where KEYGEN : IKeyGenerator<KEY>, new()
        where PROXY : System.ServiceModel.ClientBase<INTERFACE>
        where OBJECT : WCFObjBaseWithParent<KEY, WCFOBJECT, OBJECT>, new()
        where WCFOBJECT : WebAnnotationModel.Service.DataObjectWithParentOflong, new()
        
    {

        /// <summary>
        /// Known objects with no parent object
        /// </summary>
        public ConcurrentDictionary<KEY, OBJECT> rootObjects = new ConcurrentDictionary<KEY, OBJECT>();

        /// <summary>
        /// Used to populate cache when a call returns from the server
        /// </summary>
        /// <param name="updateObj"></param>
        /// <returns></returns>
        internal override OBJECT[] InternalAdd(OBJECT[] newType)
        {
            return InternalAdd(newType, false);
        }

        /// <summary>
        /// Used to populate cache when a call returns from the server
        /// </summary>
        /// <param name="updateObj"></param>
        /// <returns></returns>
        internal virtual OBJECT[] InternalAdd(OBJECT[] addObjs, bool LoadParents)
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

                bool added = IDToObject.TryAdd(newObj.ID, newObj);
                if (false == added)
                {
                    listUpdateObj.Add(newObj);
                }
                else
                {
                    newObj.PropertyChanged += this.OnOBJECTPropertyChangedEventHandler;
                    if (newObj.ParentID.HasValue == false)
                    {
                        rootObjects.TryAdd(newObj.ID, newObj);
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

            //Go find all of the missing parent objects and make sure they have been downloaded
            if(listMissingParents.Count > 0)
                GetObjectsByIDs(listMissingParents.ToArray(), true);
            
            if (listUpdateObj.Count > 0)
            {
                OBJECT[] updatedObjs = InternalUpdate(listUpdateObj.ToArray());
                listAddedObj.AddRange(updatedObjs);
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
            
            if (listAddedObj.Count > 0)
            {
                OBJECT[] listCopy = new OBJECT[listAddedObj.Count];
                listAddedObj.CopyTo(listCopy);
                CallOnCollectionChanged(new NotifyCollectionChangedEventArgs(NotifyCollectionChangedAction.Add, listCopy));
            }

            return listAddedObj.ToArray();
        }

        internal override OBJECT[] InternalUpdate(OBJECT[] newObjs)
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
                            OBJECT removedObj;
                            rootObjects.TryRemove(existingObj.ID, out removedObj);
                        }
                        else
                        {
                            //Remove ourselves from our parent object
                            existingObj.Parent = null;
                        }
                    }

                    existingObj.Synch(updateObj.GetData());

                    listUpdatedObjs.Add(existingObj);

                    //Add ourselves from the root list if we do not have a ParentID
                    if (!existingObj.ParentID.HasValue)
                    {
                        rootObjects.TryAdd(existingObj.ID, existingObj);
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

            if (listUpdatedObjs.Count > 0)
            {
                //Create copies of the changed objects because I have a mysterious bug where the collections in these notifications have been modified
                OBJECT[] newItemsCopy = null;
                OBJECT[] oldItemsCopy = null;

                if (listUpdatedObjs != null)
                {
                    newItemsCopy = new OBJECT[listUpdatedObjs.Count];
                    listUpdatedObjs.CopyTo(newItemsCopy);
                }

                if (listOldObjs != null)
                {
                    oldItemsCopy = new OBJECT[listOldObjs.Count];
                    listOldObjs.CopyTo(oldItemsCopy);
                }

                CallOnCollectionChanged(new NotifyCollectionChangedEventArgs(NotifyCollectionChangedAction.Replace, newItemsCopy, oldItemsCopy));

                foreach(OBJECT obj in listOldObjs)
                {
                    obj.Dispose(); 
                }

                listOldObjs.Clear(); 
            }

            return listUpdatedObjs.ToArray();
        }

        

        /// <summary>
        /// Used to populate cache when a call returns from the server
        /// </summary>
        /// <param name="updateObj"></param>
        /// <returns></returns>
        internal override void InternalDelete(KEY[] IDs)
        {
            List<OBJECT> listDeleted = new List<OBJECT>(IDs.Length);

            for (int iObj = 0; iObj < IDs.Length; iObj++)
            {
                KEY ID = IDs[iObj];
                OBJECT obj;
                bool Success = IDToObject.TryRemove(ID, out obj);

                if (Success)
                {
                    listDeleted.Add(obj);
                    obj.PropertyChanged -= this.OnOBJECTPropertyChangedEventHandler;

                    if (obj.ParentID.HasValue == false)
                    {
                        OBJECT outVal;
                        rootObjects.TryRemove(ID, out outVal);
                    }
                    else
                    {
                        //Long winded way of removing ourselves from our parents list
                        if(obj.Parent != null)
                            obj.Parent.RemoveChild(obj);
                    }
                }
            }

            //Let consumers know this key is about to go away
            if (listDeleted.Count > 0)
            {
                OBJECT[] listCopy = new OBJECT[listDeleted.Count];
                listDeleted.CopyTo(listCopy);
                CallOnCollectionChanged(new NotifyCollectionChangedEventArgs(NotifyCollectionChangedAction.Remove, listCopy));
            }
        }
    }
}
