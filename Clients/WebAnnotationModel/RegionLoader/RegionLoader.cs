using Geometry;
using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Diagnostics;
using System.Threading;
using System.Threading.Tasks;

namespace WebAnnotationModel
{
    /// <summary>
    /// Stores information about location queries for this region in the volume
    /// </summary>
    public class RegionRequestData<OBJECT>
        where OBJECT : class
    {
        public DateTime? LastQuery = new DateTime?();

#if DEBUG
        private static int NumOutstandingQueries = 0;

        /// <summary>
        /// Optional message for debugging
        /// </summary>
        public string debug_message;

        static readonly ConcurrentDictionary<string, string> active_requests = new ConcurrentDictionary<string, string>();
#endif


        public bool HasBeenQueried
        {
            get { return LastQuery.HasValue; }
        }

        /// <summary>
        /// True if a query has been sent to the server but has not returned
        /// </summary>
        public bool OutstandingQuery
        {
            get
            {
                if (this.AsyncResult == null)
                    return false;

                return AsyncResult.IsCompleted;
            }
        }

        public IAsyncResult AsyncResult = null;

        /// <summary>
        /// Functions to call when the load is complete
        /// </summary>
        public List<Action<ICollection<OBJECT>>> OnCompletionCallbacks = new List<Action<ICollection<OBJECT>>>();

        public RegionRequestData()
        {
        }

        public void SetQuery(IAsyncResult result)
        {
            this.AsyncResult = result;
            this.LastQuery = DateTime.UtcNow;


#if DEBUG
            System.Threading.Interlocked.Increment(ref RegionRequestData<OBJECT>.NumOutstandingQueries);
            active_requests.TryAdd(debug_message, debug_message);

            if (RegionRequestData<OBJECT>.NumOutstandingQueries > 30)
            {
                Trace.WriteLine(string.Format("{0} Outstanding queries", RegionRequestData<OBJECT>.NumOutstandingQueries));
            }
#endif 
        }

        public void AddCallback(Action<ICollection<OBJECT>> callback)
        {
            if (callback == null)
                return;

            lock (this)
            {
                OnCompletionCallbacks.Add(callback);
            }
        }

        public void OnLoadCompleted(ICollection<OBJECT> objects)
        {
            lock (this)
            {


#if DEBUG
                System.Threading.Interlocked.Decrement(ref RegionRequestData<OBJECT>.NumOutstandingQueries);
                string removed_message;
                active_requests.TryRemove(debug_message, out removed_message);

                if (this.OnCompletionCallbacks.Count > 1)
                {
                    Trace.WriteLine(string.Format("{0} callbacks registered in region", this.OnCompletionCallbacks.Count));
                }
#endif
                foreach (Action<ICollection<OBJECT>> a in this.OnCompletionCallbacks)
                {
                    Task.Run(() => { a(objects); });
                }

                this.OnCompletionCallbacks.Clear();

                AsyncResult = null;
            }
        }

        public override string ToString()
        {
#if DEBUG
            return (this.debug_message ?? "") +  $" {this.LastQuery} InProgress: {this.OutstandingQuery} OutstandingQuery: {this.OutstandingQuery}";
#else
            return $" {this.LastQuery} InProgress: {this.OutstandingQuery} OutstandingQuery: {this.OutstandingQuery}";
#endif
        }
    }

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
    /// Loads objects from a section based on region queries
    /// </summary>
    public class RegionLoader<KEY, OBJECT>
        where KEY : struct
        where OBJECT : class
    {
        readonly GridCellDimensions CellDimensions;
        readonly double PowerScale;
        static readonly double RegionUpdateInterval = 180;
        readonly IRegionQuery<KEY, OBJECT> objectStore;

        readonly ConcurrentDictionary<int, RegionPyramid<OBJECT>> sectionPyramids = new ConcurrentDictionary<int, RegionPyramid<OBJECT>>();

        public RegionLoader(IRegionQuery<KEY, OBJECT> store) : this(store, new GridCellDimensions(2000, 2000), 3)
        {

            this.objectStore = store;
        }

        /// <summary>
        /// 
        /// </summary>
        /// <param name="store"></param>
        /// <param name="CellSize">Size of full-resolution region at level 0.</param>
        /// <param name="LevelPowerScalar">The exponent we use to map a request to a pyramid level</param>
        public RegionLoader(IRegionQuery<KEY, OBJECT> store, GridCellDimensions CellSize, double LevelPowerScalar)
        {
            this.objectStore = store;
            this.CellDimensions = CellSize;
            this.PowerScale = LevelPowerScalar;
        }



        private static bool RegionIsDueForRefresh(RegionRequestData<OBJECT> cell)
        {
            if (!cell.LastQuery.HasValue)
                return true;

            return System.TimeSpan.FromTicks(DateTime.UtcNow.Ticks - cell.LastQuery.Value.Ticks).Seconds > RegionUpdateInterval;
        }

        /// <summary>
        /// 
        /// </summary>
        /// <param name="VolumeBounds"></param>
        /// <param name="ScreenPixelSizeInVolume"></param>
        /// <param name="SectionNumber"></param>
        /// <param name="callback">A thread-safe callback function to hand the loaded objects to</param>
        public void LoadSectionAnnotationsInRegion(GridRectangle? VolumeBounds,
                                                    double ScreenPixelSizeInVolume,
                                                    int SectionNumber,
                                                    Action<ICollection<OBJECT>> OnServerObjectsLoadedCallback,
                                                    Action<ICollection<OBJECT>> FoundCachedLocalObjectsCallback, 
                                                    CancellationToken token)
        {
            if (!VolumeBounds.HasValue)
            {
                return;
            }
            /*
#if REGION_LOADING_TRACE
            Trace.WriteLine(string.Format("Loading section {0} annotation region {1}", SectionNumber, VolumeBounds.Value)) 
#endif
            */

            RegionPyramid<OBJECT> RegionPyramid = GetOrAddRegionPyramidForSection(SectionNumber);
            //If we change the magnification factor we should stop loading regions

            IRegionPyramidLevel<RegionRequestData<OBJECT>> level = RegionPyramid.GetLevel(ScreenPixelSizeInVolume);
            GridRange<RegionRequestData<OBJECT>> gridRange = level.SubGridForRegion(VolumeBounds);

            DateTime currentTime = DateTime.UtcNow;

            foreach (GridIndex iCell in gridRange.Indicies)
            {
                if (token.IsCancellationRequested)
                    return;

                int iX = iCell.X;
                int iY = iCell.Y;

                //Trace.WriteLine(string.Format("Grid Region Loading Z:{0} L:{1} X:{2} Y:{3}", SectionNumber, level.Level, iX, iY));
                //Something I learned debugging why multiple requests for the same region being launched is that the delegate for GetOrAddCell can
                //be called multiple times if no value is in the dictionary and multiple threads all attempt to add a value before a thread inserts 
                //a value.  So make GetOrAdd calls cheap.
                RegionRequestData<OBJECT> cell = level.GetOrAddCell(iCell, (icell) => new RegionRequestData<OBJECT>());
                lock (cell)
                {
                    //If we are waiting on results, add our callback to the list of functions to call when the request is complete
                    if (RegionIsDueForRefresh(cell))
                    {
                        //Trace.WriteLine(string.Format("Grid Region Loading Z:{0} L:{1} X:{2} Y:{3}", SectionNumber, level.Level, iX, iY));
                        AttachRequestForRegion(cell, level, iCell, SectionNumber, OnServerObjectsLoadedCallback);
                    }
                    else
                    {
                        //Add our callback to the list, and return any known local objects
                        if (cell.OutstandingQuery)
                        {
                            if (OnServerObjectsLoadedCallback != null)
                                cell.AddCallback(OnServerObjectsLoadedCallback);
                        }

                        if (FoundCachedLocalObjectsCallback != null)
                        {
                            //Use the callback for the known local objects
                            Task.Run(() =>
                            {
                                GridRectangle cellBounds = level.CellBounds(iCell.X, iCell.Y);
                                ICollection<OBJECT> local_objects_in_region = this.objectStore.GetLocalObjectsInRegion(SectionNumber, cellBounds, level.MinRadius);
                                FoundCachedLocalObjectsCallback?.Invoke(local_objects_in_region);
                            });
                        }
                    }
                }
            }
        }

        private RegionPyramid<OBJECT> GetOrAddRegionPyramidForSection(int SectionNumber)
        {
            return this.sectionPyramids.GetOrAdd(SectionNumber, (Number) => new RegionPyramid<OBJECT>(CellDimensions, PowerScale));
        }


        private RegionRequestData<OBJECT> CreateRegionRequest(IRegionPyramidLevel<RegionRequestData<OBJECT>> level, GridIndex iCell, int SectionNumber, Action<ICollection<OBJECT>> OnLoadCompletedCallback)
        {
            //Create a new cell and hand it the callback
            RegionRequestData<OBJECT> newCell = new RegionRequestData<OBJECT>();

            AttachRequestForRegion(newCell, level, iCell, SectionNumber, OnLoadCompletedCallback);

            return newCell;
        }

        private void AttachRequestForRegion(RegionRequestData<OBJECT> cell, IRegionPyramidLevel<RegionRequestData<OBJECT>> level, GridIndex iCell, int SectionNumber, Action<ICollection<OBJECT>> OnLoadCompletedCallback)
        {
            GridRectangle cellBounds = level.CellBounds(iCell.X, iCell.Y);
            DateTime? LastQueryUtc = cell.LastQuery;

#if DEBUG
            cell.debug_message = string.Format("S:{0} L:{1} {2}", SectionNumber, level.Level, iCell);
#endif

            if (OnLoadCompletedCallback != null)
                cell.AddCallback(OnLoadCompletedCallback);

            Debug.Assert(!cell.OutstandingQuery, "Starting a query for a region we already have an outstanding request for");

            MixedLocalAndRemoteQueryResults<KEY, OBJECT> localObjects = objectStore.GetObjectsInRegionAsync(SectionNumber, cellBounds, level.MinRadius, LastQueryUtc, cell.OnLoadCompleted);
            cell.SetQuery(localObjects.ServerRequestResult);

            /*if (localObjects.KnownObjects.Count > 0 && OnLoadCompletedCallback != null)
                OnLoadCompletedCallback(localObjects.KnownObjects);*/

#if DEBUG
            //string TraceString = string.Format("CreateRegionRequest: {0} ({1},{2}) Level:{3} MinRadius:{4}", SectionNumber, iCell.X, iCell.Y, level.Level, level.MinRadius);
            //Trace.WriteLine(TraceString, "WebAnnotation");
#endif
        }

        //How do we handle the CRUD of locations?
        //Right now we simply check that each location still belongs in the location store.
    }
}
