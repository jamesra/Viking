using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.ServiceModel;
using WebAnnotation.Service; 
using WebAnnotation.Objects;
using System.Diagnostics; 
using System.Collections.Concurrent; 

namespace WebAnnotation
{
    /// <summary>
    /// This base class implements the basic functionality to talk to a WCF Service
    /// </summary>
    abstract class StoreBase<PROXY, INTERFACE, OBJECT, WCFOBJECT> 
        where INTERFACE : class
        where PROXY : System.ServiceModel.ClientBase<INTERFACE>
        where WCFOBJECT : WebAnnotation.Service.DataObjectWithKeyOflong, new()
        where OBJECT : WCFObjBaseWithKey<WCFOBJECT>, new()
    {
        //Perform any required initialization
        public abstract void Init();

        /// <summary>
        /// When we create an object we don't know the ID the database will give it.  We use this value on the local machine until the DB
        /// tells us the new ID on insert
        /// </summary>
        long nextTempID = -1;
        public long GetTempID()
        {
            return nextTempID--;
        }

        protected abstract PROXY CreateProxy();

        /// <summary>
        /// This lock must be taken any time a datastructure is accessed.  Various proxy calls are asynch
        /// </summary>
        protected object LockObject = new object();

        /// <summary>
        /// Maps IDs to the corresponding object
        /// </summary>
        protected ConcurrentDictionary<long, OBJECT> IDToObject = new ConcurrentDictionary<long, OBJECT>();

        #region Events

        /// <summary>
        /// This event fires whenever a structure in the store is added or removed.  This can occur during synchs with the database        
        /// </summary>
        public event AddUpdateRemoveKeyEventHandler OnAddUpdateRemoveKey;

        /// <summary>
        /// This is fired when all objects retrieved from a call to the database have been added/updated/removed
        /// </summary>
        public event OnAllUpdatesCompletedEventHandler OnAllUpdatesCompleted; 

        protected void CallOnAddUpdateRemoveKey(OBJECT obj, AddUpdateRemoveKeyEventArgs e)
        {
            if(OnAddUpdateRemoveKey != null)
            {
                OnAddUpdateRemoveKey(obj, e); 
            }
        }

        protected void CallOnAllUpdatesCompleted(OnAllUpdatesCompletedEventArgs e)
        {
            if (OnAllUpdatesCompleted != null)
            {
                OnAllUpdatesCompleted(this, e);
            }
        }


        #endregion 

        protected void ShowStandardExceptionMessage(Exception e)
        {
            Trace.WriteLine(e.ToString());
            Trace.WriteLine(e.Message);
            System.Windows.Forms.MessageBox.Show("An error occurred:\n" + e.Message, "WebAnnotation");
        }

        #region Add/Update/Remove methods

        internal abstract OBJECT Add(OBJECT newObj);
        internal abstract OBJECT Update(OBJECT newObj);
        internal abstract void Remove(long ID);

        #endregion

        #region Proxy Calls

        internal abstract long[] ProxyUpdate(PROXY proxy, WCFOBJECT[] objects);
        internal abstract WCFOBJECT ProxyGetByID(PROXY proxy, long ID);

        #endregion

        #region Queries

        /// <summary>
        /// Gets the requested location, first checking locally, then asking the server
        /// </summary>
        /// <param name="ID"></param>
        /// <returns></returns>
        public OBJECT GetObjectByID(long ID)
        {
            return GetObjectByID(ID, true);
        }

        /// <summary>
        /// Gets the requested location, first checking locally, then asking the server
        /// </summary>
        /// <param name="ID"></param>
        /// <param name="AskServer">If false only the local cache is checked</param>
        /// <returns></returns>
        public OBJECT GetObjectByID(long ID, bool AskServer)
        {
            OBJECT newObj = null;

            bool Success = IDToObject.TryGetValue(ID, out newObj);
            if (Success)
                return newObj;
            else
            {
                if (!AskServer)
                    return null;

                //If not check if the server knows what we're asking for
                WCFOBJECT data = null;
                PROXY proxy = CreateProxy();
                try
                {
                    Trace.WriteLine("Going to server to retrieve " + this.ToString() + " parent with ID: " + ID.ToString(), "WebAnnotation");
                    proxy.Open();
                    data = ProxyGetByID(proxy, ID);
                }
                catch (Exception e)
                {
                    Trace.WriteLine(e.ToString(), "WebAnnotation");
                    Trace.WriteLine(e.Message, "WebAnnotation");
                    data = null;
                }

                if (proxy != null)
                    proxy.Close();


                if (data != null)
                {
                    newObj = new OBJECT();
                    newObj.Synch(data);
                    newObj = Add(newObj);

                    if (OnAllUpdatesCompleted != null)
                        OnAllUpdatesCompleted(null, new OnAllUpdatesCompletedEventArgs(new OBJECT[] { newObj }));
                }

                return newObj;
            }
        }

        /// <summary>
        /// Save all changes to locations, returns true if the method completed without errors, otherwise false
        /// </summary>
        public bool Save()
        {
            List<WCFOBJECT> changed = new List<WCFOBJECT>();
            List<OBJECT> output = new List<OBJECT>();
            
            {
                try
                {
                    //TODO Parfor
                    foreach (OBJECT obj in IDToObject.Values)
                    {
                        if (obj.DBAction == DBACTION.NONE)
                            continue;

                        if (obj.DBAction == DBACTION.INSERT ||
                            obj.DBAction == DBACTION.UPDATE)
                            obj.FireBeforeSaveEvent();
                        else if (obj.DBAction == DBACTION.DELETE)
                            obj.FireBeforeDeleteEvent();

                        changed.Add(obj.GetData());
                    }

                    Trace.WriteLine("Saving this number of objects: " + changed.Count, "WebAnnotation"); 

                    /*Don't make the call if there are no changes */
                    if (changed.Count == 0)
                        return true;

                    PROXY proxy = CreateProxy();
                    proxy.Open();

                    long[] newIDs = new long[0];
                    try
                    {
                        newIDs = ProxyUpdate(proxy, changed.ToArray());
                    }
                    catch (Exception e)
                    {
                        System.Windows.Forms.MessageBox.Show("An error occurred during the update:\n" + e.Message);
                        return false;
                    }
                    finally
                    {
                        proxy.Close();
                    }

                    Debug.Assert(changed.Count == newIDs.Length);

                    //Update ID's of new objects
                    for (int iObj = 0; iObj < changed.Count; iObj++)
                    {
                        WCFOBJECT data = changed[iObj];
                        if (IDToObject.ContainsKey(data.ID) == false)
                        {
                            data.DBAction = DBACTION.NONE;
                            continue;
                        }

                        OBJECT obj = IDToObject[data.ID];
                        OBJECT newobj = null;

                        switch (data.DBAction)
                        {
                            case DBACTION.INSERT:
                                //Remove from our old spot in the database
                                Remove(data.ID);

                                //Update the ID of the object
                                data.ID = newIDs[iObj];

                                //Insert in the new correct location
                                newobj = Add(obj);
                                output.Add(newobj);

                                obj.FireAfterSaveEvent();
                                break;
                            case DBACTION.UPDATE:
                                newobj = Update(obj);
                                output.Add(newobj);

                                obj.FireAfterSaveEvent();
                                break;

                            case DBACTION.DELETE:
                                //Remove from our old spot in the database
                                Remove(obj.ID);
                                obj.FireAfterDeleteEvent();
                                break;

                            default:
                                break;
                        }

                        data.DBAction = DBACTION.NONE;
                    }
                }
                catch (FaultException e)
                {
                    System.Windows.Forms.MessageBox.Show("An exception occurred while saving structure types.  Viking is pretending none of the changes happened.  Exception Data: " + e.Message, "Error");

                    if (changed != null)
                    {
                        //Update ID's of new objects
                        for (int iObj = 0; iObj < changed.Count; iObj++)
                        {
                            WCFOBJECT data = changed[iObj];
                            if (IDToObject.ContainsKey(data.ID) == false)
                            {
                                data.DBAction = DBACTION.NONE;
                                continue;
                            }

                            OBJECT obj = IDToObject[data.ID];

                            switch (data.DBAction)
                            {
                                case DBACTION.INSERT:
                                    //Remove from our old spot in the database
                                    Remove(data.ID);

                                    break;

                                case DBACTION.DELETE:
                                    //Just reset our DBState to none after case statement
                                    break;

                                default:
                                    break;
                            }

                            data.DBAction = DBACTION.NONE;
                        }
                    }

                    //If we caught an exception return false
                    return false;
                }
            }

            if (OnAllUpdatesCompleted != null)
                OnAllUpdatesCompleted(null, new OnAllUpdatesCompletedEventArgs(output.ToArray()));

            return true; 
        }

        #endregion

    }
}
