using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Simple.OData.Client;
using AnnotationVizLib.SimpleOData;
using System.Diagnostics;
using Annotation.Interfaces;

namespace AnnotationVizLib.SimpleOData
{
    public class SimpleODataNeuronFactory
    {
        static SortedDictionary<ulong, StructureType> IDToStructureType = null;
        SortedDictionary<ulong, Structure> IDToStructure = new SortedDictionary<ulong, Structure>();

        NeuronGraph graph; 

        private SimpleODataNeuronFactory()
        {
            graph = new AnnotationVizLib.NeuronGraph();
        }

        public static NeuronGraph FromOData(ICollection<long> StructureIDs, uint numHops, Uri Endpoint)
        {
            ODataClientSettings s = new ODataClientSettings();
            Simple.OData.Client.ODataClient client = new Simple.OData.Client.ODataClient(Endpoint);
            var scale = client.GetScale();
            SimpleODataNeuronFactory graphFactory = new SimpleODataNeuronFactory();

            if (StructureIDs == null)
                return graphFactory.graph;

            if (StructureIDs.Count == 0)
                return graphFactory.graph;

            if(IDToStructureType == null)
            {
                Task<IEnumerable<StructureType>> t = client.For<StructureType>().FindEntriesAsync();
                t.Wait();

                SimpleODataNeuronFactory.PopulateStructureTypeDictionary(t.Result);
            }

            IDictionary<ulong, Structure> NetworkStructures = GetNetworkCells(client, StructureIDs, numHops);
            ICollection<Structure> listNetworkChildStructures = GetNetworkChildStructures(client, StructureIDs, numHops);
            ICollection<StructureLink> listNetworkEdges = GetNetworkLinks(client, StructureIDs, numHops);

            //Merge the child Structures into the parent network structures
            foreach (Structure child in listNetworkChildStructures)
            {
                //Find the parent in the dictionary
                Structure parent = NetworkStructures[child.ParentID.Value];

                if (parent.Children == null)
                {
                    parent.Children = new List<Structure>();
                }

                parent.Children.Add(child);
            }
            
            graphFactory.PopulateStructureDictionary(NetworkStructures.Values);

            //Merge the structureLinks into the structures
            foreach (StructureLink sl in listNetworkEdges)
            {
                if (graphFactory.IDToStructure.ContainsKey(sl.SourceID))
                {
                    Structure Source = graphFactory.IDToStructure[sl.SourceID];

                    if (Source.SourceOfLinks == null)
                    {
                        Source.SourceOfLinks = new List<StructureLink>();
                    }

                    Source.SourceOfLinks.Add(sl);
                }

                if (graphFactory.IDToStructure.ContainsKey(sl.TargetID))
                {
                    Structure Target = graphFactory.IDToStructure[sl.TargetID];

                    if (Target.TargetOfLinks == null)
                    {
                        Target.TargetOfLinks = new List<StructureLink>();
                    }

                    Target.TargetOfLinks.Add(sl);
                }
            }

            //Add nodes to graph
            graphFactory.AddStructuresAsNodes(NetworkStructures.Values);
            graphFactory.AddStructureLinksAsEdges(listNetworkEdges);
            
            return graphFactory.graph;
        }

        private static IDictionary<ulong, Structure> GetNetworkCells(Simple.OData.Client.ODataClient client, ICollection<long> StructureIDs, uint numHops)
        {
            var annotations = new ODataFeedAnnotations();
            IDictionary<ulong, Structure> NetworkStructures = new SortedDictionary<ulong, Structure>();

            Task<IEnumerable<IDictionary<string, object>>> taskStructuresDicts = client.FindEntriesAsync(string.Format("Network(IDs=@IDs,Hops={1})?@IDs={0}", StructureIDs.ToODataArrayParameterString(), numHops), annotations);
            Debug.Assert(taskStructuresDicts != null);
            taskStructuresDicts.Wait();
            IEnumerable<IDictionary<string, object>> StructuresDicts = taskStructuresDicts.Result;
            
            foreach (IDictionary<string, object> dict in StructuresDicts)
            {
                Structure s = Structure.FromDictionary(dict);
                NetworkStructures.Add(s.ID, s);
            }

            while(annotations.NextPageLink != null)
            {
                taskStructuresDicts = client.FindEntriesAsync(annotations.NextPageLink.ToString(), annotations);
                taskStructuresDicts.Wait();
                StructuresDicts = taskStructuresDicts.Result;
                
                foreach (IDictionary<string, object> dict in StructuresDicts)
                {
                    Structure s = Structure.FromDictionary(dict);
                    NetworkStructures.Add(s.ID, s);
                }
            }

            Debug.Assert(NetworkStructures.Count > 0);

            return NetworkStructures;
        }

