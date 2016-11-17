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

namespace AnnotationVizLib
{  
    public static class ODataMorphologyFactory
    {
        public static MorphologyGraph FromOData(ICollection<long> StructureIDs, bool include_children, Uri Endpoint)
        {
            ODataClient.ConnectomeODataV4.Container container = new ODataClient.ConnectomeODataV4.Container(Endpoint);

            var scale_retval = container.Scale();
            var scale = scale_retval.GetValue().ToGeometryScale();

            MorphologyGraph rootGraph = new MorphologyGraph(0, scale);

            if (StructureIDs == null)
                return rootGraph;

            List<Structure> listStructures = new List<Structure>();

            foreach(long ID in StructureIDs)
            { 
                Structure result = container.Structures.Expand(s => s.Locations).Expand(s => s.Type).Where(s => s.ID == ID).FirstOrDefault();

                if (result != null)
                {
                    var LocationLink = container.StructureLocationLinks(ID);
                    result.LocationLinks.Load(LocationLink);
                    listStructures.Add(result);
                }
            }

            //var Structures = container.Structures.Where(s => StructureIDs.Contains(s.ID));
            
            /*
            var Structures = from s in container.Structures
                             from id in StructureIDs
                             where s.ID == id
                             select s;
                             */
            //IList<Structure> listStructures = Structures.ToList();


            

            MorphologyForStructures(rootGraph, listStructures, include_children, scale);

            return rootGraph;
        }

        /// <summary>
        /// Add the morphology for the passed structure ID to the provided root graph
        /// </summary>
        /// <param name="rootGraph"></param>
        /// <param name="StructureIDs"></param>
        private static void MorphologyForStructures(MorphologyGraph rootGraph, ICollection<Structure> Structures, bool include_children, Geometry.Scale scale)
        {
            //Queries.PopulateStructureTypes();

            // Get the nodes and build graph for numHops            
            //System.Threading.Tasks.Parallel.ForEach<Structure>(structures, s =>
            foreach(Structure s in Structures)
            {
                MorphologyGraph graph = MorphologyForStructure(s, scale);
                if (graph == null)
                    return;
                                 
                rootGraph.Subgraphs.TryAdd((ulong)s.ID, graph);

                if (include_children)
                {
                    MorphologyForStructures(graph, s.Children, include_children, scale);
                }
            //);
            }
        }

        private static MorphologyGraph MorphologyForStructure(Structure s, Geometry.Scale scale)
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
                graph.AddEdge(new MorphologyEdge(loc_link.A, loc_link.B)); 
            }

            return;
        }
    }
}
