using System;
using System.Collections.Generic;
using System.Linq;
using System.Linq.Expressions;
using System.Text;
using System.Threading.Tasks;
using ODataClient;
using ODataClient.ConnectomeDataModel;
using ODataClient.ConnectomeODataV4;
using ODataClient.Geometry;
using Microsoft.SqlServer.Types;
using SqlGeometryUtils;

namespace AnnotationVizLib.OData
{
    public static class ODataMorphologyFactory
    {
        public static MorphologyGraph FromOData(ICollection<long> StructureIDs, bool include_children, Uri Endpoint)
        {
            ODataClient.ConnectomeODataV4.Container container = new ODataClient.ConnectomeODataV4.Container(Endpoint);
            container.MergeOption = Microsoft.OData.Client.MergeOption.NoTracking;
            var scale_retval = container.Scale();
            var scale = scale_retval.GetValue().ToGeometryScale();

            MorphologyGraph rootGraph = new MorphologyGraph(0, scale);

            if (StructureIDs == null)
                return rootGraph;

            List<Structure> listStructures = new List<Structure>();

            foreach(long ID in StructureIDs)
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

        private static void LoadStructureLocationLinks(Container container, ICollection<Structure> structures)
        {
            foreach(Structure s in structures)
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
                    if (graph == null)
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
            if (location_links == null)
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
