using ODataClient.ConnectomeDataModel;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

namespace AnnotationVizLib.OData
{
    public static class ODataMorphologyFactory
    {
        public static MorphologyGraph FromOData(ICollection<long> StructureIDs, bool include_children, Uri Endpoint)
        {
            Container container = new Container(Endpoint)
            {
                MergeOption = Microsoft.OData.Client.MergeOption.NoTracking
            };
            var scale_retval = container.Scale();
            var scale = scale_retval.GetValue().ToGeometryScale();

            MorphologyGraph rootGraph = new MorphologyGraph(0, scale);

            if (StructureIDs is null)
                return rootGraph;

            List<Structure> listStructures = new List<Structure>();

            foreach (long ID in StructureIDs)
            {
                Structure result = container.Structures.Expand(s => s.Locations).Expand(s => s.Type).Expand(s => s.Children).Where(s => s.ID == ID).FirstOrDefault();

                if (result != null)
                {
                    var LocationLink = container.StructureLocationLinks(ID);
                    result.LocationLinks.Load(LocationLink);
                    listStructures.Add(result);
                }
            }

            MorphologyForStructures(container, rootGraph, listStructures, include_children, scale);

            return rootGraph;
        }

        // ASYNC METHODS
        public static async Task<MorphologyGraph> FromODataByTypeIDsAsync(ICollection<long> TypeIDs, Uri Endpoint, bool include_children = false)
        {
            return await Task.Run(() =>
            {
                var container = new Container(Endpoint)
                {
                    MergeOption = Microsoft.OData.Client.MergeOption.NoTracking
                };
                var scale_retval = container.Scale();
                var scale = scale_retval.GetValue().ToGeometryScale();
                MorphologyGraph rootGraph = new MorphologyGraph(0, scale);
                if (TypeIDs == null || TypeIDs.Count == 0)
                    return rootGraph;

                // Query all structures of the given type IDs
                var allStructures = new List<Structure>();
                foreach (var typeId in TypeIDs)
                {
                    var structs = container.Structures
                        .Expand(s => s.Locations)
                        .Expand(s => s.Type)
                        .Expand(s => s.Children)
                        .Where(s => s.TypeID == typeId)
                        .ToList();
                    allStructures.AddRange(structs);
                }

                MorphologyForStructures(container, rootGraph, allStructures, include_children, scale);
                return rootGraph;
            });
        }

        public static async Task<MorphologyGraph> FromODataAsync(ICollection<long> StructureIDs, bool include_children, Uri Endpoint)
        {
            return await Task.Run(() =>
            {
                var container = new Container(Endpoint)
                {
                    MergeOption = Microsoft.OData.Client.MergeOption.NoTracking
                };
                var scale_retval = container.Scale();
                var scale = scale_retval.GetValue().ToGeometryScale();
                MorphologyGraph rootGraph = new MorphologyGraph(0, scale);
                if (StructureIDs == null || StructureIDs.Count == 0)
                    return rootGraph;

                // Query all structures by ID
                var allStructures = new List<Structure>();
                foreach (var id in StructureIDs)
                {
                    var structure = container.Structures
                        .Expand(s => s.Locations)
                        .Expand(s => s.Type)
                        .Expand(s => s.Children)
                        .Where(s => s.ID == id)
                        .FirstOrDefault();
                    if (structure != null)
                    {
                        allStructures.Add(structure);
                    }
                }

                MorphologyForStructures(container, rootGraph, allStructures, include_children, scale);
                return rootGraph;
            });
        }

