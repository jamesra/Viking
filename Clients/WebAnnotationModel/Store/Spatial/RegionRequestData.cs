using Geometry;
using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
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
        public DateTime? LastQuery { get; private set; } = DateTime.MinValue;

        /// <summary>
        /// This lock should be taken by callers before calling public methods.
        /// Internally this lock should be taken by async Task methods
        /// </summary>
        public readonly SemaphoreSlim Lock = new SemaphoreSlim(1);

        public readonly GridRectangle Bounds;

#if DEBUG
        private static int NumOutstandingQueries = 0;

        /// <summary>
        /// Optional message for debugging
        /// </summary>
        public string DebugMessage;

        static readonly ConcurrentDictionary<string, string> ActiveRequests = new ConcurrentDictionary<string, string>();
#endif


        public bool HasBeenQueried => LastQuery.HasValue;

        /// <summary>
        /// True if a query has been sent to the server but has not returned
        /// </summary>
        public bool OutstandingQuery => QueryTask != null
                                        && QueryCancellationToken.IsCancellationRequested == false;

        private Task QueryTask = null;
        private CancellationToken QueryCancellationToken = CancellationToken.None;

        /// <summary>
        /// Functions to call when the load is complete
        /// </summary>
        private readonly List<Action<ICollection<OBJECT>>> OnCompletionCallbacks; 

        public RegionRequestData(GridRectangle bounds)
        {
            Bounds = bounds;
            OnCompletionCallbacks = new List<Action<ICollection<OBJECT>>>();
        }

        /// <summary>
        /// Set the Task which is making a server request for this region
        /// </summary>
        /// <param name="queryTask"></param>
        public void SetQuery(Task queryTask, CancellationToken aToken)
        {
            Debug.Assert(QueryTask == null, $"{nameof(QueryTask)} should be null before setting a new task");
            this.QueryTask = queryTask;
            this.LastQuery = DateTime.UtcNow;
            this.QueryCancellationToken = aToken;

#if DEBUG
            System.Threading.Interlocked.Increment(ref RegionRequestData<OBJECT>.NumOutstandingQueries);
            ActiveRequests.TryAdd(DebugMessage, DebugMessage);

            if (RegionRequestData<OBJECT>.NumOutstandingQueries > 30)
            {
                Trace.WriteLine($"{RegionRequestData<OBJECT>.NumOutstandingQueries} Outstanding queries");
            }
#endif 
        }

        /// <summary>
        /// Indicates a new query can be started for this cell
        /// </summary>
        public void SetQueryCompletedOrAborted()
        { 
            OnCompletionCallbacks.Clear();
            QueryTask = null;
            QueryCancellationToken = CancellationToken.None;
#if DEBUG
            System.Threading.Interlocked.Decrement(ref RegionRequestData<OBJECT>.NumOutstandingQueries);
            ActiveRequests.TryRemove(DebugMessage, out var removed_message);
#endif
        }

        /// <summary>
        /// Add an action to be called when the current query is completed
        /// </summary>
        /// <param name="callback"></param>
        public void AddCallback(Action<ICollection<OBJECT>> callback)
        {
            if (callback == null)
                return;

            OnCompletionCallbacks.Add(callback);
        } 

        /// <summary>
        /// This should be called when a query is completed for the region this object represents
        /// </summary>
        /// <param name="objects"></param>
        public async Task OnLoadCompleted(ICollection<OBJECT> inventory, DateTime queryCompletionTime)
        {
            var tasks = new List<Task>(OnCompletionCallbacks.Count);

            try
            {
                await Lock.WaitAsync(QueryCancellationToken);
                LastQuery = queryCompletionTime;
                
                foreach (var cb in OnCompletionCallbacks)
                {
                    if (QueryCancellationToken.IsCancellationRequested)
                        return;

                    tasks.Add(Task.Run(() => cb(inventory)));
                }
            }
            finally
            {
                SetQueryCompletedOrAborted(); 
#if DEBUG
                ReportQueryStats();
#endif 
                Lock.Release();
            }

            Task.WaitAll(tasks.ToArray(), QueryCancellationToken);
        }

#if DEBUG 
        /// <summary>
        /// A debug method to record query completion
        /// </summary>
        /// <param name="objects"></param>
        private void ReportQueryStats()
        { 
            if (OnCompletionCallbacks.Count > 1)
                Trace.WriteLine($"{this.OnCompletionCallbacks.Count} callbacks registered in region");
        }
#endif
}
}