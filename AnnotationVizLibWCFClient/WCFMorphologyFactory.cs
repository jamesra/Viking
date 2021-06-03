using AnnotationService.Types;
using AnnotationVizLib.WCFClient.AnnotationClient;
using System.Collections.Generic;
using System.Linq;

namespace AnnotationVizLib.WCFClient
{
    public static class WCFMorphologyFactory
    {

        public static MorphologyGraph FromWCF(ICollection<long> StructureIDs, bool include_children, string Endpoint, System.Net.NetworkCredential userCredentials)
        {
            ConnectionFactory.SetConnection(Endpoint, userCredentials);

            UnitsAndScale.IScale scale = Queries.GetScale().ToGeometryScale();

            MorphologyGraph rootGraph = new MorphologyGraph(0, scale);

            MorphologyForStructures(rootGraph, StructureIDs, include_children, scale);

            return rootGraph;
        }

        /// <summary>
        /// Add the morphology for the passed structure ID to the provided root graph
        /// </summary>
        /// <param name="rootGraph"></param>
        /// <param name="StructureIDs"></param>
        private static void MorphologyForStructures(MorphologyGraph rootGraph, ICollection<long> StructureIDs, bool include_children, UnitsAndScale.IScale scale)
        {
            if (StructureIDs == null)
                return;

            Structure[] structures = Queries.GetStructuresByIDs(StructureIDs.ToArray(), include_children);

            Queries.PopulateStructureTypes();

            System.Threading.Tasks.ParallelOptions o = new System.Threading.Tasks.ParallelOptions();
            o.MaxDegreeOfParallelism = 8;

            // Get the nodes and build graph for numHops            
            System.Threading.Tasks.Parallel.ForEach<Structure>(structures, o, s =>
            //foreach(Structure s in structures)
            {
                MorphologyGraph graph = MorphologyForStructure(s, scale);
                if (graph == null)
                    return;

                rootGraph.AddSubgraph(graph);

                if (include_children)
                {
                    MorphologyForStructures(graph, s.ChildIDs, include_children, scale);
                }
            }
            );
        }

        private static MorphologyGraph MorphologyForStructure(Structure s, UnitsAndScale.IScale scale)
        {
            MorphologyGraph root_graph = null;
            System.Diagnostics.Debug.Assert(s != null);

            using (AnnotateLocationsClient proxy = ConnectionFactory.CreateLocationsClient())
            {
                System.Diagnostics.Debug.Assert(proxy != null);

                Location[] struct_locations = proxy.GetLocationsForStructure(s.ID);

                root_graph = BuildGraphFromLocations(s, struct_locations, scale);
            }

            return root_graph;
        }

        private static MorphologyGraph BuildGraphFromLocations(Structure s, Location[] locations, UnitsAndScale.IScale scale)
        {
            if (locations == null)
                return null;

            if (locations.Length <= 0)
            {
                return null;
            }

            MorphologyGraph graph = new MorphologyGraph((ulong)locations[0].ParentID, scale, new WCFStructureAdapter(s));

            foreach (Location loc in locations)
            {
                graph.AddNode(new MorphologyNode((ulong)loc.ID, new WCFLocationAdapter(loc, scale), graph));
            }

            foreach (Location loc in locations)
            {
                AddLocationEdges(graph, loc);
            }

            return graph;
        }

        private static void AddLocationEdges(MorphologyGraph graph, Location Loc)
        {
            if (Loc.Links == null)
                return;

            foreach (long loc_link in Loc.Links)
            {
                //Only add the links with ID's less than ours to prevent duplicate links in the graph
                if (loc_link < Loc.ID)
                {
                    graph.AddEdge(new MorphologyEdge(graph, loc_link, Loc.ID));
                }
            }

            return;
        }
    }
}
