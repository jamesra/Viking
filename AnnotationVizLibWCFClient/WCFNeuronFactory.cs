using Viking.AnnotationServiceTypes.Interfaces;
using AnnotationService.Types;
using AnnotationVizLib.WCFClient.AnnotationClient;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Threading.Tasks;

namespace AnnotationVizLib.WCFClient
{
    public class WCFNeuronFactory
    {
        SortedDictionary<ulong, IStructure> IDToStructure = new SortedDictionary<ulong, IStructure>();

        static SortedDictionary<long, StructureType> IDToStructureType = null;

        List<ulong> NextHopNodes = new List<ulong>();

        NeuronGraph graph;

        protected WCFNeuronFactory()
        {
            if (IDToStructureType == null)
                IDToStructureType = Queries.GetStructureTypes();

            graph = new NeuronGraph();
        }

        public System.Collections.ObjectModel.ReadOnlyCollection<ulong> IncompleteNodes
        {
            get
            {
                return NextHopNodes.AsReadOnly();
            }
        }

        public static NeuronGraph BuildGraph(ICollection<long> StructureIDs, uint numHops, string Endpoint, System.Net.NetworkCredential userCredentials)
        {
            ConnectionFactory.SetConnection(Endpoint, userCredentials);

            WCFNeuronFactory graphFactory = new WCFNeuronFactory();

            List<ulong> MissingParents = StructureIDs.Select(s => (ulong)s).ToList();

            using (AnnotateStructuresClient proxy = ConnectionFactory.CreateStructuresClient())
            {
                long[] struct_IDs = StructureIDs.ToArray();
                Task<Structure[]> task_nodes = Task<Structure[]>.Run(() => Queries.GetNetworkedStructures(struct_IDs, (int)numHops));
                Task<StructureLink[]> task_struct_links = Task<StructureLink[]>.Run(() => Queries.GetStructureLinksInNetwork(struct_IDs, (int)numHops));
                Structure[] childStructures = Queries.GetChildStructuresInNetwork(struct_IDs, (int)numHops);
                Task.WaitAll(new Task[] { task_nodes, /*task_childStructures,*/ task_struct_links });

                Structure[] network_node_IDs = task_nodes.Result;
                StructureLink[] struct_links = task_struct_links.Result;

                graphFactory.AddStructuresByID(proxy, network_node_IDs, childStructures, struct_links);
            }

            graphFactory.NextHopNodes = MissingParents;

            return graphFactory.graph;
        }

        private void AddStructuresByID(AnnotateStructuresClient proxy, Structure[] Nodes, Structure[] childStructures, StructureLink[] struct_links)
        {
            AddStructuresAsNodes(Nodes);

            foreach (IStructure s in childStructures.Select(s => new WCFStructureAdapter(s)))
            {
                if (!IDToStructure.ContainsKey(s.ID))
                    IDToStructure.Add(s.ID, s);
                else
                    Trace.WriteLine(string.Format("Duplicate add of structure {0}", s.ID));
            }

            AddEdgesForChildStructures(struct_links);
        }

        /// <summary>
        /// Remove nodes which are for the next hop
        /// </summary>
        public void RemoveIncompleteNodes()
        {
            foreach (long id in this.NextHopNodes)
            {
                if (graph.Nodes.ContainsKey(id))
                    graph.RemoveNode(id);
            }
        }

        private void AddEdgesForChildStructures(StructureLink[] struct_links)
        {
            foreach (StructureLink link in struct_links)
            {
                //After this point both nodes are already in the graph and we can create an edge
                if (IDToStructure.ContainsKey((ulong)link.SourceID) && IDToStructure.ContainsKey((ulong)link.TargetID))
                {
                    IStructure LinkSource = IDToStructure[(ulong)link.SourceID];
                    IStructure LinkTarget = IDToStructure[(ulong)link.TargetID];

                    if (LinkTarget.ParentID.HasValue && LinkSource.ParentID.HasValue)
                    {
                        string SourceTypeName = "";
                        if (IDToStructureType.ContainsKey((long)LinkSource.TypeID))
                        {
                            SourceTypeName = IDToStructureType[(long)LinkSource.TypeID].Name;
                        }

                        NeuronEdge E = new NeuronEdge((long)LinkSource.ParentID.Value, (long)LinkTarget.ParentID.Value, new WCFStructureLinkAdapter(link), SourceTypeName);

                        if (graph.Edges.ContainsKey(E))
                        {
                            E = graph.Edges[E];
                            E.AddLink(new WCFStructureLinkAdapter(link));
                        }
                        else
                        {
                            graph.AddEdge(E);
                        }
                    }
                }
            }
        }

