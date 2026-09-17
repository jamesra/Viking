using ODataClient.ConnectomeDataModel;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;

namespace AnnotationVizLib.OData
{
    public static class ODataMotifFactory
    {
        /// <summary>
        /// Synchronously builds a motif graph from OData service
        /// </summary>
        public static MotifGraph FromOData(Uri endpoint) => FromODataAsync(endpoint).GetAwaiter().GetResult();

        /// <summary>
        /// Asynchronously builds a motif graph from OData service
        /// </summary>
        public static async Task<MotifGraph> FromODataAsync(
            Uri endpoint,
            CancellationToken cancellationToken = default)
        {
            Container container = new(endpoint)
            {
                MergeOption = Microsoft.OData.Client.MergeOption.NoTracking
            };

            MotifGraph graph = new();

            // Get structure types
            var structureTypesTask = container.StructureTypes.GetAllPagesToListAsync(cancellationToken);

            // Get all structure links
            var structureLinksTask = GetAllStructureLinksAsync(container, cancellationToken);

            await Task.WhenAll(structureTypesTask, structureLinksTask);

            var structureTypes = await structureTypesTask;
            var structureLinks = await structureLinksTask;

            SortedDictionary<long, StructureType> typeIdToType = [];
            foreach (var type in structureTypes)
            {
                if (!typeIdToType.ContainsKey(type.ID))
                {
                    typeIdToType.Add(type.ID, type);
                }
            }

            if (structureLinks.Count == 0)
            {
                return graph;
            }

            // Get unique structure IDs involved in links
            HashSet<long> structureIds = [];
            foreach (var link in structureLinks)
            {
                structureIds.Add(link.SourceID);
                structureIds.Add(link.TargetID);
            }

            // Load structures
            var linkedStructures = await LoadStructuresByIDsAsync(container, [.. structureIds], cancellationToken);

            // Build structure dictionaries
            SortedDictionary<long, Structure> idToStructure = [];
            SortedDictionary<long, Structure> childIdToParent = [];
            List<long> parentIds = [];

            // Find parent IDs
            foreach (var structure in linkedStructures)
            {
                if (structure.ParentID.HasValue)
                {
                    idToStructure.Add(structure.ID, structure);
                    if (!parentIds.Contains(structure.ParentID.Value))
                    {
                        parentIds.Add(structure.ParentID.Value);
                    }
                }
            }

            if (parentIds.Count == 0)
            {
                return graph;
            }

            // Load parent structures
            var parentStructures = await LoadStructuresByIDsAsync(container, [.. parentIds], cancellationToken);

            foreach (var parentStructure in parentStructures)
            {
                if (!idToStructure.ContainsKey(parentStructure.ID))
                {
                    idToStructure.Add(parentStructure.ID, parentStructure);
                }
            }

            // Build child to parent mapping
            foreach (var structure in linkedStructures)
            {
                if (structure.ParentID.HasValue && idToStructure.TryGetValue(structure.ParentID.Value, out var parent))
                {
                    childIdToParent[structure.ID] = parent;
                }
            }

            // Group parent structures by label to create nodes
            SortedList<string, List<Structure>> labelToStructures = [];
            foreach (var parentStructure in parentStructures)
            {
                string label = GetBaseLabel(parentStructure.Label);
                if (!labelToStructures.ContainsKey(label))
                {
                    labelToStructures.Add(label, []);
                }
                labelToStructures[label].Add(parentStructure);
            }

            // Create motif nodes
            foreach (var kvp in labelToStructures)
            {
                MotifNode node = new(kvp.Key, kvp.Value.ConvertAll(s => new ODataStructureAdapter(s)));
                graph.AddNode(node);
            }

            // Build motif edges
            SortedDictionary<MotifEdge, MotifEdge> dictEdges = [];

            foreach (var link in structureLinks)
            {
                cancellationToken.ThrowIfCancellationRequested();

                try
                {
                    if (!idToStructure.TryGetValue(link.SourceID, out var sourceStructure))
                        continue;

                    if (!typeIdToType.TryGetValue(sourceStructure.TypeID, out var type))
                        continue;

                    string connectionLabel = type.Name;

                    if (!childIdToParent.TryGetValue(link.SourceID, out var parentOfSource))
                        continue;

                    if (!childIdToParent.TryGetValue(link.TargetID, out var parentOfTarget))
                        continue;

                    string sourceLabel = GetBaseLabel(parentOfSource.Label);
                    string targetLabel = GetBaseLabel(parentOfTarget.Label);

                    MotifEdge edge = new(sourceLabel, targetLabel, connectionLabel);

                    if (dictEdges.TryGetValue(edge, out var existingEdge))
                    {
                        edge = existingEdge;
                    }
                    else
                    {
                        dictEdges.Add(edge, edge);
                    }

                    edge.AddEdgeInstance(parentOfSource.ID, link.SourceID, parentOfTarget.ID, link.TargetID);
                }
                catch (KeyNotFoundException)
                {
                    // Skip links with missing data
                }
            }

            // Add edges to graph
            foreach (var edge in dictEdges.Values)
            {
                graph.AddEdge(edge);
            }

            return graph;
        }

        /// <summary>
        /// Asynchronously loads all structure links from the OData service
        /// </summary>
        private static async Task<List<StructureLink>> GetAllStructureLinksAsync(
            Container container,
            CancellationToken cancellationToken = default)
        {
            try
            {
                return await container.StructureLinks.GetAllPagesToListAsync(cancellationToken);
            }
            catch (Exception ex) when (ex is not OperationCanceledException)
            {
                throw new InvalidOperationException($"Failed to retrieve structure links: {ex.Message}", ex);
            }
        }

        /// <summary>
        /// Asynchronously loads structures by IDs in parallel
        /// </summary>
        private static async Task<List<Structure>> LoadStructuresByIDsAsync(
            Container container,
            long[] structureIds,
            CancellationToken cancellationToken = default)
        {
            if (structureIds is null || structureIds.Length == 0)
            {
                return [];
            }

            try
            {
                var tasks = structureIds.Select(id =>
                    Task.Run(() => container.Structures.Where(s => s.ID == id).FirstOrDefault(), cancellationToken)
                ).ToArray();

                var results = await Task.WhenAll(tasks);
                return [.. results.Where(s => s != null)];
            }
            catch (Exception ex) when (ex is not OperationCanceledException)
            {
                throw new InvalidOperationException($"Failed to load structures: {ex.Message}", ex);
            }
        }

        /// <summary>
        /// Extracts the base label from a structure label (removes cell/structure number suffix)
        /// </summary>
        private static string GetBaseLabel(string label)
        {
            if (string.IsNullOrEmpty(label))
            {
                return string.Empty;
            }

            // Remove trailing numbers and spaces (e.g., "BC 123" -> "BC")
            int lastSpace = label.LastIndexOf(' ');
            if (lastSpace > 0)
            {
                string suffix = label.Substring(lastSpace + 1);
                if (long.TryParse(suffix, out _))
                {
                    return label.Substring(0, lastSpace).Trim();
                }
            }

            return label.Trim();
        }
    }
}