        public static async Task<MorphologyGraph> FromODataLocationIDsAsync(ICollection<long> LocationIDs, Uri Endpoint, int hops = 0)
        {
            return await Task.Run(() =>
            {
                var container = new Container(Endpoint)
                {
                    MergeOption = Microsoft.OData.Client.MergeOption.NoTracking
                };
                var scale_retval = container.Scale();
                var scale = scale_retval.GetValue().ToGeometryScale();
                MorphologyGraph rootGraph = new MorphologyGraph(0, scale);
                if (LocationIDs == null || LocationIDs.Count == 0)
                    return rootGraph;

                // Download the initial set of locations
                var locations = new List<Location>();
                foreach (var id in LocationIDs.Distinct())
                {
                    var location = container.Locations.Where(l => l.ID == id).FirstOrDefault();
                    if (location != null)
                    {
                        locations.Add(location);
                    }
                }

                // Find parent structure for the first location
                if (locations.Count == 0)
                    return rootGraph;
                long structureId = locations[0].ParentID;
                var parent = container.Structures
                    .Expand(s => s.Locations)
                    .Expand(s => s.Type)
                    .Expand(s => s.Children)
                    .Where(s => s.ID == structureId)
                    .FirstOrDefault();
                if (parent == null)
                    return rootGraph;

                // Load location links
                var locLinks = container.StructureLocationLinks(structureId);
                parent.LocationLinks.Load(locLinks);

                // TODO: Implement hops logic if needed (currently only loads direct locations)

                MorphologyGraph graph = MorphologyForStructure(parent, scale);
                foreach (var loc in locations)
                {
                    if (!graph.Nodes.ContainsKey((ulong)loc.ID))
                    {
                        graph.AddNode(new MorphologyNode((ulong)loc.ID, new ODataLocationAdapter(loc, scale), graph));
                    }
                }
                AddLocationEdges(graph, parent.LocationLinks.ToArray());
                return graph;
            });
        }

        private static void LoadStructureLocationLinks(Container container, ICollection<Structure> structures)
        {
            foreach (Structure s in structures)
            {
                var LocationLinks = container.StructureLocationLinks(s.ID);
                s.LocationLinks.Load(LocationLinks);
            }
        }

        /// <summary>
        /// Add the morphology for the passed structure ID to the provided root graph
        /// </summary>
        /// <param name="rootGraph"></param>
        /// <param name="StructureIDs"></param>
        private static void MorphologyForStructures(Container container, MorphologyGraph rootGraph, ICollection<Structure> Structures, bool include_children, UnitsAndScale.IScale scale)
        {
            //Queries.PopulateStructureTypes();

            // Get the nodes and build graph for numHops            
            System.Threading.Tasks.Parallel.ForEach<Structure>(Structures, s =>

            //foreach (Structure s in Structures)
            {
                MorphologyGraph graph = MorphologyForStructure(s, scale);
                if (graph is null)
                    return;

                rootGraph.AddSubgraph(graph);

                if (include_children && s.Children.Any())
                {
                    //Optimization, use the already loaded StructureTypes instead of expand
                    IList<Structure> child_structs = container.Structures.Expand(st => st.Locations).Expand(st => st.Type).Expand(st => st.Children).Where(st => st.ParentID == s.ID).ToList();
                    LoadStructureLocationLinks(container, child_structs);
                    MorphologyForStructures(container, graph, child_structs, include_children, scale);
                }
            }
            );
        }

        private static async Task MorphologyForStructuresAsync(Container container, MorphologyGraph rootGraph, ICollection<Structure> Structures, bool include_children, UnitsAndScale.IScale scale)
        {
            await Task.Run(() =>
            {
                foreach (var s in Structures)
                {
                    MorphologyGraph graph = MorphologyForStructure(s, scale);
                    if (graph == null)
                        continue;
                    rootGraph.AddSubgraph(graph);
                    if (include_children && s.Children.Any())
                    {
                        var childStructs = container.Structures
                            .Expand(st => st.Locations)
                            .Expand(st => st.Type)
                            .Expand(st => st.Children)
                            .Where(st => st.ParentID == s.ID)
                            .ToList();
                        LoadStructureLocationLinks(container, childStructs);
                        MorphologyForStructures(container, graph, childStructs, include_children, scale);
                    }
                }
            });
        }

        private static async Task LoadStructureLocationLinksAsync(Container container, ICollection<Structure> structures)
        {
            await Task.Run(() =>
            {
                foreach (var s in structures)
                {
                    var links = container.StructureLocationLinks(s.ID);
                    s.LocationLinks.Load(links);
                }
            });
        }

        private static MorphologyGraph MorphologyForStructure(Structure s, UnitsAndScale.IScale scale)
        {
            Location[] locations = s.Locations.ToArray();
            LocationLink[] location_links = s.LocationLinks.ToArray();


            if (locations.Length <= 0)
            {
                return null;
            }

            MorphologyGraph graph = new MorphologyGraph((ulong)s.ID, scale, new ODataStructureAdapter(s));

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
                //Only add the links with ID's less than ours to prevent duplicate links in the graph
                graph.AddEdge(new MorphologyEdge(graph, loc_link.A, loc_link.B));
            }

            return;
        }
    }
}
