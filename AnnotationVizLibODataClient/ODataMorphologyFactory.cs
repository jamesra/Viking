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
        /// Loads multiple structures by their IDs in parallel
        /// </summary>
        private static async Task<List<Structure>> LoadStructuresByIDsAsync(
            Container container,
            ICollection<long> structureIDs,
            CancellationToken cancellationToken = default)
        {
            try
            {
                var tasks = structureIDs.Select(id => Task.Run(() =>
                    container.Structures
                        .Expand(s => s.Locations)
                        .Expand(s => s.Type)
                        .Expand(s => s.Children)
                        .Where(s => s.ID == id)
                        .ToList(), cancellationToken)
                ).ToArray();

                List<Structure>[] results = await Task.WhenAll(tasks);
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
                var tasks = typeIDs.Select(typeId => Task.Run(() =>
                    container.Structures
                        .Expand(s => s.Locations)
                        .Expand(s => s.Type)
                        .Expand(s => s.Children)
                        .Where(s => s.TypeID == typeId)
                        .ToList(), cancellationToken)
                ).ToArray();

                List<Structure>[] results = await Task.WhenAll(tasks);
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
                var tasks = locationIDs.Distinct().Select(id => Task.Run(() =>
                    container.Locations
                        .Where(l => l.ID == id)
                        .ToList(), cancellationToken)
                ).ToArray();

                List<Location>[] results = await Task.WhenAll(tasks);
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
                var tasks = structures
                    .Where(s => s.LocationLinks is null || !s.LocationLinks.Any())
                    .Select(s => Task.Run(() =>
                    {
                        List<LocationLink> links = [.. container.StructureLocationLinks(s.ID)];

                        s.LocationLinks = new Microsoft.OData.Client.DataServiceCollection<LocationLink>(null, Microsoft.OData.Client.TrackingMode.None);
                        foreach (var link in links)
                        {
                            s.LocationLinks.Add(link);
                        }
                    }, cancellationToken))
                    .ToArray();

                await Task.WhenAll(tasks);
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
