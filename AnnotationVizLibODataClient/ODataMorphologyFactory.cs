using Microsoft.OData.Client;
using ODataClient.ConnectomeDataModel;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;

namespace AnnotationVizLib.OData
{
    public static class ODataMorphologyFactory
    {
        /// <summary>
        /// Synchronously builds a morphology graph from structure IDs
        /// </summary>
        public static MorphologyGraph FromOData(ICollection<long> StructureIDs, bool include_children, Uri Endpoint) => FromODataAsync(StructureIDs, include_children, Endpoint).GetAwaiter().GetResult();

        /// <summary>
        /// Synchronously builds a morphology graph from <b>location</b> IDs (not structure IDs).  The parent
        /// structure of the supplied locations is loaded so the resulting graph contains the locations, their
        /// neighbors, and the edges between them.
        /// </summary>
        public static MorphologyGraph FromODataLocationIDs(ICollection<long> LocationIDs, Uri Endpoint, int hops = 0) => FromODataLocationIDsAsync(LocationIDs, Endpoint, hops).GetAwaiter().GetResult();

        #region Async Helper Methods

        /// <summary>
        /// Asynchronously retrieves the scale from the OData service
        /// </summary>
        private static async Task<UnitsAndScale.Scale> GetScaleAsync(
            Container container,
            CancellationToken cancellationToken = default)
        {
            try
            {
                var scaleTask = Task.Run(() => container.Scale().GetValue(), cancellationToken);
                var scale = await scaleTask;
                return scale.ToGeometryScale();
            }
            catch (Exception ex) when (ex is not OperationCanceledException)
            {
                throw new InvalidOperationException($"Failed to retrieve scale: {ex.Message}", ex);
            }
        }

        /// <summary>
        /// The OData client query is synchronous, so every request in flight occupies a thread pool thread for
        /// its whole duration.  Fanning out one task per ID lets a structure with hundreds of children queue more
        /// requests than the pool has threads; they then sit unstarted until the transport timeout cancels them,
        /// which surfaces as TaskCanceledException even though the server is answering in well under a second.
        /// </summary>
        private const int MaxConcurrentRequests = 8;

        /// <summary>
        /// Run <paramref name="query"/> over every item with at most <see cref="MaxConcurrentRequests"/> in flight.
        /// </summary>
        private static async Task<TResult[]> RunThrottledAsync<TSource, TResult>(
            IEnumerable<TSource> source,
            Func<TSource, TResult> query,
            CancellationToken cancellationToken)
        {
            using SemaphoreSlim throttle = new(MaxConcurrentRequests);

            var tasks = source.Select(async item =>
            {
                await throttle.WaitAsync(cancellationToken);
                try
                {
                    return await Task.Run(() => query(item), cancellationToken);
                }
                finally
                {
                    throttle.Release();
                }
            }).ToArray();

            return await Task.WhenAll(tasks);
        }

        /// <summary>
        /// Runs a query and returns every entity, following the nextLink until the service stops issuing one.
        ///
        /// Enumerating a <see cref="DataServiceQuery{T}"/> directly yields only the first page and drops the
        /// nextLink silently, so any result larger than the service page size is truncated without an error.
        /// </summary>
        /// <param name="onEntry">
        /// Invoked for each entity while it is still the materializer's current entry, which is the only point at
        /// which <see cref="QueryOperationResponse.GetContinuation{T}(IEnumerable{T})"/> can report the nextLink of
        /// one of its expanded collections. Calling that after enumeration finishes throws "the collection is not
        /// part of the current entry", so nested links must be captured here and followed afterwards.
        /// </param>
        private static List<T> ExecuteAllPages<T>(
            Container container,
            DataServiceQuery<T> query,
            CancellationToken cancellationToken,
            Action<QueryOperationResponse<T>, T> onEntry = null)
        {
            List<T> all = [];
            QueryOperationResponse<T> response = (QueryOperationResponse<T>)query.Execute();

            while (true)
            {
                cancellationToken.ThrowIfCancellationRequested();

                foreach (T entry in response)
                {
                    onEntry?.Invoke(response, entry);
                    all.Add(entry);
                }

                DataServiceQueryContinuation<T> continuation = response.GetContinuation();
                if (continuation is null)
                    break;

                response = container.Execute(continuation);
            }

            return all;
        }

