using System;
using System.Threading.Tasks;
using System.Diagnostics;
using System.Collections.Generic;
using System.Collections.Concurrent;
using System.Linq;
using Geometry;


namespace WebAnnotationModel
{

    /// <summary>
    /// Stores information about location queries for this region in the volume
    /// </summary>
    public class RegionRequestData<OBJECT>
        where OBJECT : class
    {
        public DateTime? LastQuery = new DateTime?();

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
                foreach (Action<ICollection<OBJECT>> a in this.OnCompletionCallbacks)
                {
                    Task.Run(() => { a(objects); });
                }

                this.OnCompletionCallbacks.Clear();

                AsyncResult = null;
            }
        }
    }

    public class AnnotationRegions<OBJECT> : BoundlessRegionPyramid<RegionRequestData<OBJECT>>
        where OBJECT : class
    {
        /// <summary>
        /// If set to true any threads using this objects should cancel loading operations
        /// </summary>
        public bool CancelRunningOperations = false;

        public AnnotationRegions(GridCellDimensions cellDimensions)
            : base(cellDimensions)
        { }
    }


    /// <summary>
    /// Return a flatter pyramid instead of a new level for every power of 2
    /// </summary>
    /// <typeparam name="OBJECT"></typeparam>
    public class RegionPyramid<OBJECT>: BoundlessRegionPyramid<RegionRequestData<OBJECT>>
        where OBJECT : class
    {
        public RegionPyramid(GridCellDimensions cellDimensions) : base(cellDimensions)
        {

        }

        /// <summary>
        /// 
        /// </summary>
        /// <param name="SinglePixelRadius"></param>
        /// <returns></returns>
        protected override int PixelDimensionToLevel(double SinglePixelRadius)
        {
            int Level = (int)Math.Floor(Math.Log(SinglePixelRadius, 6));
            if (Level < 0)
                Level = 0;
            return Level;
        }

        protected override double LevelToPixelDimension(int Level)
        {
            return Math.Pow(6.0, Level);
        }

    }

    /// <summary>
    /// Loads objects from a section based on region queries
    /// </summary>
    public class RegionLoader<KEY, OBJECT>
        where KEY : struct
        where OBJECT : class
    {
        static GridCellDimensions CellDimensions = new GridCellDimensions(10000, 10000);
        static double RegionUpdateInterval = 120;
        IRegionQuery<KEY, OBJECT> objectStore;

        ConcurrentDictionary<int, RegionPyramid<OBJECT>> sectionPyramids = new ConcurrentDictionary<int, RegionPyramid<OBJECT>>();
        
        public RegionLoader(IRegionQuery<KEY, OBJECT> store)
        {
            this.objectStore = store;
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
        public void LoadSectionAnnotationsInRegion(GridRectangle VolumeBounds,
                                                    double ScreenPixelSizeInVolume,
                                                    int SectionNumber, 
                                                    Action<ICollection<OBJECT>> OnObjectsLoadedCallback)
        {
            RegionPyramid<OBJECT> RegionPyramid = GetOrAddRegionPyramidForSection(SectionNumber);
            //If we change the magnification factor we should stop loading regions

            IRegionPyramidLevel<RegionRequestData<OBJECT>> level = RegionPyramid.GetLevel(ScreenPixelSizeInVolume);
            GridRange<RegionRequestData<OBJECT>> gridRange = level.SubGridForRegion(VolumeBounds);

            DateTime currentTime = DateTime.UtcNow;

            foreach (GridIndex iCell in gridRange.Indicies)
            {
                int iX = iCell.X;
                int iY = iCell.Y;

                RegionRequestData<OBJECT> cell = level.GetOrAddCell(iCell, (key) => { return CreateRegionRequest(level, key, SectionNumber, OnObjectsLoadedCallback); });
                lock(cell)
                {
                    //If we are waiting on results, add our callback to the list of functions to call when the request is complete
                    if (RegionIsDueForRefresh(cell))
                    {
                        AttachRequestForRegion(cell, level, iCell, SectionNumber, OnObjectsLoadedCallback);
                    }
                    else
                    {
                        //Add our callback to the list, and return any known local objects
                        if (cell.OutstandingQuery)
                        {
                            if (OnObjectsLoadedCallback != null)
                                cell.AddCallback(OnObjectsLoadedCallback);
                        }

                        //Use the callback for the known local objects
                        Task.Run(() =>
                        {
                            GridRectangle cellBounds = level.CellBounds(iCell.X, iCell.Y);
                            ICollection<OBJECT> local_objects_in_region = this.objectStore.GetLocalObjectsInRegion(SectionNumber, cellBounds, level.MinRadius);
                            if (OnObjectsLoadedCallback != null)
                                OnObjectsLoadedCallback(local_objects_in_region);
                        });
                    }
                }
            }
        }

        private RegionPyramid<OBJECT> GetOrAddRegionPyramidForSection(int SectionNumber)
        {
            return this.sectionPyramids.GetOrAdd(SectionNumber, (Number) => new RegionPyramid<OBJECT>(CellDimensions));
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

            if (OnLoadCompletedCallback != null)
                cell.AddCallback(OnLoadCompletedCallback);
            
            MixedLocalAndRemoteQueryResults<KEY, OBJECT> localObjects = objectStore.GetObjectsInRegionAsync(SectionNumber, cellBounds, level.MinRadius, LastQueryUtc, cell.OnLoadCompleted);
            cell.SetQuery(localObjects.ServerRequestResult);

            if (localObjects.KnownObjects.Count > 0 && OnLoadCompletedCallback != null)
                OnLoadCompletedCallback(localObjects.KnownObjects);

            string TraceString = string.Format("CreateRegionRequest: {0} ({1},{2}) Level:{3} MinRadius:{4}", SectionNumber, iCell.X, iCell.Y, level.Level, level.MinRadius);
            Trace.WriteLine(TraceString, "WebAnnotation");
        }

        //How do we handle the CRUD of locations?
        //Right now we simply check that each location still belongs in the location store.
    }
}
