using ODataClient.ConnectomeDataModel;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;

namespace AnnotationVizLib.OData
{
    public static class ODataSpatialDataFactory
    {
        #region Public API Methods

        /// <summary>
        /// Synchronously appends spatial data to a neuron graph from OData service
        /// </summary>
        public static void AppendSpatialDataFromOData(
            NeuronGraph graph,
            Uri Endpoint,
            ICollection<long> IDs,
            uint Hops)
        {
            AppendSpatialDataFromODataAsync(graph, Endpoint, IDs, Hops).GetAwaiter().GetResult();
        }

        /// <summary>
        /// Asynchronously appends spatial data to a neuron graph from OData service
        /// </summary>
        public static async Task AppendSpatialDataFromODataAsync(
            NeuronGraph graph,
            Uri Endpoint,
            ICollection<long> IDs,
            uint Hops,
            CancellationToken cancellationToken = default)
        {
            Container container = new Container(Endpoint)
            {
                MergeOption = Microsoft.OData.Client.MergeOption.NoTracking
            };

            // Fetch neuron and edge spatial data in parallel
            var neuronDataTask = AppendNeuronSpatialDataAsync(graph, container, IDs, Hops, cancellationToken);
            var edgeDataTask = AppendAreaToConnectionsAsync(graph, container, IDs, Hops, cancellationToken);

            await Task.WhenAll(neuronDataTask, edgeDataTask);
        }

        /// <summary>
        /// Synchronously appends spatial data for a single neuron node
        /// </summary>
        public static void AppendNeuronSpatialData(
            NeuronNode node,
            Container container)
        {
            AppendNeuronSpatialDataAsync(node, container).GetAwaiter().GetResult();
        }

        /// <summary>
        /// Asynchronously appends spatial data for a single neuron node
        /// </summary>
        public static async Task AppendNeuronSpatialDataAsync(
            NeuronNode node,
            Container container,
            CancellationToken cancellationToken = default)
        {
            try
            {
                var spatialCacheTask = Task.Run(() =>
                    container.StructureSpatialCaches
                        .Where(s => s.ID == (long)node.Key)
                        .FirstOrDefault(), cancellationToken);

                var cache = await spatialCacheTask;
                if (cache != null)
                {
                    AppendSpatialCacheToNode(node, cache);
                }
            }
            catch (Exception ex) when (!(ex is OperationCanceledException))
            {
                throw new InvalidOperationException(
                    $"Failed to append spatial data for node {node.Key}: {ex.Message}", ex);
            }
        }

        #endregion

        #region Private Async Methods

        /// <summary>
        /// Asynchronously appends neuron spatial data to all nodes in the graph
        /// </summary>
        private static async Task AppendNeuronSpatialDataAsync(
            NeuronGraph graph,
            Container container,
            ICollection<long> IDs,
            uint Hops,
            CancellationToken cancellationToken = default)
        {
            try
            {
                // Build query for network spatial data
                var spatialCacheQuery = (IDs == null || IDs.Count == 0) && graph.Nodes.Count > 0
                    ? container.NetworkSpatialData(new List<long>(), 0)
                    : container.NetworkSpatialData(IDs, (int)Hops);

                // Fetch all spatial cache data
                var spatialCachesTask = Task.Run(() => spatialCacheQuery.ToList(), cancellationToken);
                var spatialCaches = await spatialCachesTask;

                // Append to graph nodes
                foreach (var cache in spatialCaches)
                {
                    cancellationToken.ThrowIfCancellationRequested();

                    if (graph.Nodes.TryGetValue((long)cache.ID, out var node))
                    {
                        AppendSpatialCacheToNode(node, cache);
                    }
                }
            }
            catch (Exception ex) when (!(ex is OperationCanceledException))
            {
                throw new InvalidOperationException(
                    $"Failed to append neuron spatial data: {ex.Message}", ex);
            }
        }

