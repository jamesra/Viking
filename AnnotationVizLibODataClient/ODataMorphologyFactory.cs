using Microsoft.OData.Client;
using ODataClient.ConnectomeDataModel;
using System;
using System.Collections.Concurrent;
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
        /// Keep this low: openresty in front of RPC1 returns 502 when too many expands / link queries pile up.
        /// </summary>
        private const int MaxConcurrentRequests = 4;

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

                QueryOperationResponse<T> page = ExecuteContinuationWithRetry(container, continuation, cancellationToken);
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

            List<Structure> structures = ExecuteAllPagesWithRetry(container, query, cancellationToken, (response, s) =>
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
                    ExecuteAllPagesWithRetry(container,
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
                        List<LocationLink> links = ExecuteAllPagesWithRetry(container, container.StructureLocationLinks(s.ID), cancellationToken);

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
        /// Synapse / junction type IDs — blobs, not Z-traveling processes. Excluded from neighbor hop corpus.
        /// </summary>
        static readonly HashSet<long> ExcludedNeighborTypeIds = [28, 34, 35, 73, 85];

        /// <summary>
        /// Soft cap so a huge AABB does not pull an entire volume into memory for the PoC.
        /// </summary>
        public const int MaxNeighborStructures = 32;

        /// <summary>
        /// Cap metadata lookups before filtering to <see cref="MaxNeighborStructures"/>. Loading Type/Parent for
        /// every ParentID in a Muller AABB (often 1000+) 502s the gateway and stalls Init for minutes.
        /// </summary>
        const int MaxNeighborMetadataQueries = 96;

        /// <summary>
        /// Cap on per-section Location queries during neighbor discovery (full Z stacks would 502 the gateway).
        /// </summary>
        const int MaxNeighborSectionQueries = 40;

        /// <summary>
        /// Discover top-level structure IDs with at least one location inside the padded AABB of
        /// <paramref name="root"/>'s cells, across a subsample of occupied sections. VolumeX/Y filters use
        /// unscaled DB units; <paramref name="radiusNm"/> pads the scaled morphology bbox.
        /// </summary>
        public static async Task<List<long>> FindNearbyStructureIdsAsync(
            MorphologyGraph root,
            Uri endpoint,
            double radiusNm,
            CancellationToken cancellationToken = default)
        {
            if (root is null || endpoint is null)
                return [];

            List<MorphologyGraph> cells = [.. root.Subgraphs.Values.Where(sg => sg.StructureID != 0)];
            if (cells.Count == 0 && root.StructureID != 0 && root.Nodes.Count > 0)
                cells.Add(root);
            if (cells.Count == 0)
                return [];

            HashSet<ulong> excludeIds = [.. cells.Select(c => c.StructureID)];

            Geometry.Box union = default;
            foreach (MorphologyGraph cell in cells)
            {
                Geometry.Box box = cell.NodesBoundingBox;
                if (box == default)
                    continue;
                union = union == default ? box : Geometry.Box.Union(union, box);
            }

            if (union == default)
                return [];

            double scaleX = root.scale?.X.Value ?? 1.0;
            double scaleY = root.scale?.Y.Value ?? 1.0;
            if (scaleX <= 0 || scaleY <= 0)
            {
                scaleX = 1.0;
                scaleY = 1.0;
            }

            double pad = Math.Max(0, radiusNm);
            double minX = (union.MinCorner.X - pad) / scaleX;
            double maxX = (union.MaxCorner.X + pad) / scaleX;
            double minY = (union.MinCorner.Y - pad) / scaleY;
            double maxY = (union.MaxCorner.Y + pad) / scaleY;

            int[] allSections = [.. cells
                .SelectMany(c => c.Nodes.Values.Select(n => (int)Math.Round(n.UnscaledZ)))
                .Distinct()
                .OrderBy(z => z)];

            if (allSections.Length == 0)
                return [];

            int[] sections = SubsampleSections(allSections, MaxNeighborSectionQueries);
            Console.WriteLine($"Neighbor discover: querying {sections.Length}/{allSections.Length} sections in padded AABB");

            Container container = new(endpoint)
            {
                MergeOption = MergeOption.NoTracking
            };

            ConcurrentDictionary<long, byte> parentIds = new();

            // Throttle: unbounded Task.WhenAll over a Muller Z-stack 502s openresty.
            await RunThrottledAsync(sections, z =>
            {
                cancellationToken.ThrowIfCancellationRequested();
                try
                {
                    long sectionZ = z;
                    DataServiceQuery<Location> query = (DataServiceQuery<Location>)container.Locations
                        .Where(l => l.Z == sectionZ
                            && l.VolumeX >= minX && l.VolumeX <= maxX
                            && l.VolumeY >= minY && l.VolumeY <= maxY);

                    List<Location> locs = ExecuteAllPagesWithRetry(container, query, cancellationToken);
                    foreach (Location loc in locs)
                    {
                        if (excludeIds.Contains((ulong)loc.ParentID))
                            continue;
                        parentIds.TryAdd(loc.ParentID, 0);
                    }
                }
                catch (Exception ex) when (ex is not OperationCanceledException)
                {
                    Console.WriteLine($"Neighbor discover Z={z}: {ex.Message}");
                }

                return 0;
            }, cancellationToken);

            if (parentIds.IsEmpty)
                return [];

            // Metadata only (no Locations expand) — LoadStructuresByIDsAsync would 502 on dozens of full expands.
            long[] candidates = [.. parentIds.Keys.OrderBy(id => id)];
            if (candidates.Length > MaxNeighborMetadataQueries)
            {
                Console.WriteLine($"Neighbor discover: sampling {MaxNeighborMetadataQueries}/{candidates.Length} ParentIDs for metadata (cap before full morphology load)");
                candidates = SubsampleIds(candidates, MaxNeighborMetadataQueries);
            }

            Console.WriteLine($"Neighbor discover: {candidates.Length} candidate ParentIDs; loading structure metadata");
            List<Structure> structures = await LoadStructureMetadataByIDsAsync(container, candidates, cancellationToken);

            List<long> neighbors = [.. structures
                .Where(s => !s.ParentID.HasValue)
                .Where(s => !ExcludedNeighborTypeIds.Contains(s.TypeID))
                .Select(s => s.ID)
                .Distinct()
                .OrderBy(id => id)];

            if (neighbors.Count > MaxNeighborStructures)
            {
                Console.WriteLine($"Neighbor discover: truncating {neighbors.Count} structures to {MaxNeighborStructures}");
                neighbors = neighbors.Take(MaxNeighborStructures).ToList();
            }

            return neighbors;
        }

        /// <summary>
        /// Load morphology for structures near <paramref name="root"/> (no children) for hop-field sampling.
        /// Returns null when nothing was found or the OData gateway fails (non-fatal for meshing).
        /// </summary>
        public static async Task<MorphologyGraph> LoadNeighborHopSourcesAsync(
            MorphologyGraph root,
            Uri endpoint,
            double radiusNm,
            CancellationToken cancellationToken = default)
        {
            try
            {
                List<long> ids = await FindNearbyStructureIdsAsync(root, endpoint, radiusNm, cancellationToken);
                if (ids.Count == 0)
                {
                    Console.WriteLine("Neighbor discover: no nearby structures found");
                    return null;
                }

                Console.WriteLine($"Neighbor discover: loading {ids.Count} structures within {radiusNm:F0} nm");
                return await FromODataAsync(ids, include_children: false, endpoint, cancellationToken);
            }
            catch (Exception ex) when (ex is not OperationCanceledException)
            {
                Console.WriteLine($"Neighbor discover failed (continuing without neighbor corpus): {ex.Message}");
                return null;
            }
        }

        static int[] SubsampleSections(int[] sections, int maxQueries)
        {
            if (sections.Length <= maxQueries)
                return sections;

            int stride = (int)Math.Ceiling(sections.Length / (double)maxQueries);
            List<int> sampled = [];
            for (int i = 0; i < sections.Length; i += stride)
                sampled.Add(sections[i]);
            if (sampled[^1] != sections[^1])
                sampled.Add(sections[^1]);
            return [.. sampled];
        }

        static long[] SubsampleIds(long[] ids, int maxQueries)
        {
            if (ids.Length <= maxQueries)
                return ids;

            int stride = (int)Math.Ceiling(ids.Length / (double)maxQueries);
            List<long> sampled = [];
            for (int i = 0; i < ids.Length; i += stride)
                sampled.Add(ids[i]);
            if (sampled[^1] != ids[^1])
                sampled.Add(ids[^1]);
            return [.. sampled];
        }

        /// <summary>
        /// Structure rows only (Type / ParentID) — no Locations expand.
        /// </summary>
        static async Task<List<Structure>> LoadStructureMetadataByIDsAsync(
            Container container,
            ICollection<long> structureIDs,
            CancellationToken cancellationToken)
        {
            List<Structure>[] results = await RunThrottledAsync(structureIDs, id =>
            {
                try
                {
                    return ExecuteAllPagesWithRetry(container,
                        (DataServiceQuery<Structure>)container.Structures
                            .Expand(s => s.Type)
                            .Where(s => s.ID == id),
                        cancellationToken);
                }
                catch (Exception ex) when (ex is not OperationCanceledException)
                {
                    Console.WriteLine($"Neighbor metadata Structure {id}: {ex.Message}");
                    return [];
                }
            }, cancellationToken);

            List<Structure> all = [];
            foreach (List<Structure> list in results)
                all.AddRange(list);
            return all;
        }

        static List<T> ExecuteAllPagesWithRetry<T>(
            Container container,
            DataServiceQuery<T> query,
            CancellationToken cancellationToken,
            Action<QueryOperationResponse<T>, T> onEntry = null,
            int maxAttempts = 5)
        {
            Exception last = null;
            for (int attempt = 1; attempt <= maxAttempts; attempt++)
            {
                cancellationToken.ThrowIfCancellationRequested();
                try
                {
                    return ExecuteAllPages(container, query, cancellationToken, onEntry);
                }
                catch (Exception ex) when (ex is not OperationCanceledException && IsTransientODataFailure(ex))
                {
                    last = ex;
                    int delayMs = 500 * attempt * attempt;
                    Console.WriteLine($"OData transient failure (attempt {attempt}/{maxAttempts}), retry in {delayMs}ms: {ex.Message}");
                    Thread.Sleep(delayMs);
                }
            }

            throw last ?? new InvalidOperationException("OData query failed with no exception");
        }

        static QueryOperationResponse<T> ExecuteContinuationWithRetry<T>(
            Container container,
            DataServiceQueryContinuation<T> continuation,
            CancellationToken cancellationToken,
            int maxAttempts = 5)
        {
            Exception last = null;
            for (int attempt = 1; attempt <= maxAttempts; attempt++)
            {
                cancellationToken.ThrowIfCancellationRequested();
                try
                {
                    return container.Execute(continuation);
                }
                catch (Exception ex) when (ex is not OperationCanceledException && IsTransientODataFailure(ex))
                {
                    last = ex;
                    int delayMs = 500 * attempt * attempt;
                    Console.WriteLine($"OData continuation transient failure (attempt {attempt}/{maxAttempts}), retry in {delayMs}ms: {ex.Message}");
                    Thread.Sleep(delayMs);
                }
            }

            throw last ?? new InvalidOperationException("OData continuation failed with no exception");
        }

        static bool IsTransientODataFailure(Exception ex)
        {
            for (Exception e = ex; e != null; e = e.InnerException)
            {
                string msg = e.Message ?? string.Empty;
                if (msg.Contains("502", StringComparison.OrdinalIgnoreCase)
                    || msg.Contains("503", StringComparison.OrdinalIgnoreCase)
                    || msg.Contains("504", StringComparison.OrdinalIgnoreCase)
                    || msg.Contains("Bad Gateway", StringComparison.OrdinalIgnoreCase)
                    || msg.Contains("Gateway Time-out", StringComparison.OrdinalIgnoreCase)
                    || msg.Contains("timed out", StringComparison.OrdinalIgnoreCase))
                    return true;
            }

            return false;
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

            // OData $expand + nextLink drain can surface the same Location twice; Graph.AddNode rejects duplicate keys.
            HashSet<ulong> seenLocationIds = [];
            foreach (Location loc in locations)
            {
                ulong id = (ulong)loc.ID;
                if (!seenLocationIds.Add(id))
                    continue;

                graph.AddNode(new MorphologyNode(id, new ODataLocationAdapter(loc, scale), graph));
            }

            if (graph.Nodes.Count == 0)
                return null;

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
