using Geometry;
using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Collections.Specialized;
using System.Diagnostics;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using RTree;
using Viking.AnnotationServiceTypes.Interfaces;
using WebAnnotationModel.ServerInterface;

namespace WebAnnotationModel
{ 
    public class AnnotationRegions<OBJECT> : BoundlessRegionPyramid<RegionRequestData<OBJECT>>
        where OBJECT : class
    {
        /// <summary>
        /// If set to true any threads using this objects should cancel loading operations
        /// </summary>
        public bool CancelRunningOperations = false;

        public AnnotationRegions(GridCellDimensions cellDimensions, double PowerScale)
            : base(cellDimensions, PowerScale)
        { }
    }
     
    /// <summary>
    /// Return a flatter pyramid instead of a new level for every power of 2
    /// </summary>
    /// <typeparam name="OBJECT"></typeparam>
    public class RegionPyramid<OBJECT> : BoundlessRegionPyramid<RegionRequestData<OBJECT>>
        where OBJECT : class
    {
        public RegionPyramid(GridCellDimensions cellDimensions, double PowerScale) : base(cellDimensions, PowerScale)
        {

        }
    }

    /// <summary>
    /// Section-scoped region queries. SpatialSearch is cached objects only; a miss still requires a store or server read.
    /// Pan/zoom token cancels waiting. Streams for cells still in the padded viewport keep running.
    /// </summary>
    public class RegionLoader<KEY, OBJECT, SERVER_OBJECT> : IRegionLoader<OBJECT>
        where KEY : struct, IEquatable<KEY>, IComparable<KEY>
        where OBJECT : class, IDataObjectWithKey<KEY>
    {
        readonly GridCellDimensions CellDimensions;
        private readonly double PowerScale;
        static double RegionUpdateInterval = RegionQueryBounds.RefreshIntervalSeconds;

        readonly IStoreWithKey<KEY, OBJECT> objectStore;

        private readonly RTree.RTree<KEY> SpatialSearch = new RTree<KEY>();

         //<summary>
         //   8/25/21 
        //    I left off here.  I was going to see about moving the rTree from locationStore to this
        /// class to make it match how SectionIndexStore works.
        /// I think I can do this by making a converter class that converts a location or structure to a
        /// single or set of bounding boxes
        /// </summary>
          
        ConcurrentDictionary<int, RegionPyramid<OBJECT>> sectionPyramids = new ConcurrentDictionary<int, RegionPyramid<OBJECT>>();
        readonly ConcurrentDictionary<RegionQueryKey, RegionRequestData<OBJECT>> liveQueries = new ConcurrentDictionary<RegionQueryKey, RegionRequestData<OBJECT>>();
         
        private readonly IServerAnnotationsClientFactory<IServerSpatialAnnotationsClient<KEY, SERVER_OBJECT>> ServerClient;

        private readonly IServerQueryMultipleAddsOrUpdatesHandler<SERVER_OBJECT> ServerObjProcessor;
        private readonly IServerQueryDeleteHandler<KEY> ServerDeletesProcessor;

        private readonly IBoundingBoxConverter<OBJECT> RTreeConverter;
         

        internal RegionLoader(IStoreWithKey<KEY, OBJECT> store,
            IServerAnnotationsClientFactory<IServerSpatialAnnotationsClient<KEY, SERVER_OBJECT>> clientFactory,
            IServerQueryMultipleAddsOrUpdatesHandler<SERVER_OBJECT> serverObjProcessor,
            IServerQueryDeleteHandler<KEY> serverDeletesProcessor,
                              IBoundingBoxConverter<OBJECT> geometryConverter) : this(store, clientFactory, serverObjProcessor, serverDeletesProcessor, geometryConverter, new GridCellDimensions(2000, 2000), 3)
        {
        }
          
        /// <summary>
        /// 
        /// </summary>
        /// <param name="store"></param>
        /// <param name="CellSize">Size of full-resolution region at level 0.</param>
        /// <param name="LevelPowerScalar">The exponent we use to map a request to a pyramid level</param>
        internal RegionLoader(IStoreWithKey<KEY, OBJECT> store,
            IServerAnnotationsClientFactory<IServerSpatialAnnotationsClient<KEY, SERVER_OBJECT>> clientFactory,
            IServerQueryMultipleAddsOrUpdatesHandler<SERVER_OBJECT> serverObjProcessor,
            IServerQueryDeleteHandler<KEY> serverDeletesProcessor,
            IBoundingBoxConverter<OBJECT> geometryConverter,
            GridCellDimensions CellSize, double LevelPowerScalar)
        {
            objectStore = store;
            store.CollectionChanged += OnStoreChanged;
            ServerClient = clientFactory;
            ServerObjProcessor = serverObjProcessor;
            ServerDeletesProcessor = serverDeletesProcessor;
            RTreeConverter = geometryConverter;
            this.CellDimensions = CellSize;
            this.PowerScale = LevelPowerScalar;
        }

        private void OnStoreChanged(object sender, NotifyCollectionChangedEventArgs e)
        {
            DoStoreChangedTask(e);
        }

        private Task DoStoreChangedTask(NotifyCollectionChangedEventArgs e)
        {
            if (e.OldItems != null)
            {
                foreach (OBJECT o in e.OldItems.Cast<OBJECT>())
                {
                    SpatialSearch.Delete(o.ID, out var _);
                }
            }

            if (e.NewItems != null)
            {
                foreach (OBJECT o in e.NewItems.Cast<OBJECT>())
                {
                    SpatialSearch.TryAdd(RTreeConverter.BoundingRect(o), o.ID);
                }
            }

            return Task.CompletedTask;
        }

        private static bool RegionIsDueForRefresh(RegionRequestData<OBJECT> cell)
        {
            return (!cell.LastQuery.HasValue ||
                    System.TimeSpan.FromTicks(DateTime.UtcNow.Ticks - cell.LastQuery.Value.Ticks).Seconds >
                    RegionUpdateInterval) &&
                   cell.OutstandingQuery == false;
        }

        /// <summary>
        /// Loads cells covering the visible region plus <see cref="VisibleRegionPadFactor"/>.
        /// In-flight streams for this section are cancelled only when their cell no longer
        /// intersects that padded rectangle. The wait <paramref name="token"/> stops this
        /// method from blocking on pan; it does not abort still-visible cell streams.
        /// </summary>
        public async Task<List<OBJECT>> GetObjectsInRegionAsync(Geometry.Rectangle VolumeBounds,
                                                    double screenPixelSizeInVolume,
                                                    int sectionNumber,
                                                    QueryTargets queryTargets,
                                                    CancellationToken token,
                                                    Action<ICollection<OBJECT>> foundObjectsCallback)
        {
            /*
#if REGION_LOADING_TRACE
            Trace.WriteLine(string.Format("Loading section {0} annotation region {1}", SectionNumber, VolumeBounds.Value)) 
#endif
            */

            RegionPyramid<OBJECT> RegionPyramid = GetOrAddRegionPyramidForSection(sectionNumber);
            IRegionPyramidLevel<RegionRequestData<OBJECT>> level = RegionPyramid.GetLevel(screenPixelSizeInVolume);
            Geometry.Rectangle paddedBounds = RegionQueryBounds.PadVisible(VolumeBounds);
            GridRange<RegionRequestData<OBJECT>> gridRange = level.SubGridForRegion(paddedBounds);

            CancelQueriesOutsidePaddedView(sectionNumber, paddedBounds);

            List<Task> regionTasks = new List<Task>();

            try
            {
                foreach (GridIndex iCell in gridRange.Indices)
                {
                    if (token.IsCancellationRequested)
                        break;

                    //Something I learned debugging why multiple requests for the same region being launched is that the delegate for GetOrAddCell can
                    //be called multiple times if no value is in the dictionary and multiple threads all attempt to add a value before a thread inserts 
                    //a value.  So make GetOrAdd calls cheap.
                    RegionRequestData<OBJECT> cell = level.GetOrAddCell(iCell, (icell) => new RegionRequestData<OBJECT>(bounds: level.CellBounds(iCell.X, iCell.Y)));
                    await cell.Lock.WaitAsync(token);
                    try
                    {
                        if ((queryTargets & QueryTargets.Server) > 0)
                        {
                            if (cell.CurrentQuery != null && !cell.CurrentQuery.IsCompleted)
                            {
                                cell.AddCallback(foundObjectsCallback);
                                regionTasks.Add(cell.CurrentQuery);
                            }
                            else if (RegionIsDueForRefresh(cell))
                            {
                                if (cell.CurrentQuery != null)
                                    cell.SetQueryCompletedOrAborted();
                                regionTasks.Add(CreateRegionServerRequest(cell, level, iCell, sectionNumber,
                                    foundObjectsCallback));
                            }
                        }
                    }
                    finally
                    {
                        cell.Lock.Release();
                    }
                }

                token.ThrowIfCancellationRequested();

                if (regionTasks.Count > 0)
                    await Task.WhenAll(regionTasks);
            }
            catch (Exception)
            {
                token.ThrowIfCancellationRequested();
                throw;
            }

            token.ThrowIfCancellationRequested();

            var localObjectKeys = SpatialSearch.Intersects(VolumeBounds.ToRTreeRect(sectionNumber));
            objectStore.TryGetObjectsByIDs(localObjectKeys, out var found, out _);
            List<OBJECT> localsObjects = found.ToList();
            if (foundObjectsCallback != null)
                foundObjectsCallback(localsObjects);
            return localsObjects;
        }
         
        private RegionPyramid<OBJECT> GetOrAddRegionPyramidForSection(int sectionNumber)
        {
            return this.sectionPyramids.GetOrAdd(sectionNumber, (n) => new RegionPyramid<OBJECT>(CellDimensions, PowerScale));
        }

        /*
        private async Task ReportLocalObjectsInRegion(RegionRequestData<OBJECT> cell,
            IRegionPyramidLevel<RegionRequestData<OBJECT>> level, int sectionNumber, CancellationToken aToken,
            Action<ICollection<OBJECT>> foundObjectsCallback)
        {
            var locals = await GetLocalObjectsInRegion(cell, level, sectionNumber, aToken);
            foundObjectsCallback?.Invoke(locals);
        }

        private async Task<ICollection<OBJECT>> GetLocalObjectsInRegion(RegionRequestData<OBJECT> cell,
            IRegionPyramidLevel<RegionRequestData<OBJECT>> level, int sectionNumber, CancellationToken aToken)
        {
            if (aToken.IsCancellationRequested)
                return Array.Empty<OBJECT>();

            return await
                this.objectStore.GetLocalObjectsInRegion(sectionNumber, cell.Bounds,
                    level.MinRadius); 
        } 
        */

        private Task CreateRegionServerRequest(RegionRequestData<OBJECT> cell, IRegionPyramidLevel<RegionRequestData<OBJECT>> level, GridIndex iCell, int sectionNumber, Action<ICollection<OBJECT>> foundObjectsCallback)
        {
#if DEBUG
            cell.DebugMessage = $"S:{sectionNumber} L:{level.Level} {iCell}";
#endif

            Debug.Assert(!cell.OutstandingQuery,
                "Starting a query for a region we already have an outstanding request for");
            
            //Add the callback right away in case the query task completes before we can add it afterword
            cell.AddCallback(foundObjectsCallback);
            CancellationTokenSource cts = new CancellationTokenSource();
            RegionQueryKey key = new RegionQueryKey(sectionNumber, level.Level, iCell.X, iCell.Y);
            cell.PrepareQuery(cts);
            liveQueries[key] = cell;
            Task task = DoServerRequestAndCallbackAsync(cell, level, sectionNumber, key, cts.Token);
            cell.SetQuery(task, cts);
            return task;
        }

        /// <summary>
        /// Abort streams for this section whose cell no longer intersects the padded visible rectangle.
        /// </summary>
        void CancelQueriesOutsidePaddedView(int sectionNumber, Geometry.Rectangle paddedVisible)
        {
            foreach (KeyValuePair<RegionQueryKey, RegionRequestData<OBJECT>> kv in liveQueries)
            {
                if (kv.Key.Section != sectionNumber)
                    continue;
                if (kv.Value.Bounds.Intersects(paddedVisible))
                    continue;
                kv.Value.CancelQuery();
            }
        }

        readonly struct RegionQueryKey : IEquatable<RegionQueryKey>
        {
            public readonly int Section;
            public readonly int Level;
            public readonly int X;
            public readonly int Y;

            public RegionQueryKey(int section, int level, int x, int y)
            {
                Section = section;
                Level = level;
                X = x;
                Y = y;
            }

            public bool Equals(RegionQueryKey other) =>
                Section == other.Section && Level == other.Level && X == other.X && Y == other.Y;

            public override bool Equals(object obj) => obj is RegionQueryKey other && Equals(other);

            public override int GetHashCode()
            {
                unchecked
                {
                    int hash = 17;
                    hash = hash * 31 + Section;
                    hash = hash * 31 + Level;
                    hash = hash * 31 + X;
                    hash = hash * 31 + Y;
                    return hash;
                }
            }
        }

        private async Task DoServerRequestAndCallbackAsync(RegionRequestData<OBJECT> cell, IRegionPyramidLevel<RegionRequestData<OBJECT>> level, int sectionNumber, RegionQueryKey key, CancellationToken aToken)
        {
            try
            {
                var client = ServerClient.GetOrCreate();

                ServerUpdate<KEY, SERVER_OBJECT[]> serverResult;
                var processedChunks = false;
                try
                {
                    serverResult = await client.GetAsync(
                        sectionNumber,
                        ToWktPolygon(cell.Bounds),
                        level.MinRadius,
                        cell.LastQuery,
                        aToken,
                        async update =>
                        {
                            processedChunks = true;
                            await ApplyServerUpdateAsync(update).ConfigureAwait(false);
                        }).ConfigureAwait(false);
                }
                catch (OperationCanceledException)
                {
                    await AbortQueryAsync(cell).ConfigureAwait(false);
                    return;
                }

                if (aToken.IsCancellationRequested)
                {
                    await AbortQueryAsync(cell).ConfigureAwait(false);
                    return;
                }

                if (!processedChunks)
                    await ApplyServerUpdateAsync(serverResult).ConfigureAwait(false);

                var localObjectKeys = SpatialSearch.Intersects(cell.Bounds.ToRTreeRect(sectionNumber));
                objectStore.TryGetObjectsByIDs(localObjectKeys, out var found, out _);
                List<OBJECT> localsObjects = found.ToList();

                try
                {
                    await cell.OnLoadCompleted(localsObjects, serverResult.QueryTime);
                }
                catch (OperationCanceledException)
                {
                    await AbortQueryAsync(cell).ConfigureAwait(false);
                }
            }
            finally
            {
                liveQueries.TryRemove(key, out _);
            }
        }

        async Task ApplyServerUpdateAsync(ServerUpdate<KEY, SERVER_OBJECT[]> update)
        {
            if (update.NewOrUpdated != null && update.NewOrUpdated.Length > 0)
                await ServerObjProcessor.ProcessServerResults(update.QueryTime, update.NewOrUpdated).ConfigureAwait(false);

            if (update.DeletedIDs != null && update.DeletedIDs.Length > 0)
                await ServerDeletesProcessor.ProcessServerDelete(update.DeletedIDs).ConfigureAwait(false);
        }

        static string ToWktPolygon(Geometry.Rectangle bounds)
        {
            var ci = System.Globalization.CultureInfo.InvariantCulture;
            return string.Format(ci,
                "POLYGON(({0} {1}, {2} {1}, {2} {3}, {0} {3}, {0} {1}))",
                bounds.Left, bounds.Bottom, bounds.Right, bounds.Top);
        }

        static async Task AbortQueryAsync(RegionRequestData<OBJECT> cell)
        {
            await cell.Lock.WaitAsync().ConfigureAwait(false);
            try
            {
                if (cell.CurrentQuery != null)
                    cell.ClearLastQuery();
                cell.SetQueryCompletedOrAborted();
            }
            finally
            {
                cell.Lock.Release();
            }
        }
    }
}