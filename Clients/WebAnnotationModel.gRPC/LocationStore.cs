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

    /// <summary>
    /// Location cache. Implements IRegionLoader for mosaic-bounds queries after the view exists.
    /// </summary>
    public class LocationStore : StoreBaseWithKey<long, LocationObj, ILocation, ILocation, ILocation>, ILocationStore, IRegionLoader<LocationObj>
    {
        /// <summary>
        /// Per-section index maintained from CollectionChanged, not from GetOrAdd.
        /// Not guaranteed to match IDToObject until CallOnCollectionChanged has run.
        /// </summary>
        System.Collections.Concurrent.ConcurrentDictionary<long, ConcurrentDictionary<long, LocationObj>> SectionToLocations = new ConcurrentDictionary<long, ConcurrentDictionary<long, LocationObj>>();

        /// <summary>
        /// Last successful region/section query time per section. Used by FreeExcessSections
        /// and delta link sync. Stamp after success; stamping on send/failure skips the section.
        /// </summary>
        private readonly ConcurrentDictionary<long, DateTime> LastQueryForSection = new ConcurrentDictionary<long, DateTime>();

        readonly object _liveRegionLock = new object();
        readonly Dictionary<int, List<LiveRegionQuery>> _liveRegionQueries = new Dictionary<int, List<LiveRegionQuery>>();
        readonly Dictionary<int, CompletedRegionQuery> _lastCompletedRegion = new Dictionary<int, CompletedRegionQuery>();

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

            try
            {
                await _locationLinkStore.GetLinksForSectionAsync(sectionNumber, modifiedAfter, token)
                    .ConfigureAwait(false);
            }
            catch (Exception e)
            {
                Trace.WriteLine(
                    $"GetLocationLinksForSection failed for section {sectionNumber}: {e.Message}",
                    "WebAnnotation");
            }
        }

        /// <summary>
        /// One GetStructuresByIDs for every parent missing from the local store. Region views
        /// read Location.Parent; without this each Parent getter hits the server by ID.
        /// </summary>
        private async Task EnsureParentStructuresAsync(IEnumerable<ILocation> locations, CancellationToken token)
        {
            var missing = locations
                .Where(l => l != null && l.ParentID.HasValue && !_structureStore.Contains(l.ParentID.Value))
                .Select(l => l.ParentID.Value)
                .Distinct()
                .ToArray();
            if (missing.Length == 0)
                return;

            await _structureStore.GetObjectsByIDs(missing, token).ConfigureAwait(false);
        }

        /// <summary>
        /// Hydrate LocationLinkStore from Location.Links peer IDs embedded on by-ID responses.
        /// </summary>
        protected override Task OnServerObjectsLoaded(IEnumerable<ILocation> objs, DateTime queryTime)
        {
            var links = objs
                .Where(l => l != null && l.Links != null)
                .SelectMany(l => l.Links.Select(peer => (ILocationLink)new LocationLinkObj(peer, l.ID)))
                .ToArray();
            return _locationLinkStore.MergeServerLinksAsync(links, queryTime);
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
        /// One gRPC region stream. Cancelled only when its queried rectangle leaves the padded visible view.
        /// Draw-loop wait tokens must not be passed into this stream.
        /// </summary>
        sealed class LiveRegionQuery
        {
            public Geometry.Rectangle Bounds;
            public double PixelSize;
            public CancellationTokenSource Cts;
            public Task<List<LocationObj>> Task;
        }

        readonly struct CompletedRegionQuery
        {
            public CompletedRegionQuery(Geometry.Rectangle bounds, double pixelSize, DateTime completedUtc)
            {
                Bounds = bounds;
                PixelSize = pixelSize;
                CompletedUtc = completedUtc;
            }

            public Geometry.Rectangle Bounds { get; }
            public double PixelSize { get; }
            public DateTime CompletedUtc { get; }
        }

        /// <summary>
        /// Queries the server for locations on the given section whose mosaic-space bounding box intersects
        /// VolumeBounds, merges them into the local store, and invokes foundObjectCallback with the results.
        /// In-flight streams that still intersect the padded visible region are reused; they are not cancelled
        /// when the draw loop replaces its wait token.
        /// </summary>
        public async Task<List<LocationObj>> GetObjectsInRegionAsync(Geometry.Rectangle VolumeBounds,
            double ScreenPixelSizeInVolume,
            int SectionNumber,
            QueryTargets queryTargets,
            CancellationToken token,
            Action<ICollection<LocationObj>> foundObjectCallback)
        {
            if (queryTargets == QueryTargets.ClientCache)
            {
                var cached = GetLocalObjectsForSection(SectionNumber).Values
                    .Where(l => VolumeBounds.Covers(l.Position)).ToList();
                foundObjectCallback?.Invoke(cached);
                return cached;
            }

            Geometry.Rectangle padded = RegionQueryBounds.PadVisible(VolumeBounds);
            Task<List<LocationObj>> queryTask = null;
            List<LocationObj> cachedHit = null;
            lock (_liveRegionLock)
            {
                if (!_liveRegionQueries.TryGetValue(SectionNumber, out List<LiveRegionQuery> live))
                {
                    live = new List<LiveRegionQuery>();
                    _liveRegionQueries[SectionNumber] = live;
                }

                for (int i = live.Count - 1; i >= 0; i--)
                {
                    LiveRegionQuery q = live[i];
                    if (q.Task.IsCompleted)
                    {
                        live.RemoveAt(i);
                        continue;
                    }

                    if (!q.Bounds.Intersects(padded))
                    {
                        q.Cts.Cancel();
                        live.RemoveAt(i);
                    }
                }

                LiveRegionQuery covering = null;
                foreach (LiveRegionQuery q in live)
                {
                    if (!RegionQueryBounds.SameLod(q.PixelSize, ScreenPixelSizeInVolume))
                        continue;
                    if (q.Bounds.Contains(VolumeBounds))
                    {
                        covering = q;
                        break;
                    }
                }

                if (covering != null)
                {
                    queryTask = covering.Task;
                }
                else if (TryGetFreshCompletedCovering(SectionNumber, VolumeBounds, ScreenPixelSizeInVolume, out cachedHit))
                {
                }
                else
                {
                    CancellationTokenSource cts = new CancellationTokenSource();
                    LiveRegionQuery started = new LiveRegionQuery
                    {
                        Bounds = padded,
                        PixelSize = ScreenPixelSizeInVolume,
                        Cts = cts
                    };
                    started.Task = RunRegionQueryAsync(VolumeBounds, padded, ScreenPixelSizeInVolume, SectionNumber,
                        foundObjectCallback, cts);
                    live.Add(started);
                    queryTask = started.Task;
                }
            }

            if (cachedHit != null)
            {
                foundObjectCallback?.Invoke(cachedHit);
                return cachedHit;
            }

            try
            {
                return await queryTask.ConfigureAwait(false);
            }
            catch (OperationCanceledException)
            {
                var cached = GetLocalObjectsForSection(SectionNumber).Values
                    .Where(l => VolumeBounds.Covers(l.Position)).ToList();
                foundObjectCallback?.Invoke(cached);
                return cached;
            }
        }

        bool TryGetFreshCompletedCovering(int sectionNumber, Geometry.Rectangle visible, double pixel,
            out List<LocationObj> cached)
        {
            cached = null;
            if (!_lastCompletedRegion.TryGetValue(sectionNumber, out CompletedRegionQuery last))
                return false;
            if (!RegionQueryBounds.SameLod(last.PixelSize, pixel))
                return false;
            if (!last.Bounds.Contains(visible))
                return false;
            if ((DateTime.UtcNow - last.CompletedUtc).TotalSeconds > RegionQueryBounds.RefreshIntervalSeconds)
                return false;
            cached = GetLocalObjectsForSection(sectionNumber).Values
                .Where(l => visible.Covers(l.Position)).ToList();
            return true;
        }

        async Task<List<LocationObj>> RunRegionQueryAsync(Geometry.Rectangle visibleBounds,
            Geometry.Rectangle paddedBounds,
            double screenPixelSizeInVolume,
            int sectionNumber,
            Action<ICollection<LocationObj>> foundObjectCallback,
            CancellationTokenSource cts)
        {
            CancellationToken streamToken = cts.Token;
            try
            {
                var client = _spatialClientFactory.GetOrCreate();
                string regionWKT = ToWktPolygon(paddedBounds);
                var progressiveResults = new List<LocationObj>();

                DateTime? modifiedAfter = null;
                if (LastQueryForSection.TryGetValue(sectionNumber, out var lastQuery) && lastQuery > DateTime.MinValue)
                    modifiedAfter = lastQuery;

                if (client is LocationsClient locationsClient)
                {
                    var response = await locationsClient.GetAsync(
                        sectionNumber, regionWKT, screenPixelSizeInVolume, modifiedAfter, streamToken,
                        onChunk: async update =>
                        {
                            if (streamToken.IsCancellationRequested)
                                return;
                            await EnsureParentStructuresAsync(update.NewOrUpdated, streamToken);
                            var chunkChanges = await ServerQueryResultsHandler
                                .ProcessServerUpdate(update.NewOrUpdated, update.DeletedIDs);
                            await CallOnCollectionChanged(chunkChanges);
                            await OnServerObjectsLoaded(update.NewOrUpdated, update.QueryTime);
                            var chunkObjs = chunkChanges.ObjectsInStore
                                .Where(l => paddedBounds.Covers(l.Position)).ToList();
                            progressiveResults.AddRange(chunkObjs);
                            foundObjectCallback?.Invoke(chunkObjs);
                        });

                    if (streamToken.IsCancellationRequested)
                        return progressiveResults;

                    await OnServerObjectsLoaded(response.NewOrUpdated, response.QueryTime);
                    await SyncLocationLinksForSectionAsync(sectionNumber, streamToken);
                    TouchSectionQueryTime(sectionNumber);
                    RecordCompletedRegion(sectionNumber, paddedBounds, screenPixelSizeInVolume);
                    return progressiveResults.Count > 0
                        ? progressiveResults.Distinct().ToList()
                        : response.NewOrUpdated
                            .Select(l => ServerObjConverter.Convert(l))
                            .Where(l => paddedBounds.Covers(l.Position))
                            .ToList();
                }

                var unary = await client.GetAsync(sectionNumber, regionWKT, screenPixelSizeInVolume, modifiedAfter, streamToken);

                if (streamToken.IsCancellationRequested)
                    return new List<LocationObj>();

                await EnsureParentStructuresAsync(unary.NewOrUpdated, streamToken);
                var changes = await ServerQueryResultsHandler.ProcessServerUpdate(unary.NewOrUpdated, unary.DeletedIDs);
                await CallOnCollectionChanged(changes);
                await OnServerObjectsLoaded(unary.NewOrUpdated, unary.QueryTime);
                await SyncLocationLinksForSectionAsync(sectionNumber, streamToken);
                TouchSectionQueryTime(sectionNumber);
                RecordCompletedRegion(sectionNumber, paddedBounds, screenPixelSizeInVolume);

                List<LocationObj> results = changes.ObjectsInStore
                    .Where(l => paddedBounds.Covers(l.Position)).ToList();
                foundObjectCallback?.Invoke(results);
                return results;
            }
            catch (OperationCanceledException)
            {
                var cached = GetLocalObjectsForSection(sectionNumber).Values
                    .Where(l => visibleBounds.Covers(l.Position)).ToList();
                foundObjectCallback?.Invoke(cached);
                return cached;
            }
            finally
            {
                try
                {
                    cts.Dispose();
                }
                catch (ObjectDisposedException)
                {
                }
            }
        }

        void RecordCompletedRegion(int sectionNumber, Geometry.Rectangle paddedBounds, double pixelSize)
        {
            lock (_liveRegionLock)
            {
                _lastCompletedRegion[sectionNumber] =
                    new CompletedRegionQuery(paddedBounds, pixelSize, DateTime.UtcNow);
            }
        }

        private static string ToWktPolygon(Geometry.Rectangle bounds)
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
        public async Task<LocationObj> Create(LocationObj new_location, long[] linked_locations = null)
        {
            var client = ClientFactory.GetOrCreate();
            var serverObj = ClientObjConverter.Convert(new_location);
            var created = await client.Create(serverObj, CancellationToken.None).ConfigureAwait(false);
            if (created == null)
                return null;

            var created_location = GetOrAdd(created.ID, id => ServerObjConverter.Convert(created), out _);

            if (linked_locations != null && linked_locations.Length > 0)
            {
                foreach (long linkedId in linked_locations)
                {
                    await Store.LocationLinks.CreateLink(created_location.ID, linkedId).ConfigureAwait(false);
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

        public override async Task<LocationObj> Add(LocationObj obj)
        {
            await EnsureParentsFromLocationObjs(new[] { obj }, CancellationToken.None).ConfigureAwait(false);
            return await base.Add(obj).ConfigureAwait(false);
        }

        public override async Task<ICollection<LocationObj>> Add(ICollection<LocationObj> objs)
        {
            await EnsureParentsFromLocationObjs(objs, CancellationToken.None).ConfigureAwait(false);
            return await base.Add(objs).ConfigureAwait(false);
        }

        private Task EnsureParentsFromLocationObjs(IEnumerable<LocationObj> locs, CancellationToken token)
        {
            var missing = locs
                .Where(loc => loc?.ParentID != null && !_structureStore.Contains(loc.ParentID.Value))
                .Select(loc => loc.ParentID.Value)
                .Distinct()
                .ToArray();
            if (missing.Length == 0)
                return Task.CompletedTask;
            return _structureStore.GetObjectsByIDs(missing, token);
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

        public async Task<ICollection<LocationObj>> GetStructureLocations(long structureID, QueryTargets targets)
        {
            var client = _locationClientFactory.GetOrCreate();
            var response = await client.GetStructureLocations(structureID);
            var queryTime = DateTime.UtcNow;
            await EnsureParentStructuresAsync(response, CancellationToken.None);
            var changes = await ServerQueryResultsHandler.ProcessServerUpdate(response, Array.Empty<long>());
            await CallOnCollectionChanged(changes).ConfigureAwait(false);
            await OnServerObjectsLoaded(response, queryTime);
            return changes.ObjectsInStore; 
        }


        #endregion
        
        public List<LocationObj> GetStructureLocationChangeLog(long structureid)
        {
            // Server RPC is Unimplemented until audit tables are mapped in the EF model.
            // Return empty rather than throw so the property page can open without crashing.
            Trace.WriteLine(
                $"Location change log unavailable for structure {structureid} (gRPC audit tables unmapped).",
                nameof(WebAnnotationModel));
            return new List<LocationObj>();
        }

        public bool Contains(LocationObj o, Geometry.Rectangle bounds)
        {
            return bounds.Covers(o.Position);
        }

        /// <summary>
        /// Objects in the section index, without contacting the server.
        /// Empty until CollectionChanged has indexed those adds (GetOrAdd alone is not enough).
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