        /// <summary>
        /// Appends the remaining pages of an $expand'ed collection onto the collection the materializer built.
        ///
        /// The service caps each expanded collection at its page size (2048 on RC1) and reports the remainder as a
        /// nested nextLink, e.g. "Locations@odata.nextLink". The typed client materializes the first page and never
        /// requests the rest, so the truncation is invisible: structure 476 arrived with 2048 of its 3161 locations,
        /// leaving holes in its mesh that made correctly placed child synapses look like they floated in space.
        /// </summary>
        private static void DrainExpandedCollection<T>(
            Container container,
            ICollection<T> collection,
            DataServiceQueryContinuation<T> continuation,
            CancellationToken cancellationToken)
        {
            while (continuation != null)
            {
                cancellationToken.ThrowIfCancellationRequested();

                QueryOperationResponse<T> page = container.Execute(continuation);
                foreach (T item in page)
                    collection.Add(item);

                continuation = page.GetContinuation();
            }
        }

        /// <summary>
        /// Reads the nextLink of an expanded collection, treating a collection the response never materialized
        /// (an $expand the query did not ask for) as complete rather than as an error.
        /// </summary>
        private static DataServiceQueryContinuation<T> NestedContinuation<T>(
            QueryOperationResponse response,
            ICollection<T> collection)
        {
            if (collection is null)
                return null;

            try
            {
                return response.GetContinuation(collection);
            }
            catch (ArgumentException)
            {
                return null;
            }
        }

        /// <summary>
        /// Runs a structure query to exhaustion, including the expanded Locations and Children of every structure.
        /// </summary>
        private static List<Structure> ExecuteStructureQuery(
            Container container,
            DataServiceQuery<Structure> query,
            CancellationToken cancellationToken)
        {
            List<(ICollection<Location> Collection, DataServiceQueryContinuation<Location> Continuation)> pendingLocations = [];
            List<(ICollection<Structure> Collection, DataServiceQueryContinuation<Structure> Continuation)> pendingChildren = [];

            List<Structure> structures = ExecuteAllPages(container, query, cancellationToken, (response, s) =>
            {
                DataServiceQueryContinuation<Location> locations = NestedContinuation(response, s.Locations);
                if (locations != null)
                    pendingLocations.Add((s.Locations, locations));

                DataServiceQueryContinuation<Structure> children = NestedContinuation(response, s.Children);
                if (children != null)
                    pendingChildren.Add((s.Children, children));
            });

            foreach (var (collection, continuation) in pendingLocations)
                DrainExpandedCollection(container, collection, continuation, cancellationToken);

            foreach (var (collection, continuation) in pendingChildren)
                DrainExpandedCollection(container, collection, continuation, cancellationToken);

            return structures;
        }

        /// <summary>
        /// Loads multiple structures by their IDs
        /// </summary>
        private static async Task<List<Structure>> LoadStructuresByIDsAsync(
            Container container,
            ICollection<long> structureIDs,
            CancellationToken cancellationToken = default)
        {
            try
            {
                List<Structure>[] results = await RunThrottledAsync(structureIDs, id =>
                    ExecuteStructureQuery(container,
                        (DataServiceQuery<Structure>)container.Structures
                            .Expand(s => s.Locations)
                            .Expand(s => s.Type)
                            .Expand(s => s.Children)
                            .Where(s => s.ID == id),
                        cancellationToken), cancellationToken);

                List<Structure> allStructures = [];
                foreach (var list in results)
                {
                    cancellationToken.ThrowIfCancellationRequested();
                    allStructures.AddRange(list);
                }

                return allStructures;
            }
            catch (Exception ex) when (ex is not OperationCanceledException)
            {
                throw new InvalidOperationException($"Failed to load structures by IDs: {ex.Message}", ex);
            }
        }

