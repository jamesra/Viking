using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Collections.Specialized;
using System.ComponentModel;
using System.Diagnostics;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using Grpc.Core;
using Viking.AnnotationServiceTypes.Interfaces;
using WebAnnotationModel.Objects;
using WebAnnotationModel.ServerInterface;

namespace WebAnnotationModel.gRPC
{
    internal abstract class SectionIndexedStore<KEY, OBJECT, SERVER_OBJECT> : ISectionIndexedStore<KEY, OBJECT>
        where KEY : struct, IEquatable<KEY>, IComparable<KEY>
        where OBJECT : IDataObjectWithKey<KEY>
        where SERVER_OBJECT : IEquatable<SERVER_OBJECT>, IDataObjectWithKey<KEY>
    {
        /// <summary>
        /// Maps sections to a sorted list of locations on that section.
        /// This collection is not guaranteed to match the ObjectToID collection.  Adding spin-locks to the Add/Remove functions could solve this if it becomes an issue.
        /// </summary>
        readonly ConcurrentDictionary<long, ConcurrentDictionary<KEY, OBJECT>> SectionToObjects =
            new ConcurrentDictionary<long, ConcurrentDictionary<KEY, OBJECT>>();

        /// <summary>
        /// When we query the database for objects on a section we store the query time for the section
        /// That way on the next query we only need to store the updates.
        /// </summary>
        private readonly ConcurrentDictionary<long, DateTime> LastQueryForSection =
            new ConcurrentDictionary<long, DateTime>();

        /// <summary>
        /// A collection of values indicating which sections have an outstanding query. 
        /// The existence of a key indicates a query is in progress
        /// </summary>
        private readonly ConcurrentDictionary<long, TaskAndToken<ConcurrentDictionary<KEY, OBJECT>>>
            OutstandingSectionQueries =
                new ConcurrentDictionary<long, TaskAndToken<ConcurrentDictionary<KEY, OBJECT>>>();

        private readonly IStoreWithKey<KEY, OBJECT> Store;

        private readonly IServerAnnotationsClientFactory<IServerAnnotationsBySectionClient<KEY, SERVER_OBJECT>> ClientFactory;

        private readonly IStoreServerQueryResultsHandler<KEY, OBJECT, SERVER_OBJECT> ServerObjProcessor;

        private readonly ISectionQueryLogger QueryLogger;

        internal SectionIndexedStore(IStoreWithKey<KEY, OBJECT> store,
            IServerAnnotationsClientFactory<IServerAnnotationsBySectionClient<KEY, SERVER_OBJECT>> clientFactory,
            IStoreServerQueryResultsHandler<KEY, OBJECT, SERVER_OBJECT> serverObjProcessor,
            ISectionQueryLogger queryLog)
        {
            Store = store;
            ClientFactory = clientFactory;
            ServerObjProcessor = serverObjProcessor;
            QueryLogger = queryLog;
            Store.CollectionChanged += OnStoreCollectionChanged;
        }

        private void OnStoreCollectionChanged(object sender, NotifyCollectionChangedEventArgs e)
        {
            //Todo, order by section number and optimize removal
            foreach (var obj in e.OldItems)
            {
                if (obj is OBJECT o)
                    TryRemoveObjectFromSection(o);
            }

            foreach (var obj in e.NewItems)
            {
                if (obj is OBJECT o)
                    TryAddObjectToSection(o);
            }
        }

        public ConcurrentDictionary<KEY, OBJECT> GetLocalObjectsForSection(long SectionNumber)
        {
            bool Success = SectionToObjects.TryGetValue(SectionNumber, out var SectionLocationLinks);
            if (Success)
            {
                return SectionLocationLinks;
            }

            return new ConcurrentDictionary<KEY, OBJECT>();
        }

        private readonly SemaphoreSlim SectionQueryLock = new SemaphoreSlim(1);
        

        public Task<System.Collections.Concurrent.ConcurrentDictionary<KEY, OBJECT>> GetObjectsForSectionAsync(
            long SectionNumber, QueryTargets targets)
        {
            throw new NotImplementedException();
        }

        /// <summary>
        /// Free all resources and objects related to the section.
        /// </summary>
        /// <param name="SectionNumber"></param>
        /// <returns>True if the section resources were freed.</returns>
        public bool RemoveSection(long SectionNumber)
        {
            // 
            bool success = SectionToObjects.TryRemove(SectionNumber, out var sectionObjects
            );
            if (!success)
                return true;

            Store.ForgetLocally(sectionObjects.Keys.ToArray());
            sectionObjects.Clear();
            return true;
        }

        public virtual async Task<ConcurrentDictionary<KEY, OBJECT>> GetObjectsForSectionAsync(long SectionNumber)
        {
            TaskAndToken<ConcurrentDictionary<KEY, OBJECT>> queryTask;
            if (OutstandingSectionQueries.TryGetValue(SectionNumber, out queryTask))
            {
                if (false == queryTask.TokenSource.IsCancellationRequested)
                    return await queryTask.Task;
            }

            try
            {
                await SectionQueryLock.WaitAsync();

                if (OutstandingSectionQueries.TryGetValue(SectionNumber, out queryTask))
                {
                    if (false == queryTask.TokenSource.IsCancellationRequested)
                        return await queryTask.Task;
                }

                var cancelSource = new CancellationTokenSource();
                queryTask = new TaskAndToken<ConcurrentDictionary<KEY, OBJECT>>(
                    _GetObjectsForSectionAsync(SectionNumber, cancelSource.Token), cancelSource);
                OutstandingSectionQueries.TryAdd(SectionNumber, queryTask);
            }
            finally
            {
                SectionQueryLock.Release();
            }

            return await queryTask.Task;
        }

        /// <summary>
        /// Runs a query against the server.  Does not take care of any logic beyond running the query, such as ensuring another identical query is already running
        /// </summary>
        /// <param name="SectionNumber"></param>
        /// <returns></returns>
        protected virtual async Task<ConcurrentDictionary<KEY, OBJECT>> _GetObjectsForSectionAsync(long SectionNumber,
            CancellationToken token)
        {
            DateTime StartTime = DateTime.UtcNow;

            var client = ClientFactory.GetOrCreate();
            try
            { 
                var lastQueryExecuted = GetLastQueryTimeForSection(SectionNumber);
                var results = await client.GetAsync(SectionNumber, lastQueryExecuted, token);
                if (token.IsCancellationRequested)
                    return new ConcurrentDictionary<KEY, OBJECT>();

                var TraceQueryEnd = results.QueryTime;
                var inventory = await ParseSectionQuery(SectionNumber, results);
                var TraceParseEnd = DateTime.UtcNow;
                
                QueryLogger?.LogQuery(nameof(OBJECT), SectionNumber, inventory.ObjectsInStore.Count, StartTime, TraceQueryEnd,
                    TraceParseEnd); 

                await ServerObjProcessor.EndBatch(inventory);

                if (token.IsCancellationRequested)
                    return new ConcurrentDictionary<KEY, OBJECT>();

                return GetLocalObjectsForSection(SectionNumber); 
            }
            catch (Exception e)
            {
                Trace.WriteLine(e.ToString());
                Trace.WriteLine(e.Message);
            }

            return new ConcurrentDictionary<KEY, OBJECT>();
        }

        private async Task<ChangeInventory<OBJECT>> ParseSectionQuery(long sectionNumber, ServerUpdate<KEY, SERVER_OBJECT> results)
        {
            if (TrySetLastQueryTimeForSection(sectionNumber, results.QueryTime))
            {
                return await ServerObjProcessor.ProcessServerUpdate(results);
            }
            else
            {
                Trace.WriteLine($"{this.GetType()} ignoring stale query results for section {sectionNumber}",
                    "WebAnnotation");

                return null;
            }
        }

        private DateTime GetLastQueryTimeForSection(long SectionNumber)
        {
            return LastQueryForSection.TryGetValue(SectionNumber, out var LastQuery) ? LastQuery : DateTime.MinValue;
        }

        /// <summary>
        /// Updates the last query timestamp with the specified value
        /// </summary>
        /// <param name="SectionNumber"></param>
        /// <param name="TicksAtQueryExecute"></param>
        /// <returns>True if the passed timestamp was newer than the previous value</returns>
        private bool TrySetLastQueryTimeForSection(long SectionNumber, DateTime queryExecuteTimeUTC)
        {
            var value = LastQueryForSection.AddOrUpdate(SectionNumber, queryExecuteTimeUTC,
                (_, existing) => existing < queryExecuteTimeUTC ? queryExecuteTimeUTC : existing);
            return queryExecuteTimeUTC == value;
        }

        public virtual void CancelExcessSectionQueries(int LoadingSectionLimit)
        {
            if (OutstandingSectionQueries.Count <= LoadingSectionLimit)
                return;

            //Sort the outstanding queries and kill the oldest
            var tasks = OutstandingSectionQueries.ToArray();
            var sortedTaskList = tasks.OrderByDescending(v => v.Value.CreationTime).ToList();

            while (OutstandingSectionQueries.Count > LoadingSectionLimit && sortedTaskList.Count > 0)
            {
                var kvPair = sortedTaskList[0];
                sortedTaskList.RemoveAt(0);

                if (OutstandingSectionQueries.TryRemove(kvPair.Key, out var entry))
                {
                    entry.TokenSource.Cancel();
                }
            }
        }

        /// <summary>
        /// This is called to instruct the store to eliminate objects from the oldest section query.
        /// This is done to save memory
        /// </summary>
        /// <param name="LoadedSectionLimit">Number of loaded sections we want in memory</param>
        /// <param name="LoadingSectionLimit">Number of sections we want to be actively loading</param>
        public virtual void FreeExcessSections(int LoadedSectionLimit, int LoadingSectionLimit)
        {
            CancelExcessSectionQueries(LoadingSectionLimit);

            //Return if we are under the limit
            if (LastQueryForSection.Count < LoadedSectionLimit)
                return;

            var listQueryTimes = LastQueryForSection.ToArray().OrderByDescending(i => i.Value).ToList();

            while (listQueryTimes.Count > LoadedSectionLimit)
            {
                var kvPair = listQueryTimes[0];
                listQueryTimes.RemoveAt(0);

                DateTime cutoffTime = kvPair.Value;

                var sectionNumber = kvPair.Key;

                //If it has an outstanding query it lives
                if (OutstandingSectionQueries.ContainsKey(sectionNumber))
                    continue;

                if (LastQueryForSection.TryRemove(sectionNumber, out var _))
                    RemoveSection(sectionNumber);
            }
        }
         
        private bool TryAddObjectToSection(OBJECT obj)
        {
            bool Success = false;
            if (obj is ISectionIndex si)
            {
                ConcurrentDictionary<KEY, OBJECT> listSectionLocations;
                listSectionLocations = SectionToObjects.GetOrAdd(si.Section,
                    (key) => new ConcurrentDictionary<KEY, OBJECT>());

                Success = listSectionLocations.TryAdd(obj.ID, obj);
                if (!Success)
                {
                    //Somebody already added the object to the list sections collection...
                    Trace.WriteLine("Race condition in LocationStore.Add", "WebAnnotation");
                    Debug.Assert(false);
                }
            }

            return Success;
        }

        private bool TryRemoveObjectFromSection(OBJECT obj)
        { 
            if (obj is ISectionIndex si)
            {
                //Remove it from the mapping of sections to locations on that section  
                if (SectionToObjects.TryGetValue(si.Section, out var listSectionLocations))
                {
                    return listSectionLocations.TryRemove(obj.ID, out var listSectionLocationsObj);
                } 
            }

            return false;
        }
    }
}
