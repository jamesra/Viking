using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using GraphLib;
using System.Threading.Tasks;
using AnnotationVizLib.AnnotationService;
using System.Diagnostics;

namespace AnnotationVizLib
{
    public class StructureLinkComparer : Comparer<StructureLink>
    {
        public override int Compare(StructureLink x, StructureLink y)
        {
            if (object.ReferenceEquals(x, y))
                return 0;

            bool XIsNull = (object)x == null;
            bool YIsNull = (object)y == null;

            if (XIsNull)
                return -1;
            else if (YIsNull)
                return 1;

            int SourceCompare = x.SourceID.CompareTo(y.SourceID);
            if (SourceCompare != 0)
                return SourceCompare;
            else
            {
                int TargetCompare = x.TargetID.CompareTo(y.TargetID);
                return TargetCompare;
            }
        }         
    }

    public class NeuronEdge : Edge<long>, IComparer<NeuronEdge>, IComparable<NeuronEdge>, IEquatable<NeuronEdge>
    {
        public string SynapseType;

        /// <summary>
        /// List of child structures involved in the link
        /// </summary>
        public SortedSet<StructureLink> Links = new SortedSet<StructureLink>(new StructureLinkComparer());

        public override float Weight
        {
            get
            {
                return (float)Links.Count();
            }
        }

        public NeuronEdge(long SourceKey, long TargetKey, StructureLink Link, string SynapseType)
            : base(SourceKey, TargetKey, !Link.Bidirectional)
        {
            this.Links.Add(Link);
            this.SynapseType = SynapseType;
        } 

        public void AddLink(StructureLink link)
        {
            Debug.Assert(!Links.Contains(link));
            Debug.Assert(this.Directional != link.Bidirectional);

            Links.Add(link);
        }

        public string PrintChildLinks()
        {
            string output = "";
            bool first = true;
            foreach( StructureLink link in Links)
            {
                if (!first)
                {
                    output += ", ";
                }

                first = false;

                output += link.SourceID.ToString() + " -> " + link.TargetID.ToString();
            }
            
            return output;
        }

        public override string ToString()
        {
            return this.SourceNodeKey.ToString() + "-" + this.TargetNodeKey.ToString() + " via " + this.SynapseType + " " + PrintChildLinks();
        }

        public int Compare(NeuronEdge x, NeuronEdge y)
        {
            int comparison = base.Compare(x, y);
            if (comparison != 0)
                return comparison;

            if((object)x != null && (object)y != null)
            {
                return string.Compare(x.SynapseType, y.SynapseType);
            }
            else
                return comparison;
        }

        public int CompareTo(NeuronEdge other)
        {
            int comparison = base.CompareTo(other);
            if (comparison != 0)
                return comparison;

            if((object)other != null)
            {
                return this.SynapseType.CompareTo(other.SynapseType);
            }
            else
                return comparison;
        }

        public bool Equals(NeuronEdge other)
        {
            bool baseEquals = base.Equals(other);
            if(baseEquals && ((object)other != null))
            {
                return this.SynapseType.Equals(other.SynapseType);
            }

            return false;
        }
    }

    public class NeuronNode : Node<long, NeuronEdge>
    {
        //Structure this node represents
        public Structure Structure;

        public NeuronNode(long key, Structure value)
            : base(key)
        {
            this.Structure = value;
            
        }

        public override string ToString()
        {
            return this.Key.ToString() + " : " + Structure.Label;
        }
    }

    public class NeuronGraph : Graph<long, NeuronNode, NeuronEdge>
    { 
        SortedDictionary<long, Structure> IDToStructure = new SortedDictionary<long, Structure>();

        SortedDictionary<long, StructureType> IDToStructureType = new SortedDictionary<long, StructureType>();

        List<long> NextHopNodes = new List<long>();

        public System.Collections.ObjectModel.ReadOnlyCollection<long> IncompleteNodes
        {
            get
            {
                return NextHopNodes.AsReadOnly();
            }
        }

        public static NeuronGraph BuildGraph(ICollection<long> StructureIDs, uint numHops, string Endpoint, System.Net.NetworkCredential userCredentials)
        {
            ConnectionFactory.SetConnection(Endpoint, userCredentials);

            NeuronGraph graph = new NeuronGraph();

            graph.IDToStructureType = Queries.GetStructureTypes();

            List<long> MissingParents = new List<long>(StructureIDs);

            using (AnnotateStructuresClient proxy = ConnectionFactory.CreateStructuresClient())
            {
                long[] struct_IDs = StructureIDs.ToArray();
                Task<Structure[]> task_nodes = Task<Structure[]>.Run(() => Queries.GetNetworkedStructures(struct_IDs, (int)numHops));
                Task<Structure[]> task_childStructures = Task<Structure[]>.Run(() => Queries.GetChildStructuresInNetwork(struct_IDs, (int)numHops));
                Task<StructureLink[]> task_struct_links = Task<StructureLink[]>.Run(() => Queries.GetStructureLinksInNetwork(struct_IDs, (int)numHops));

                Task.WaitAll(new Task[] { task_nodes, task_childStructures, task_struct_links });
               
                Structure[] network_node_IDs = task_nodes.Result;
                Structure[] childStructures = task_childStructures.Result;
                StructureLink[] struct_links = task_struct_links.Result;

                graph.AddStructuresByID(proxy, network_node_IDs, childStructures, struct_links);
            }

            graph.NextHopNodes = MissingParents;

            return graph;
        }