        /// <summary>
        /// Asynchronously appends edge spatial data to all edges in the graph
        /// </summary>
        private static async Task AppendAreaToConnectionsAsync(
            NeuronGraph graph,
            Container container,
            ICollection<long> IDs,
            uint Hops,
            CancellationToken cancellationToken = default)
        {
            try
            {
                // Build lookup map
                var childToEdgeMap = BuildChildToEdgeMap(graph);

                // Build query for network edge spatial data
                var edgeSpatialQuery = (IDs == null || IDs.Count == 0) && graph.Nodes.Count > 0
                    ? container.NetworkEdgeSpatialData(new List<long>(), 0)
                    : container.NetworkEdgeSpatialData(IDs, (int)Hops);

                // Fetch all edge spatial cache data
                var edgeSpatialCachesTask = Task.Run(() => edgeSpatialQuery.ToList(), cancellationToken);
                var edgeSpatialCaches = await edgeSpatialCachesTask;

                // Append to graph edges
                foreach (var cache in edgeSpatialCaches)
                {
                    cancellationToken.ThrowIfCancellationRequested();

                    AddSpatialDataToEdges(cache, childToEdgeMap);
                }
            }
            catch (Exception ex) when (!(ex is OperationCanceledException))
            {
                throw new InvalidOperationException(
                    $"Failed to append edge spatial data: {ex.Message}", ex);
            }
        }

        #endregion

        #region Helper Methods - Data Appending

        /// <summary>
        /// Appends spatial cache data to a neuron node's attributes
        /// </summary>
        private static void AppendSpatialCacheToNode(
            NeuronNode node,
            StructureSpatialCache cache)
        {
            // Add all spatial cache properties to node attributes
            node.Attributes["Area"] = cache.Area;
            node.Attributes["Volume"] = cache.Volume;
            node.Attributes["MaxDimension"] = cache.MaxDimension;
            node.Attributes["MinZ"] = cache.MinZ;
            node.Attributes["MaxZ"] = cache.MaxZ;

            if (cache.BoundingRect != null)
                node.Attributes["BoundingRect"] = cache.BoundingRect;
        }

        /// <summary>
        /// Adds spatial data to edges based on child structure spatial cache
        /// </summary>
        private static void AddSpatialDataToEdges(
            StructureSpatialCache childCache,
            Dictionary<ulong, SortedSet<NeuronEdge>> childToEdgeMap)
        {
            ulong childKey = (ulong)childCache.ID;

            if (!childToEdgeMap.TryGetValue(childKey, out var edges))
                return;

            // Add area data
            double area = childCache.Area;

            foreach (var edge in edges.Where(e => e.SourceIDs.Contains(childKey)))
            {
                edge.TotalSourceArea += area;
            }

            foreach (var edge in edges.Where(e => e.TargetIDs.Contains(childKey)))
            {
                edge.TotalTargetArea += area;
            }

            // Add Z range data
            double minZ = childCache.MinZ;
            double maxZ = childCache.MaxZ;

            foreach (var edge in edges.Where(e => e.SourceIDs.Contains(childKey)))
            {
                if (edge.MinZ > minZ)
                    edge.MinZ = minZ;

                if (edge.MaxZ < maxZ)
                    edge.MaxZ = maxZ;
            }
        }

        #endregion

        #region Helper Methods - Map Building

        /// <summary>
        /// Builds a map from child structure IDs to their parent neuron node IDs
        /// </summary>
        private static Dictionary<ulong, long> BuildChildToParentMap(NeuronGraph graph)
        {
            var childToParent = new Dictionary<ulong, long>();

            foreach (NeuronNode node in graph.Nodes.Values)
            {
                foreach (ulong childID in node.EdgeSourceChildStructureIDs)
                {
                    childToParent.TryAdd(childID, node.Key);
                }

                foreach (ulong childID in node.EdgeTargetChildStructureIDs)
                {
                    childToParent.TryAdd(childID, node.Key);
                }
            }

            return childToParent;
        }

        /// <summary>
        /// Builds a map from child structure IDs to the edges they belong to
        /// </summary>
        private static Dictionary<ulong, SortedSet<NeuronEdge>> BuildChildToEdgeMap(NeuronGraph graph)
        {
            var idToEdge = new Dictionary<ulong, SortedSet<NeuronEdge>>();

            foreach (NeuronEdge edge in graph.Edges.Values)
            {
                foreach (ulong sourceID in edge.SourceIDs)
                {
                    if (!idToEdge.TryGetValue(sourceID, out var edgeSet))
                    {
                        edgeSet = new SortedSet<NeuronEdge>();
                        idToEdge[sourceID] = edgeSet;
                    }
                    edgeSet.Add(edge);
                }

                foreach (ulong targetID in edge.TargetIDs)
                {
                    if (!idToEdge.TryGetValue(targetID, out var edgeSet))
                    {
                        edgeSet = new SortedSet<NeuronEdge>();
                        idToEdge[targetID] = edgeSet;
                    }
                    edgeSet.Add(edge);
                }
            }

            return idToEdge;
        }

        #endregion
    }
}

