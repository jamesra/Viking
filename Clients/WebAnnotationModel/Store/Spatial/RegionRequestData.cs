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

        public void ClearLastQuery() => LastQuery = null;

        /// <summary>
        /// This lock should be taken by callers before calling public methods.
        /// Internally this lock should be taken by async Task methods
        /// </summary>
        public readonly SemaphoreSlim Lock = new SemaphoreSlim(1);

        public readonly Rectangle Bounds;

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

        public Task CurrentQuery => QueryTask;

        private Task QueryTask = null;
        private CancellationTokenSource QueryCts;
        private CancellationToken QueryCancellationToken = CancellationToken.None;

        /// <summary>
        /// Functions to call when the load is complete
        /// </summary>
        private readonly List<Action<ICollection<OBJECT>>> OnCompletionCallbacks; 

        public RegionRequestData(Rectangle bounds)
        {
            Bounds = bounds;
            OnCompletionCallbacks = new List<Action<ICollection<OBJECT>>>();
        }

        /// <summary>
        /// Arm this cell's cancel source before the stream starts so off-screen cancel can see it immediately.
        /// </summary>
        public void PrepareQuery(CancellationTokenSource cts)
        {
            Debug.Assert(QueryTask == null, $"{nameof(QueryTask)} should be null before preparing a new query");
            QueryCts = cts;
            QueryCancellationToken = cts != null ? cts.Token : CancellationToken.None;
        }

        /// <summary>
        /// Bind this cell to an in-flight server stream. The stream is cancelled only via
        /// <see cref="CancelQuery"/> when the cell leaves the padded visible region.
        /// </summary>
        public void SetQuery(Task queryTask, CancellationTokenSource cts)
        {
            Debug.Assert(QueryTask == null, $"{nameof(QueryTask)} should be null before setting a new task");
            QueryTask = queryTask;
            if (cts != null)
            {
                QueryCts = cts;
                QueryCancellationToken = cts.Token;
            }

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
        /// Abort this cell's stream because it is no longer in the padded visible region.
        /// Pan/zoom wait tokens must not call this for cells that still intersect that region.
        /// </summary>
        public void CancelQuery()
        {
            CancellationTokenSource cts = QueryCts;
            if (cts == null)
                return;

            try
            {
                cts.Cancel();
            }
            catch (ObjectDisposedException)
            {
            }
        }

        /// <summary>
        /// Indicates a new query can be started for this cell
        /// </summary>
        public void SetQueryCompletedOrAborted()
        {
            if (QueryTask == null && QueryCts == null)
                return;

            bool hadTrackedQuery = QueryTask != null;
            OnCompletionCallbacks.Clear();
            QueryTask = null;
            QueryCancellationToken = CancellationToken.None;
            CancellationTokenSource cts = QueryCts;
            QueryCts = null;
#if DEBUG
            if (hadTrackedQuery)
            {
                System.Threading.Interlocked.Decrement(ref RegionRequestData<OBJECT>.NumOutstandingQueries);
                ActiveRequests.TryRemove(DebugMessage, out var removed_message);
            }
#endif
            if (cts == null)
                return;

            try
            {
                cts.Dispose();
            }
            catch (ObjectDisposedException)
            {
            }
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
            bool locked = false;

            try
            {
                await Lock.WaitAsync(QueryCancellationToken);
                locked = true;
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
                if (locked)
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