        private void AddStructuresByID(AnnotateStructuresClient proxy, Structure[] Nodes, Structure[] childStructures, StructureLink[] struct_links)
        {
            AddStructuresAsNodes(Nodes);

            foreach (Structure s in childStructures)
            {
                IDToStructure.Add(s.ID, s);
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
                if (this.Nodes.ContainsKey(id))
                    this.Nodes.Remove(id);
            }
        }

        private void AddEdgesForChildStructures(StructureLink[] struct_links)
        {
            foreach (StructureLink link in struct_links)
            {
                //After this point both nodes are already in the graph and we can create an edge
                Structure LinkSource = IDToStructure[link.SourceID];
                Structure LinkTarget = IDToStructure[link.TargetID];

                if (LinkTarget.ParentID != null && LinkSource.ParentID != null)
                {
                    string SourceTypeName = "";
                    if (IDToStructureType.ContainsKey(LinkSource.TypeID))
                    {
                        SourceTypeName = IDToStructureType[LinkSource.TypeID].Name;
                    }

                    //Links should have parents
                    if (!(LinkSource.ParentID.HasValue && LinkTarget.ParentID.HasValue))
                        continue;

                    NeuronEdge E = new NeuronEdge(LinkSource.ParentID.Value, LinkTarget.ParentID.Value, link, SourceTypeName);

                    if (this.Edges.ContainsKey(E))
                    {
                        E = this.Edges[E];
                        E.AddLink(link);
                    }
                    else
                    {
                        this.Edges.Add(E, E);
                    }
                }
            }
        }

        private void AddEdgesForChildStructures(Structure[] ChildStructures)
        {
            //Create edges
            foreach (Structure child in ChildStructures.Where(child => child.Links != null && child.Links.Length > 0))
            {
                AddEdgesForChildStructures(child.Links.Where(link => IDToStructure.ContainsKey(link.SourceID) && IDToStructure.ContainsKey(link.TargetID)).ToArray());
            }
        }

        private List<long> GetHop(AnnotateStructuresClient proxy, IList<long> CellIDs)
        {
            if (CellIDs.Count == 0)
                return new List<long>();

            //Remove nodes we have already mapped
            CellIDs = CellIDs.Where(id => !this.Nodes.ContainsKey(id)).ToList();
           
            Structure[] MissingStructures = proxy.GetStructuresByIDs(CellIDs.ToArray(), true);

            Structure[] ChildStructures = FindMissingChildStructures(proxy, MissingStructures);

            Structure[] LinkedStructurePartners = FindMissingLinkedStructures(proxy, ChildStructures);

            foreach(Structure s in LinkedStructurePartners.Where(s => !IDToStructure.ContainsKey(s.ID)))
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

                if (this.Nodes.ContainsKey(sibling.ParentID.Value))
                    continue;

                if (ListAbsentParents.Contains(sibling.ParentID.Value) == false)
                    ListAbsentParents.Add(sibling.ParentID.Value);
            }

            return ListAbsentParents;
            
        }

        private void AddStructuresAsNodes(Structure[] structs)
        {
            foreach (Structure s in structs)
            {
                NeuronNode node = new NeuronNode(s.ID, s);
                this.Nodes[s.ID] = node;

                IDToStructure[s.ID] = s;
            }
        }

        private Structure[]  FindMissingChildStructures(AnnotateStructuresClient proxy, Structure[] MissingStructures)
        {
            List<long> ListMissingChildrenIDs = new List<long>(MissingStructures.Length);

            foreach (Structure s in MissingStructures)
            {
                NeuronNode node = new NeuronNode(s.ID, s);
                this.Nodes[s.ID] = node;

                IDToStructure[s.ID] = s;

                if (s.ChildIDs == null)
                    continue; 

                //Find all of the details on child synapses, which we probably do not have
                foreach (long childID in s.ChildIDs.Where(childID => !IDToStructure.ContainsKey(childID)))
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
            SortedSet<long> ListAbsentLinkPartners = new SortedSet<long>();

            //Find missing structures and populate the list
            foreach (Structure child in ChildStructures)
            {
                if (!IDToStructure.ContainsKey(child.ID))
                {
                    IDToStructure.Add(child.ID, child); 
                }

                if (child.Links == null)
                    continue; 

                foreach (StructureLink link in child.Links)
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

            Structure[] LinkedStructurePartners = proxy.GetStructuresByIDs(ListAbsentLinkPartners.Distinct().ToArray(), false);
            return LinkedStructurePartners;
        } 
        
    }

}
