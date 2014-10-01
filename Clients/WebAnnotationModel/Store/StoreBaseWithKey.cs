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
    public abstract class StoreBaseWithKey<PROXY, INTERFACE, KEY, OBJECT, WCFOBJECT> : StoreBase<PROXY, INTERFACE, OBJECT, WCFOBJECT>
        where INTERFACE : class
        where KEY : struct
        where PROXY : System.ServiceModel.ClientBase<INTERFACE>
        where WCFOBJECT : DataObject, new()
        where OBJECT : WCFObjBaseWithKey<KEY, WCFOBJECT>, new()
    {
        /// <summary>
        /// Maps IDs to the corresponding object
        /// </summary>
        protected ConcurrentDictionary<KEY, OBJECT> IDToObject = new ConcurrentDictionary<KEY, OBJECT>();

        /// <summary>
        /// When we query the database for objects on a section we store the query time for the section
        /// That way on the next query we only need to store the updates.
        /// </summary>
        private ConcurrentDictionary<long, DateTime> LastQueryForSection = new ConcurrentDictionary<long, DateTime>();

        /// <summary>
        /// A collection of values indicating which sections have an outstanding query. 
        /// The existence of a key indicates a query is in progress
        /// </summary>
        private ConcurrentDictionary<long, GetObjectBySectionCallbackState> OutstandingSectionQueries = new ConcurrentDictionary<long, GetObjectBySectionCallbackState>();

        protected ConcurrentDictionary<KEY, OBJECT> ChangedObjects = new ConcurrentDictionary<KEY, OBJECT>();

        protected System.ComponentModel.PropertyChangedEventHandler OnOBJECTPropertyChangedEventHandler;

        public StoreBaseWithKey()
        {
            OnOBJECTPropertyChangedEventHandler = new System.ComponentModel.PropertyChangedEventHandler(OnObjectPropertyChanged); 
        }

        protected void OnObjectPropertyChanged(object sender, System.ComponentModel.PropertyChangedEventArgs e)
        {
            if (e.PropertyName.ToLower() == "dbaction")
            {
                OBJECT obj = sender as OBJECT;
                if (obj == null)
                    return;

                if (obj.DBAction == DBACTION.NONE)
                {
                    OBJECT removedObj;
                    ChangedObjects.TryRemove(obj.ID, out removedObj);
                }
                else
                {
                    ChangedObjects.TryAdd(obj.ID, obj); 
                }
            }
        }

        #region Proxy Calls

        protected abstract WCFOBJECT ProxyGetByID(PROXY proxy, KEY ID);
        protected abstract WCFOBJECT[] ProxyGetByIDs(PROXY proxy, KEY[] IDs);
        protected abstract ConcurrentDictionary<KEY, OBJECT> ProxyBeginGetBySection(PROXY proxy,
                                                             long SectionNumber,
                                                             DateTime LastQuery,
                                                             AsyncCallback callback,
                                                             object asynchState);
        protected abstract WCFOBJECT[] ProxyGetBySectionCallback(out long TicksAtQueryExecute,
                                                                out KEY[] DeletedLocations,
                                                                GetObjectBySectionCallbackState state, 
                                                                IAsyncResult result);

        #endregion

        /// <summary>
        /// Create a local instance of a new item in the store
        /// This item is not sent to the server until save is 
        /// called.
        /// 
        /// Each store took a different set of parameters so I removed this, but it belongs here in spirit
        /// </summary>
        /// <param name="obj"></param>
        /// <returns></returns>
        public override OBJECT Add(OBJECT obj)
        {
            //Default implementation
            InternalAdd(obj);
            ChangedObjects.TryAdd(obj.ID, obj);
            return obj; 
        }

        /// <summary>
        /// Remove the passed object from the store. The item will be
        /// deleted from the server until save is called
        /// </summary>
        /// <param name="obj"></param>
        /// <returns></returns>
        public override bool Remove(OBJECT obj)
        {
            //Default implementation
            obj.DBAction = DBACTION.DELETE;
            InternalDelete(obj.ID);
            ChangedObjects.TryAdd(obj.ID, obj);
            return true; 
        }

        
        #region Internal Add/Update/Remove methods
       
        /// <summary>
        /// Used to populate cache when a call returns from the server. 
        /// These internal add/update/remove functions should not change
        /// the DBAction of the object unless the passed object already 
        /// has those changes
        /// 
        /// These methods should fire collection changed notifications
        /// </summary>
        /// <param name="updateObj"></param>
        /// <returns></returns>
        internal OBJECT InternalAdd(OBJECT newObj)
        {
            OBJECT[] retVal = InternalAdd(new OBJECT[] { newObj });
            if (retVal != null && retVal.Length > 0)
                return retVal[0];
            return null; 
        }

        /// <summary>
        /// Used to populate cache when a call returns from the server
        /// These internal add/update/remove functions should not change
        /// the DBAction of the object unless the passed object already 
        /// has those changes
        /// 
        /// These methods should fire collection changed notifications
        /// </summary>
        /// <param name="updateObj"></param>
        /// <returns></returns>
        internal OBJECT InternalUpdate(OBJECT newObj)
        {
            OBJECT[] retVal = InternalUpdate(new OBJECT[] { newObj });
            if (retVal != null && retVal.Length > 0)
                return retVal[0];
            return null; 
        }

        /// <summary>
        /// Used to populate cache when a call returns from the server
        /// These internal add/update/remove functions should not change
        /// the DBAction of the object unless the passed object already 
        /// has those changes
        /// 
        /// These methods should fire collection changed notifications
        /// </summary>
        /// <param name="updateObj"></param>
        /// <returns></returns>
        internal void InternalDelete(KEY ID)
        {
            InternalDelete(new KEY[] { ID });
        }

        /*
        /// <summary>
        /// Used to populate cache when a call returns from the server. 
        /// These internal add/update/remove functions should not change
        /// the DBAction of the object unless the passed object already 
        /// has those changes
        /// 
        /// These methods should fire collection changed notifications
        /// </summary>
        /// <param name="updateObj"></param>
        /// <returns></returns>
        internal abstract OBJECT[] InternalAdd(OBJECT[] newObjs);

        /// <summary>
        /// Used to populate cache when a call returns from the server
        /// These internal add/update/remove functions should not change
        /// the DBAction of the object unless the passed object already 
        /// has those changes
        /// 
        /// InternalUpdate returns an array containing every object which
        /// accepted the update
        /// 
        /// These methods should fire collection changed notifications
        /// </summary>
        /// <param name="updateObj"></param>
        /// <returns></returns>
        internal abstract OBJECT[] InternalUpdate(OBJECT[] newObjs);

        /// <summary>
        /// Used to populate cache when a call returns from the server
        /// These internal add/update/remove functions should not change
        /// the DBAction of the object unless the passed object already 
        /// has those changes
        /// 
        /// These methods should fire collection changed notifications
        /// </summary>
        /// <param name="updateObj"></param>
        /// <returns></returns>
        internal abstract void InternalDelete(KEY[] IDs);
        */

        #endregion

        #region Queries

        /// <summary>
        /// Gets the requested location, first checking locally, then asking the server
        /// </summary>
        /// <param name="ID"></param>
        /// <returns></returns>
        public OBJECT GetObjectByID(KEY ID)
        {
            return GetObjectByID(ID, true);
        }

        public OBJECT this [KEY index]
        {
            get { return IDToObject[index]; }
        }

        /// <summary>
        /// Gets the requested location, first checking locally, then asking the server
        /// </summary>
        /// <param name="ID"></param>
        /// <param name="AskServer">If false only the local cache is checked</param>
        /// <returns></returns>
        public OBJECT GetObjectByID(KEY ID, bool AskServer)
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
                    newObj = InternalAdd(newObj);

 //                   CallOnAllUpdatesCompleted(new OnAllUpdatesCompletedEventArgs(new OBJECT[] { newObj }));
                }

                return newObj;
            }
        }

        public List<OBJECT> GetObjectsByIDs(KEY[] IDs, bool AskServer)
        {

            //Objects we've already fetched
            List<OBJECT> listObjs = new List<OBJECT>(IDs.Length);

            //Objects not cached locally
            List<KEY> listRemoteObjs = new List<KEY>(IDs.Length);

            foreach (KEY ID in IDs)
            {
                OBJECT obj = null;
                bool Success = IDToObject.TryGetValue(ID, out obj);
                if (Success)
                {
                    listObjs.Add(obj);
                }
                else
                {
                    listRemoteObjs.Add(ID);
                }
            }

            if (!AskServer || listRemoteObjs.Count == 0)
                return listObjs;

            //If not check if the server knows what we're asking for
            WCFOBJECT[] listServerObjs = null;
            PROXY proxy = null;
            try
            {
                proxy = CreateProxy();
                //Trace.WriteLine("Going to server to retrieve " + this.ToString() + " parent with ID: " + ID.ToString(), "WebAnnotation");
                proxy.Open();
                listServerObjs = ProxyGetByIDs(proxy, listRemoteObjs.ToArray());
            }
            catch (Exception e)
            {
                Trace.WriteLine(e.ToString(), "WebAnnotation");
                Trace.WriteLine(e.Message, "WebAnnotation");
                listServerObjs = null;
            }
            finally
            {
                if (proxy != null)
                {
                    proxy.Close();
                    proxy = null;
                }
            }


            OBJECT[] serverObjs = ParseQuery(listServerObjs, new KEY[0], null);

            listObjs.AddRange(serverObjs); 

            return listObjs;
        }

        public abstract ConcurrentDictionary<KEY, OBJECT> GetLocalObjectsForSection(long SectionNumber);

        public virtual ConcurrentDictionary<KEY, OBJECT> GetObjectsForSection(long SectionNumber)
        {
            GetObjectBySectionCallbackState requestState;
            bool OutstandingRequest = OutstandingSectionQueries.TryGetValue(SectionNumber, out requestState);                
            if(OutstandingRequest)
            {
                return GetLocalObjectsForSection(SectionNumber); 
            }
            
            PROXY proxy = null;
            ConcurrentDictionary<KEY, OBJECT> knownObjects = new ConcurrentDictionary<KEY, OBJECT>();
            try
            {
                proxy = CreateProxy();
                proxy.Open();

//                WCFOBJECT[] locations = new WCFOBJECT[0];
                GetObjectBySectionCallbackState newState = new GetObjectBySectionCallbackState(proxy, SectionNumber);
                bool NoOutstandingRequest = OutstandingSectionQueries.TryAdd(SectionNumber, newState);
                if (NoOutstandingRequest)
                {
                    DateTime LastQuery = DateTime.MinValue;
                    if (LastQueryForSection.ContainsKey(SectionNumber))
                        LastQuery = LastQueryForSection[SectionNumber];

                    //Build list of Locations to check
                    knownObjects = ProxyBeginGetBySection(proxy,
                                            SectionNumber,
                                            LastQuery,
                                            new AsyncCallback(GetObjectsBySectionCallback),
                                            newState);
                }
            }
                
            catch (EndpointNotFoundException e)
            {
                Trace.WriteLine("Could not connect to annotation database: " + e.ToString());
            }
            catch (Exception e)
            {
                ShowStandardExceptionMessage(e);
                if (proxy != null)
                {
                    proxy.Close();
                    proxy = null;
                }
            }
            
            return knownObjects; 
        }

        protected class GetObjectBySectionCallbackState
        {
            public readonly PROXY Proxy;
            public readonly long SectionNumber;
            public readonly DateTime StartTime = DateTime.Now;

            public override string ToString()
            {
                return SectionNumber.ToString() + " : " + StartTime.TimeOfDay.ToString(); 
            }

            public GetObjectBySectionCallbackState(PROXY proxy, long number)
            {
                this.Proxy = proxy;
                SectionNumber = number;
            }
        }

        protected void GetObjectsBySectionCallback(IAsyncResult result)
        {
            GetObjectBySectionCallbackState state = result.AsyncState as GetObjectBySectionCallbackState;
            PROXY proxy = state.Proxy;

            //This happens if we called abort
            if (state.Proxy.State == CommunicationState.Closed ||
               state.Proxy.State == CommunicationState.Closing ||
               state.Proxy.State == CommunicationState.Faulted)
                return; 

            Debug.Assert(proxy != null);

            KEY[] DeletedLocations = new KEY[0];
            long TicksAtQueryExecute = 0;

            WCFOBJECT[] objs;
            try
            {
                objs = ProxyGetBySectionCallback(out TicksAtQueryExecute, out DeletedLocations, state, result);
            }
            catch (TimeoutException )
            {
                Debug.Write("Timeout waiting for server results");
                return;
            }
            catch (EndpointNotFoundException )
            {
                Debug.Write("GetLocationChangesCallback - Endpoint not found exception");
                return;
            }
            catch (Exception e)
            {
                ShowStandardExceptionMessage(e);
                return;
            }
            finally
            {
                if (proxy != null)
                    proxy.Close();
            }

            DateTime TraceQueryEnd = DateTime.Now; 

            DateTime QueryExecuteTime = new DateTime(TicksAtQueryExecute, DateTimeKind.Utc);
            DateTime LastQuery;
            bool HaveLastQuery = LastQueryForSection.TryGetValue(state.SectionNumber, out LastQuery);
            if(false == HaveLastQuery)
                LastQuery = DateTime.MinValue;

            //Don't update if we've got results from a query executed after this one
            

            if(LastQuery < QueryExecuteTime)
            {
                ParseQuery(objs, DeletedLocations, state);

                DateTime TraceParseEnd = DateTime.Now; 
                

                //Record the time our query executed
                LastQueryForSection[state.SectionNumber] = QueryExecuteTime;

                Trace.WriteLine("Sxn " + state.SectionNumber.ToString() + " finished " + typeof(OBJECT).ToString() + " query.  " + objs.Length.ToString() + " returned");
                Trace.WriteLine("\tQuery Time: " + new TimeSpan(TraceQueryEnd.Ticks - state.StartTime.Ticks).TotalSeconds.ToString() + " (sec) elapsed");
                Trace.WriteLine("\tParse Time: " + new TimeSpan(TraceParseEnd.Ticks - TraceQueryEnd.Ticks).TotalSeconds.ToString() + " (sec) elapsed");
            }
            else
                Trace.WriteLine(this.GetType().ToString() + " ignoring stale query results for section: " + state.SectionNumber.ToString(), "WebAnnotation");
            
            //Remove the entry from outstanding queries so we can query again
            OutstandingSectionQueries.TryRemove(state.SectionNumber, out state);
        }

        /// <summary>
        /// This function is called when a query returns from a asynch method.  You can override it to produce different behaviors
        /// </summary>
        /// <param name="locations">Objects which have been added or modified since the last query</param>
        /// <param name="DeletedLocations">Objects which have been deleted since the last query</param>
        /// <param name="state">Reports section number of query, Nullable</param>
        protected virtual OBJECT[] ParseQuery(WCFOBJECT[] locations, KEY[] DeletedLocations, GetObjectBySectionCallbackState state)
        {
            OBJECT[] listObj = new OBJECT[0];
                       
            if (DeletedLocations != null)
            {
                InternalDelete(DeletedLocations);
            }

            OBJECT[] listNewObj = new OBJECT[locations.Length];
            System.Threading.Tasks.Parallel.For(0, locations.Length, (i) =>
            {
                OBJECT newObj = new OBJECT();
                newObj.Synch(locations[i]);
                listNewObj[i] = newObj;
            });

            listObj = InternalAdd(listNewObj);
            return listObj; 
        }

        #endregion

        private static int CompareCallbacksByTime(GetObjectBySectionCallbackState x, GetObjectBySectionCallbackState y)
        {
            if (x == null)
            {
                if (y == null)
                    return 0;
                else
                    return -1;
            }
            else
            {
                if (y == null)
                    return 1;
                else
                {
                    return x.StartTime.CompareTo(y.StartTime);
                }
            }
        }

        /// <summary>
        /// This is called to instruct the store to eliminate objects from the oldest section query.
        /// This is done to save memory
        /// </summary>
        /// <param name="LoadedSectionLimit">Number of loaded sections we want in memory</param>
        /// <param name="LoadingSectionLimit">Number of sections we want to be actively loading</param>
        public virtual void FreeExcessSections(int LoadedSectionLimit, int LoadingSectionLimit)
        {
            if (OutstandingSectionQueries.Count > LoadingSectionLimit)
            {
                //Sort the outstanding queries and kill the oldest
                List<GetObjectBySectionCallbackState> stateList = OutstandingSectionQueries.Values.ToList<GetObjectBySectionCallbackState>();
                stateList.Sort(CompareCallbacksByTime);

                int indexOfCutoff = stateList.Count - (LoadingSectionLimit+1);
                if (indexOfCutoff >= 0)
                {
                    for (int iCut = 0; iCut <= indexOfCutoff; iCut++)
                    {
                        GetObjectBySectionCallbackState state = stateList[iCut];
                        bool success = OutstandingSectionQueries.TryRemove(state.SectionNumber, out state);
                        if (success == false)
                            continue;

                        state.Proxy.Abort();
                    }
                }
            }

            //Return if we are under the limit
            if (LastQueryForSection.Count >= LoadedSectionLimit)
            {
                List<DateTime> listQueryTimes = LastQueryForSection.Values.ToList<DateTime>();
                listQueryTimes.Sort();

                int indexOfCutoff = LastQueryForSection.Values.Count - (LoadedSectionLimit+1);
                if (indexOfCutoff >= 0)
                {

                    DateTime CutoffTime = listQueryTimes[indexOfCutoff];

                    foreach (int SectionNumber in LastQueryForSection.Keys)
                    {
                        DateTime lastQuery;
                        bool success = LastQueryForSection.TryGetValue(SectionNumber, out lastQuery);
                        if (!success)
                            continue;

                        //If it was queried after the cutoff time it lives
                        if (lastQuery > CutoffTime)
                            continue;

                        //If it has an outstanding query it lives
                        if (OutstandingSectionQueries.ContainsKey(SectionNumber))
                            continue;

                        //OK, remove the loaded annotations
                        if (RemoveSection(SectionNumber))
                        {
                            LastQueryForSection.TryRemove(SectionNumber, out lastQuery);
                        }

                    }
                }
            }
        }

        /// <summary>
        /// Return true if section was successfully removed
        /// </summary>
        /// <param name="SectionNumber"></param>
        /// <returns></returns>
        public virtual bool RemoveSection(int SectionNumber)
        {
            throw new NotImplementedException(); 
        }

        public virtual bool Save()
        {
            List<OBJECT> changed = new List<OBJECT>(ChangedObjects.Count);

            while (ChangedObjects.Count > 0)
            {
                KeyValuePair<KEY, OBJECT> KeyValue = ChangedObjects.FirstOrDefault();

                OBJECT obj = null;
                bool success = ChangedObjects.TryRemove(KeyValue.Key, out obj);
                if (!success)
                    continue; 
                if (obj.DBAction == DBACTION.NONE)
                    continue;

                changed.Add(obj);
            }

            return Save(changed);
        }


        /// <summary>
        /// Save all changes to locations, returns true if the method completed without errors, otherwise false
        /// This implementation assumes that the user/programmer provides a key which is either unique in the database
        /// or repeatable and that the database does not update the key value on insert.
        /// </summary>
        protected virtual bool Save(List<OBJECT> changedObjects)
        {
            Trace.WriteLine("Saving this number of objects: " + changedObjects.Count, "WebAnnotation");

            /*Don't make the call if there are no changes */
            if (changedObjects.Count == 0)
                return true;

            List<OBJECT> output = new List<OBJECT>(changedObjects.Count);
            List<WCFOBJECT> changedDBObj = new List<WCFOBJECT>(changedObjects.Count);

            try
            {
                foreach (OBJECT dbObj in changedObjects)
                {
                    changedDBObj.Add(dbObj.GetData()); 
                }

                PROXY proxy = CreateProxy();
                proxy.Open();

                long[] newIDs = new long[0];
                try
                {
                    newIDs = ProxyUpdate(proxy, changedDBObj.ToArray());
                }
                catch (Exception e)
                {
                    Trace.WriteLine("An error occurred during the update:\n" + e.Message);
                    return false;
                }
                finally
                {
                    proxy.Close();
                }

                List<KEY> delObjList = new List<KEY>(changedDBObj.Count);

                //Reset DBAction of each object, fire events
                for (int iObj = 0; iObj < changedDBObj.Count; iObj++)
                {
                    WCFOBJECT data = changedDBObj[iObj];
                    DBACTION lastAction = data.DBAction; 
                    data.DBAction = DBACTION.NONE;
                        
                    WCFObjBaseWithKey<KEY, WCFOBJECT> keyObj = data as WCFObjBaseWithKey<KEY, WCFOBJECT>;
                    if (keyObj == null)
                        continue;

                    switch (lastAction)
                    {
                        case DBACTION.INSERT:
                            //keyObj.FireAfterSaveEvent();
                            break; 
                        case DBACTION.UPDATE:
                            //keyObj.FireAfterSaveEvent();
                            break; 
                        case DBACTION.DELETE:
                            //keyObj.FireAfterDeleteEvent();
                            //this.InternalDelete(keyObj.ID); 
                            delObjList.Add(keyObj.ID);
                            break;
                    }
                }

                InternalDelete(delObjList.ToArray()); 
            }
            catch (FaultException )
            {
                //  System.Windows.Forms.MessageBox.Show("An exception occurred while saving structure types.  Viking is pretending none of the changes happened.  Exception Data: " + e.Message, "Error");

                if (changedDBObj != null)
                {
                    //Remove new objects and rescue deleted objects
                    for (int iObj = 0; iObj < changedDBObj.Count; iObj++)
                    {
                        WCFOBJECT data = changedDBObj[iObj];
                        data.DBAction = DBACTION.NONE;

                        if(data.DBAction == DBACTION.INSERT)
                        {
                            WCFObjBaseWithKey<KEY, WCFOBJECT> keyObj = data as WCFObjBaseWithKey<KEY,WCFOBJECT>;
                            if (keyObj == null)
                                continue; 

                            InternalDelete(keyObj.ID); 
                        }
                    }
                }

                //If we caught an exception return false
                return false;
            }
            
            //CallOnAllUpdatesCompleted(new OnAllUpdatesCompletedEventArgs(output.ToArray()));

            return true;
        }

        internal virtual OBJECT[] InternalAdd(OBJECT[] newObjs)
        {
            List<OBJECT> listAddedObj = new List<OBJECT>(newObjs.Length);

            //This list records objects we can't add which must be updated instead
            List<OBJECT> listUpdateObj = new List<OBJECT>(newObjs.Length);

            for (int iObj = 0; iObj < newObjs.Length; iObj++)
            {
                OBJECT newObj = newObjs[iObj];

                bool added = IDToObject.TryAdd(newObj.ID, newObj);
                if (false == added)
                {
                    listUpdateObj.Add(newObj);
                }
                else
                {
                    listAddedObj.Add(newObj);
                    newObj.PropertyChanged += this.OnOBJECTPropertyChangedEventHandler;
                }
            }

            if (listAddedObj.Count > 0)
            {
                OBJECT[] listCopy = new OBJECT[listAddedObj.Count];
                listAddedObj.CopyTo(listCopy);

                CallOnCollectionChanged(new NotifyCollectionChangedEventArgs(NotifyCollectionChangedAction.Add, listCopy));
            }

            if (listUpdateObj.Count > 0)
            {
                OBJECT[] links = InternalUpdate(listUpdateObj.ToArray());
                listAddedObj.AddRange(links);
            }

            return listAddedObj.ToArray();
        }

        internal virtual OBJECT[] InternalUpdate(OBJECT[] updateObjs)
        {
            List<OBJECT> listUpdatedObjs = new List<OBJECT>(updateObjs.Length);
            //List<OBJECT> listOldObjs = new List<OBJECT>(updateObjs.Length);

            for (int iObj = 0; iObj < updateObjs.Length; iObj++)
            {
                OBJECT updateObj = updateObjs[iObj];
                OBJECT existingObj = null;
                bool Success = IDToObject.TryGetValue(updateObj.ID, out existingObj);
                if (Success)
                {

                    //OBJECT oldObj = existingObj.Clone() as OBJECT;
                    //Debug.Assert(oldObj != null);

                    //listOldObjs.Add(oldObj);

                    existingObj.Synch(updateObj.GetData());

                    listUpdatedObjs.Add(existingObj);

                    //if (oldObj != null)
                   // {
                    //    oldObj.Dispose();
                    //    oldObj = null;
                   // }

                    if (existingObj != null)
                    {
                        existingObj.Dispose();
                        existingObj = null; 
                    }

                    if (updateObj != null)
                    {
                        updateObj.Dispose();
                        updateObj = null;
                    }
                }
            }

//            if(listUpdatedObjs.Count > 0)
//                CallOnCollectionChanged(new NotifyCollectionChangedEventArgs(NotifyCollectionChangedAction.Replace, listUpdatedObjs, listOldObjs));

            return listUpdatedObjs.ToArray();
        }

        internal virtual void InternalDelete(KEY[] Keys)
        {
            List<OBJECT> listDeleted = new List<OBJECT>(Keys.Length);

            for (int iObj = 0; iObj < Keys.Length; iObj++)
            {
                KEY Key = Keys[iObj];
                OBJECT existingObj;
                bool success = IDToObject.TryRemove(Key, out existingObj);

                if (success)
                {
                    listDeleted.Add(existingObj);
                    existingObj.PropertyChanged -= this.OnOBJECTPropertyChangedEventHandler;
                    existingObj.Dispose(); 
                }
            }

            if (listDeleted.Count > 0)
            {
                OBJECT[] listCopy = new OBJECT[listDeleted.Count];
                listDeleted.CopyTo(listCopy);

                CallOnCollectionChanged(new NotifyCollectionChangedEventArgs(NotifyCollectionChangedAction.Remove, listCopy));
            }
        }
    }
}
