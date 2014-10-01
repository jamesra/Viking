using System;
using System.Collections.Generic; 
using System.Collections.Concurrent;
using System.Linq;
using System.Text;

using System.Diagnostics;
using System.ServiceModel; 

using WebAnnotation.Service; 
using WebAnnotation.Objects;

namespace WebAnnotation
{
    internal class LocationStore : StoreBase<AnnotateLocationsClient, IAnnotateLocations, LocationObj, Location>
    {
        /// <summary>
        /// When we query the database for locations on a section we store the query time for the section
        /// That way on the next query we only need to store the updates.
        /// </summary>
        private ConcurrentDictionary<long, DateTime> LastQueryForSection = new ConcurrentDictionary<long, DateTime>();

        /// <summary>
        /// A collection of values indicating which sections have an outstanding query. 
        /// The existence of a key indicates a query is in progress
        /// </summary>
        private ConcurrentDictionary<long, bool> OutstandingSectionQueries = new ConcurrentDictionary<long, bool>();

        

        #region Proxy

        protected override AnnotateLocationsClient CreateProxy()
        {
            AnnotateLocationsClient proxy = new Service.AnnotateLocationsClient("Annotation.Service.Interfaces.IAnnotateLocations-Binary",
                Global.EndpointAddress);
            proxy.ClientCredentials.UserName.UserName = Viking.UI.State.UserCredentials.UserName;
            proxy.ClientCredentials.UserName.Password = Viking.UI.State.UserCredentials.Password;
            return proxy; 
        }

        internal override long[] ProxyUpdate(AnnotateLocationsClient proxy, Location[] objects)
        {
            return proxy.Update(objects); 
        }

        internal override Location ProxyGetByID(AnnotateLocationsClient proxy, long ID)
        {
            return proxy.GetLocationByID(ID); 
        }

        #endregion
        
        /// <summary>
        /// Maps sections to a sorted list of locations on that section.
        /// This collection is not guaranteed to match the ObjectToID collection.  Adding spin-locks to the Add/Remove functions could solve this if it becomes an issue.
        /// </summary>
        System.Collections.Concurrent.ConcurrentDictionary<long, ConcurrentDictionary<long, LocationObj>> SectionToLocations = new ConcurrentDictionary<long, ConcurrentDictionary<long, LocationObj>>();

        private class GetLocationsCallbackState
        {
            public readonly AnnotateLocationsClient Proxy;
            public readonly long SectionNumber;

            public GetLocationsCallbackState(AnnotateLocationsClient proxy, long number)
            {
                this.Proxy = proxy; 
                SectionNumber = number;
            }
        }

        public LocationStore()
        {
        }

        public override void Init()
        {
           
        }

        public ConcurrentDictionary<long, LocationObj> GetLocationsForSection(long SectionNumber)
        {
            if (LastQueryForSection.ContainsKey(SectionNumber))
            {
                ConcurrentDictionary<long, LocationObj> listLocations = new ConcurrentDictionary<long, LocationObj>();

                //Return the objects we have and then send a request for updates
                //lock (LockObject)
                {
                    bool Success = SectionToLocations.TryGetValue(SectionNumber, out listLocations);
                    if (Success)
                    {
                        return listLocations;

                    }
                }

                //Request updates after fetching the list so we don't update the list mid-query
                GetUpdatedLocationsForSection(SectionNumber);

                return listLocations;
            }
            else
            {
                
                LocationObj[] objs = GetAllLocationsForSection(SectionNumber);
                ConcurrentDictionary<long, LocationObj> dictLocs = new ConcurrentDictionary<long,LocationObj>(); 
                foreach (LocationObj loc in objs)
                {
                    dictLocs.TryAdd(loc.ID, loc); 
                }

                return dictLocs;

            }
        
        }

