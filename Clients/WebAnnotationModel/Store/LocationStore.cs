using System;
using System.Collections.Generic;
using System.Collections.Concurrent;
using System.Collections.Specialized; 
using System.Linq;
using System.Text;

using System.Diagnostics;
using System.ServiceModel; 

using WebAnnotationModel.Service; 
using WebAnnotationModel.Objects;
using SqlGeometryUtils;
using Geometry;

namespace WebAnnotationModel
{
    public class LocationRTree
    {
        public RTree.RTree<long> SpatialSearch = new RTree.RTree<long>();

        LocationStore Store = null;

        public LocationRTree(LocationStore store)
        {
            this.Store = store;
        }

        public void AddObject(LocationObj obj)
        {
            RTree.Rectangle bbox = obj.MosaicShape.Envelope().ToRTreeRect((float)obj.Z);

            Debug.Assert(!SpatialSearch.Contains(obj.ID));
            SpatialSearch.Add(bbox, obj.ID);
        }

        public void RemoveObject(long key)
        {
            long removedID;
            SpatialSearch.Delete(key, out removedID);
            return;
        }

        public ICollection<LocationObj> Intersects(GridRectangle bbox, float SectionNumber)
        {
            List<long> objIDs = SpatialSearch.Intersects(bbox.ToRTreeRect(SectionNumber));

            return Store.GetObjectsByIDs(objIDs, false);
        }
    }

    public class LocationStore : StoreBaseWithIndexKey<AnnotateLocationsClient, IAnnotateLocations, long, LongIndexGenerator, LocationObj, Location>, IRegionQuery<long, LocationObj>
    {
        /// <summary>
        /// Maps sections to a sorted list of locations on that section.
        /// This collection is not guaranteed to match the ObjectToID collection.  Adding spin-locks to the Add/Remove functions could solve this if it becomes an issue.
        /// </summary>
        System.Collections.Concurrent.ConcurrentDictionary<long, ConcurrentDictionary<long, LocationObj>> SectionToLocations = new ConcurrentDictionary<long, ConcurrentDictionary<long, LocationObj>>();

        public LocationRTree SpatialSearch;

        #region Proxy

        protected override AnnotateLocationsClient CreateProxy()
        {
            AnnotateLocationsClient proxy = new Service.AnnotateLocationsClient("Annotation.Service.Interfaces.IAnnotateLocations-Binary",
                State.EndpointAddress);
            proxy.ClientCredentials.UserName.UserName = State.UserCredentials.UserName;
            proxy.ClientCredentials.UserName.Password = State.UserCredentials.Password;
            return proxy; 
        }

        protected override long[] ProxyUpdate(AnnotateLocationsClient proxy, Location[] objects)
        {
            return proxy.Update(objects); 
        }

        protected override Location ProxyGetByID(AnnotateLocationsClient proxy, long ID)
        {
            return proxy.GetLocationByID(ID); 
        }

        protected override Location[] ProxyGetByIDs(AnnotateLocationsClient proxy, long[] IDs)
        {
            return proxy.GetLocationsByID(IDs);
        }
        
        public override ConcurrentDictionary<long, LocationObj> GetLocalObjectsForSection(long SectionNumber)
        {
            ConcurrentDictionary<long, LocationObj> SectionLocationLinks;
            bool Success = SectionToLocations.TryGetValue(SectionNumber, out SectionLocationLinks);
            if (Success)
            {
                return SectionLocationLinks;
            }

            return new ConcurrentDictionary<long, LocationObj>();
        }

        protected override Location[] ProxyGetBySection(AnnotateLocationsClient proxy, long SectionNumber, DateTime LastQuery, out long TicksAtQueryExecute, out long[] deleted_objs)
        {
            return proxy.GetLocationChanges(out TicksAtQueryExecute, out deleted_objs, SectionNumber, LastQuery.Ticks);
        }

        protected override IAsyncResult ProxyBeginGetBySection(AnnotateLocationsClient proxy,
                                                        long SectionNumber,
                                                        DateTime LastQuery,
                                                        AsyncCallback callback,
                                                        object asynchState)
        {
            return proxy.BeginGetLocationChanges(SectionNumber, LastQuery.Ticks, callback, asynchState);
        }


