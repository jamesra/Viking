﻿using AnnotationService.Types;
using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.ServiceModel;
using System.Text;
using WebAnnotationModel.Objects;

namespace WebAnnotationModel
{

    public class GetObjectBySectionCallbackState<PROXY, T> : IEquatable<GetObjectBySectionCallbackState<PROXY, T>>
    {
        public readonly PROXY Proxy;
        public readonly long SectionNumber;
        public readonly DateTime LastQueryExecutedTime;
        public readonly DateTime StartTime = DateTime.UtcNow;
        public readonly Action<ICollection<T>> OnLoadCompletedCallBack;

        public override string ToString()
        {
            return SectionNumber.ToString() + " : " + StartTime.TimeOfDay.ToString();
        }

        public bool Equals(GetObjectBySectionCallbackState<PROXY, T> other)
        {
            if (other is null)
                return false;

            return SectionNumber == other.SectionNumber;
        }

        public GetObjectBySectionCallbackState(PROXY proxy, long number, DateTime lastQueryExecutedTime, Action<ICollection<T>> LoadCompletedCallback)
        {
            this.Proxy = proxy;
            SectionNumber = number;
            this.LastQueryExecutedTime = lastQueryExecutedTime;
            this.OnLoadCompletedCallBack = LoadCompletedCallback;
        }
    }

    /// <summary>
    /// This base class implements the basic functionality to talk to a WCF Service
    /// </summary>
    public abstract class StoreBaseWithKey<PROXY, INTERFACE, KEY, OBJECT, WCFOBJECT> : StoreBase<PROXY, INTERFACE, OBJECT, WCFOBJECT>, ISectionQuery<KEY, OBJECT>
        where INTERFACE : class
        where KEY : struct, IEquatable<KEY>
        where PROXY : System.ServiceModel.ClientBase<INTERFACE>
        where WCFOBJECT : AnnotationService.Types.DataObject, new()
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
        private readonly ConcurrentDictionary<long, DateTime> LastQueryForSection = new ConcurrentDictionary<long, DateTime>();