        /// <summary>
        /// Loads multiple structures by their type IDs in parallel
        /// </summary>
        private static async Task<List<Structure>> LoadStructuresByTypeIDsAsync(
            Container container,
            ICollection<long> typeIDs,
            CancellationToken cancellationToken = default)
        {
            try
            {
                List<Structure>[] results = await RunThrottledAsync(typeIDs, typeId =>
                    ExecuteStructureQuery(container,
                        (DataServiceQuery<Structure>)container.Structures
                            .Expand(s => s.Locations)
                            .Expand(s => s.Type)
                            .Expand(s => s.Children)
                            .Where(s => s.TypeID == typeId),
                        cancellationToken), cancellationToken);

                List<Structure> allStructures = [];
                foreach (var list in results)
                {
                    cancellationToken.ThrowIfCancellationRequested();
                    allStructures.AddRange(list);
                }

                return allStructures;
            }
            catch (Exception ex) when (ex is not OperationCanceledException)
            {
                throw new InvalidOperationException($"Failed to load structures by type IDs: {ex.Message}", ex);
            }
        }

        /// <summary>
        /// Loads multiple locations by their IDs in parallel
        /// </summary>
        private static async Task<List<Location>> LoadLocationsByIDsAsync(
            Container container,
            ICollection<long> locationIDs,
            CancellationToken cancellationToken = default)
        {
            try
            {
                List<Location>[] results = await RunThrottledAsync(locationIDs.Distinct(), id =>
                    ExecuteAllPages(container,
                        (DataServiceQuery<Location>)container.Locations.Where(l => l.ID == id),
                        cancellationToken), cancellationToken);

                List<Location> allLocations = [];
                foreach (var list in results)
                {
                    cancellationToken.ThrowIfCancellationRequested();
                    allLocations.AddRange(list);
                }

                return allLocations;
            }
            catch (Exception ex) when (ex is not OperationCanceledException)
            {
                throw new InvalidOperationException($"Failed to load locations by IDs: {ex.Message}", ex);
            }
        }

        /// <summary>
        /// Asynchronously loads location links for multiple structures in parallel
        /// </summary>
        private static async Task LoadStructureLocationLinksAsync(
            Container container,
            ICollection<Structure> structures,
            CancellationToken cancellationToken = default)
        {
            try
            {
                await RunThrottledAsync(
                    structures.Where(s => s.LocationLinks is null || !s.LocationLinks.Any()),
                    s =>
                    {
                        List<LocationLink> links = ExecuteAllPages(container, container.StructureLocationLinks(s.ID), cancellationToken);

                        s.LocationLinks = new DataServiceCollection<LocationLink>(null, TrackingMode.None);
                        foreach (var link in links)
                        {
                            s.LocationLinks.Add(link);
                        }

                        return true;
                    },
                    cancellationToken);
            }
            catch (Exception ex) when (ex is not OperationCanceledException)
            {
                throw new InvalidOperationException($"Failed to load structure location links: {ex.Message}", ex);
            }
        }

        #endregion

        #region Public Async Methods

        /// <summary>
        /// Asynchronously builds morphology graphs for structures matching the given type IDs
        /// </summary>
        public static async Task<MorphologyGraph> FromODataByTypeIDsAsync(
            ICollection<long> TypeIDs,
            Uri Endpoint,
            bool include_children = false,
            CancellationToken cancellationToken = default)
        {
            Container container = new(Endpoint)
            {
                MergeOption = Microsoft.OData.Client.MergeOption.NoTracking
            };

            var scale = await GetScaleAsync(container, cancellationToken);
            MorphologyGraph rootGraph = new(0, scale);

            if (TypeIDs is null || TypeIDs.Count == 0)
                return rootGraph;

            // Load structures by type IDs in parallel
            var structures = await LoadStructuresByTypeIDsAsync(container, TypeIDs, cancellationToken);

            // Load location links in parallel
            await LoadStructureLocationLinksAsync(container, structures, cancellationToken);

            // Build morphology graphs
            await MorphologyForStructuresAsync(container, rootGraph, structures, include_children, scale, cancellationToken);

            return rootGraph;
        }

        /// <summary>
        /// Asynchronously builds morphology graphs for the specified structure IDs
        /// </summary>
        public static async Task<MorphologyGraph> FromODataAsync(
            ICollection<long> StructureIDs,
            bool include_children,
            Uri Endpoint,
            CancellationToken cancellationToken = default)
        {
            Container container = new(Endpoint)
            {
                MergeOption = Microsoft.OData.Client.MergeOption.NoTracking
            };

            var scale = await GetScaleAsync(container, cancellationToken);
            return await FromODataAsync(StructureIDs, include_children, Endpoint, scale, cancellationToken);
        }