        protected override Location[] ProxyGetBySectionRegion(AnnotateLocationsClient proxy,
                                                             long SectionNumber,
                                                             BoundingRectangle BBox,
                                                             double MinRadius,
                                                             DateTime LastQuery,
                                                             out long TicksAtQueryExecute,
                                                             out long[] deleted_objs)
        {
            return proxy.GetLocationChangesInMosaicRegion(out TicksAtQueryExecute, out deleted_objs, SectionNumber, BBox, MinRadius, LastQuery.Ticks);
        }

        protected override IAsyncResult ProxyBeginGetBySectionRegion(AnnotateLocationsClient proxy, long SectionNumber, BoundingRectangle BBox, double MinRadius, DateTime LastQuery, AsyncCallback callback, object asynchState)
        {
            return proxy.BeginGetLocationChangesInMosaicRegion(SectionNumber, BBox, MinRadius, LastQuery.Ticks, callback, asynchState);
        }

        protected override Location[] ProxyGetBySectionRegionCallback(out long TicksAtQueryExecute,
                                                                      out long[] DeletedLocations,
                                                                      GetObjectBySectionCallbackState<AnnotateLocationsClient, LocationObj> state,
                                                                      IAsyncResult result)
        {
            return state.Proxy.EndGetLocationChangesInMosaicRegion(out TicksAtQueryExecute, out DeletedLocations, result);
        }

        protected override Location[] ProxyGetBySectionCallback(out long TicksAtQueryExecute,
                                                              out long[] DeletedLocations,
                                                              GetObjectBySectionCallbackState<AnnotateLocationsClient, LocationObj> state,
                                                              IAsyncResult result)
        {
            return state.Proxy.EndGetLocationChanges(out TicksAtQueryExecute, out DeletedLocations, result);
        }


        #endregion



        public LocationStore()
        {
            SpatialSearch = new LocationRTree(this);
        }

        public override void Init()
        {
           
        }


        public LocationObj GetLastModifiedLocation()
        {
            using (AnnotateLocationsClient proxy = CreateProxy())
            {
                Location loc = proxy.GetLastModifiedLocation();
                if (loc != null)
                {
                    LocationObj LastModifiedLoc = new LocationObj(loc);
                    LastModifiedLoc = Add(LastModifiedLoc);
                    return LastModifiedLoc;
                }
            }

            return null; 
        }


        /// <summary>
        /// Create a new location on the server.  Add the location to the local store.
        /// </summary>
        /// <param name="new_location"></param>
        /// <param name="linked_locations"></param>
        /// <returns></returns>
        public LocationObj Create(LocationObj new_location, long[] linked_locations)
        {
            AnnotateLocationsClient proxy = null;
            LocationObj created_location = null; 
            try
            {
                proxy = CreateProxy();
                Location created_db_location = proxy.CreateLocation(new_location.GetData(), linked_locations);
                if (created_db_location == null)
                    return null; 

                created_location = new LocationObj(created_db_location);

                Add(created_location);

                /*
                //Ensure linked locations are updated
                List<LocationLinkObj> listLinks = new List<LocationLinkObj>(linked_locations.Length);
                foreach(long linked_ID in linked_locations)
                {
                    listLinks.Add(new LocationLinkObj(created_location.ID, linked_ID));
                }

                Store.LocationLinks.Add(listLinks); 
                */
                return created_location;
            }
            finally
            {
                if (proxy != null)
                    proxy.Close(); 
            }
        }

        public override bool Remove(LocationObj obj)
        {
            obj.DBAction = DBACTION.DELETE;

            LocationObj deletedObj = InternalDelete(obj.ID);
            CallOnCollectionChangedForDelete(new LocationObj[] { deletedObj }); 

            return true; 
        }

        #region Add/Update/Remove

        /// <summary>
        /// Send a request to load all structure parents in one batch before adding locations
        /// </summary>
        /// <param name="newObjs"></param>
        /// <returns></returns>
        protected override ChangeInventory<LocationObj> InternalAdd(LocationObj[] newObjs)
        {
            long[] MissingParentIDs = newObjs.Where(loc => loc.ParentID.HasValue && Store.Structures.Contains(loc.ParentID.Value) == false).Select(loc => loc.ParentID.Value).Distinct().ToArray();
            if(MissingParentIDs.Length > 0)
                Store.Structures.GetObjectsByIDs(MissingParentIDs, true);

            return base.InternalAdd(newObjs);
        }