        private void AddEdgesForChildStructures(Structure[] ChildStructures)
        {
            //Create edges
            foreach (Structure child in ChildStructures.Where(child => child.Links != null && child.Links.Length > 0))
            {
                AddEdgesForChildStructures(child.Links.Where(link => IDToStructure.ContainsKey((ulong)link.SourceID) && IDToStructure.ContainsKey((ulong)link.TargetID)).ToArray());
            }
        }

        private List<long> GetHop(AnnotateStructuresClient proxy, IList<long> CellIDs)
        {
            if (CellIDs.Count == 0)
                return new List<long>();

            //Remove nodes we have already mapped
            CellIDs = CellIDs.Where(id => !graph.Nodes.ContainsKey(id)).ToList();

            Structure[] MissingStructures = proxy.GetStructuresByIDs(CellIDs.ToArray(), true);

            Structure[] ChildStructures = FindMissingChildStructures(proxy, MissingStructures);

            Structure[] LinkedStructurePartners = FindMissingLinkedStructures(proxy, ChildStructures);

            foreach (IStructure s in LinkedStructurePartners.Where(s => !IDToStructure.ContainsKey((ulong)s.ID)).Select(s => new WCFStructureAdapter(s)))
            {
                IDToStructure.Add(s.ID, s);
            }

            AddEdgesForChildStructures(ChildStructures);

            List<long> ListAbsentParents = new List<long>(LinkedStructurePartners.Length);

            //Find a list of the parentIDs we are missing, and add them to the graph, and return them
            //so we can easily make another hop later
            foreach (Structure sibling in LinkedStructurePartners)
            {
                if (sibling.ParentID.HasValue == false)
                    continue;

                if (graph.Nodes.ContainsKey(sibling.ParentID.Value))
                    continue;

                if (ListAbsentParents.Contains(sibling.ParentID.Value) == false)
                    ListAbsentParents.Add(sibling.ParentID.Value);
            }

            return ListAbsentParents;

        }

        private void AddStructuresAsNodes(Structure[] structs)
        {
            foreach (IStructure s in structs.Select(s => new WCFStructureAdapter(s)))
            {
                NeuronNode node = new NeuronNode((long)s.ID, s);
                graph.AddNode(node);

                IDToStructure[s.ID] = s;
            }
        }

        private Structure[] FindMissingChildStructures(AnnotateStructuresClient proxy, Structure[] MissingStructures)
        {
            List<long> ListMissingChildrenIDs = new List<long>(MissingStructures.Length);

            foreach (Structure s in MissingStructures)
            {
                IStructure adapter = new WCFStructureAdapter(s);
                NeuronNode node = new NeuronNode(s.ID, adapter);
                graph.AddNode(node);

                IDToStructure[(ulong)s.ID] = adapter;

                if (s.ChildIDs == null)
                    continue;

                //Find all of the details on child synapses, which we probably do not have
                foreach (long childID in s.ChildIDs.Where(childID => !IDToStructure.ContainsKey((ulong)childID)))
                {
                    ListMissingChildrenIDs.Add(childID);
                }
            }

            //Find all synapses and gap junctions

            Structure[] ChildStructures = Queries.GetStructuresByIDs(proxy, ListMissingChildrenIDs.ToArray());
            return ChildStructures;
        }


        private Structure[] FindMissingLinkedStructures(AnnotateStructuresClient proxy, Structure[] ChildStructures)
        {
            SortedSet<ulong> ListAbsentLinkPartners = new SortedSet<ulong>();

            //Find missing structures and populate the list
            foreach (IStructure child in ChildStructures.Select(s => new WCFStructureAdapter(s)))
            {
                if (!IDToStructure.ContainsKey((ulong)child.ID))
                {
                    IDToStructure.Add((ulong)child.ID, child);
                }

                if (child.Links == null)
                    continue;

                foreach (IStructureLink link in child.Links)
                {
                    if (!IDToStructure.ContainsKey(link.SourceID))
                    {
                        ListAbsentLinkPartners.Add(link.SourceID);
                    }

                    if (!IDToStructure.ContainsKey(link.TargetID))
                    {
                        ListAbsentLinkPartners.Add(link.TargetID);
                    }
                }
            }

            Structure[] LinkedStructurePartners = proxy.GetStructuresByIDs(ListAbsentLinkPartners.Distinct().Cast<long>().ToArray(), false);
            return LinkedStructurePartners;
        }
    }
}