        /// <summary>
        /// Refreshes all the locations for a section from the server.
        /// It doesn't delete old locations on the same section from our
        /// datastructures, so it should be called once at the beginning
        /// of execution and never again unless that is fixed. 
        /// </summary>
        /// <param name="SectionNumber"></param>
        /// <returns></returns>
        private LocationObj[] GetAllLocationsForSection(long SectionNumber)
        {
            AnnotateLocationsClient proxy = CreateProxy();
            proxy.Open();

            Location[] locations = new Location[0];
            try
            {
                bool NoOutstandingRequest = OutstandingSectionQueries.TryAdd(SectionNumber, true);
                if (NoOutstandingRequest)
                {
                    //    locations = proxy.GetLocationsForSection(out QueryTime, SectionNumber);
                    proxy.BeginGetLocationsForSection(SectionNumber,
                        GetLocationsCallback,
                        new GetLocationsCallbackState(proxy, SectionNumber));
                }
               
            }
            catch (EndpointNotFoundException e)
            {
                System.Windows.Forms.MessageBox.Show("Could not connect to annotation database: " + e.ToString());
                proxy.Close();
            }
            catch (Exception e)
            {
                ShowStandardExceptionMessage(e);
                proxy.Close();
            }

//            GetLocationsCallbackState state = new GetLocationsCallbackState(proxy, SectionNumber);
//            ParseLocationQuery(locations, QueryTime, state);

            return new LocationObj[0]; 
             
        }

        /// <summary>
        /// Returns only the location objects that have changed since our last query
        /// </summary>
        /// <param name="SectionNumber"></param>
        /// <returns></returns>
        private LocationObj[] GetUpdatedLocationsForSection(long SectionNumber)
        {
            AnnotateLocationsClient proxy = CreateProxy();
            proxy.Open();

            Location[] locations = new Location[0];            

            try
            {
                bool NoOutstandingRequest = OutstandingSectionQueries.TryAdd(SectionNumber, true);
                if (NoOutstandingRequest)
                {
                    DateTime LastQuery = DateTime.MinValue; 
                    if (LastQueryForSection.ContainsKey(SectionNumber))
                        LastQuery = LastQueryForSection[SectionNumber];

                    //Build list of Locations to check
                    proxy.BeginGetLocationChanges(SectionNumber,
                                                  LastQuery.Ticks,
                                                  GetLocationChangesCallback,
                                                  new GetLocationsCallbackState(proxy, SectionNumber));
                    //                 locations = proxy.GetLocationChanges(out DeletedLocations,
                    //                                                   SectionNumber, 
                    //                                                    LastQuery.Ticks);
                }
            }
            catch (EndpointNotFoundException e)
            {
                System.Windows.Forms.MessageBox.Show("Could not connect to annotation database: " + e.ToString());
                proxy.Close();
            }
            catch (Exception e)
            {
                ShowStandardExceptionMessage(e);
                proxy.Close();
            }


            return new LocationObj[0]; 
        }

        internal void CreateLink(LocationObj A, LocationObj B)
        {
            //lock (LockObject)
            {
                AnnotateLocationsClient proxy = CreateProxy();

                try
                {
                    proxy.Open();
                    proxy.CreateLocationLink(A.ID, B.ID);
                    A.AddLink(B.ID);
                    B.AddLink(A.ID);
                    Update(A);
                    Update(B);
                }
                catch (Exception e)
                {
                    System.Windows.Forms.MessageBox.Show("Error creating link between locations, link not created: " + e.Message, "Error", System.Windows.Forms.MessageBoxButtons.OK);
                }
                finally
                {
                    if(proxy != null)
                        proxy.Close();
                }
            }
        }

        internal void DeleteLink(long A, long B)
        {
            //lock (LockObject)
            {
                AnnotateLocationsClient proxy = CreateProxy();

                LocationObj AObj = GetObjectByID(A);
                LocationObj BObj = GetObjectByID(B);

                try
                {
                    proxy.Open();
                    proxy.DeleteLocationLink(A, B);

                    if (AObj != null)
                    {
                        AObj.RemoveLink(B);
                        Update(AObj);
                    }

                    if (BObj != null)
                    {
                        BObj.RemoveLink(A);
                        Update(BObj);
                    }
                }
                catch (Exception e)
                {
                    System.Windows.Forms.MessageBox.Show("Error deleting link between locations, link not created: " + e.Message, "Error", System.Windows.Forms.MessageBoxButtons.OK);
                }
                finally
                {
                    if(proxy != null)
                        proxy.Close();
                }
            }
        }

        #region Add/Update/Remove

        internal override LocationObj Update(LocationObj updateObj)
        {
            //lock (LockObject)
            {
                LocationObj obj;
                bool success = IDToObject.TryGetValue(updateObj.ID, out obj);

                //Update if the new DB object has a later modified date. 
                if (updateObj.GetData().LastModified > obj.GetData().LastModified)
                {
                    obj.Synch(updateObj.GetData());
                }
                 
                CallOnAddUpdateRemoveKey(updateObj, new AddUpdateRemoveKeyEventArgs(updateObj.ID, AddUpdateRemoveKeyEventArgs.Action.UPDATE));
                
                return obj; 
            }
        }

