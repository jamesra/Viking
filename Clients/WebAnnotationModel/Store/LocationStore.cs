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
                    LastModifiedLoc  = InternalAdd(LastModifiedLoc);
                    return LastModifiedLoc;
                }
            }

            return null; 
        }

        public  LocationObj Create(Structure s)
        {
            Location loc = new Location();
            loc.ID = GetTempKey();
            loc.ParentID = s.ID; 
            
            LocationObj newObj = new LocationObj(loc);

            newObj = InternalAdd(newObj); 

            return newObj; 
        }

        public override bool Remove(LocationObj obj)
        {
            obj.DBAction = DBACTION.DELETE;

            InternalDelete(obj.ID);

            return true; 
        }

        #region Add/Update/Remove

        /*
        internal override LocationObj[] InternalUpdate(LocationObj[] updateObjs)
        {
            List<LocationObj> updatedObjs = new List<LocationObj>(updateObjs.Length);
            List<LocationObj> oldObjs = new List<LocationObj>(updateObjs.Length);

            for (int iObj = 0; iObj < updateObjs.Length; iObj++)
            {
                LocationObj updateObj = updateObjs[iObj]; 
                LocationObj existingObj; 
                bool success =  IDToObject.TryGetValue(updateObj.ID, out existingObj);

                //Update if the new DB object has a later modified date. 
                if (success)
                {
                    if (updateObj.GetData().LastModified >= existingObj.GetData().LastModified)
                    {

                        LocationObj oldLoc = new LocationObj(existingObj.GetData());
                        existingObj.Synch(updateObj.GetData());

                        //Record which objects have changed
                        updatedObjs.Add(existingObj);
                        oldObjs.Add(oldLoc); 
                    }
                }
            }

            if (updatedObjs.Count > 0)
            {
                NotifyCollectionChangedEventArgs args = new NotifyCollectionChangedEventArgs(NotifyCollectionChangedAction.Replace, updatedObjs, oldObjs);
                CallOnCollectionChanged(args);
            }

            return updatedObjs.ToArray(); 
        }
        */

        internal override LocationObj[] InternalAdd(LocationObj[] addObjs)
        {
            List<LocationObj> listAddedObj = new List<LocationObj>(addObjs.Length);

            //This list records objects we can't add which must be updated instead
            List<LocationObj> listUpdateObj = new List<LocationObj>(addObjs.Length); 

            for (int iObj = 0; iObj < addObjs.Length; iObj++)
            {
                LocationObj obj = addObjs[iObj];
                Debug.Assert(obj != null); 

                if (IDToObject.TryAdd(obj.ID, obj))
                {
                    ConcurrentDictionary<long, LocationObj> listSectionLocations;

                    listSectionLocations = SectionToLocations.GetOrAdd(obj.Section, (key) => {return new ConcurrentDictionary<long,LocationObj>();});

                    bool Success = listSectionLocations.TryAdd(obj.ID, obj);
                    if (!Success)
                    {
                        //Somebody already added the object to the list sections collection...
                        Trace.WriteLine("Race condition in LocationStore.Add", "WebAnnotation");
                        Debug.Assert(false);
                    }
                    

                    listAddedObj.Add(obj);
                    obj.PropertyChanged += this.OnOBJECTPropertyChangedEventHandler;
                }
                else
                {
                    listUpdateObj.Add(obj);
                }
            }

            //InternalUpdate will send its own notification for the updated objects
            CallOnCollectionChangedForAdd(listAddedObj);

            if (listUpdateObj.Count > 0)
            {
                LocationObj[] updatedObjs = InternalUpdate(listUpdateObj.ToArray());

                //Add the updated objects to our output array
                listAddedObj.AddRange(updatedObjs); 
            }

            return listAddedObj.ToArray(); 
        }

        protected void InternalDelete(LocationObj[] objs)
        {
            long[] IDs = new long[objs.Length];
            for (int i = 0; i < objs.Length; i++)
            {
                IDs[i] = objs[i].ID; 
            }

            InternalDelete(IDs);
        }

        internal override void InternalDelete(long[] IDs)
        {

            List<LocationObj> listDeleted = new List<LocationObj>(IDs.Length); 

            for (int iObj = 0; iObj < IDs.Length; iObj++)
            {
                long ID = IDs[iObj];
                LocationObj loc = null;
                
                bool Success = IDToObject.TryRemove(ID, out loc);
                
                if(Success)              
                {
                    listDeleted.Add(loc);
                    loc.PropertyChanged -= this.OnOBJECTPropertyChangedEventHandler;

                    //Remove it from the mapping of sections to locations on that section
                    ConcurrentDictionary<long, LocationObj> listSectionLocations = null;
                    Success = SectionToLocations.TryGetValue(loc.Section, out listSectionLocations);
                    if (Success)
                    {
                        LocationObj listSectionLocationsObj = null;
                        listSectionLocations.TryRemove(ID, out listSectionLocationsObj);
                    }
                }

            }

            //Let consumers know the key went away
            //We used to do this before removing from the collection before using collections.Concurrent
            if (listDeleted.Count > 0)
            {
                LocationObj[] listCopy = new LocationObj[listDeleted.Count];
                listDeleted.CopyTo(listCopy);
                CallOnCollectionChanged(new NotifyCollectionChangedEventArgs(NotifyCollectionChangedAction.Remove, listCopy));
            }
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

            InternalDelete(sectionLocations.Values.ToArray());

            sectionLocations.Clear(); 
            return true; 
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