        /// <summary>
        /// A collection of values indicating which sections have an outstanding query. 
        /// The existence of a key indicates a query is in progress
        /// </summary>
        private readonly ConcurrentDictionary<long, GetObjectBySectionCallbackState<INTERFACE, OBJECT>> OutstandingSectionQueries = new ConcurrentDictionary<long, GetObjectBySectionCallbackState<INTERFACE, OBJECT>>();

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
                    ChangedObjects.TryRemove(obj.ID, out OBJECT removedObj);
                }
                else
                {
                    ChangedObjects.TryAdd(obj.ID, obj);
                }
            }
        }

        #region Proxy Calls

        protected abstract WCFOBJECT ProxyGetByID(INTERFACE proxy, KEY ID);
        protected abstract WCFOBJECT[] ProxyGetByIDs(INTERFACE proxy, KEY[] IDs);

        /// <summary>
        /// Synchronous query for objects on the section
        /// </summary>
        /// <param name="proxy"></param>
        /// <param name="SectionNumber"></param>
        /// <param name="LastQuery"></param>
        /// <returns></returns>
        protected abstract WCFOBJECT[] ProxyGetBySection(INTERFACE proxy,
                                                             long SectionNumber,
                                                             DateTime LastQuery,
                                                             out long TicksAtQueryExecute,
                                                             out KEY[] DeletedLocations);

        /// <summary>
        /// Asynchronous query for objects on the section
        /// </summary>
        /// <param name="proxy"></param>
        /// <param name="SectionNumber"></param>
        /// <param name="LastQuery"></param>
        /// <param name="callback"></param>
        /// <param name="asynchState"></param>
        /// <returns></returns>
        protected abstract IAsyncResult ProxyBeginGetBySection(INTERFACE proxy,
                                                             long SectionNumber,
                                                             DateTime LastQuery,
                                                             AsyncCallback callback,
                                                             object asynchState);


        /// <summary>
        /// Synchronous query for objects on the section
        /// </summary>
        /// <param name="proxy"></param>
        /// <param name="SectionNumber"></param>
        /// <param name="LastQuery"></param>
        /// <returns></returns>
        protected abstract WCFOBJECT[] ProxyGetBySectionRegion(INTERFACE proxy,
                                                             long SectionNumber,
                                                             BoundingRectangle BBox,
                                                             double MinRadius,
                                                             DateTime LastQuery,
                                                             out long TicksAtQueryExecute,
                                                             out KEY[] DeletedLocations);

        /// <summary>
        /// Synchronous query for objects on the section
        /// </summary>
        /// <param name="proxy"></param>
        /// <param name="SectionNumber"></param>
        /// <param name="LastQuery"></param>
        /// <returns></returns>
        protected abstract IAsyncResult ProxyBeginGetBySectionRegion(INTERFACE proxy,
                                                             long SectionNumber,
                                                             BoundingRectangle BBox,
                                                             double MinRadius,
                                                             DateTime LastQuery,
                                                             AsyncCallback callback,
                                                             object asynchState);

        protected abstract WCFOBJECT[] ProxyGetBySectionCallback(out long TicksAtQueryExecute,
                                                                out KEY[] DeletedLocations,
                                                                GetObjectBySectionCallbackState<INTERFACE, OBJECT> state,
                                                                IAsyncResult result);

        protected abstract WCFOBJECT[] ProxyGetBySectionRegionCallback(out long TicksAtQueryExecute,
                                                                out KEY[] DeletedLocations,
                                                                GetObjectBySectionCallbackState<INTERFACE, OBJECT> state,
                                                                IAsyncResult result);


        /// <summary>
        /// Update the server with the new values
        /// </summary>
        /// <param name="proxy"></param>
        /// <param name="objects"></param>
        /// <returns></returns>
        protected abstract KEY[] ProxyUpdate(INTERFACE proxy, WCFOBJECT[] objects);

        #endregion



        /// <summary>
        /// Add an item to the store and send notification events
        /// The item should already exist on the server
        /// 
        /// Each store took a different set of parameters so I removed this, but it belongs here in spirit
        /// </summary>
        /// <param name="obj"></param>
        /// <returns></returns>
        public override OBJECT Add(OBJECT obj)
        {
            //Default implementation
            ChangeInventory<OBJECT> inventory = InternalAdd(obj);
            CallOnCollectionChanged(inventory);
            if (inventory.ObjectsInStore.Count > 0)
            {
                return inventory.ObjectsInStore[0];
            }

            return null;
        }

        public OBJECT GetOrAdd(KEY key, Func<KEY, OBJECT> createFunc, out bool added)
        {
            return this.InternalGetOrAdd(key, createFunc, out added);
        }

        /// <summary>
        /// Add an item to the store and send notification events
        /// The item should already exist on the server
        /// 
        /// Each store took a different set of parameters so I removed this, but it belongs here in spirit
        /// </summary>
        /// <param name="obj"></param>
        /// <returns></returns>
        public override ICollection<OBJECT> Add(ICollection<OBJECT> objs)
        {
            //Default implementation
            ChangeInventory<OBJECT> inventory = InternalAdd(objs.ToArray());
            CallOnCollectionChanged(inventory);
            return inventory.ObjectsInStore;
        }

        public virtual bool Contains(KEY key) => this.IDToObject.ContainsKey(key);

        /// <summary>
        /// Remove the passed object from the store. The item will be
        /// deleted from the server until save is called
        /// </summary>
        /// <param name="obj"></param>
        /// <returns></returns>
        public bool Remove(KEY ID)
        {
            //Default implementation
            if (IDToObject.TryGetValue(ID, out OBJECT obj))
            {
                obj.DBAction = DBACTION.DELETE;
                OBJECT deleted_obj = InternalDelete(obj.ID);
                ChangedObjects.TryAdd(obj.ID, obj);
                CallOnCollectionChangedForDelete(new OBJECT[] { deleted_obj });
                return true;
            }

            return false;
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
            OBJECT deleted_obj = InternalDelete(obj.ID);
            ChangedObjects.TryAdd(obj.ID, obj);
            CallOnCollectionChangedForDelete(new OBJECT[] { deleted_obj });
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
        protected ChangeInventory<OBJECT> InternalAdd(OBJECT newObj)
        {
            ChangeInventory<OBJECT> retVal = InternalAdd(new OBJECT[] { newObj });
            return retVal;
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
        protected OBJECT InternalDelete(KEY ID)
        {
            List<OBJECT> listDeleted = InternalDelete(new KEY[] { ID });
            if (listDeleted.Count == 0)
                return null;

            return listDeleted[0];
        }


        /// <summary>
        /// Replace the object entirely with the new object
        /// </summary>
        /// <param name="ID"></param>
        /// <param name="newObj"></param>
        protected ChangeInventory<OBJECT> InternalReplace(KEY ID, OBJECT newObj)
        {
            return InternalReplace(new KEY[] { ID }, new OBJECT[] { newObj });
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

        public OBJECT this[KEY index]
        {
            get { return IDToObject[index]; }
        }

        /// <summary>
        /// Gets the requested location, first checking locally, then asking the server
        /// </summary>
        /// <param name="ID"></param>
        /// <param name="AskServer">If false only the local cache is checked</param>
        /// <param name="ForceRefreshFromServer">If true we ignore local data and refresh from the server</param>
        /// <returns></returns>
        public OBJECT GetObjectByID(KEY ID, bool AskServer, bool ForceRefreshFromServer = false)
        {
            OBJECT newObj = null;

            if (ForceRefreshFromServer)
                AskServer = true;

            if (!ForceRefreshFromServer)
            {
                bool Success = IDToObject.TryGetValue(ID, out newObj);
                if (Success)
                    return newObj;
            }

            if (!AskServer)
                return null;

            //If not check if the server knows what we're asking for
            WCFOBJECT data = null;
            var proxy = CreateProxy();
            try
            {
                Trace.WriteLine("Going to server to retrieve " + this.ToString() + " parent with ID: " + ID.ToString(), "WebAnnotation");
                var client = (INTERFACE)proxy;
                data = ProxyGetByID(client, ID);
            }
            catch (Exception e)
            {
                Trace.WriteLine(e.ToString(), "WebAnnotation");
                Trace.WriteLine(e.Message, "WebAnnotation");
                data = null;
            }

            proxy?.Close();

            if (data != null)
            {
                newObj = new OBJECT();
                newObj.Synch(data);
                newObj = Add(newObj);
            }

            return newObj;
        }

        /// <summary>
        /// Get objects with the specified ID.  Change notifications are sent for objects fetched from server.
        /// </summary>
        /// <param name="IDs"></param>
        /// <param name="AskServer"></param>
        /// <returns></returns>
        public List<OBJECT> GetObjectsByIDs(ICollection<KEY> IDs, bool AskServer)
        {
            ChangeInventory<OBJECT> inventory = InternalGetObjectsByIDs(IDs, AskServer);

            CallOnCollectionChanged(inventory);

            return inventory.ObjectsInStore;
        }


        /// <summary>
        /// Does not fire collection change events
        /// </summary>
        /// <param name="IDs"></param>
        /// <param name="AskServer"></param>
        /// <returns></returns>
        protected ChangeInventory<OBJECT> InternalGetObjectsByIDs(ICollection<KEY> IDs, bool AskServer)
        {
            //Objects not cached locally

            //Objects we've already fetched
            List<OBJECT> listLocalObjs = GetLocalObjects(IDs, out var listRemoteObjs);

            if (!AskServer || listRemoteObjs.Count == 0)
            {
                ChangeInventory<OBJECT> inventory = new ChangeInventory<OBJECT>(IDs.Count);
                inventory.UnchangedObjects.AddRange(listLocalObjs);
                return inventory;
            }

            //If not check if the server knows what we're asking for
            WCFOBJECT[] listServerObjs = null;
            IClientChannel proxy = null;
            try
            {
                proxy = CreateProxy();
                //Trace.WriteLine("Going to server to retrieve " + this.ToString() + " parent with ID: " + ID.ToString(), "WebAnnotation");

                listServerObjs = ProxyGetByIDs((INTERFACE)proxy, listRemoteObjs.ToArray());
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

            ChangeInventory<OBJECT> server_inventory = ParseQuery(listServerObjs, new KEY[0], null);

            server_inventory.UnchangedObjects.AddRange(listLocalObjs);

            return server_inventory;
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
                bool Success = IDToObject.TryGetValue(ID, out var obj);
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


        public abstract ConcurrentDictionary<KEY, OBJECT> GetLocalObjectsForSection(long SectionNumber);


        public virtual ConcurrentDictionary<KEY, OBJECT> GetObjectsForSection(long SectionNumber)
        {
            GetObjectBySectionCallbackState<INTERFACE, OBJECT> state = new GetObjectBySectionCallbackState<INTERFACE, OBJECT>(null, SectionNumber, GetLastQueryTimeForSection(SectionNumber), null);

            WCFOBJECT[] objects = new WCFOBJECT[0];
            KEY[] deleted_objects = new KEY[0];
            IClientChannel proxy = null;
            DateTime StartTime = DateTime.UtcNow;

            try
            {

                proxy = CreateProxy();
                
                objects = ProxyGetBySection((INTERFACE)proxy,
                                                        SectionNumber,
                                                        state.LastQueryExecutedTime,
                                                        out long QueryExecutedTime,
                                                        out deleted_objects);
            }
            catch (EndpointNotFoundException e)
            {
                Trace.WriteLine("Could not connect to annotation database: " + e.ToString());
            }
            catch (Exception e)
            {
                ShowStandardExceptionMessage(e);
            }
            finally
            {
                if (proxy != null)
                {
                    proxy.Close();
                    proxy = null;
                }
            }

            DateTime TraceQueryEnd = DateTime.UtcNow;

            ChangeInventory<OBJECT> inventory = ParseQuery(objects, deleted_objects, state);

            DateTime TraceParseEnd = DateTime.UtcNow;

#if DEBUG
            TraceQueryDetails(SectionNumber, inventory.ObjectsInStore.Count, StartTime, TraceQueryEnd, TraceParseEnd);
#endif

            //Trace.WriteLine(sb.ToString());

            CallOnCollectionChanged(inventory);

            return GetLocalObjectsForSection(SectionNumber);
        }





        /// <summary>
        /// Get objects appearing on the section asynchronously.  Locally cached objects may be returned first.  Objects
        /// returned remotely can be detected with the OnCollectionChanged notification
        /// </summary>
        /// <param name="SectionNumber"></param>
        /// <returns></returns>
        public virtual MixedLocalAndRemoteQueryResults<KEY, OBJECT> GetObjectsForSectionAsynch(long SectionNumber, Action<ICollection<OBJECT>> OnLoadCompletedCallBack)
        {
            ConcurrentDictionary<KEY, OBJECT> knownObjects = GetLocalObjectsForSection(SectionNumber);

            bool OutstandingRequest = OutstandingSectionQueries.TryGetValue(SectionNumber, out GetObjectBySectionCallbackState<INTERFACE, OBJECT> requestState);
            if (OutstandingRequest)
            {
                return new MixedLocalAndRemoteQueryResults<KEY, OBJECT>(null, knownObjects.Values);
            }

            IClientChannel proxy = null;

            IAsyncResult result = null;
            try
            {
                proxy = CreateProxy(); 

                //                WCFOBJECT[] locations = new WCFOBJECT[0];
                GetObjectBySectionCallbackState<INTERFACE, OBJECT> newState = new GetObjectBySectionCallbackState<INTERFACE, OBJECT>((INTERFACE)proxy, SectionNumber, GetLastQueryTimeForSection(SectionNumber), OnLoadCompletedCallBack);
                bool NoOutstandingRequest = OutstandingSectionQueries.TryAdd(SectionNumber, newState);
                if (NoOutstandingRequest)
                {

                    //Build list of Locations to check
                    result = ProxyBeginGetBySection((INTERFACE)proxy,
                                            SectionNumber,
                                            newState.LastQueryExecutedTime,
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
            finally
            {
                //Do not free the proxy.  The callback function handles that
            }

            return new MixedLocalAndRemoteQueryResults<KEY, OBJECT>(result, knownObjects.Values);
        }

        private RTree.Rectangle BuildRTreeRectangle(long SectionNumber, Geometry.GridRectangle bounds)
        {
            return new RTree.Rectangle(bounds.Left, bounds.Bottom, bounds.Right, bounds.Top, SectionNumber, SectionNumber);
        }



        protected bool IsProxyBroken(IClientChannel proxy)
        {
            return proxy.State == CommunicationState.Closed ||
                   proxy.State == CommunicationState.Closing ||
                   proxy.State == CommunicationState.Faulted;
        }

        private string PrepareQueryDetails(long SectionNumber, long numObjects, DateTime StartTime, DateTime QueryEndTime, DateTime ParseEndTime)
        {
            StringBuilder sb = new StringBuilder();
            sb.AppendFormat("Sxn {0} finished {1} query, {2} returned\n", SectionNumber, typeof(OBJECT), numObjects);
            sb.AppendFormat("\tQuery time: {0} (sec)\n", new TimeSpan(QueryEndTime.Ticks - StartTime.Ticks).TotalSeconds);
            sb.AppendFormat("\tParse time: {0} (sec)\n", new TimeSpan(ParseEndTime.Ticks - QueryEndTime.Ticks).TotalSeconds);

            return sb.ToString();
        }

        private string PrepareQueryDetails(long SectionNumber, long numObjects, DateTime StartTime, DateTime QueryEndTime, DateTime ParseEndTime, DateTime EventsEndTime)
        {
            StringBuilder sb = new StringBuilder(PrepareQueryDetails(SectionNumber, numObjects, StartTime, QueryEndTime, ParseEndTime));
            sb.AppendFormat("\tEvents Time: {0} (sec)\n", new TimeSpan(EventsEndTime.Ticks - ParseEndTime.Ticks).TotalSeconds);
            return sb.ToString();
        }

        protected void TraceQueryDetails(long SectionNumber, long numObjects, DateTime StartTime, DateTime QueryEndTime, DateTime ParseEndTime)
        {
#if DEBUG
            //Trace.WriteLine(PrepareQueryDetails(SectionNumber, numObjects, StartTime, QueryEndTime, ParseEndTime));
#endif
        }

        protected void TraceQueryDetails(long SectionNumber, long numObjects, DateTime StartTime, DateTime QueryEndTime, DateTime ParseEndTime, DateTime EventsEndTime)
        {
#if DEBUG
            //Trace.WriteLine(PrepareQueryDetails(SectionNumber, numObjects, StartTime, QueryEndTime, ParseEndTime, EventsEndTime));
#endif
        }

        protected void GetObjectsBySectionCallback(IAsyncResult result)
        {
            //Remove the entry from outstanding queries so we can query again.  It also prevents the proxy from being aborted if too many 
            //queries are in-flight
            GetObjectBySectionCallbackState<INTERFACE, OBJECT> state = result.AsyncState as GetObjectBySectionCallbackState<INTERFACE, OBJECT>;

            if (!OutstandingSectionQueries.TryRemove(state.SectionNumber, out GetObjectBySectionCallbackState<INTERFACE, OBJECT> unused))
                //We aren't in the outstanding queries collection.  Currently the only reason would be we are about to be aborted
                return;

            INTERFACE client = state.Proxy;

            //This happens if we called abort
            if (IsProxyBroken((IClientChannel)state.Proxy))
                return;

            Debug.Assert(client != null);

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
                if (client is IClientChannel proxy)
                    proxy.Close();
            }

            DateTime TraceQueryEnd = DateTime.Now;

            //Don't update if we've got results from a query executed after this one
            if (TrySetLastQueryTimeForSection(state.SectionNumber, TicksAtQueryExecute, state.LastQueryExecutedTime))
            {
                ChangeInventory<OBJECT> inventory = ParseQuery(objs, DeletedLocations, state);

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

        /// <summary>
        /// This function is called on objects returned from a server call that we wish to add to our local store
        /// </summary>
        /// <param name="serverObjects">Objects which have been added or modified since the last query</param>
        /// <param name="serverDeletedObjects">Objects which have been deleted since the last query</param>
        public virtual ChangeInventory<OBJECT> ParseQuery(WCFOBJECT[] serverObjects, KEY[] serverDeletedObjects, GetObjectBySectionCallbackState<INTERFACE, OBJECT> state)
        {
            if (serverObjects == null)
                return new ChangeInventory<OBJECT>();

            OBJECT[] listObj = new OBJECT[0];
            List<OBJECT> deleted = new List<OBJECT>(serverDeletedObjects.Length);
            if (serverDeletedObjects != null)
            {
                deleted = InternalDelete(serverDeletedObjects);
            }

            OBJECT[] listNewObj = new OBJECT[serverObjects.Length];
            System.Threading.Tasks.Parallel.For(0, serverObjects.Length, (i) =>
            {
                OBJECT newObj = new OBJECT();
                newObj.Synch(serverObjects[i]);
                listNewObj[i] = newObj;
            });

            ChangeInventory<OBJECT> inventory = InternalAdd(listNewObj);
            inventory.DeletedObjects = deleted;
            return inventory;
        }

        #endregion

        public DateTime GetLastQueryTimeForSection(long SectionNumber)
        {
            return LastQueryForSection.GetOrAdd(SectionNumber, DateTime.MinValue);
        }

        private bool TrySetLastQueryTimeForSection(long SectionNumber, long TicksAtQueryExecute, DateTime OldQueryExecuteTime)
        {
            DateTime QueryExecuteTime = new DateTime(TicksAtQueryExecute, DateTimeKind.Utc);
            if (!LastQueryForSection.TryAdd(SectionNumber, QueryExecuteTime))
            {
                return LastQueryForSection.TryUpdate(SectionNumber, QueryExecuteTime, OldQueryExecuteTime);
            }
            return true;

        }

        private static int CompareCallbacksByTime(GetObjectBySectionCallbackState<INTERFACE, OBJECT> x, GetObjectBySectionCallbackState<INTERFACE, OBJECT> y)
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
                List<GetObjectBySectionCallbackState<INTERFACE, OBJECT>> stateList = OutstandingSectionQueries.Values.ToList<GetObjectBySectionCallbackState<INTERFACE, OBJECT>>();
                stateList.Sort(CompareCallbacksByTime);

                int indexOfCutoff = stateList.Count - (LoadingSectionLimit + 1);
                if (indexOfCutoff >= 0)
                {
                    for (int iCut = 0; iCut <= indexOfCutoff; iCut++)
                    {
                        GetObjectBySectionCallbackState<INTERFACE, OBJECT> state = stateList[iCut];
                        bool success = OutstandingSectionQueries.TryRemove(state.SectionNumber, out state);
                        if (success == false)
                            continue;

                        ((IClientChannel)state.Proxy).Abort();
                    }
                }
            }

            //Return if we are under the limit
            if (LastQueryForSection.Count >= LoadedSectionLimit)
            {
                List<DateTime> listQueryTimes = LastQueryForSection.Values.ToList<DateTime>();
                listQueryTimes.Sort();

                int indexOfCutoff = LastQueryForSection.Values.Count - (LoadedSectionLimit + 1);
                if (indexOfCutoff >= 0)
                {

                    DateTime CutoffTime = listQueryTimes[indexOfCutoff];

                    foreach (int SectionNumber in LastQueryForSection.Keys)
                    {
                        bool success = LastQueryForSection.TryGetValue(SectionNumber, out var lastQuery);
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

                bool success = ChangedObjects.TryRemove(KeyValue.Key, out var obj);
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
        /// <exception cref="FaultException"></exception>
        protected virtual bool Save(List<OBJECT> changedObjects)
        {
            Trace.WriteLine("Saving this number of objects: " + changedObjects.Count, "WebAnnotation");

            /*Don't make the call if there are no changes */
            if (changedObjects.Count == 0)
                return true;

            List<WCFOBJECT> changedDBObj = new List<WCFOBJECT>(changedObjects.Count);
            KEY[] keys;

            try
            {
                foreach (OBJECT dbObj in changedObjects)
                {
                    changedDBObj.Add(dbObj.GetData());
                }


                IClientChannel proxy = null;
                try
                {
                    proxy = CreateProxy(); 
                    keys = ProxyUpdate((INTERFACE)proxy, changedDBObj.ToArray());
                }
                catch (Exception e)
                {
                    Trace.WriteLine("An error occurred during the update:\n" + e.Message);
                    return false;
                }
                finally
                {
                    proxy?.Close();
                }

                List<OBJECT> addObjList = new List<OBJECT>(changedDBObj.Count);
                List<OBJECT> updateObjList = new List<OBJECT>(changedDBObj.Count);
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

                    OBJECT obj = IDToObject[keys[iObj]];

                    switch (lastAction)
                    {
                        case DBACTION.INSERT:
                            //keyObj.FireAfterSaveEvent();
                            addObjList.Add(obj);
                            break;
                        case DBACTION.UPDATE:
                            updateObjList.Add(obj);
                            //keyObj.FireAfterSaveEvent();
                            break;
                        case DBACTION.DELETE:
                            //keyObj.FireAfterDeleteEvent();
                            //this.InternalDelete(keyObj.ID); 
                            delObjList.Add(keyObj.ID);
                            break;
                    }
                }

                ChangeInventory<OBJECT> inventory = InternalAdd(addObjList.ToArray());
                inventory.DeletedObjects.AddRange(InternalDelete(delObjList.ToArray()));
                inventory.OldObjectsReplaced = updateObjList;
                inventory.NewObjectReplacements = updateObjList;
                inventory.UpdatedObjects = updateObjList;
                CallOnCollectionChanged(inventory);
            }
            catch (FaultException e)
            {
                //  System.Windows.Forms.MessageBox.Show("An exception occurred while saving structure types.  Viking is pretending none of the changes happened.  Exception Data: " + e.Message, "Error");
                System.Diagnostics.Trace.WriteLine(string.Format("Exception saving: {0}", e));
                if (changedDBObj != null)
                {
                    //Remove new objects and rescue deleted objects
                    for (int iObj = 0; iObj < changedDBObj.Count; iObj++)
                    {
                        WCFOBJECT data = changedDBObj[iObj];
                        data.DBAction = DBACTION.NONE;

                        if (data.DBAction == DBACTION.INSERT)
                        {
                            WCFObjBaseWithKey<KEY, WCFOBJECT> keyObj = data as WCFObjBaseWithKey<KEY, WCFOBJECT>;
                            if (keyObj == null)
                                continue;

                            InternalDelete(keyObj.ID);
                        }
                    }
                }

                //If we caught an exception return false
                throw;
            }

            //CallOnAllUpdatesCompleted(new OnAllUpdatesCompletedEventArgs(output.ToArray()));

            return true;
        }

        /// <summary>
        /// A workaround used when another store has objects for us to add.  In this case we are resposible for firing events and should
        /// replace any passed objects with local copies if they already existed
        /// </summary>
        /// <param name="newObjs"></param>
        /// <returns></returns>
        internal List<OBJECT> AddFromFriend(OBJECT[] newObjs)
        {
            ChangeInventory<OBJECT> inventory = InternalAdd(newObjs);

            CallOnCollectionChanged(inventory);

            return inventory.ObjectsInStore;
        }

        protected virtual ChangeInventory<OBJECT> InternalAdd(OBJECT[] newObjs)
        {
            List<OBJECT> listAddedObj = new List<OBJECT>(newObjs.Length);

            //This list records objects we can't add which must be updated instead
            List<OBJECT> listUpdateObj = new List<OBJECT>(newObjs.Length);

            for (int iObj = 0; iObj < newObjs.Length; iObj++)
            {
                OBJECT newObj = newObjs[iObj];

                bool added = TryAddObject(newObj);
                if (false == added)
                {
                    listUpdateObj.Add(newObj);
                }
                else
                {
                    listAddedObj.Add(newObj);
                }
            }

            ChangeInventory<OBJECT> changeInventory = new ChangeInventory<OBJECT>(newObjs.Length)
            {
                AddedObjects = listAddedObj
            };

            if (listUpdateObj.Count > 0)
            {
                changeInventory.UpdatedObjects.AddRange(InternalUpdate(listUpdateObj.ToArray()));
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

        protected virtual OBJECT[] InternalUpdate(OBJECT[] updateObjs)
        {
            List<OBJECT> listUpdatedObjs = new List<OBJECT>(updateObjs.Length);
            //List<OBJECT> listOldObjs = new List<OBJECT>(updateObjs.Length);

            for (int iObj = 0; iObj < updateObjs.Length; iObj++)
            {
                OBJECT updateObj = updateObjs[iObj];
                bool Success = IDToObject.TryGetValue(updateObj.ID, out var existingObj);
                if (Success)
                {
                    existingObj.Update(updateObj.GetData());

                    listUpdatedObjs.Add(existingObj);
                }
            }

            //            if(listUpdatedObjs.Count > 0)
            //                CallOnCollectionChanged(new NotifyCollectionChangedEventArgs(NotifyCollectionChangedAction.Replace, listUpdatedObjs, listOldObjs));

            return listUpdatedObjs.ToArray();
        }


        /// <summary>
        /// Delete the specified keys.  Return the objects removed from the store.
        /// </summary>
        /// <param name="Keys"></param>
        /// <returns></returns>
        protected virtual List<OBJECT> InternalDelete(KEY[] Keys)
        {
            List<OBJECT> listDeleted = new List<OBJECT>(Keys.Length);

            for (int iObj = 0; iObj < Keys.Length; iObj++)
            {
                KEY Key = Keys[iObj];
                OBJECT removedObj = TryRemoveObject(Key);
                if (removedObj != null)
                {
                    listDeleted.Add(removedObj);
                }
            }

            //CallOnCollectionChangedForDelete(listDeleted);

            return listDeleted;
        }

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
                OBJECT old_object = TryReplaceObject(Key, inserted_object, out var ObjectAdded);
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


        /// <summary>
        /// Add the object to our collection.  Return true if the object was not already in the collection. 
        /// PropertyChanged events should be subscribed to.
        /// </summary>
        /// <param name="newObj"></param>
        /// <returns></returns>
        protected virtual bool TryAddObject(OBJECT newObj)
        {
            bool added = IDToObject.TryAdd(newObj.ID, newObj);
            if (added)
            {
                newObj.PropertyChanged += this.OnOBJECTPropertyChangedEventHandler;
            }

            return added;
        }

        /// <summary>
        /// Delete data for an object from our client and request new data from the server
        /// </summary>
        /// <param name="key"></param>
        /// <returns></returns>
        public virtual OBJECT Refresh(KEY key)
        {
            List<OBJECT> listForgotten = Refresh(new KEY[] { key });
            return listForgotten.First();
        }

        /// <summary>
        /// Delete data for an object from our client and request new data from the server
        /// </summary>
        /// <param name="key"></param>
        /// <returns></returns>
        public virtual List<OBJECT> Refresh(KEY[] keys)
        {

            List<OBJECT> listForgotten = InternalDelete(keys);
            CallOnCollectionChangedForDelete(listForgotten);

            return this.GetObjectsByIDs(keys, true);
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
            if (listForgotten.Count == 0)
                return null;

            return listForgotten.First();
        }

        /// <summary>
        /// Forget everything we know on the client about an object.  This will force a refresh from the
        /// server for the next request.
        /// </summary>
        /// <param name="key"></param>
        /// <returns></returns>
        public virtual List<OBJECT> ForgetLocally(KEY[] keys)
        {

            List<OBJECT> listForgotten = InternalDelete(keys);
            CallOnCollectionChangedForDelete(listForgotten);
            return listForgotten;
        }

        /// <summary>
        /// Remove our local cache for an object.  Delete event subscriptions on the object.
        /// Return object reference if the object was found an removed.
        /// </summary>
        protected virtual OBJECT TryRemoveObject(KEY key)
        {
            bool success = IDToObject.TryRemove(key, out var existingObj);
            if (success)
            {
                existingObj.PropertyChanged -= this.OnOBJECTPropertyChangedEventHandler;
                //existingObj.Dispose(); 
            }
            else
            {
                existingObj = null;
            }

            return existingObj;
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


    }
}
