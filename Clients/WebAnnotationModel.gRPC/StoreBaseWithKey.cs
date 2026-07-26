using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.ComponentModel;
using System.Diagnostics;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Viking.AnnotationServiceTypes.Interfaces;
using WebAnnotationModel.Objects;
using WebAnnotationModel.ServerInterface;

namespace WebAnnotationModel.gRPC
{
    /// <summary>
    /// This base class implements the basic functionality to talk to a WCF Service
    /// </summary>
    public abstract class StoreBaseWithKey<KEY, OBJECT, SERVER_OBJECT, CREATION_DATA_TYPE, CREATION_RESULT> : StoreBase<OBJECT>, 
        IStoreWithKey<KEY, OBJECT>, IStoreEditor<KEY, OBJECT>
        where OBJECT : AnnotationModelObjBaseWithKey<KEY, SERVER_OBJECT>, IEquatable<AnnotationModelObjBaseWithKey<KEY, SERVER_OBJECT>>, 
          IDataObjectWithKey<KEY>, IEquatable<OBJECT>
        where KEY : struct, IEquatable<KEY>, IComparable<KEY>
        where SERVER_OBJECT : IEquatable<SERVER_OBJECT>, IDataObjectWithKey<KEY>
    {
        /// <summary>
        /// Maps IDs to the corresponding object
        /// </summary>
        protected ConcurrentDictionary<KEY, OBJECT> IDToObject = new ConcurrentDictionary<KEY, OBJECT>();
          
        /// <summary>
        /// Objects that have changed which we need to submit on save
        /// </summary>
        protected ConcurrentDictionary<KEY, OBJECT> ChangedObjects = new ConcurrentDictionary<KEY, OBJECT>();

        protected readonly System.ComponentModel.PropertyChangedEventHandler OnOBJECTPropertyChangedEventHandler;

        protected readonly IServerAnnotationsClientFactory<IServerAnnotationsClient<KEY, SERVER_OBJECT, CREATION_DATA_TYPE, CREATION_RESULT>> ClientFactory;

        protected readonly IStoreServerQueryResultsHandler<KEY, OBJECT, SERVER_OBJECT> ServerQueryResultsHandler;

        protected readonly IObjectConverter<SERVER_OBJECT, OBJECT> ServerObjConverter;
        protected readonly IObjectConverter<OBJECT, SERVER_OBJECT> ClientObjConverter;
        protected readonly IQueryLogger QueryLogger;

        protected StoreBaseWithKey(IServerAnnotationsClientFactory<IServerAnnotationsClient<KEY, SERVER_OBJECT, CREATION_DATA_TYPE, CREATION_RESULT>> clientFactory,
                IStoreServerQueryResultsHandler<KEY, OBJECT, SERVER_OBJECT> serverQueryResultsHandler,
                IObjectConverter<OBJECT, SERVER_OBJECT> objToServerObjConverter,
                IObjectConverter<SERVER_OBJECT, OBJECT> serverObjToObjConverter,
                IQueryLogger queryLogger = null)
        {
            ClientFactory = clientFactory;
            ClientObjConverter = objToServerObjConverter;
            ServerObjConverter = serverObjToObjConverter;
            ServerQueryResultsHandler = serverQueryResultsHandler;
            QueryLogger = queryLogger;
            OnOBJECTPropertyChangedEventHandler = new System.ComponentModel.PropertyChangedEventHandler(OnObjectPropertyChanged);
        }

        protected void OnObjectPropertyChanged(object sender, System.ComponentModel.PropertyChangedEventArgs e)
        {
            if (sender is IChangeAction changeObj && sender is OBJECT obj && e.PropertyName == nameof(IChangeAction.DBAction))
            {
                if (changeObj.DBAction == DBACTION.NONE)
                {
                    ChangedObjects.TryRemove(obj.ID, out OBJECT removedObj);
                }
                else
                {
                    ChangedObjects.TryAdd(obj.ID, obj);
                }
            }
        }


        /// <summary>
        /// Add an item to the store and send notification events
        /// The item should already exist on the server
        /// 
        /// Each store took a different set of parameters so I removed this, but it belongs here in spirit
        /// </summary>
        /// <param name="obj"></param>
        /// <returns></returns>
        public override async Task<OBJECT> Add(OBJECT obj)
        {
            //Default implementation
            ChangeInventory<OBJECT> inventory = InternalAdd(new OBJECT[]{obj});
            CallOnCollectionChanged(inventory);
            if (inventory.ObjectsInStore.Count > 0)
            {
                return inventory.ObjectsInStore[0];
            }

            return default;
        } 

        /// <summary>
        /// Add an item to the store and send notification events
        /// The item should already exist on the server
        /// 
        /// Each store took a different set of parameters so I removed this, but it belongs here in spirit
        /// </summary>
        /// <param name="obj"></param>
        /// <returns></returns>
        public override async Task<ICollection<OBJECT>> Add(ICollection<OBJECT> objs)
        {
            //Default implementation
            ChangeInventory<OBJECT> inventory = InternalAdd(objs.ToArray());
            CallOnCollectionChanged(inventory);
            return inventory.ObjectsInStore; 
        }

        public OBJECT GetOrAdd(KEY key, Func<KEY, OBJECT> createFunc, out bool added)
        {
            var result = this.InternalGetOrAdd(key, createFunc, out added);
            if (added)
            {
                CallOnCollectionChangedForAdd(result);
            }

            return result;
        }

        public virtual bool Contains(KEY key)
        {
            return this.IDToObject.ContainsKey(key);
        }

        /// <summary>
        /// Remove the passed object from the store. The item will not be
        /// deleted from the server until save is called
        /// </summary>
        /// <param name="obj"></param>
        /// <returns></returns>
        public async Task<OBJECT> Remove(KEY ID)
        {
            //Default implementation
            if (IDToObject.TryGetValue(ID, out OBJECT obj))
            {
                if(obj is IChangeAction changeObj)
                    changeObj.DBAction = DBACTION.DELETE;

                OBJECT deleted_obj = InternalDelete(obj.ID);
                if (deleted_obj != default)
                {
                    ChangedObjects.TryAdd(obj.ID, obj);
                    CallOnCollectionChangedForDelete(deleted_obj );
                }

                return deleted_obj;
            }

            return default;
        }

        /// <summary>
        /// Remove the passed object from the store. The item will not be
        /// deleted from the server until save is called
        /// </summary>
        /// <param name="obj"></param>
        /// <returns></returns>
        public override async Task<bool> Remove(OBJECT obj)
        {
            //Default implementation
            if (obj is IChangeAction changeObj)
                changeObj.DBAction = DBACTION.DELETE;

            OBJECT deleted_obj = InternalDelete(obj.ID);
            if (deleted_obj != default)
            {
                ChangedObjects.TryAdd(obj.ID, obj);
                CallOnCollectionChangedForDelete( deleted_obj );
                return true;
            }

            return false;
        }
          
        #region Internal Add/Update/Remove methods
         

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
        protected OBJECT InternalDelete(KEY ID)
        {
            List<OBJECT> listDeleted = InternalDelete(new KEY[] { ID });
            if (listDeleted.Count == 0)
                return default;

            return listDeleted[0];
        }

        /// <summary>
        /// Delete the specified keys.  Return the objects removed from the store.
        /// </summary>
        /// <param name="Keys"></param>
        /// <returns>A list of removed objects or null if no object was found</returns>
        protected virtual List<OBJECT> InternalDelete(KEY[] Keys)
        {
            List<OBJECT> listDeleted = new List<OBJECT>(Keys.Length);
            var editor = (IStoreEditor<KEY, OBJECT>)this;

            for (int iObj = 0; iObj < Keys.Length; iObj++)
            {
                KEY Key = Keys[iObj];
                OBJECT removedObj = editor.TryRemoveObject(Key);
                listDeleted.Add(removedObj);
            }

            //CallOnCollectionChangedForDelete(listDeleted);

            return listDeleted;
        }
        /*
        /// <summary>
        /// Replace the object entirely with the new object
        /// </summary>
        /// <param name="ID"></param>
        /// <param name="newObj"></param>
        protected ChangeInventory<OBJECT> InternalReplace(KEY ID, OBJECT newObj)
        {
            return InternalReplace(new KEY[] { ID }, new OBJECT[] { newObj });
        }
        */
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
        public Task<OBJECT> GetObjectByID(KEY ID, CancellationToken token)
        {
            return GetObjectByID(ID, true, false, token);
        }

        /*
        public OBJECT this[KEY index]
        {
            get { return IDToObject[index]; }
        }
        */

        /// <summary>
        /// Gets the requested location, first checking locally, then asking the server
        /// </summary>
        /// <param name="ID"></param>
        /// <param name="AskServer">If false only the local cache is checked</param>
        /// <param name="ForceRefreshFromServer">If true we ignore local data and refresh from the server</param>
        /// <returns></returns>
        public async Task<OBJECT> GetObjectByID(KEY ID, bool AskServer, bool ForceRefreshFromServer, CancellationToken token)
        {
            OBJECT newObj = default;

            if (ForceRefreshFromServer)
                AskServer = true;

            if (!ForceRefreshFromServer)
            {
                bool Success = IDToObject.TryGetValue(ID, out newObj);
                if (Success)
                    return newObj;
            }

            if (!AskServer)
                return default;
             
            //If not check if the server knows what we're asking for
            var client = ClientFactory.GetOrCreate();
            SERVER_OBJECT obj;
            try
            {
                Trace.WriteLine("Going to server to retrieve " + this.ToString() + " parent with ID: " + ID.ToString(), "WebAnnotation");
                obj = await client.GetAsync(ID, token);
            }
            catch (Exception e)
            {
                Trace.WriteLine(e.ToString(), nameof(WebAnnotationModel));
                Trace.WriteLine(e.Message, nameof(WebAnnotationModel));
                obj = default;
            }

            var inventory = await ServerQueryResultsHandler.ProcessServerUpdate(
                new ServerUpdate<KEY, SERVER_OBJECT>(DateTime.UtcNow, obj, Array.Empty<KEY>()));

            return inventory.AddedObjects.Union(inventory.UpdatedObjects).First();
        }
         

        /// <summary>
        /// Get objects with the specified ID.  Change notifications are sent for objects fetched from server.
        /// </summary>
        /// <param name="IDs"></param>
        /// <param name="AskServer"></param>
        /// <returns></returns>
        public async Task<List<OBJECT>> GetObjectsByIDs(ICollection<KEY> IDs, bool AskServer, CancellationToken token)
        {
            ChangeInventory<OBJECT> inventory = await InternalGetObjectsByIDs(IDs, AskServer, token);

            CallOnCollectionChanged(inventory);

            return inventory.ObjectsInStore;
        }


        /// <summary>
        /// Does not fire collection change events
        /// </summary>
        /// <param name="IDs"></param>
        /// <param name="AskServer"></param>
        /// <returns></returns>
        protected async Task<ChangeInventory<OBJECT>> InternalGetObjectsByIDs(ICollection<KEY> IDs, bool AskServer, CancellationToken token)
        {
            //Objects not cached locally

            //Objects we've already fetched
            List<OBJECT> listLocalObjs = GetLocalObjects(IDs, out List<KEY> listRemoteObjs);

            if (!AskServer || listRemoteObjs.Count == 0)
            {
                ChangeInventory<OBJECT> inventory = new ChangeInventory<OBJECT>(IDs.Count);
                inventory.UnchangedObjects.AddRange(listLocalObjs);
                return inventory;
            }

            //If not check if the server knows what we're asking for 
            var client = ClientFactory.GetOrCreate(); 
            IList<SERVER_OBJECT> listServerObjs;
            try
            { 
                //Trace.WriteLine("Going to server to retrieve " + this.ToString() + " parent with ID: " + ID.ToString(), "WebAnnotation");
                listServerObjs = await client.GetAsync(listRemoteObjs.ToArray(), token);
            }
            catch (Exception e)
            {
                Trace.WriteLine(e.ToString(), "WebAnnotation");
                Trace.WriteLine(e.Message, "WebAnnotation");
                listServerObjs = null;
                return new ChangeInventory<OBJECT>();
            }

            var changes = await ServerQueryResultsHandler.ProcessServerUpdate(
                new ServerUpdate<KEY, SERVER_OBJECT[]>(DateTime.UtcNow, listServerObjs.ToArray(), Array.Empty<KEY>()));
            
            changes.UnchangedObjects.AddRange(listLocalObjs);

            return changes;
        }


        /// <summary>
        /// Returns a list of objects that we have locally and a list of objects which are not local
        /// </summary>
        /// <param name="IDs"></param>
        /// <param name="listKeysNotFound"></param>
        /// <returns></returns>
        private List<OBJECT> GetLocalObjects(ICollection<KEY> IDs, out List<KEY> listKeysNotFound)
        {
            List<OBJECT> localObjs = new List<OBJECT>(IDs.Count);
            listKeysNotFound = new List<KEY>(IDs.Count);
            foreach (KEY ID in IDs)
            {
                bool Success = IDToObject.TryGetValue(ID, out OBJECT obj);
                if (Success)
                {
                    localObjs.Add(obj);
                }
                else
                {
                    listKeysNotFound.Add(ID);
                }
            }

            return localObjs;
        }
        
        /*
        Task<OBJECT> IStoreWithKey<KEY, OBJECT>.this[KEY index] =>
            GetObjectByID(index, false, false, CancellationToken.None);
        */
        
        
        /*
        /// <summary>
        /// Get objects appearing on the section asynchronously.  Locally cached objects may be returned first.  Objects
        /// returned remotely can be detected with the OnCollectionChanged notification
        /// </summary>
        /// <param name="SectionNumber"></param>
        /// <returns></returns>
        public virtual async Task<ConcurrentDictionary<KEY, OBJECT>> GetObjectsForSectionAsync(long SectionNumber, Action<ICollection<OBJECT>> OnLoadCompleted)
        {
            GetObjectBySectionCallbackState<OBJECT> state = new GetObjectBySectionCallbackState<OBJECT>(SectionNumber, GetLastQueryTimeForSection(SectionNumber), null); 
            ConcurrentDictionary<KEY, OBJECT> knownObjects = GetLocalObjectsForSection(SectionNumber);

            bool OutstandingRequest = OutstandingSectionQueries.TryGetValue(SectionNumber, out var requestState);
            if (OutstandingRequest)
            {
                //return new MixedLocalAndRemoteQueryResults<KEY, OBJECT>(null, knownObjects.Values);
                return new ConcurrentDictionary<KEY, OBJECT>();
            }
              
            IAsyncResult result = null;
            var client = ClientFactory.GetOrCreateClient();
            try
            {
                if (client is IServerSpatialAnnotations<KEY, SERVER_OBJECT> sectionClient)
                {
                    var results = await sectionClient.GetAsync(SectionNumber,
                        state.LastQueryExecutedTime,
                        out var deletedids,
                        out var queryExecutedTime);

                    var TraceQueryEnd = DateTime.UtcNow;
                    var inventory = ParseQuery(results.ToArray(), deletedids);
                    var TraceParseEnd = DateTime.UtcNow;

                    GetObjectBySectionCallbackState<OBJECT> newState =
                        new GetObjectBySectionCallbackState<OBJECT>(SectionNumber,
                            GetLastQueryTimeForSection(SectionNumber), OnLoadCompleted);
                    bool NoOutstandingRequest = OutstandingSectionQueries.TryAdd(SectionNumber, newState);
                    if (NoOutstandingRequest)
                    {
                        //Build list of Locations to check
                        result = ProxyBeginGetBySection(proxy,
                            SectionNumber,
                            newState.LastQueryExecutedTime,
                            new AsyncCallback(GetObjectsBySectionCallback),
                            newState);
                    }
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
            finally
            {
                //Do not free the proxy.  The callback function handles that
            }

            return new MixedLocalAndRemoteQueryResults<KEY, OBJECT>(result, knownObjects.Values);
        }
          */


        

        /*
        protected void GetObjectsBySectionCallback(IAsyncResult result)
        {
            //Remove the entry from outstanding queries so we can query again.  It also prevents the proxy from being aborted if too many 
            //queries are in-flight
            GetObjectBySectionCallbackState<OBJECT> state = result.AsyncState as GetObjectBySectionCallbackState<OBJECT>;

            if (!OutstandingSectionQueries.TryRemove(state.SectionNumber, out GetObjectBySectionCallbackState<OBJECT> unused))
                //We aren't in the outstanding queries collection.  Currently the only reason would be we are about to be aborted
                return;
             
            PROXY proxy = state.Proxy;

            //This happens if we called abort
            if (IsProxyBroken(state.Proxy))
                return;

            Debug.Assert(proxy != null);

            KEY[] DeletedLocations = new KEY[0];
            long TicksAtQueryExecute = 0;

            WCFOBJECT[] objs;
            try
            {
                objs = ProxyGetBySectionCallback(out TicksAtQueryExecute, out DeletedLocations, state, result);
            }
            catch (TimeoutException)
            {
                Debug.Write("Timeout waiting for server results");
                return;
            }
            catch (EndpointNotFoundException)
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


            //Don't update if we've got results from a query executed after this one
            if (TrySetLastQueryTimeForSection(state.SectionNumber, TicksAtQueryExecute))
            {
                ChangeInventory<OBJECT> inventory = ParseQuery(objs, DeletedLocations);

                CallOnCollectionChanged(inventory);

                DateTime TraceParseEnd = DateTime.Now;
                TraceQueryDetails(state.SectionNumber, objs.Length, state.StartTime, TraceQueryEnd, TraceParseEnd);

                if (state.OnLoadCompletedCallBack != null)
                {
                    if (State.UseAsynchEvents)
                    {
                        System.Threading.Tasks.Task.Run(() => state.OnLoadCompletedCallBack(inventory.ObjectsInStore));
                        //state.OnLoadCompletedCallBack.BeginInvoke(inventory.ObjectsInStore, null, null);
                    }
                    else
                    {
                        state.OnLoadCompletedCallBack.Invoke(inventory.ObjectsInStore);
                    }
                }
            }
            else
                Trace.WriteLine(this.GetType().ToString() + " ignoring stale query results for section: " + state.SectionNumber.ToString(), "WebAnnotation");
        }
        */

        

        /*
        protected void GetObjectsBySectionRegionCallback(IAsyncResult result)
        {
            //Remove the entry from outstanding queries so we can query again.  It also prevents the proxy from being aborted if too many 
            //queries are in-flight
            GetObjectBySectionCallbackState<PROXY, OBJECT> state = result.AsyncState as GetObjectBySectionCallbackState<PROXY, OBJECT>;
            
            PROXY proxy = state.Proxy;

            //This happens if we called abort
            if (IsProxyBroken(state.Proxy))
                return;

            Debug.Assert(proxy != null);

            KEY[] DeletedLocations = new KEY[0];
            long TicksAtQueryExecute = 0;

            WCFOBJECT[] objs;
            try
            {
                objs = ProxyGetBySectionRegionCallback(out TicksAtQueryExecute, out DeletedLocations, state, result);
            }
            catch (TimeoutException)
            {
                Debug.Write("Timeout waiting for server results");
                return;
            }
            catch (EndpointNotFoundException)
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

            //Don't update if we've got results from a query executed after this one 
            ChangeInventory<OBJECT> inventory = ParseQuery(objs, DeletedLocations, state);

            CallOnCollectionChanged(inventory);

            DateTime TraceParseEnd = DateTime.Now;
            TraceQueryDetails(state.SectionNumber, objs.Length, state.StartTime, TraceQueryEnd, TraceParseEnd);

            if(state.OnLoadCompletedCallBack != null)
            {
                if (State.UseAsynchEvents)
                {
                    System.Threading.Tasks.Task.Run(() => state.OnLoadCompletedCallBack(inventory.ObjectsInStore));
                    //state.OnLoadCompletedCallBack.BeginInvoke(inventory.ObjectsInStore, null, null);
                }
                else
                {
                    state.OnLoadCompletedCallBack.Invoke(inventory.ObjectsInStore);
                }
            }
        }
        */

        /*
        /// <summary>
        /// This function is called on objects returned from a server call that we wish to add to our local store.
        /// When the function is done server objects have been inserted, updated or deleted in the store.
        /// Collection events have not been fired
        /// </summary>
        /// <param name="serverObjects">Objects which have been added or modified since the last query</param>
        /// <param name="serverDeletedObjects">Objects which have been deleted since the last query</param>
        public virtual ChangeInventory<OBJECT> ParseQuery(IReadOnlyList<SERVER_OBJECT> serverObjects, KEY[] serverDeletedObjects)
        {
            if (serverObjects == null)
                return new ChangeInventory<OBJECT>();
             
            var deleted = serverDeletedObjects.Length > 0 ? InternalDelete(serverDeletedObjects) : null;

            OBJECT[] listNewObj = new OBJECT[serverObjects.Count];
            System.Threading.Tasks.Parallel.For(0, serverObjects.Count, (i) =>
            {
                var newObj = ServerObjConverter.Convert(serverObjects[i]);
                listNewObj[i] = newObj;
            });

            ChangeInventory<OBJECT> inventory = InternalAdd(listNewObj);
            if(deleted != null)
                inventory.DeletedObjects.AddRange(deleted);

            return inventory;
        }
        */

        #endregion
         
        public virtual async Task<bool> Save(CancellationToken token)
        {
            List<OBJECT> changed = new List<OBJECT>(ChangedObjects.Count);

            while (ChangedObjects.Count > 0)
            {
                KeyValuePair<KEY, OBJECT> KeyValue = ChangedObjects.FirstOrDefault();

                bool success = ChangedObjects.TryRemove(KeyValue.Key, out OBJECT obj);
                if (!success)
                    continue;
                if (obj.DBAction == DBACTION.NONE)
                    continue;

                changed.Add(obj);
            }

            return await Save(changed, token);
        }


        /// <summary>
        /// Save all changes to locations, returns true if the method completed without errors, otherwise false
        /// This implementation assumes that the user/programmer provides a key which is either unique in the database
        /// or repeatable and that the database does not update the key value on insert.
        /// </summary>
        /// <exception cref="FaultException"></exception>
        protected virtual async Task<bool> Save(List<OBJECT> changedObjects, CancellationToken token)
        {
            Trace.WriteLine("Saving this number of objects: " + changedObjects.Count, "WebAnnotation");

            /*Don't make the call if there are no changes */
            if (changedObjects.Count == 0)
                return true;

            List<SERVER_OBJECT> changedDBObj = new List<SERVER_OBJECT>(changedObjects.Count); 

            try
            {
                foreach (OBJECT dbObj in changedObjects)
                {
                    changedDBObj.Add(ClientObjConverter.Convert(dbObj));
                }

                var client = ClientFactory.GetOrCreate();
                UpdateResults<KEY, SERVER_OBJECT> updateResults;
                try
                { 
                    updateResults = await client.UpdateAsync(changedDBObj, token);
                }
                catch (Exception e)
                {
                    Trace.WriteLine($"An error occurred during the update:\n{e.Message}");
                    return false;
                }
                finally
                {
                }

                //var inventory = await ProcessServerObjects(updateResults);
                //CallOnCollectionChanged(inventory);
            }
            catch(Exception e)
            {
                //  System.Windows.Forms.MessageBox.Show("An exception occurred while saving structure types.  Viking is pretending none of the changes happened.  Exception Data: " + e.Message, "Error");
                System.Diagnostics.Trace.WriteLine($"Exception saving: {e}");
                if (changedDBObj != null && changedDBObj.Count > 0)
                {
                    //Remove new objects and //TODO: rescue deleted objects?
                    for (int iObj = 0; iObj < changedObjects.Count; iObj++)
                    {
                        OBJECT data = changedObjects[iObj];

                        if (data.DBAction == DBACTION.INSERT)
                        {  
                            InternalDelete(data.ID);
                        }

                        data.DBAction = DBACTION.NONE;
                    }
                }

                //If we caught an exception return false
                throw;
            }

            //CallOnAllUpdatesCompleted(new OnAllUpdatesCompletedEventArgs(output.ToArray()));

            return true;
        }

        Task IStoreEditor<KEY, OBJECT>.EndBatch(ChangeInventory<OBJECT> inventory)
        {
            return base.CallOnCollectionChanged(inventory);
        }
         

        /// <summary>
        /// Used to populate cache when a call returns from the server. 
        /// These internal add/update/remove functions should not change
        /// the DBAction of the object unless the passed object already 
        /// has those changes
        /// 
        /// These methods should fire collection changed notifications
        /// </summary>
        /// <param name="updateObj"></param>
        /// <returns>True if added, false if updated</returns>
        protected bool InternalAdd(OBJECT newObj)
        {
            return TryAddObject(newObj);
        }

        
        protected virtual ChangeInventory<OBJECT> InternalAdd(OBJECT[] newObjs)
        {
            List<OBJECT> listAddedObj = new List<OBJECT>(newObjs.Length);

            //This list records objects we can't add which must be updated instead
            List<OBJECT> listUpdateObj = new List<OBJECT>(newObjs.Length);

            for (int iObj = 0; iObj < newObjs.Length; iObj++)
            {
                OBJECT newObj = newObjs[iObj];

                if(TryAddObject(newObj))
                {
                    listAddedObj.Add(newObj);
                }
                else
                {
                    listUpdateObj.Add(newObj);
                }
            }

            ChangeInventory<OBJECT> changeInventory = new ChangeInventory<OBJECT>(newObjs.Length);

            changeInventory.AddedObjects.AddRange(listAddedObj);

            if (listUpdateObj.Count > 0)
            {
                //changeInventory.UpdatedObjects.AddRange(InternalUpdate(listUpdateObj.ToArray()));
                throw new NotImplementedException();
            }

            return changeInventory;
        }
        

        protected virtual OBJECT InternalGetOrAdd(KEY key, Func<KEY, OBJECT> createFunc, out bool added)
        {
            bool func_called = false;
            OBJECT value = IDToObject.GetOrAdd(key, obj =>
                {
                    func_called = true;
                    OBJECT new_obj = createFunc(key);
                    new_obj.PropertyChanged += this.OnOBJECTPropertyChangedEventHandler;
                    return new_obj;
                });

            added = func_called;
            return value;
        }
        /*
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
        protected virtual OBJECT[] InternalUpdate(OBJECT[] updateObjs)
        {
            List<OBJECT> listUpdatedObjs = new List<OBJECT>(updateObjs.Length);

            for (int iObj = 0; iObj < updateObjs.Length; iObj++)
            {
                OBJECT updateObj = updateObjs[iObj];
                if (IDToObject.TryGetValue(updateObj.ID, out OBJECT existingObj))
                { 
                    //existingObj.Update(updateObj.GetData());
                    ClientObjUpdater.Update(existingObj, updateObj);

                    listUpdatedObjs.Add(existingObj);
                }
            }

            //            if(listUpdatedObjs.Count > 0)
            //                CallOnCollectionChanged(new NotifyCollectionChangedEventArgs(NotifyCollectionChangedAction.Replace, listUpdatedObjs, listOldObjs));

            return listUpdatedObjs.ToArray();
        }
        */

        /*

        protected virtual ChangeInventory<OBJECT> InternalReplace(KEY[] Keys, OBJECT[] newObjs)
        {
            ChangeInventory<OBJECT> output = new ChangeInventory<OBJECT>(Keys.Length);
            Debug.Assert(Keys.Length == newObjs.Length);
            List<KEY> listReplacedObjects = new List<KEY>(Keys.Length);
            List<OBJECT> listAddedObjects = new List<OBJECT>();
            for (int iObj = 0; iObj < Keys.Length; iObj++)
            {
                KEY Key = Keys[iObj];
                OBJECT inserted_object = newObjs[iObj];
                OBJECT old_object = TryReplaceObject(Key, inserted_object, out bool ObjectAdded);
                if (old_object != null && ObjectAdded)
                {
                    //Everything is OK
                    output.OldObjectsReplaced.Add(old_object);
                    output.NewObjectReplacements.Add(inserted_object);
                }
                else if (ObjectAdded)
                {
                    listAddedObjects.Add(inserted_object);
                }
            }

            return output;

            //CallOnCollectionChangedForReplace(listReplacedObjects, newObjs);
            //CallOnCollectionChangedForAdd(listAddedObjects); 
        }
        */

        /// <summary>
        /// Add the object to our collection.  Return true if the object was not already in the collection. 
        /// PropertyChanged events should be subscribed to.
        /// </summary>
        /// <param name="newObj"></param>
        /// <returns></returns>
        bool IStoreEditor<KEY, OBJECT>.TryAddObject(OBJECT newObj)
        {
            return TryAddObject(newObj);
        }

        protected bool TryAddObject(OBJECT newObj)
        {
            bool added = false;
            IDToObject.GetOrAdd(newObj.ID, (key) =>
            {
                added = true;
                if (newObj is INotifyPropertyChanged notifyObj)
                    notifyObj.PropertyChanged += OnOBJECTPropertyChangedEventHandler;
                return newObj;
            });

            return added;
        }

        /// <summary>
        /// Remove our local cache for an object.  Delete event subscriptions on the object.
        /// Return object reference if the object was found an removed.
        /// </summary> 
        protected virtual OBJECT TryRemoveObject(KEY key)
        {
            bool success = IDToObject.TryRemove(key, out OBJECT existingObj);
            if (success)
            {
                if (existingObj is INotifyPropertyChanged notifyObj)
                    notifyObj.PropertyChanged -= this.OnOBJECTPropertyChangedEventHandler;
                //existingObj.Dispose(); 
            }
            else
            {
                existingObj = default;
            }

            return existingObj;
        }

        /// <summary>
        /// Remove our local cache for an object.  Delete event subscriptions on the object.
        /// Return object reference if the object was found an removed.
        /// </summary> 
        OBJECT IStoreEditor<KEY, OBJECT>.TryRemoveObject(KEY key)
        {
            return TryRemoveObject(key);
        }
         
        bool IStoreEditor<KEY, OBJECT>.TryGetObject(KEY ID, out OBJECT obj)
        {
            return IDToObject.TryGetValue(ID, out obj);
        }

        OBJECT IStoreEditor<KEY, OBJECT>.GetOrAdd(KEY key, Func<KEY, OBJECT> valueFactory)
        {
            return IDToObject.GetOrAdd(key, (k) =>
            {
                var newobj = valueFactory(k);
                if (newobj is INotifyPropertyChanged notifyObj)
                    newobj.PropertyChanged += this.OnOBJECTPropertyChangedEventHandler;
                return newobj;
            });
        }

        /// <summary>
        /// Delete data for an object from our client and request new data from the server
        /// </summary>
        /// <param name="key"></param>
        /// <returns></returns>
        public virtual async Task<OBJECT> Refresh(KEY key, CancellationToken token)
        {
            var listForgotten = await Refresh(new KEY[] { key }, token);
            return listForgotten.FirstOrDefault();
        }

        /// <summary>
        /// Delete data for an object from our client and request new data from the server
        /// </summary>
        /// <param name="key"></param>
        /// <returns></returns>
        public virtual async Task<IList<OBJECT>> Refresh(KEY[] keys, CancellationToken token)
        { 
            ForgetLocally(keys);
            return await GetObjectsByIDs(keys, true, token);
        }

        /// <summary>
        /// Forget everything we know on the client about an object.  This will force a refresh from the
        /// server for the next request.
        /// </summary>
        /// <param name="key"></param>
        /// <returns></returns>
        public virtual OBJECT ForgetLocally(KEY key)
        {
            List<OBJECT> listForgotten = ForgetLocally(new KEY[] { key });
            return listForgotten[0];
        }

        /// <summary>
        /// Forget everything we know on the client about an object.  This will force a refresh from the
        /// server for the next request.
        /// </summary>
        /// <param name="keys"></param>
        /// <returns></returns>
        public virtual List<OBJECT> ForgetLocally(KEY[] keys)
        { 
            List<OBJECT> listForgotten = InternalDelete(keys);
            CallOnCollectionChangedForDelete(listForgotten);
            return listForgotten;
        }
         

        /// <summary>
        /// Replace an existing object with a new object.
        /// </summary>
        /// <param name="key"></param>
        /// <param name="newObj"></param>
        /// /// <param name="ObjectAdded">Return true if the new object was added</param>
        /// <returns></returns>
        protected virtual OBJECT TryReplaceObject(KEY key, OBJECT newObj, out bool ObjectAdded)
        {
            //InternalUpdate(keyObj); 
            //Remove from our old spot in the database 
            OBJECT ExistingObj = TryRemoveObject(key);
            ObjectAdded = TryAddObject(newObj);

            return ExistingObj;
        }

        #region IStoreWithKey
         
        Task<OBJECT> IStoreWithKey<KEY, OBJECT>.GetOrAdd(KEY key, Func<KEY, OBJECT> createFunc, out bool added)
        {
            throw new NotImplementedException();
            var createFuncCalled = false;
            var result =Task.FromResult(IDToObject.GetOrAdd(key, (k) =>
            {
                createFuncCalled = true;
                var newobj = createFunc(k);
                if (newobj is INotifyPropertyChanged notifyObj)
                    newobj.PropertyChanged += this.OnOBJECTPropertyChangedEventHandler;
                return newobj;
            }));
            added = createFuncCalled;
            return result;
        }

        bool IStoreWithKey<KEY, OBJECT>.Contains(KEY key)
        {
            return this.Contains(key);
        }

        Task<OBJECT> IStoreWithKey<KEY, OBJECT>.Remove(KEY key)
        {
            throw new NotImplementedException();
        }

        Task<OBJECT> IStoreWithKey<KEY, OBJECT>.GetObjectByID(KEY ID, CancellationToken token)
        {
            return GetObjectByID(ID, token);
        }

        Task<OBJECT> IStoreWithKey<KEY, OBJECT>.GetObjectByID(KEY ID, bool AskServer, bool ForceRefreshFromServer, CancellationToken token)
        {
            return GetObjectByID(ID, AskServer, ForceRefreshFromServer, token);
        }

        Task<List<OBJECT>> IStoreWithKey<KEY, OBJECT>.GetObjectsByIDs(ICollection<KEY> IDs, bool AskServer, CancellationToken token)
        {
            return GetObjectsByIDs(IDs, AskServer, token);
        }

        Task<OBJECT> IStoreWithKey<KEY, OBJECT>.Refresh(KEY key, CancellationToken token)
        {
            return Refresh(key, token);
        }

        async Task<IList<OBJECT>> IStoreWithKey<KEY, OBJECT>.Refresh(KEY[] keys, CancellationToken token)
        {
            return await Refresh(keys, token);
        }
          
        #endregion
    }
}
