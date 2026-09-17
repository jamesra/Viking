using Viking.AnnotationServiceTypes.Interfaces;
using ODataClient.ConnectomeDataModel;
using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using System.Threading;

namespace AnnotationVizLib.OData
{
    public class ODataNeuronFactory
    {
        // Static cache of instances per endpoint
        private static readonly ConcurrentDictionary<Uri, ODataNeuronFactory> instances = new();

        // Instance-level structure type dictionary
        private SortedDictionary<long, StructureType> IDToStructureType = null;
        private readonly SemaphoreSlim loadLock = new(1, 1);
        private readonly Task structureTypeLoadTask = null;

        private readonly Uri endpoint;

        private ODataNeuronFactory(Uri endpoint)
        {
            this.endpoint = endpoint;

            // Start loading structure types asynchronously
            structureTypeLoadTask = Task.Run(async () =>
            {
                try
                {
                    Container container = new(endpoint)
                    {
                        MergeOption = Microsoft.OData.Client.MergeOption.NoTracking
                    };
                    var structureTypes = await container.StructureTypes.GetAllPagesToListAsync();
                    await PopulateStructureTypeDictionaryAsync(structureTypes);
                }
                catch
                {
                    // Silently handle errors - will be loaded on demand if needed
                }
            });
        }

        /// <summary>
        /// Gets or creates an instance for the specified endpoint
        /// </summary>
        private static ODataNeuronFactory GetOrCreateInstance(Uri endpoint) => instances.GetOrAdd(endpoint, ep => new ODataNeuronFactory(ep));

        /// <summary>
        /// Synchronously builds a neuron graph from OData service
        /// </summary>
        public static NeuronGraph FromOData(ICollection<long> StructureIDs, uint numHops, Uri Endpoint) => FromODataAsync(StructureIDs, numHops, Endpoint).GetAwaiter().GetResult();

        /// <summary>
        /// Asynchronously builds a neuron graph from OData service
        /// </summary>
        public static async Task<NeuronGraph> FromODataAsync(
            ICollection<long> StructureIDs,
            uint numHops,
            Uri Endpoint,
            CancellationToken cancellationToken = default)
        {
            // Get or create the instance for this endpoint
            ODataNeuronFactory factory = GetOrCreateInstance(Endpoint);
            return await factory.BuildGraphAsync(StructureIDs, numHops, cancellationToken);
        }

        /// <summary>
        /// Asynchronously builds a neuron graph from structure IDs
        /// </summary>
        private async Task<NeuronGraph> BuildGraphAsync(
            ICollection<long> StructureIDs,
            uint numHops,
            CancellationToken cancellationToken)
        {
            NeuronGraph graph = new();

            if (StructureIDs is null || StructureIDs.Count == 0)
                return graph;

            Container container = new(endpoint)
            {
                MergeOption = Microsoft.OData.Client.MergeOption.NoTracking
            };

            // Ensure structure types are loaded
            await EnsureStructureTypesLoadedAsync(container, cancellationToken);

            // Fetch all data in parallel for better performance
            var networkStructuresTask = GetNetworkStructuresAsync(container, StructureIDs, numHops, cancellationToken);
            var childStructuresTask = GetNetworkChildStructuresAsync(container, StructureIDs, numHops, cancellationToken);
            var structureLinksTask = GetNetworkLinksAsync(container, StructureIDs, numHops, cancellationToken);

            await Task.WhenAll(networkStructuresTask, childStructuresTask, structureLinksTask);

            var networkStructures = await networkStructuresTask;
            var childStructures = await childStructuresTask;
            var structureLinks = await structureLinksTask;

            // Build structure dictionary for lookups
            SortedDictionary<ulong, Structure> IDToStructure = [];

            // Merge child structures into parent structures
            foreach (Structure child in childStructures)
            {
                if (child.ParentID.HasValue && networkStructures.TryGetValue((ulong)child.ParentID.Value, out var parent))
                {
                    parent.Children ??= new Microsoft.OData.Client.DataServiceCollection<Structure>(null, Microsoft.OData.Client.TrackingMode.None);
                    parent.Children.Add(child);
                }
            }

            // Populate the structure dictionary with all structures (parents and children)
            PopulateStructureDictionary(networkStructures.Values, IDToStructure);

            // Attach structure links to their source and target structures
            foreach (StructureLink link in structureLinks)
            {
                if (IDToStructure.TryGetValue((ulong)link.SourceID, out var source))
                {
                    source.SourceOfLinks ??= new Microsoft.OData.Client.DataServiceCollection<StructureLink>(null, Microsoft.OData.Client.TrackingMode.None);
                    source.SourceOfLinks.Add(link);
                }

                if (IDToStructure.TryGetValue((ulong)link.TargetID, out var target))
                {
                    target.TargetOfLinks ??= new Microsoft.OData.Client.DataServiceCollection<StructureLink>(null, Microsoft.OData.Client.TrackingMode.None);
                    target.TargetOfLinks.Add(link);
                }
            }

            // Add nodes to graph
            AddStructuresAsNodes(networkStructures.Values, graph);

            // Add edges to graph
            AddStructureLinksAsEdges(structureLinks, IDToStructure, graph);

            return graph;
        }