        private static ICollection<Structure> GetNetworkChildStructures(Simple.OData.Client.ODataClient client, ICollection<long> StructureIDs, uint numHops)
        {
            List<Structure> listNetworkChildStructures = new List<SimpleOData.Structure>();

            ODataFeedAnnotations annotations = new ODataFeedAnnotations(); 
            Task<IEnumerable<IDictionary<string, object>>> taskStructuresDicts = client.FindEntriesAsync(string.Format("NetworkChildStructures(IDs=@IDs,Hops={1})?@IDs={0}", StructureIDs.ToODataArrayParameterString(), numHops), annotations);
            Debug.Assert(taskStructuresDicts != null);

            taskStructuresDicts.Wait();
            IEnumerable<IDictionary<string, object>> StructuresDicts = taskStructuresDicts.Result;
             
            foreach (IDictionary<string, object> dict in StructuresDicts)
            {
                Structure s = Structure.FromDictionary(dict);
                listNetworkChildStructures.Add(s);
            }

            while (annotations.NextPageLink != null)
            {
                taskStructuresDicts = client.FindEntriesAsync(annotations.NextPageLink.ToString(), annotations);
                taskStructuresDicts.Wait();
                StructuresDicts = taskStructuresDicts.Result;

                foreach (IDictionary<string, object> dict in StructuresDicts)
                {
                    Structure s = Structure.FromDictionary(dict);
                    listNetworkChildStructures.Add(s);
                }
            }

            Debug.Assert(listNetworkChildStructures.Count > 0);

            return listNetworkChildStructures;
        }

        private static ICollection<StructureLink> GetNetworkLinks(Simple.OData.Client.ODataClient client, ICollection<long> StructureIDs, uint numHops)
        {
            ODataFeedAnnotations annotations = new ODataFeedAnnotations();
            Task<IEnumerable<IDictionary<string, object>>> taskStructureLinksDict = client.FindEntriesAsync(string.Format("NetworkLinks(IDs=@IDs,Hops={1})?@IDs={0}", StructureIDs.ToODataArrayParameterString(), numHops), annotations);
            Debug.Assert(taskStructureLinksDict != null);

            taskStructureLinksDict.Wait();
            IEnumerable<IDictionary<string, object>> StructureLinksDicts = taskStructureLinksDict.Result;
            List<StructureLink> listStructureLinks = new List<SimpleOData.StructureLink>();

            foreach (IDictionary<string, object> dict in StructureLinksDicts)
            {
                StructureLink sl = StructureLink.FromDictionary(dict);
                listStructureLinks.Add(sl);
            }

            while (annotations.NextPageLink != null)
            {
                taskStructureLinksDict = client.FindEntriesAsync(annotations.NextPageLink.ToString(), annotations);
                taskStructureLinksDict.Wait();
                StructureLinksDicts = taskStructureLinksDict.Result;

                foreach (IDictionary<string, object> dict in StructureLinksDicts)
                {
                    StructureLink sl = StructureLink.FromDictionary(dict);
                    listStructureLinks.Add(sl);
                }
            }

            Debug.Assert(listStructureLinks.Count > 0);

            return listStructureLinks;
        }

        private static void PopulateStructureTypeDictionary(IEnumerable<StructureType> types)
        {
            SimpleODataNeuronFactory.IDToStructureType = new SortedDictionary<ulong, StructureType>();

            foreach (StructureType t in types)
            {
                SimpleODataNeuronFactory.IDToStructureType.Add(t.ID, t);
            }
        }

        private void PopulateStructureDictionary(ICollection<Structure> structs)
        {
            foreach(Structure s in structs)
            {
                if (IDToStructure.ContainsKey(s.ID))
                    continue;

                IDToStructure.Add(s.ID, s);

                if(s.Children != null)
                    PopulateStructureDictionary(s.Children);
            }
        }

        /// <summary>
        /// Add all top-level structures as nodes in our graph
        /// </summary>
        /// <param name="structs"></param>
        private void AddStructuresAsNodes(ICollection<Structure> structs)
        {
            foreach (IStructure s in structs)
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
                if (IDToStructure.ContainsKey(link.SourceID) && IDToStructure.ContainsKey(link.TargetID))
                {
                    IStructure LinkSource = IDToStructure[(ulong)link.SourceID];
                    IStructure LinkTarget = IDToStructure[(ulong)link.TargetID];

                    if (LinkTarget.ParentID.HasValue && LinkSource.ParentID.HasValue)
                    {
                        string SourceTypeName = "";
                        if (IDToStructureType.ContainsKey(LinkSource.TypeID))
                        {
                            SourceTypeName = IDToStructureType[LinkSource.TypeID].Name;
                        }

                        NeuronEdge E = new NeuronEdge((long)LinkSource.ParentID.Value, (long)LinkTarget.ParentID.Value, link, SourceTypeName);

                        if (graph.Edges.ContainsKey(E))
                        {
                            E = graph.Edges[E];
                            E.AddLink(link);
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
}
