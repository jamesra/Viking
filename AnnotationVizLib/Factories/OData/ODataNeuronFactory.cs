using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using ODataClient.ConnectomeDataModel;

namespace AnnotationVizLib
{
    public class ODataNeuronFactory
    {
        static SortedDictionary<long, StructureType> IDToStructureType = null;
        SortedDictionary<ulong, IStructure> IDToStructure = new SortedDictionary<ulong, IStructure>();

        NeuronGraph graph; 

        private ODataNeuronFactory()
        {
            graph = new AnnotationVizLib.NeuronGraph();
        }

        public static NeuronGraph FromOData(ICollection<long> StructureIDs, uint numHops, Uri Endpoint)
        {
            ODataClient.ConnectomeODataV4.Container container = new ODataClient.ConnectomeODataV4.Container(Endpoint);

            var scale_retval = container.Scale();
            var scale = scale_retval.GetValue().ToGeometryScale();

            ODataNeuronFactory graphFactory = new ODataNeuronFactory();

            if (StructureIDs == null)
                return graphFactory.graph;

            if (StructureIDs.Count == 0)
                return graphFactory.graph;

            if(IDToStructureType == null)
            {
                  ODataNeuronFactory.PopulateStructureTypeDictionary(container.StructureTypes.ToList());
            }

            //List<long> listNetworkStructureID = container.Network(StructureIDs, (int)numHops).ToList();
            List<Structure> listNetworkStructures = container.NetworkCells(StructureIDs, (int)numHops).Expand(s => s.Children).ToList();

            graphFactory.PopulateStructureDictionary(listNetworkStructures);

            //Add nodes to graph
            graphFactory.AddStructuresAsNodes(listNetworkStructures);
            
            return graphFactory.graph;
        }

        private static void PopulateStructureTypeDictionary(ICollection<StructureType> types)
        {
            ODataNeuronFactory.IDToStructureType = new SortedDictionary<long, StructureType>();

            foreach (StructureType t in types)
            {
                ODataNeuronFactory.IDToStructureType.Add(t.ID, t);
            }
        }

        private void PopulateStructureDictionary(ICollection<Structure> structs)
        {
            foreach(Structure s in structs)
            {
                ODataStructureAdapter adapter = new AnnotationVizLib.ODataStructureAdapter(s);
                IDToStructure.Add((ulong)s.ID, adapter);

                PopulateStructureDictionary(s.Children);
            }
        }

        /// <summary>
        /// Add all top-level structures as nodes in our graph
        /// </summary>
        /// <param name="structs"></param>
        private void AddStructuresAsNodes(ICollection<Structure> structs)
        {
            foreach (IStructure s in structs.Select(s => new ODataStructureAdapter(s)))
            {
                NeuronNode node = new NeuronNode((long)s.ID, s);
                graph.AddNode(node);
            }
        }

        private void AddStructureLinksAsEdges(ICollection<StructureLink> struct_links)
        {
            foreach (StructureLink link in struct_links)
            {
                //After this point both nodes are already in the graph and we can create an edge
                IStructure LinkSource = IDToStructure[(ulong)link.SourceID];
                IStructure LinkTarget = IDToStructure[(ulong)link.TargetID];

                if (LinkTarget.ParentID.HasValue && LinkSource.ParentID.HasValue)
                {
                    string SourceTypeName = "";
                    if (IDToStructureType.ContainsKey((long)LinkSource.TypeID))
                    {
                        SourceTypeName = IDToStructureType[(long)LinkSource.TypeID].Name;
                    }

                    NeuronEdge E = new NeuronEdge((long)LinkSource.ParentID.Value, (long)LinkTarget.ParentID.Value, new ODataStructureLinkAdapter(link), SourceTypeName);

                    if (graph.Edges.ContainsKey(E))
                    {
                        E = graph.Edges[E];
                        E.AddLink(new ODataStructureLinkAdapter(link));
                    }
                    else
                    {
                        graph.AddEdge(E);
                    }
                }
            }
        }
    }
}