        protected ICollection<LocationObj> InternalDelete(LocationObj[] objs)
        {
            long[] IDs = new long[objs.Length];
            for (int i = 0; i < objs.Length; i++)
            {
                IDs[i] = objs[i].ID; 
            }

            return InternalDelete(IDs);
        }
         

        protected override bool TryAddObject(LocationObj newObj)
        {
            bool added = IDToObject.TryAdd(newObj.ID, newObj);
            if (added)
            {
                newObj.PropertyChanged += this.OnOBJECTPropertyChangedEventHandler; 
                TryAddLocationToSection(newObj);
                SpatialSearch.AddObject(newObj);
            }

            return added;
        }

        protected override LocationObj TryRemoveObject(long key)
        {
            LocationObj existingObj;
            bool success = IDToObject.TryRemove(key, out existingObj);
            if (success)
            {
                existingObj.PropertyChanged -= this.OnOBJECTPropertyChangedEventHandler;
                //existingObj.Dispose(); 

                TryRemoveLocationFromSection(existingObj);
                SpatialSearch.RemoveObject(key);
            }
            else
            {
                existingObj = null;
            }

            return existingObj;
        }


        private bool TryAddLocationToSection(LocationObj obj)
        {
            ConcurrentDictionary<long, LocationObj> listSectionLocations;
            listSectionLocations = SectionToLocations.GetOrAdd(obj.Section, (key) => { return new ConcurrentDictionary<long, LocationObj>(); });

            bool Success = listSectionLocations.TryAdd(obj.ID, obj);
            if (!Success)
            {
                //Somebody already added the object to the list sections collection...
                Trace.WriteLine("Race condition in LocationStore.Add", "WebAnnotation");
                Debug.Assert(false);
            }

            return Success;
        }

        private bool TryRemoveLocationFromSection(LocationObj removed_loc)
        {
            //Remove it from the mapping of sections to locations on that section
            ConcurrentDictionary<long, LocationObj> listSectionLocations = null;
            bool Success = SectionToLocations.TryGetValue(removed_loc.Section, out listSectionLocations);
            if (Success)
            {
                LocationObj listSectionLocationsObj = null;
                return listSectionLocations.TryRemove(removed_loc.ID, out listSectionLocationsObj);
            }

            return false; 
        } 

        public ICollection<LocationObj> GetLocationsForStructure(long StructureID)
        {
            Location[] data = null;
            AnnotateLocationsClient proxy = null;
            try
            {
                proxy = CreateProxy();
                proxy.Open();

                data = proxy.GetLocationsForStructure(StructureID);
            }
            catch (Exception e)
            {
                ShowStandardExceptionMessage(e);
                data = null;
            }
            finally
            {
                if (proxy != null)
                    proxy.Close();
            }

            if (null == data)
                return new LocationObj[0];

            List<LocationObj> listLocations = new List<LocationObj>(data.Length);
            foreach (Location loc in data)
            {
                Debug.Assert(loc != null);

                LocationObj newObj = new LocationObj(loc);
                listLocations.Add(newObj);
            }

            ChangeInventory<LocationObj> output = InternalAdd(listLocations.ToArray()); //Add might return an existing object, which we should use instead
            CallOnCollectionChanged(output); 
            //TODO, handle events
            return output.ObjectsInStore;
        }


        #endregion


        /// <summary>
        /// Return true if section was successfully removed
        /// </summary>
        /// <param name="SectionNumber"></param>
        /// <returns></returns>
        public override bool RemoveSection(int SectionNumber)
        {
            //
            ConcurrentDictionary<long, LocationObj> sectionLocations;
            bool success = SectionToLocations.TryRemove(SectionNumber, out sectionLocations);
            if (!success)
                return true;

            ICollection<LocationObj> deleted_list = InternalDelete(sectionLocations.Values.ToArray());

            CallOnCollectionChangedForDelete(deleted_list); 

            sectionLocations.Clear(); 
            return true; 
        }

