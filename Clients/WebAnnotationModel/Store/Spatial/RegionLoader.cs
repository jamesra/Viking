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

    public interface IRegionLoader<OBJECT>
    {
        /// <summary>
        /// Divides the requested region into a grid and requests intersecting grid cells.
        /// The callbacks are invoked multiple times as local and server objects are identified
        /// within each grid
        /// </summary>
        /// <param name="VolumeBounds"></param>
        /// <param name="ScreenPixelSizeInVolume"></param>
        /// <param name="SectionNumber"></param>
        /// <param name="queryTargets"></param>
        /// <param name="OnServerObjectsLoadedCallback"></param>
        /// <param name="FoundCachedLocalObjectsCallback"></param>
        /// <returns></returns>
        Task<List<OBJECT>> GetObjectsInRegionAsync(GridRectangle VolumeBounds,
            double ScreenPixelSizeInVolume,
            int SectionNumber,
            QueryTargets queryTargets,
            CancellationToken token,
            Action<ICollection<OBJECT>> foundObjectCallback);
    }

    /// <summary>
    /// Loads objects from a section based on region queries
    /// </summary>
    public class RegionLoader<KEY, OBJECT, SERVER_OBJECT> : IRegionLoader<OBJECT>
        where KEY : struct, IEquatable<KEY>, IComparable<KEY>
        where OBJECT : class, IDataObjectWithKey<KEY>
        where SERVER_OBJECT : IEquatable<SERVER_OBJECT>, IDataObjectWithKey<KEY>
    {
        readonly GridCellDimensions CellDimensions;
        private readonly double PowerScale;
        static double RegionUpdateInterval = 180;
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
            foreach (OBJECT o in e.OldItems.Cast<OBJECT>())
            {
                SpatialSearch.Delete(o.ID, out var _);
            }

            foreach (OBJECT o in e.NewItems.Cast<OBJECT>())
            {
                SpatialSearch.TryAdd(RTreeConverter.BoundingRect(o), o.ID);
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
        /// 
        /// </summary>
        /// <param name="VolumeBounds"></param>
        /// <param name="ScreenPixelSizeInVolume"></param>
        /// <param name="SectionNumber"></param>
        /// <param name="callback">A thread-safe callback function to hand the loaded objects to</param>
        public async Task<List<OBJECT>> GetObjectsInRegionAsync(GridRectangle VolumeBounds,
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
            //If we change the magnification factor we should stop loading regions

            IRegionPyramidLevel<RegionRequestData<OBJECT>> level = RegionPyramid.GetLevel(screenPixelSizeInVolume);
            GridRange<RegionRequestData<OBJECT>> gridRange = level.SubGridForRegion(VolumeBounds);

            DateTime currentTime = DateTime.UtcNow;

            List<Tuple<GridIndex, Task>> listTasks = new List<Tuple<GridIndex, Task>>();

            List<OBJECT> localObjects = new List<OBJECT>();

            foreach (GridIndex iCell in gridRange.Indicies)
            {
                int iX = iCell.X;
                int iY = iCell.Y;

                if (token.IsCancellationRequested)
                    return new List<OBJECT>();
                
                //Trace.WriteLine(string.Format("Grid Region Loading Z:{0} L:{1} X:{2} Y:{3}", SectionNumber, level.Level, iX, iY));
                //Something I learned debugging why multiple requests for the same region being launched is that the delegate for GetOrAddCell can
                //be called multiple times if no value is in the dictionary and multiple threads all attempt to add a value before a thread inserts 
                //a value.  So make GetOrAdd calls cheap.
                RegionRequestData<OBJECT> cell = level.GetOrAddCell(iCell, (icell) => new RegionRequestData<OBJECT>(bounds: level.CellBounds(iCell.X, iCell.Y)));
                try
                {
                    await cell.Lock.WaitAsync(token);
                    if (token.IsCancellationRequested)
                        return new List<OBJECT>();

                    if ((queryTargets & QueryTargets.Server) > 0)
                    {
                        if (RegionIsDueForRefresh(cell))
                        {
                            //Trace.WriteLine(string.Format("Grid Region Loading Z:{0} L:{1} X:{2} Y:{3}", SectionNumber, level.Level, iX, iY));
                            CreateRegionServerRequest(cell, level, iCell, sectionNumber, token,
                                foundObjectsCallback);
                        }
                        else
                        {
                            //Add our callback to the list, and return any known local objects
                            //If we are waiting on results, add our callback to the list of functions to call when the request is complete
                            if (cell.OutstandingQuery)
                            {
                                cell.AddCallback(foundObjectsCallback);
                            }
                        }
                    }

                    if ((queryTargets & QueryTargets.ClientCache) > 0 && foundObjectsCallback != null)
                    {
                        //Begin a task to load the local objects and perform the callback
                        //await ReportLocalObjectsInRegion(cell, level, sectionNumber, aToken, foundObjectsCallback); 
                    }
                }
                finally
                {
                    cell.Lock.Release();
                }
            }

            var localObjectKeys = SpatialSearch.Intersects(VolumeBounds);
            var localsObjects = await objectStore.GetObjectsByIDs(localObjectKeys, false, token);
            if(foundObjectsCallback != null)
                foundObjectsCallback(localObjects);
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

        private Task CreateRegionServerRequest(RegionRequestData<OBJECT> cell, IRegionPyramidLevel<RegionRequestData<OBJECT>> level, GridIndex iCell, int sectionNumber, CancellationToken aToken, Action<ICollection<OBJECT>> foundObjectsCallback)
        {
            //Task<ICollection<OBJECT>> localTask;
            //Task<ICollection<OBJECT>> serverTask;
             
            //Create a new cell and hand it the callback
            DateTime? lastQueryUtc = cell.LastQuery;

#if DEBUG
            cell.DebugMessage = $"S:{sectionNumber} L:{level.Level} {iCell}";
#endif

            Debug.Assert(!cell.OutstandingQuery,
                "Starting a query for a region we already have an outstanding request for");
            
            //Add the callback right away in case the query task completes before we can add it afterword
            cell.AddCallback(foundObjectsCallback);
            var task = DoServerRequestAndCallbackAsync(cell, level, iCell, sectionNumber, aToken);
            cell.SetQuery(task, aToken);
            
#if DEBUG
            //string TraceString = string.Format("CreateRegionRequest: {0} ({1},{2}) Level:{3} MinRadius:{4}", SectionNumber, iCell.X, iCell.Y, level.Level, level.MinRadius);
            //Trace.WriteLine(TraceString, "WebAnnotation");
#endif
            return task;
        } 

        private async Task DoServerRequestAndCallbackAsync(RegionRequestData<OBJECT> cell, IRegionPyramidLevel<RegionRequestData<OBJECT>> level, GridIndex iCell, int sectionNumber, CancellationToken aToken)
        {
            var client = ServerClient.GetOrCreate();

            var serverResult = await client.GetAsync(sectionNumber, cell.Bounds.ToWKT(), level.MinRadius, cell.LastQuery, aToken);
            
            if (aToken.IsCancellationRequested)
                return;

            await ServerObjProcessor.ProcessServerResults(serverResult.QueryTime, serverResult.NewOrUpdated);

            //The locals should now include the new objects
            var localObjectKeys = SpatialSearch.Intersects(cell.Bounds);
            var localsObjects = await objectStore.GetObjectsByIDs(localObjectKeys, false, aToken); 

            await cell.OnLoadCompleted(localsObjects, serverResult.QueryTime);
        }
    }
}