        /// <summary>
        /// Asynchronously builds morphology graphs for the specified structure IDs using a pre-fetched scale
        /// </summary>
        public static async Task<MorphologyGraph> FromODataAsync(
            ICollection<long> StructureIDs,
            bool include_children,
            Uri Endpoint,
            UnitsAndScale.Scale scale,
            CancellationToken cancellationToken = default)
        {
            Container container = new(Endpoint)
            {
                MergeOption = Microsoft.OData.Client.MergeOption.NoTracking
            };

            MorphologyGraph rootGraph = new(0, scale);

            if (StructureIDs is null || StructureIDs.Count == 0)
                return rootGraph;

            // Load structures in parallel
            var structures = await LoadStructuresByIDsAsync(container, StructureIDs, cancellationToken);

            // Load location links in parallel
            await LoadStructureLocationLinksAsync(container, structures, cancellationToken);

            // Build morphology graphs
            await MorphologyForStructuresAsync(container, rootGraph, structures, include_children, scale, cancellationToken);

            return rootGraph;
        }

        /// <summary>
        /// Asynchronously builds morphology graph for specified location IDs.  When <paramref name="hops"/>
        /// is greater than zero the resulting graph is limited to the seed locations plus every location
        /// reachable within <paramref name="hops"/> location-link traversals (a breadth-first neighborhood).
        /// When <paramref name="hops"/> is zero or negative the entire parent structure is loaded.
        /// </summary>
        public static async Task<MorphologyGraph> FromODataLocationIDsAsync(
            ICollection<long> LocationIDs,
            Uri Endpoint,
            int hops = 0,
            CancellationToken cancellationToken = default)
        {
            Container container = new(Endpoint)
            {
                MergeOption = Microsoft.OData.Client.MergeOption.NoTracking
            };

            var scale = await GetScaleAsync(container, cancellationToken);
            MorphologyGraph rootGraph = new(0, scale);

            if (LocationIDs is null || LocationIDs.Count == 0)
                return rootGraph;

            // Load locations in parallel
            var locations = await LoadLocationsByIDsAsync(container, LocationIDs, cancellationToken);

            if (locations.Count == 0)
                return rootGraph;

            // The seed locations may belong to more than one structure; load every distinct parent.
            long[] parentIds = [.. locations.Select(l => l.ParentID).Distinct()];
            var parents = await LoadStructuresByIDsAsync(container, parentIds, cancellationToken);

            if (parents.Count == 0)
                return rootGraph;

            // Load location links for all parents
            await LoadStructureLocationLinksAsync(container, parents, cancellationToken);

            long primaryStructureId = locations[0].ParentID;
            Structure primaryParent = parents.FirstOrDefault(p => p.ID == primaryStructureId) ?? parents[0];

            // hops <= 0: legacy behavior, load the entire (primary) parent structure.
            if (hops <= 0)
            {
                MorphologyGraph fullGraph = MorphologyForStructure(primaryParent, scale);

                foreach (var loc in locations)
                {
                    cancellationToken.ThrowIfCancellationRequested();

                    if (!fullGraph.Nodes.TryGetValue((ulong)loc.ID, out _))
                        fullGraph.AddNode(new MorphologyNode((ulong)loc.ID, new ODataLocationAdapter(loc, scale), fullGraph));
                }

                AddLocationEdges(fullGraph, [.. primaryParent.LocationLinks]);

                return fullGraph;
            }

            // hops >= 1: build a breadth-first neighborhood around the seed locations.
            Dictionary<ulong, Location> locById = [];
            Dictionary<ulong, HashSet<ulong>> adjacency = [];
            List<LocationLink> allLinks = [];

            void Link(ulong from, ulong to)
            {
                if (!adjacency.TryGetValue(from, out var set))
                {
                    set = [];
                    adjacency[from] = set;
                }
                set.Add(to);
            }

            foreach (var p in parents)
            {
                foreach (var loc in p.Locations)
                    locById[(ulong)loc.ID] = loc;

                foreach (var link in p.LocationLinks)
                {
                    allLinks.Add(link);
                    Link((ulong)link.A, (ulong)link.B);
                    Link((ulong)link.B, (ulong)link.A);
                }
            }

            // Seed the neighborhood with the requested locations.
            HashSet<ulong> neighborhood = [];
            List<ulong> frontier = [];
            foreach (var loc in locations)
            {
                ulong id = (ulong)loc.ID;
                locById[id] = loc;
                if (neighborhood.Add(id))
                    frontier.Add(id);
            }

            // Expand the frontier one ring at a time, up to 'hops' traversals.
            for (int h = 0; h < hops && frontier.Count > 0; h++)
            {
                cancellationToken.ThrowIfCancellationRequested();

                List<ulong> next = [];
                foreach (ulong id in frontier)
                {
                    if (!adjacency.TryGetValue(id, out var neighbors))
                        continue;

                    foreach (ulong nb in neighbors)
                    {
                        if (neighborhood.Add(nb))
                            next.Add(nb);
                    }
                }

                frontier = next;
            }

            MorphologyGraph graph = new((ulong)primaryStructureId, scale, new ODataStructureAdapter(primaryParent));

            foreach (ulong id in neighborhood)
            {
                if (locById.TryGetValue(id, out var loc))
                    graph.AddNode(new MorphologyNode(id, new ODataLocationAdapter(loc, scale), graph));
            }

            // AddLocationEdges only adds links whose endpoints are both present in the graph, so edges
            // dangling outside the neighborhood are naturally dropped.
            AddLocationEdges(graph, [.. allLinks]);

            return graph;
        }