        public List<LocationObj> GetStructureLocationChangeLog(long structureid)
        {
            AnnotateLocationsClient proxy = null;
            List<LocationObj> listLocations = new List<LocationObj>(); 
            using(proxy = CreateProxy())
            {
                LocationHistory[] history = proxy.GetLocationChangeLog(structureid, new DateTime?(), new DateTime?());

                listLocations.Capacity = history.Length;
                foreach (LocationHistory db_loc in history)
                {
                    listLocations.Add(new LocationObj(db_loc));
                }
            }

            return listLocations;
        }

        public bool Contains(LocationObj o, Geometry.GridRectangle bounds)
        {
            return bounds.Contains(o.Position);
        }


        #region Callbacks

        /*
        private void GetLocationsCallback(IAsyncResult result)
        {
            GetLocationsCallbackState state = result.AsyncState as GetLocationsCallbackState;
            AnnotateLocationsClient proxy = state.Proxy; 
            Debug.Assert(proxy != null);
            long TicksAtQueryExecute = 0; 

            Location[] locations;
            try
            {
                locations = proxy.EndGetLocationsForSection(out TicksAtQueryExecute, result);
            }
            catch (TimeoutException except)
            {
                Debug.Write("Timeout waiting for server results");
                return;
            }
            catch (EndpointNotFoundException except)
            {
                Debug.Write("GetLocationsCallback - Endpoint not found exception");
                return;
            }
            catch (Exception e)
            {
                ShowStandardExceptionMessage(e);
                return; 
            }

            finally
            {
                if(proxy != null)
                    proxy.Close();
            }

            ParseLocationQuery(locations, TicksAtQueryExecute, state);
        }


        private void GetLocationChangesCallback(IAsyncResult result)
        {
            GetLocationsCallbackState state = result.AsyncState as GetLocationsCallbackState;
            AnnotateLocationsClient proxy = state.Proxy;
            Debug.Assert(proxy != null);

            long[] DeletedLocations = new long[0] ;
            long TicksAtQueryExecute = 0; 

            Location[] locations;
            try
            {
                locations = proxy.EndGetLocationChanges(out TicksAtQueryExecute, out DeletedLocations, result);
            }
            catch (TimeoutException except)
            {
                Debug.Write("Timeout waiting for server results");
                return;
            }
            catch (EndpointNotFoundException except)
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

            ParseLocationQuery(locations, TicksAtQueryExecute, state, DeletedLocations);

            bool boolVal;
            //Remove the entry from outstanding queries so we can query again
            OutstandingSectionQueries.TryRemove(state.SectionNumber, out boolVal); 
        }

        */

        #endregion

