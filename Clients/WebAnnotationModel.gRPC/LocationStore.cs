using Geometry; 
using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Collections.Specialized;
using System.ComponentModel;
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

    public class LocationStore : StoreBaseWithKey<long, LocationObj, ILocation, ILocation, ILocation>, ILocationStore, IRegionLoader<LocationObj>
    {
        /// <summary>
        /// Maps sections to a sorted list of locations on that section.
        /// This collection is not guaranteed to match the ObjectToID collection.  Adding spin-locks to the Add/Remove functions could solve this if it becomes an issue.
        /// </summary>
        System.Collections.Concurrent.ConcurrentDictionary<long, ConcurrentDictionary<long, LocationObj>> SectionToLocations = new ConcurrentDictionary<long, ConcurrentDictionary<long, LocationObj>>();

        /// <summary>
        /// Last successful region/section query time per section, used by FreeExcessSections.
        /// </summary>
        private readonly ConcurrentDictionary<long, DateTime> LastQueryForSection = new ConcurrentDictionary<long, DateTime>();

        private readonly IStructureStore _structureStore;
        private readonly ILocationLinkStore _locationLinkStore;

        private readonly IServerAnnotationsClientFactory<ILocationsClient> _locationClientFactory;
        private readonly IServerAnnotationsClientFactory<IServerSpatialAnnotationsClient<long, ILocation>> _spatialClientFactory;
        private readonly IStoreEditor<long, LocationObj> _storeEditor;


        public LocationObj[] GetLocalObjectsForStructure(long StructureID)
        {
            return IDToObject.Values.Where(l => l.ParentID.HasValue && l.ParentID.Value == StructureID).ToArray();
        }
          
        public LocationStore(IServerAnnotationsClientFactory<IServerAnnotationsClient<long, ILocation, ILocation, ILocation>> clientFactory,
            IServerAnnotationsClientFactory<ILocationsClient> locationClientFactory,
            IServerAnnotationsClientFactory<IServerSpatialAnnotationsClient<long, ILocation>> spatialClientFactory,
            IObjectConverter<LocationObj, ILocation> objToServerObjConverter,
            IObjectConverter<ILocation, LocationObj> serverObjToObjConverter,
            IStructureStore structureStore,
            ILocationLinkStore locationLinkStore) : base(clientFactory, null, objToServerObjConverter,
            serverObjToObjConverter)
        {
            _structureStore = structureStore;
            _locationLinkStore = locationLinkStore;
            _locationClientFactory = locationClientFactory;
            _spatialClientFactory = spatialClientFactory;
            _storeEditor = this as IStoreEditor<long, LocationObj>;
            OnCollectionChanged += OnStoreCollectionChanged;
        }

        private void OnStoreCollectionChanged(object sender, NotifyCollectionChangedEventArgs e)
        {
            if (e.OldItems != null)
            {
                foreach (var item in e.OldItems)
                {
                    if (item is LocationObj loc)
                        TryRemoveFromSectionIndex(loc);
                }
            }

            if (e.NewItems != null)
            {
                foreach (var item in e.NewItems)
                {
                    if (item is LocationObj loc)
                        TryAddToSectionIndex(loc);
                }
            }
        }

        private void TryAddToSectionIndex(LocationObj loc)
        {
            var sectionMap = SectionToLocations.GetOrAdd(loc.Section,
                _ => new ConcurrentDictionary<long, LocationObj>());
            sectionMap.TryAdd(loc.ID, loc);
        }

        private void TryRemoveFromSectionIndex(LocationObj loc)
        {
            if (!SectionToLocations.TryGetValue(loc.Section, out var sectionMap))
                return;

            sectionMap.TryRemove(loc.ID, out _);
            if (sectionMap.IsEmpty)
                SectionToLocations.TryRemove(loc.Section, out _);
        }

        private void TouchSectionQueryTime(long sectionNumber) =>
            LastQueryForSection.AddOrUpdate(sectionNumber, DateTime.UtcNow, (_, __) => DateTime.UtcNow);

        /// <summary>
        /// After locations for a section are refreshed, sync location links (including deletes)
        /// using the prior section query watermark for incremental ModifiedAfter filtering.
        /// </summary>
        private async Task SyncLocationLinksForSectionAsync(long sectionNumber, CancellationToken token)
        {
            DateTime? modifiedAfter = null;
            if (LastQueryForSection.TryGetValue(sectionNumber, out var last) && last > DateTime.MinValue)
                modifiedAfter = last;

            await _locationLinkStore.GetLinksForSectionAsync(sectionNumber, modifiedAfter, token)
                .ConfigureAwait(false);
        }

        /// <summary>
        /// When a cached location moves between sections, keep the section index coherent.
        /// </summary>
        protected override void OnObjectPropertyChanged(object sender, PropertyChangedEventArgs e)
        {
            base.OnObjectPropertyChanged(sender, e);

            var loc = sender as LocationObj;
            if (loc == null)
                return;

            if (e.PropertyName != nameof(LocationObj.Section) && e.PropertyName != nameof(LocationObj.Z))
                return;

            // Rebuild membership from the live Section value: remove from every map, then re-add.
            foreach (var map in SectionToLocations.Values)
                map.TryRemove(loc.ID, out _);

            foreach (var empty in SectionToLocations.Where(kv => kv.Value.IsEmpty).Select(kv => kv.Key).ToArray())
                SectionToLocations.TryRemove(empty, out _);

            TryAddToSectionIndex(loc);
        } 

        /// <summary>
        /// Queries the server for locations on the given section whose mosaic-space bounding box intersects
        /// VolumeBounds, merges them into the local store, and invokes foundObjectCallback with the results.
        /// </summary>
        public async Task<List<LocationObj>> GetObjectsInRegionAsync(Geometry.GridRectangle VolumeBounds,
            double ScreenPixelSizeInVolume,
            int SectionNumber,
            QueryTargets queryTargets,
            CancellationToken token,
            Action<ICollection<LocationObj>> foundObjectCallback)
        {
            if (queryTargets == QueryTargets.ClientCache)
            {
                var cached = GetLocalObjectsForSection(SectionNumber).Values
                    .Where(l => VolumeBounds.Contains(l.Position)).ToList();
                foundObjectCallback?.Invoke(cached);
                return cached;
            }

            var client = _spatialClientFactory.GetOrCreate();
            string regionWKT = ToWktPolygon(VolumeBounds);
            var progressiveResults = new List<LocationObj>();

            // Incremental refresh: pass prior section watermark so DeletedLocations / updates apply.
            DateTime? modifiedAfter = null;
            if (LastQueryForSection.TryGetValue(SectionNumber, out var lastQuery) && lastQuery > DateTime.MinValue)
                modifiedAfter = lastQuery;

            // Prefer progressive merge when the concrete gRPC client is available.
            if (client is LocationsClient locationsClient)
            {
                var response = await locationsClient.GetAsync(
                    SectionNumber, regionWKT, ScreenPixelSizeInVolume, modifiedAfter, token,
                    onChunk: async update =>
                    {
                        if (token.IsCancellationRequested)
                            return;
                        var chunkChanges = await ServerQueryResultsHandler
                            .ProcessServerUpdate(update.NewOrUpdated, update.DeletedIDs);
                        await CallOnCollectionChanged(chunkChanges);
                        var chunkObjs = chunkChanges.ObjectsInStore
                            .Where(l => VolumeBounds.Contains(l.Position)).ToList();
                        progressiveResults.AddRange(chunkObjs);
                        foundObjectCallback?.Invoke(chunkObjs);
                    });

                if (token.IsCancellationRequested)
                    return new List<LocationObj>();

                await SyncLocationLinksForSectionAsync(SectionNumber, token);
                TouchSectionQueryTime(SectionNumber);
                return progressiveResults.Count > 0
                    ? progressiveResults.Distinct().ToList()
                    : response.NewOrUpdated
                        .Select(l => ServerObjConverter.Convert(l))
                        .Where(l => VolumeBounds.Contains(l.Position))
                        .ToList();
            }

            var unary = await client.GetAsync(SectionNumber, regionWKT, ScreenPixelSizeInVolume, modifiedAfter, token);

            if (token.IsCancellationRequested)
                return new List<LocationObj>();

            var changes = await ServerQueryResultsHandler.ProcessServerUpdate(unary.NewOrUpdated, unary.DeletedIDs);
            await CallOnCollectionChanged(changes);
            await SyncLocationLinksForSectionAsync(SectionNumber, token);
            TouchSectionQueryTime(SectionNumber);

            List<LocationObj> results = changes.ObjectsInStore
                .Where(l => VolumeBounds.Contains(l.Position)).ToList();
            foundObjectCallback?.Invoke(results);
            return results;
        }

        private static string ToWktPolygon(Geometry.GridRectangle bounds)
        {
            System.Globalization.CultureInfo ci = System.Globalization.CultureInfo.InvariantCulture;
            return string.Format(ci,
                "POLYGON(({0} {1}, {2} {1}, {2} {3}, {0} {3}, {0} {1}))",
                bounds.Left, bounds.Bottom, bounds.Right, bounds.Top);
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
            var client = ClientFactory.GetOrCreate();
            var serverObj = ClientObjConverter.Convert(new_location);
            var created = client.Create(serverObj, CancellationToken.None).Result;
            if (created == null)
                return null;

            var created_location = GetOrAdd(created.ID, id => ServerObjConverter.Convert(created), out _);

            if (linked_locations != null && linked_locations.Length > 0)
            {
                foreach (long linkedId in linked_locations)
                {
                    Store.LocationLinks.CreateLink(created_location.ID, linkedId);
                }
            }

            return created_location;
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
            var changes = await ServerQueryResultsHandler.ProcessServerUpdate(response, Array.Empty<long>());
            CallOnCollectionChanged(changes);
            return changes.ObjectsInStore; 
        }


        #endregion
        
        public List<LocationObj> GetStructureLocationChangeLog(long structureid)
        {
            // Server RPC is Unimplemented until audit tables are mapped in the EF model.
            throw new InvalidOperationException(
                "Location change log is not available from the gRPC annotation service yet (audit tables unmapped).");
        }

        public bool Contains(LocationObj o, Geometry.GridRectangle bounds)
        {
            return bounds.Contains(o.Position);
        }

        /// <summary>
        /// Synchronous convenience wrapper over GetStructureLocations(structureID, QueryTargets.Server).
        /// </summary>
        public ICollection<LocationObj> GetLocationsForStructure(long StructureID)
        {
            return GetStructureLocations(StructureID, QueryTargets.Server).Result;
        }

        /// <summary>
        /// Objects known locally to belong to the given section, without contacting the server.
        /// </summary>
        public ConcurrentDictionary<long, LocationObj> GetLocalObjectsForSection(long SectionNumber)
        {
            return SectionToLocations.TryGetValue(SectionNumber, out var sectionLocations)
                ? sectionLocations
                : new ConcurrentDictionary<long, LocationObj>();
        }

        /// <summary>
        /// Drop cached locations for a section from the local store.
        /// </summary>
        public bool RemoveSection(long SectionNumber)
        {
            LastQueryForSection.TryRemove(SectionNumber, out _);
            if (!SectionToLocations.TryRemove(SectionNumber, out var sectionObjects))
                return true;

            ForgetLocally(sectionObjects.Keys.ToArray());
            sectionObjects.Clear();
            return true;
        }

        /// <summary>
        /// Evict least-recently-queried section caches when over <paramref name="LoadedSectionLimit"/>.
        /// <paramref name="LoadingSectionLimit"/> is reserved for cancelling in-flight section loads
        /// once those are wired through the gRPC region path.
        /// </summary>
        public void FreeExcessSections(int LoadedSectionLimit, int LoadingSectionLimit)
        {
            _ = LoadingSectionLimit;

            if (LoadedSectionLimit < 0 || LastQueryForSection.Count <= LoadedSectionLimit)
                return;

            var oldestFirst = LastQueryForSection.OrderBy(kv => kv.Value).ToList();
            while (LastQueryForSection.Count > LoadedSectionLimit && oldestFirst.Count > 0)
            {
                var section = oldestFirst[0].Key;
                oldestFirst.RemoveAt(0);
                RemoveSection(section);
            }
        }

        /// <summary>
        /// Check the local cache only, without contacting the server.
        /// </summary>
        public bool TryGetValue(long ID, out LocationObj obj) => IDToObject.TryGetValue(ID, out obj);
          
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