        #endregion

        /// <summary>
        /// Asynchronously processes structures and builds morphology subgraphs
        /// </summary>
        private static async Task MorphologyForStructuresAsync(
            Container container,
            MorphologyGraph rootGraph,
            ICollection<Structure> structures,
            bool include_children,
            UnitsAndScale.Scale scale,
            CancellationToken cancellationToken = default)
        {
            foreach (Structure s in structures)
            {
                cancellationToken.ThrowIfCancellationRequested();

                MorphologyGraph graph = MorphologyForStructure(s, scale);
                if (graph is null)
                    continue;

                rootGraph.AddSubgraph(graph);

                if (include_children && s.Children != null && s.Children.Any())
                {
                    // Load child structures
                    List<long> childIds = [.. s.Children.Select(c => (long)c.ID)];
                    var childStructures = await LoadStructuresByIDsAsync(container, childIds, cancellationToken);

                    // Load location links for children
                    await LoadStructureLocationLinksAsync(container, childStructures, cancellationToken);

                    // Recursively process children
                    await MorphologyForStructuresAsync(container, graph, childStructures, include_children, scale, cancellationToken);
                }
            }
        }

        private static MorphologyGraph MorphologyForStructure(Structure s, UnitsAndScale.IScale scale)
        {
            Location[] locations = [.. s.Locations];
            LocationLink[] location_links = [.. s.LocationLinks];


            if (locations.Length <= 0)
            {
                return null;
            }

            MorphologyGraph graph = new((ulong)s.ID, scale, new ODataStructureAdapter(s));

            foreach (Location loc in locations)
            {

                graph.AddNode(new MorphologyNode((ulong)loc.ID, new ODataLocationAdapter(loc, scale), graph));
            }

            AddLocationEdges(graph, location_links);

            return graph;
        }

        private static void AddLocationEdges(MorphologyGraph graph, LocationLink[] location_links)
        {
            if (location_links is null)
                return;

            foreach (LocationLink loc_link in location_links)
            {
                // Only add links if both nodes exist in the graph
                if (graph.Nodes.TryGetValue((ulong)loc_link.A, out _) && graph.Nodes.TryGetValue((ulong)loc_link.B, out _))
                {
                    MorphologyEdge edge = new(graph, loc_link.A, loc_link.B);
                    // Idempotent: the same links may be added more than once (e.g. MorphologyForStructure adds
                    // them, then FromODataLocationIDsAsync adds them again). Skip duplicates so the underlying
                    // SortedList does not throw "An item with the same key has already been added".
                    if (graph.Edges.ContainsKey(edge))
                        continue;

                    graph.AddEdge(edge);
                }
            }
        }
    }
}
