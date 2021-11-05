using Geometry; 
using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Viking.AnnotationServiceTypes.gRPC.V1.Protos;
using Viking.AnnotationServiceTypes.Interfaces;
using WebAnnotationModel;
using WebAnnotationModel.Objects;
using WebAnnotationModel.ServerInterface;


namespace WebAnnotationModel.gRPC
{ 

    public class LocationStore : StoreBaseWithKey<long, LocationObj, ILocation, LocationObj, ILocation>, ILocationStore
    {
        /// <summary>
        /// Maps sections to a sorted list of locations on that section.
        /// This collection is not guaranteed to match the ObjectToID collection.  Adding spin-locks to the Add/Remove functions could solve this if it becomes an issue.
        /// </summary>
        System.Collections.Concurrent.ConcurrentDictionary<long, ConcurrentDictionary<long, LocationObj>> SectionToLocations = new ConcurrentDictionary<long, ConcurrentDictionary<long, LocationObj>>();

        private readonly IStructureStore _structureStore;

        private readonly IServerAnnotationsClientFactory<ILocationsClient> _locationClientFactory;
        private readonly IStoreEditor<long, LocationObj> _storeEditor;
        private readonly IStoreServerQueryResultsHandler<long, LocationObj, ILocation> _queryResultsHandler;


        public LocationObj[] GetLocalObjectsForStructure(long StructureID)
        {
            return IDToObject.Values.Where(l => l.ParentID.HasValue && l.ParentID.Value == StructureID).ToArray();
        }
          
        public LocationStore(IServerAnnotationsClientFactory<IServerAnnotationsClient<long, ILocation, LocationObj, ILocation>> clientFactory,
            IServerAnnotationsClientFactory<ILocationsClient> locationClientFactory,
            IStoreServerQueryResultsHandler<long, LocationObj, ILocation> queryResultsHandler,
            IObjectConverter<LocationObj, ILocation> objToServerObjConverter,
            IObjectConverter<ILocation, LocationObj> serverObjToObjConverter,
            IStructureStore structureStore) : base(clientFactory, queryResultsHandler, objToServerObjConverter,
            serverObjToObjConverter)
        {
            _structureStore = structureStore;
            _locationClientFactory = locationClientFactory;
            _queryResultsHandler = queryResultsHandler;
            _storeEditor = this as IStoreEditor<long, LocationObj>;
        } 
         
        public async Task<LocationObj> GetLastModifiedLocation()
        {
            var client = _locationClientFactory.GetOrCreate();
            var result = await client.GetLastModifiedLocation();
            var obj = GetOrAdd(result.ID, (id) => ServerObjConverter.Convert(result), out var added);
            return obj;
        }
         
        /// <summary>
        /// Create a new location on the server.  Add the location to the local store.
        /// </summary>
        /// <param name="new_location"></param>
        /// <param name="linked_locations"></param>
        /// <returns></returns>
        public LocationObj Create(LocationObj new_location, long[] linked_locations = null)
        {
            throw new NotImplementedException();
        }

        public override Task<bool> Remove(LocationObj obj)
        {
            obj.DBAction = DBACTION.DELETE;

            LocationObj deletedObj = InternalDelete(obj.ID);
            CallOnCollectionChangedForDelete(new LocationObj[] { deletedObj });

            return Task.FromResult(true);
        }

        #region Add/Update/Remove

        /*
        /// <summary>
        /// Send a request to load all structure parents in one batch before adding locations
        /// </summary>
        /// <param name="newObjs"></param>
        /// <returns></returns>
        protected override ChangeInventory<LocationObj> InternalAdd(LocationObj[] newObjs)
        {
            long[] MissingParentIDs = newObjs.Where(loc => loc.ParentID.HasValue && _structureStore.Contains(loc.ParentID.Value) == false).Select(loc => loc.ParentID.Value).Distinct().ToArray();
            if (MissingParentIDs.Length > 0)
                _structureStore.GetObjectsByIDs(MissingParentIDs, true, CancellationToken.None);

            return base.InternalAdd(newObjs);
        }*/

        protected ICollection<LocationObj> InternalDelete(LocationObj[] objs)
        {
            long[] IDs = new long[objs.Length];
            for (int i = 0; i < objs.Length; i++)
            {
                IDs[i] = objs[i].ID;
            }

            return InternalDelete(IDs);
        }
         

        public async Task<ICollection<LocationObj>> GetStructureLocations(long structureID, QueryTargets targets)
        {
            var client = _locationClientFactory.GetOrCreate();
            var response = await client.GetStructureLocations(structureID);
            var changes = await _queryResultsHandler.ProcessServerUpdate(response, Array.Empty<long>());
            CallOnCollectionChanged(changes);
            return changes.ObjectsInStore; 
        }


        #endregion
        
        public List<LocationObj> GetStructureLocationChangeLog(long structureid)
        {
            /*
            AnnotateLocations.AnnotateLocationsClient proxy = null;
            List<LocationObj> listLocations = new List<LocationObj>();
            using (proxy = CreateProxy())
            {
                LocationHistory[] history = proxy.GetLocationChangeLog(structureid, new DateTime?(), new DateTime?());

                listLocations.Capacity = history.Length;
                foreach (LocationHistory db_loc in history)
                {
                    listLocations.Add(new LocationObj(db_loc));
                }
            }

            return listLocations;
            */
            throw new NotImplementedException();
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

        /*
        private ChangeInventory<LocationObj> ProcessAnnotationSet(AnnotationSet serverAnnotations, long[] deleted_objects, DateTime? StartTime, long SectionNumber)
        {
            DateTime TraceQueryEnd = DateTime.UtcNow;

            ChangeInventory<StructureObj> structure_inventory = _structureStore.ParseQuery(serverAnnotations.Structures, new long[] { }, null);
            ChangeInventory<LocationObj> location_inventory = ParseQuery(serverAnnotations.Locations, deleted_objects, null);

            DateTime TraceParseEnd = DateTime.UtcNow;

            Store.Structures.CallOnCollectionChanged(structure_inventory);
            CallOnCollectionChanged(location_inventory);

            if (StartTime.HasValue)
                TraceQueryDetails(SectionNumber, location_inventory.ObjectsInStore.Count, StartTime.Value, TraceQueryEnd, TraceParseEnd, DateTime.UtcNow);

            return location_inventory;
        }
        */
    }
}
