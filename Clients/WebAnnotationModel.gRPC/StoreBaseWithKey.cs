using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.ComponentModel;
using System.Diagnostics;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Viking.AnnotationServiceTypes.Interfaces;
using WebAnnotationModel;
using WebAnnotationModel.Objects;
using WebAnnotationModel.ServerInterface;

namespace WebAnnotationModel.gRPC
{
    /// <summary>
    /// Keyed gRPC store. Add paths do not share the same post-add hooks; see InternalAdd,
    /// GetOrAdd, and IStoreEditor.GetOrAdd.
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

        /// <summary>Applies server payloads to IDToObject. Does not raise CollectionChanged.</summary>
        protected readonly IStoreServerQueryResultsHandler<KEY, OBJECT, SERVER_OBJECT> ServerQueryResultsHandler;

        /// <summary>Server proto/interface → client object.</summary>
        protected readonly IObjectConverter<SERVER_OBJECT, OBJECT> ServerObjConverter;

        /// <summary>Client object → server proto/interface for Create/Update.</summary>
        protected readonly IObjectConverter<OBJECT, SERVER_OBJECT> ClientObjConverter;
        /// <summary>Optional timing log for server queries. Null is allowed.</summary>
        protected readonly IQueryLogger QueryLogger;

        protected StoreBaseWithKey(IServerAnnotationsClientFactory<IServerAnnotationsClient<KEY, SERVER_OBJECT, CREATION_DATA_TYPE, CREATION_RESULT>> clientFactory,
                IStoreServerQueryResultsHandler<KEY, OBJECT, SERVER_OBJECT> serverQueryResultsHandler,
                IObjectConverter<OBJECT, SERVER_OBJECT> objToServerObjConverter,
                IObjectConverter<SERVER_OBJECT, OBJECT> serverObjToObjConverter,
                IQueryLogger queryLogger = null,
                IObjectUpdater<OBJECT, SERVER_OBJECT> objUpdater = null)
        {
            ClientFactory = clientFactory;
            ClientObjConverter = objToServerObjConverter;
            ServerObjConverter = serverObjToObjConverter;
            // When DI cannot supply a handler (circular store↔handler dependency), build one against this store.
            ServerQueryResultsHandler = serverQueryResultsHandler
                ?? new StoreServerQueryResultsHandler<KEY, OBJECT, SERVER_OBJECT>(this, serverObjToObjConverter, objUpdater);
            QueryLogger = queryLogger;
            OnOBJECTPropertyChangedEventHandler = new System.ComponentModel.PropertyChangedEventHandler(OnObjectPropertyChanged);
        }

