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

namespace WebAnnotationModel
{
    public class LocationStore : StoreBaseWithIndexKey<AnnotateLocationsClient, IAnnotateLocations, long, LongIndexGenerator, LocationObj, Location>
    {
        /// <summary>
        /// Maps sections to a sorted list of locations on that section.
        /// This collection is not guaranteed to match the ObjectToID collection.  Adding spin-locks to the Add/Remove functions could solve this if it becomes an issue.
        /// </summary>
        System.Collections.Concurrent.ConcurrentDictionary<long, ConcurrentDictionary<long, LocationObj>> SectionToLocations = new ConcurrentDictionary<long, ConcurrentDictionary<long, LocationObj>>();
        
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
            return proxy.GetLocationChangesInRegion(out TicksAtQueryExecute, out deleted_objs, SectionNumber, BBox, MinRadius, LastQuery.Ticks);
        }

        protected override IAsyncResult ProxyBeginGetBySectionRegion(AnnotateLocationsClient proxy, long SectionNumber, BoundingRectangle BBox, double MinRadius, DateTime LastQuery, AsyncCallback callback, object asynchState)
        {
            return proxy.BeginGetLocationChangesInRegion(SectionNumber, BBox, MinRadius, LastQuery.Ticks, callback, asynchState);
        }

        protected override Location[] ProxyGetBySectionRegionCallback(out long TicksAtQueryExecute, out long[] DeletedLocations, GetObjectBySectionCallbackState state, IAsyncResult result)
        {
            return state.Proxy.EndGetLocationChangesInRegion(out TicksAtQueryExecute, out DeletedLocations, result);
        }

        protected override Location[] ProxyGetBySectionCallback(out long TicksAtQueryExecute,
                                                          out long[] DeletedLocations,
                                                          GetObjectBySectionCallbackState state,
                                                          IAsyncResult result)
        {
            return state.Proxy.EndGetLocationChanges(out TicksAtQueryExecute, out DeletedLocations, result);
        }


        #endregion



        public LocationStore()
        {
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
    }
}