        /// <summary>
        /// Ensures structure types are loaded asynchronously
        /// </summary>
        private async Task EnsureStructureTypesLoadedAsync(Container container, CancellationToken cancellationToken)
        {
            if (IDToStructureType != null)
                return;

            // Wait for async load to complete if it's still running
            if (structureTypeLoadTask != null && !structureTypeLoadTask.IsCompleted)
            {
                try
                {
                    await structureTypeLoadTask;
                    if (IDToStructureType != null)
                        return;
                }
                catch
                {
                    // If async load failed, try loading now
                }
            }

            // If still not loaded, load now
            if (IDToStructureType is null)
            {
                var types = await container.StructureTypes.GetAllPagesToListAsync(cancellationToken);
                await PopulateStructureTypeDictionaryAsync(types);
            }
        }

        /// <summary>
        /// Populates the structure type dictionary from a list
        /// </summary>
        private async Task PopulateStructureTypeDictionaryAsync(IEnumerable<StructureType> types)
        {
            await loadLock.WaitAsync();
            try
            {
                if (IDToStructureType != null)
                    return;

                IDToStructureType = [];

                foreach (StructureType t in types)
                {
                    IDToStructureType.Add(t.ID, t);
                }
            }
            finally
            {
                loadLock.Release();
            }
        }

        /// <summary>
        /// Fetches network parent/cell structures asynchronously
        /// </summary>
        private static async Task<Dictionary<ulong, Structure>> GetNetworkStructuresAsync(
            Container container,
            ICollection<long> StructureIDs,
            uint numHops,
            CancellationToken cancellationToken)
        {
            try
            {
                var structures = await container.Network(StructureIDs, (int)numHops)
                    .GetAllPagesToListAsync(cancellationToken);

                Dictionary<ulong, Structure> dictionary = [];
                foreach (var structure in structures)
                {
                    cancellationToken.ThrowIfCancellationRequested();
                    dictionary[(ulong)structure.ID] = structure;
                }

                return dictionary;
            }
            catch (Exception ex) when (ex is not OperationCanceledException)
            {
                throw new InvalidOperationException($"Failed to fetch network structures: {ex.Message}", ex);
            }
        }

        /// <summary>
        /// Fetches network child structures asynchronously
        /// </summary>
        private static async Task<List<Structure>> GetNetworkChildStructuresAsync(
            Container container,
            ICollection<long> StructureIDs,
            uint numHops,
            CancellationToken cancellationToken)
        {
            try
            {
                var childStructures = await container.NetworkChildStructures(StructureIDs, (int)numHops)
                    .GetAllPagesToListAsync(cancellationToken);

                return childStructures;
            }
            catch (Exception ex) when (ex is not OperationCanceledException)
            {
                throw new InvalidOperationException($"Failed to fetch network child structures: {ex.Message}", ex);
            }
        }

        /// <summary>
        /// Fetches network structure links asynchronously
        /// </summary>
        private static async Task<List<StructureLink>> GetNetworkLinksAsync(
            Container container,
            ICollection<long> StructureIDs,
            uint numHops,
            CancellationToken cancellationToken)
        {
            try
            {
                var structureLinks = await container.NetworkLinks(StructureIDs, (int)numHops)
                    .GetAllPagesToListAsync(cancellationToken);

                return structureLinks;
            }
            catch (Exception ex) when (ex is not OperationCanceledException)
            {
                throw new InvalidOperationException($"Failed to fetch network structure links: {ex.Message}", ex);
            }
        }


        /// <summary>
        /// Recursively populates the structure dictionary
        /// </summary>
        private static void PopulateStructureDictionary(ICollection<Structure> structs, SortedDictionary<ulong, Structure> IDToStructure)
        {
            foreach (Structure s in structs)
            {
                if (IDToStructure.ContainsKey((ulong)s.ID))
                    continue;

                IDToStructure.Add((ulong)s.ID, s);

                if (s.Children != null)
                {
                    PopulateStructureDictionary(s.Children, IDToStructure);
                }
            }
        }

        /// <summary>
        /// Add all top-level structures as nodes in our graph
        /// </summary>
        private static void AddStructuresAsNodes(ICollection<Structure> structs, NeuronGraph graph)
        {
            foreach (IStructureReadOnly s in structs.Select(s => new ODataStructureAdapter(s)))
            {
                NeuronNode node = new((long)s.ID, s);
                graph.AddNode(node);
            }
        }

        /// <summary>
        /// Add structure links as edges in the graph
        /// </summary>
        private void AddStructureLinksAsEdges(
            ICollection<StructureLink> structureLinks,
            SortedDictionary<ulong, Structure> IDToStructure,
            NeuronGraph graph)
        {
            foreach (StructureLink link in structureLinks)
            {
                // Look up source and target structures
                if (!IDToStructure.TryGetValue((ulong)link.SourceID, out var linkSource) ||
                    !IDToStructure.TryGetValue((ulong)link.TargetID, out var linkTarget))
                {
                    continue; // Skip if either structure not found
                }

                // Both structures must have parents to create an edge
                if (!linkTarget.ParentID.HasValue || !linkSource.ParentID.HasValue)
                {
                    continue;
                }

                // Get source type name if available
                string sourceTypeName = "";
                if (IDToStructureType != null && IDToStructureType.TryGetValue((long)linkSource.TypeID, out var structureType))
                {
                    sourceTypeName = structureType.Name;
                }

                // Create or update edge
                NeuronEdge edge = new(
                    (long)linkSource.ParentID.Value,
                    (long)linkTarget.ParentID.Value,
                    new ODataStructureLinkAdapter(link),
                    sourceTypeName);

                if (graph.Edges.TryGetValue(edge, out var existingEdge))
                {
                    // Add link to existing edge
                    existingEdge.AddLink(new ODataStructureLinkAdapter(link));
                }
                else
                {
                    // Add new edge to graph
                    graph.AddEdge(edge);
                }
            }
        }
    }
}
