using System;
using System.Threading.Tasks;
using System.Diagnostics;
using System.Collections.Generic;
using System.Collections.Concurrent;
using System.Linq;
using Geometry;


namespace WebAnnotationModel
{
    public class RegionLocalObjects<OBJECT>
    {
        public readonly OBJECT[] Objects;

        public RegionLocalObjects(ICollection<OBJECT> RegionObjects)
        {
            Debug.Assert(RegionObjects != null);
            Objects = RegionObjects.ToArray();
        }
    }

    /// <summary>
    /// Stores information about location queries for this region in the volume
    /// </summary>
    public class RegionRequestData<OBJECT>
        where OBJECT : class
    {
        public DateTime? LastQuery = new DateTime?();

        private RegionLocalObjects<OBJECT> RegionObjects = null;

        public bool HasObjects
        {
            get { return RegionObjects != null && RegionObjects.Objects != null && RegionObjects.Objects.Length > 0; }
        }

        public OBJECT[] Objects
        {
            get
            {
                if (this.HasObjects)
                {
                    return RegionObjects.Objects;
                }

                return null;
            }
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

            lock(this)
            {
                OnCompletionCallbacks.Add(callback);
            }
        }

        public void OnLoadCompleted(ICollection<OBJECT> objects)
        {
            lock(this)
            {
                this.RegionObjects = new RegionLocalObjects<OBJECT>(objects);

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
    /// Loads objects from a section based on region queries
    /// </summary>
    public class RegionLoader<KEY, OBJECT>
        where KEY : struct
        where OBJECT : class
    {
        static GridCellDimensions CellDimensions = new GridCellDimensions(1000, 1000);
        static double RegionUpdateInterval = 120;
        IRegionQuery<KEY, OBJECT> objectStore;

        ConcurrentDictionary<int, BoundlessRegionPyramid<RegionRequestData<OBJECT>>> sectionPyramids = new ConcurrentDictionary<int, Geometry.BoundlessRegionPyramid<RegionRequestData<OBJECT>>>();
        ConcurrentDictionary<int, BoundlessRegionPyramid<RegionLocalObjects<OBJECT>>> localObjectsPyramids = new ConcurrentDictionary<int, Geometry.BoundlessRegionPyramid<RegionLocalObjects<OBJECT>>>();

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
            BoundlessRegionPyramid<RegionRequestData<OBJECT>> RegionPyramid = GetOrAddRegionPyramidForSection(SectionNumber);
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
                    if (cell.HasObjects && OnObjectsLoadedCallback != null)
                    {
                        Task.Run(() => { OnObjectsLoadedCallback(cell.Objects); }); 
                    }

                    //If we are waiting on results, add our callback to the list of functions to call when the request is complete
                    if (cell.OutstandingQuery)
                    {
                        cell.AddCallback(OnObjectsLoadedCallback);
                    }
                    else if (RegionIsDueForRefresh(cell))
                    {
                        AttachRequestForRegion(cell, level, iCell, SectionNumber, OnObjectsLoadedCallback);
                    }
                }
            }
        }

        private BoundlessRegionPyramid<RegionRequestData<OBJECT>> GetOrAddRegionPyramidForSection(int SectionNumber)
        {
            return this.sectionPyramids.GetOrAdd(SectionNumber, (Number) => new BoundlessRegionPyramid<RegionRequestData<OBJECT>>(CellDimensions));
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

            cell.AddCallback(OnLoadCompletedCallback);

            //2/25/2016: There are no local objects for region requests in the current implementation
            MixedLocalAndRemoteQueryResults<KEY, OBJECT> localObjects = objectStore.GetObjectsInRegionAsync(SectionNumber, cellBounds, level.MinRadius, LastQueryUtc, cell.OnLoadCompleted);
            cell.SetQuery(localObjects.ServerRequestResult);

            if (localObjects.KnownObjects.Values.Count > 0)
                OnLoadCompletedCallback(localObjects.KnownObjects.Values);

            string TraceString = string.Format("CreateRegionRequest: {0} ({1},{2}) Level:{3} MinRadius:{4}", SectionNumber, iCell.X, iCell.Y, level.Level, level.MinRadius);
            Trace.WriteLine(TraceString, "WebAnnotation");
        }
    }
}
