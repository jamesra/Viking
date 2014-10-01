using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using GraphLib;
using AnnotationUtils.AnnotationService;
using System.Diagnostics;

namespace AnnotationUtils.Graph.Structures
{
    public class LocationEdge : Edge<long>
    {  
        public LocationEdge(long SourceKey, long TargetKey)
            : base(SourceKey, TargetKey)
        { 
        }

        public override string ToString()
        {
            return this.SourceNodeKey.ToString() + " -> " + this.TargetNodeKey.ToString();
        }
    }

    public class LocationNode : Node<long, LocationEdge>
    {
        //Structure this node represents
        public Location Location;

        public LocationNode(long key, Location value)
            : base(key)
        {
            this.Location = value;
        }

        public override string ToString()
        {
            return this.Key.ToString();
        }
    }

    public class StructureGraph : Graph<long, LocationNode, LocationEdge>
    {
        public Structure structure { get; private set; }

        private static void CreateNodesForLocations(StructureGraph graph, IEnumerable<Location> locations)
        {
            foreach (Location loc in locations)
            {
                LocationNode node = new LocationNode(loc.ID, loc);
                graph.AddNode(node);
            }
        }

        private static void CreateLinksForLocations(StructureGraph graph, IEnumerable<Location> locations)
        {
            foreach (Location loc in locations)
            {
                foreach (long link in loc.Links)
                {
                    LocationEdge edge = new LocationEdge(loc.ID, link);
                    graph.AddEdge(edge);
                }
            }
        }


        public static StructureGraph BuildGraph(long StructureID, string Endpoint, System.Net.NetworkCredential userCredentials)
        {
            StructureGraph graph = new StructureGraph();

            ConnectionFactory.SetConnection(Endpoint, userCredentials);
            using(AnnotateStructuresClient proxy = ConnectionFactory.CreateStructuresClient())
            {
                graph.structure = proxy.GetStructureByID(StructureID, true);

                Location[] locations = Queries.GetLocationsForStructure(proxy, StructureID);
            
                CreateNodesForLocations(graph, locations);
                CreateLinksForLocations(graph, locations);

                return graph;
            }
        }
    }
}