        internal override LocationObj Add(LocationObj newType)
        {
            //lock (LockObject)
            {
                if (IDToObject.TryAdd(newType.ID, newType))
                {
                    ConcurrentDictionary<long, LocationObj> listSectionLocations = new ConcurrentDictionary<long,LocationObj>();
                    
                    listSectionLocations = SectionToLocations.GetOrAdd(newType.Section, listSectionLocations);

                    bool Success = listSectionLocations.TryAdd(newType.ID, newType);
                    if (!Success)
                    {
                        //Somebody already added the object to the list sections collection...
                        Trace.WriteLine("Race condition in LocationStore.Add", "WebAnnotation"); 
                        Debug.Assert(false);                         
                    }

                    CallOnAddUpdateRemoveKey(newType, new AddUpdateRemoveKeyEventArgs(newType.ID, AddUpdateRemoveKeyEventArgs.Action.ADD));
                    
                    return newType;
                }
                else
                {
                    //Update an existing object if it exists...
                    return Update(newType); 
                }
            }
        }

        internal override void Remove(long ID)
        {

        //    lock (LockObject)
            {
                LocationObj loc = null;

                bool Success = IDToObject.TryGetValue(ID, out loc);

                if (Success)
                {
                    //Let consumers know this key is about to go away
                    //We used to do this before removing from the collection before using collections.Concurrent.
                    CallOnAddUpdateRemoveKey(loc, new AddUpdateRemoveKeyEventArgs(ID, AddUpdateRemoveKeyEventArgs.Action.REMOVE));
                }
                else
                    return;

                Success = IDToObject.TryRemove(ID, out loc);
                
                if(Success)              
                {
                    //Remove it from the mapping of sections to locations on that section
                    ConcurrentDictionary<long, LocationObj> listSectionLocations = null;
                    Success = SectionToLocations.TryGetValue(loc.Section, out listSectionLocations);
                    if (Success)
                    {
                        LocationObj listSectionLocationsObj = null;
                        listSectionLocations.TryRemove(ID, out listSectionLocationsObj);
                    }
                }

                return;
            }
        }

        #endregion


        #region Callbacks

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


        private void ParseLocationQuery(Location[] locations, long TicksAtQueryExecute, GetLocationsCallbackState state)
        {
            ParseLocationQuery(locations, TicksAtQueryExecute, state, null);
        }

        private void ParseLocationQuery(Location[] locations, long TicksAtQueryExecute, GetLocationsCallbackState state, long[] DeletedLocations)
        {

            DateTime QueryExecuteTime = new DateTime(TicksAtQueryExecute, DateTimeKind.Utc);

            List<LocationObj> listObj = new List<LocationObj>(locations.Length);
  //          List<long> listMissingStructures = new List<long>(locations.Length); 

  //          lock (LockObject)
            {
                DateTime LastQuery = DateTime.MinValue;
                if (LastQueryForSection.ContainsKey(state.SectionNumber))
                    LastQuery = LastQueryForSection[state.SectionNumber];

                //Don't update if we've got results from a query executed after this one
                if (LastQuery < QueryExecuteTime)
                {
                    if (DeletedLocations != null)
                    {
                        foreach (long ID in DeletedLocations)
                        {
                            Remove(ID);
                        }
                    }

                    /*
                    foreach (Location loc in locations)
                    {
                       //StructureObj parentStructure = Store.Structures.GetObjectByID(newObj.ParentID.Value, false);
                       //if (parentStructure == null)
                       //{
                            listMissingStructures.Add(loc.ParentID);
                       //}
                    }
                    
                    Store.Structures.GetStructuresByIDs(listMissingStructures, true);
                    */

                    System.Threading.Tasks.Parallel.ForEach(locations, (loc) =>
                    //foreach (Location loc in locations)
                    {
                        LocationObj newObj = new LocationObj(loc);
                        newObj = Add(newObj);
                        listObj.Add(newObj);
                    });

                    LastQueryForSection[state.SectionNumber] = QueryExecuteTime;
                }
                else
                {
                    Trace.WriteLine("Ignoring stale query results", "WebAnnotation");
                }
            }

            CallOnAllUpdatesCompleted(new OnAllUpdatesCompletedEventArgs(state.SectionNumber, listObj.ToArray()));

            bool boolVal;
            //Remove the entry from outstanding queries so we can query again
            OutstandingSectionQueries.TryRemove(state.SectionNumber, out boolVal); 
        }

        #endregion
    }
}