        public virtual ICollection<LocationObj> GetObjectsInRegion(long SectionNumber, Geometry.GridRectangle bounds, double MinRadius, DateTime? LastQueryUtc)
        {
            GetObjectBySectionCallbackState<AnnotateLocationsClient, LocationObj> state = new GetObjectBySectionCallbackState<AnnotateLocationsClient, LocationObj>(null, SectionNumber, GetLastQueryTimeForSection(SectionNumber), null);

            Location[] objects = new Location[0];
            long QueryExecutedTime;
            long[] deleted_objects = new long[0];
            AnnotateLocationsClient proxy = null;
            DateTime StartTime = DateTime.UtcNow;
            AnnotationSet serverAnnotations = null;
            try
            {
                proxy = CreateProxy();
                proxy.Open();

                serverAnnotations = proxy.GetAnnotationsInMosaicRegion(out QueryExecutedTime, 
                                                                       out deleted_objects,
                                                                       SectionNumber,
                                                                       bounds.ToBoundingRectangle(),
                                                                       MinRadius,
                                                                       LastQueryUtc.HasValue ? LastQueryUtc.Value.Ticks : DateTime.MinValue.Ticks);
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

            ProcessAnnotationSet(serverAnnotations, deleted_objects, StartTime, SectionNumber);
            
            return SpatialSearch.Intersects(bounds, SectionNumber);
        }

        private ChangeInventory<LocationObj> ProcessAnnotationSet(AnnotationSet serverAnnotations, long[] deleted_objects, DateTime? StartTime, long SectionNumber)
        {
            DateTime TraceQueryEnd = DateTime.UtcNow;

            ChangeInventory<StructureObj> structure_inventory = Store.Structures.ParseQuery(serverAnnotations.Structures, new long[] { }, null);
            ChangeInventory<LocationObj> location_inventory = ParseQuery(serverAnnotations.Locations, deleted_objects, null);

            DateTime TraceParseEnd = DateTime.UtcNow;

            Store.Structures.CallOnCollectionChanged(structure_inventory);
            CallOnCollectionChanged(location_inventory);

            if(StartTime.HasValue)
                TraceQueryDetails(SectionNumber, location_inventory.ObjectsInStore.Count, StartTime.Value, TraceQueryEnd, TraceParseEnd, DateTime.UtcNow);

            return location_inventory;
        }

        public virtual MixedLocalAndRemoteQueryResults<long, LocationObj> GetObjectsInRegionAsync(long SectionNumber,
                                                                                           Geometry.GridRectangle bounds,
                                                                                           double MinRadius,
                                                                                           DateTime? LastQueryUtc,
                                                                                           Action<ICollection<LocationObj>> OnLoadCompletedCallBack)
        {
            AnnotateLocationsClient proxy = null;

            IAsyncResult result = null;
            try
            {
                proxy = CreateProxy();
                proxy.Open();

                //                WCFOBJECT[] locations = new WCFOBJECT[0];
                GetObjectBySectionCallbackState<AnnotateLocationsClient, LocationObj> newState = new GetObjectBySectionCallbackState<AnnotateLocationsClient, LocationObj>(proxy, SectionNumber, LastQueryUtc.HasValue ? LastQueryUtc.Value : DateTime.MinValue, OnLoadCompletedCallBack);

                //Build list of Locations to check
                proxy.BeginGetAnnotationsInMosaicRegion(SectionNumber,
                                        bounds.ToBoundingRectangle(),
                                        MinRadius,
                                        newState.LastQueryExecutedTime.Ticks,
                                        new AsyncCallback(GetObjectsBySectionRegionCallback),
                                        newState);

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

            return new MixedLocalAndRemoteQueryResults<long, LocationObj>(result, SpatialSearch.Intersects(bounds, SectionNumber));
        }

        protected void GetObjectsBySectionRegionCallback(IAsyncResult result)
        {
            //Remove the entry from outstanding queries so we can query again.  It also prevents the proxy from being aborted if too many 
            //queries are in-flight
            GetObjectBySectionCallbackState<AnnotateLocationsClient, LocationObj> state = result.AsyncState as GetObjectBySectionCallbackState<AnnotateLocationsClient, LocationObj>;

            AnnotateLocationsClient proxy = state.Proxy;

            //This happens if we called abort
            if (IsProxyBroken(state.Proxy))
                return;

            Debug.Assert(proxy != null);

            long[] DeletedLocations = new long[0];
            long TicksAtQueryExecute = 0;

            AnnotationSet serverAnnotations = null;
            try
            {
                serverAnnotations = proxy.EndGetAnnotationsInMosaicRegion(out TicksAtQueryExecute, out DeletedLocations, result);
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

            ChangeInventory<LocationObj> location_inventory = ProcessAnnotationSet(serverAnnotations, DeletedLocations, state.StartTime, state.SectionNumber);

            if (state.OnLoadCompletedCallBack != null)
            {
                if (State.UseAsynchEvents)
                {
                    System.Threading.Tasks.Task.Run(() => state.OnLoadCompletedCallBack(location_inventory.ObjectsInStore));
                    //state.OnLoadCompletedCallBack.BeginInvoke(inventory.ObjectsInStore, null, null);
                }
                else
                {
                    state.OnLoadCompletedCallBack.Invoke(location_inventory.ObjectsInStore);
                }
            }
        }

        public ICollection<LocationObj> GetLocalObjectsInRegion(long SectionNumber, GridRectangle bounds, double MinRadius)
        {
            return SpatialSearch.Intersects(bounds, SectionNumber).Where(l => l.Radius >= MinRadius).ToList();
        }
    }
}