        protected virtual void OnObjectPropertyChanged(object sender, System.ComponentModel.PropertyChangedEventArgs e)
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
                var added = inventory.ObjectsInStore[0];
                TrackForSaveIfPending(added);
                return added;
            }

            return default;
        } 

        /// <summary>
        /// Add() subscribes to PropertyChanged after the object already exists, so a DBAction assigned
        /// during construction (e.g. DBACTION.INSERT) is never observed by OnObjectPropertyChanged.
        /// Queue it for Save() explicitly here instead.
        /// </summary>
        private void TrackForSaveIfPending(OBJECT obj)
        {
            if (obj is IChangeAction changeObj && changeObj.DBAction != DBACTION.NONE)
                ChangedObjects.TryAdd(obj.ID, obj);
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
            foreach (var added in inventory.ObjectsInStore)
                TrackForSaveIfPending(added);
            return inventory.ObjectsInStore; 
        }

        /// <summary>
        /// Insert-or-get for UI/create callers. Fires CollectionChanged immediately when added.
        /// Server ingest should use IStoreEditor.GetOrAdd and batch events via CallOnCollectionChanged.
        /// </summary>
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

        public OBJECT this[KEY key]
        {
            get
            {
                if (IDToObject.TryGetValue(key, out OBJECT obj))
                    return obj;
                throw new KeyNotFoundException(
                    $"{typeof(OBJECT).Name} with key {key} is not in the local cache. Use TryGetObjectByID when a miss is possible, or GetObjectByID to fetch from the server.");
            }
        }

        public bool TryGetObjectByID(KEY key, out OBJECT obj) => IDToObject.TryGetValue(key, out obj);

        public bool TryGetObjectsByIDs(ICollection<KEY> keys, out IReadOnlyList<OBJECT> found, out IReadOnlyList<KEY> missing)
        {
            if (keys == null || keys.Count == 0)
            {
                found = Array.Empty<OBJECT>();
                missing = Array.Empty<KEY>();
                return true;
            }

            List<OBJECT> localObjs = GetLocalObjects(keys, out List<KEY> notFound);
            found = localObjs;
            missing = notFound;
            return notFound.Count == 0;
        }

        public Task<OBJECT> GetObjectByID(KEY ID, CancellationToken token = default)
        {
            if (IDToObject.TryGetValue(ID, out OBJECT cached))
                return Task.FromResult(cached);

            return FetchObjectByID(ID, token);
        }

        private async Task<OBJECT> FetchObjectByID(KEY ID, CancellationToken token)
        {
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

            if (obj == null)
                return default;

            var queryTime = DateTime.UtcNow;
            await ServerQueryResultsHandler.ProcessServerUpdate(
                new ServerUpdate<KEY, SERVER_OBJECT>(queryTime, obj, Array.Empty<KEY>()));
            await OnServerObjectsLoaded(new[] { obj }, queryTime);

            return IDToObject.TryGetValue(ID, out OBJECT newObj) ? newObj : default;
        }

        /// <summary>
        /// Hook for stores that need to hydrate related collections from embedded server payloads
        /// (e.g. Structure.Links → StructureLinkStore).
        /// </summary>
        protected virtual Task OnServerObjectsLoaded(IEnumerable<SERVER_OBJECT> objs, DateTime queryTime) =>
            Task.CompletedTask;
         

        public async Task<GetByIDResult<KEY, OBJECT>> GetObjectsByIDs(ICollection<KEY> IDs, CancellationToken token = default)
        {
            if (IDs == null || IDs.Count == 0)
                return GetByIDResult<KEY, OBJECT>.Empty;

            TryGetObjectsByIDs(IDs, out IReadOnlyList<OBJECT> found, out IReadOnlyList<KEY> missing);
            if (missing.Count == 0)
                return new GetByIDResult<KEY, OBJECT>(found, missing);

            ChangeInventory<OBJECT> inventory = await InternalGetObjectsByIDs(missing as ICollection<KEY> ?? missing.ToList(), token);
            await CallOnCollectionChanged(inventory);

            TryGetObjectsByIDs(IDs, out found, out missing);
            return new GetByIDResult<KEY, OBJECT>(found, missing);
        }

        /// <summary>
        /// Fetches the given keys from the server and merges them. Does not fire collection change events.
        /// </summary>
        protected async Task<ChangeInventory<OBJECT>> InternalGetObjectsByIDs(ICollection<KEY> IDs, CancellationToken token)
        {
            List<OBJECT> listLocalObjs = GetLocalObjects(IDs, out List<KEY> listRemoteObjs);

            if (listRemoteObjs.Count == 0)
            {
                ChangeInventory<OBJECT> inventory = new ChangeInventory<OBJECT>(IDs.Count);
                inventory.UnchangedObjects.AddRange(listLocalObjs);
                return inventory;
            }

            var client = ClientFactory.GetOrCreate();
            IList<SERVER_OBJECT> listServerObjs;
            try
            {
                listServerObjs = await client.GetAsync(listRemoteObjs.ToArray(), token);
            }
            catch (Exception e)
            {
                Trace.WriteLine(e.ToString(), "WebAnnotation");
                Trace.WriteLine(e.Message, "WebAnnotation");
                ChangeInventory<OBJECT> failed = new ChangeInventory<OBJECT>(IDs.Count);
                failed.UnchangedObjects.AddRange(listLocalObjs);
                return failed;
            }

            var queryTime = DateTime.UtcNow;
            var serverArray = listServerObjs?.ToArray() ?? Array.Empty<SERVER_OBJECT>();
            KEY[] deletedIds = listRemoteObjs
                .Where(id => serverArray.All(s => !s.ID.Equals(id)))
                .ToArray();
            var changes = await ServerQueryResultsHandler.ProcessServerUpdate(
                new ServerUpdate<KEY, SERVER_OBJECT[]>(queryTime, serverArray, deletedIds));

            await OnServerObjectsLoaded(serverArray, queryTime);

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
         
        public override async Task<bool> Save(CancellationToken token)
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

        /// <summary>
        /// Fires collection events for a handler batch. Calls StoreBase.CallOnCollectionChanged
        /// (not the virtual override), so StoreBaseWithKeyAndParent.WireParentsAndRoots does not run.
        /// StructureTypeStore.GetAll uses this.CallOnCollectionChanged instead for that reason.
        /// </summary>
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

        
        /// <summary>
        /// Cache insert used by Store.Add. Server GetAll / region queries do not call this;
        /// they use IStoreEditor.GetOrAdd. Do not put parent/root side effects only here.
        /// </summary>
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
                // Already cached — keep the existing instance (server payload already applied via GetOrAdd/Sync elsewhere).
                foreach (var updateObj in listUpdateObj)
                {
                    if (IDToObject.TryGetValue(updateObj.ID, out var existing))
                        changeInventory.UnchangedObjects.Add(existing);
                }
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

        /// <summary>
        /// Server-ingest insert. Puts the object in IDToObject and subscribes PropertyChanged.
        /// Does not fire CollectionChanged and does not update RootObjects or Parent.Children.
        /// </summary>
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
        public virtual async Task<OBJECT> Refresh(KEY key, CancellationToken token = default)
        {
            var result = await Refresh(new[] { key }, token);
            return result.Found.Count > 0 ? result.Found[0] : default;
        }

        public virtual async Task<GetByIDResult<KEY, OBJECT>> Refresh(ICollection<KEY> keys, CancellationToken token = default)
        {
            if (keys == null || keys.Count == 0)
                return GetByIDResult<KEY, OBJECT>.Empty;

            KEY[] keyArray = keys as KEY[] ?? keys.ToArray();
            ForgetLocally(keyArray);
            return await GetObjectsByIDs(keys, token);
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
            return Task.FromResult(GetOrAdd(key, createFunc, out added));
        }

        bool IStoreWithKey<KEY, OBJECT>.Contains(KEY key)
        {
            return this.Contains(key);
        }

        Task<OBJECT> IStoreWithKey<KEY, OBJECT>.Remove(KEY key)
        {
            return Remove(key);
        }

        Task<OBJECT> IStoreWithKey<KEY, OBJECT>.GetObjectByID(KEY ID, CancellationToken token)
        {
            return GetObjectByID(ID, token);
        }

        Task<GetByIDResult<KEY, OBJECT>> IStoreWithKey<KEY, OBJECT>.GetObjectsByIDs(ICollection<KEY> IDs, CancellationToken token)
        {
            return GetObjectsByIDs(IDs, token);
        }

        Task<OBJECT> IStoreWithKey<KEY, OBJECT>.Refresh(KEY key, CancellationToken token)
        {
            return Refresh(key, token);
        }

        Task<GetByIDResult<KEY, OBJECT>> IStoreWithKey<KEY, OBJECT>.Refresh(ICollection<KEY> keys, CancellationToken token)
        {
            return Refresh(keys, token);
        }
          
        #endregion
    }
}
