using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using GraphLib;
using AnnotationUtils.AnnotationService;
using System.Diagnostics;

namespace AnnotationUtils
{
    public class NeuronEdge : Edge<long>
    {
        public string SynapseType;
        public StructureLink Link;

        public NeuronEdge(long SourceKey, long TargetKey, StructureLink Link, string SynapseType)
            : base(SourceKey, TargetKey)
        {
            this.Link = Link; 
            this.SynapseType = SynapseType;
        }

        public override string ToString()
        {
            return this.SourceNodeKey.ToString() + " -> " + this.TargetNodeKey.ToString() + " via " + this.SynapseType;
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

        public static NeuronGraph BuildGraph(long[] StructureIDs, int numHops, string Endpoint, System.Net.NetworkCredential userCredentials)
        {
            ConnectionFactory.SetConnection(Endpoint, userCredentials);

            NeuronGraph graph = new NeuronGraph();

            graph.IDToStructureType = Queries.GetStructureTypes(); 

            List<long> MissingNodes = new List<long>(StructureIDs);

            using (AnnotateStructuresClient proxy = ConnectionFactory.CreateStructuresClient())
            {
                // Get the nodes and build graph for numHops
                for (int i = 0; i < numHops+1; i++)
                {
                    MissingNodes = graph.GetHop(proxy, MissingNodes);
                }
            }

            return graph;
        }

        private List<long> GetHop(AnnotateStructuresClient proxy, IList<long> CellIDs)
        {
            if (CellIDs.Count == 0)
                return new List<long>();

            //Remove nodes we have already mapped
            for (int i = CellIDs.Count - 1; i >= 0; i--)
            {
                long id = CellIDs[i];
                // Test to see if the ID is already in the nodelist            
                if (this.Nodes.ContainsKey(id))
                    CellIDs.RemoveAt(i);
            }

            Structure[] MissingStructures = proxy.GetStructuresByIDs(CellIDs.ToArray(), true);

            Structure[] ChildStructures = FindMissingChildStructures(proxy, MissingStructures);

            Structure[] LinkedStructurePartners = FindMissingLinkedStructures(proxy, ChildStructures);

            foreach(Structure s in LinkedStructurePartners)
            {
                if(!IDToStructure.ContainsKey(s.ID))
                {
                    IDToStructure.Add(s.ID, s); 
                }
            }

            //Create edges
            foreach (Structure child in ChildStructures)
            {
                if (child.Links.Length == 0)
                    continue; 

                foreach (StructureLink link in child.Links)
                {
                    if (!IDToStructure.ContainsKey(link.SourceID))
                    {
                        continue;
                    }

                    if (!IDToStructure.ContainsKey(link.TargetID))
                    {
                        continue;
                    }

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

                        NeuronEdge E = new NeuronEdge(LinkSource.ParentID.Value, LinkTarget.ParentID.Value, link, SourceTypeName);
                        this.Edges.Add(E); 
                    }
                }
            }

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

        private Structure[]  FindMissingChildStructures(AnnotateStructuresClient proxy, Structure[] MissingStructures)
        {
            List<long> ListMissingChildrenIDs = new List<long>(MissingStructures.Length);

            foreach (Structure s in MissingStructures)
            {
                NeuronNode node = new NeuronNode(s.ID, s);
                this.Nodes[s.ID] = node;

                IDToStructure[s.ID] = s;

                //Find all of the details on child synapses, which we probably do not have
                foreach (long childID in s.ChildIDs)
                {
                    if (IDToStructure.ContainsKey(childID) == false)
                    {
                        ListMissingChildrenIDs.Add(childID);
                    }
                }
            }

            //Find all synapses and gap junctions

            //Fragment the array into multiple chunks if needed
            List<long>[] arrayOfMissing = Utils<long>.SplitListIntoChunks(ListMissingChildrenIDs, 16384);

            List<Structure> ChildStructures = new List<Structure>(ListMissingChildrenIDs.Count);
            for (int i = 0; i < arrayOfMissing.Length; i++)
            {
                ChildStructures.AddRange(proxy.GetStructuresByIDs(arrayOfMissing[i].ToArray(), false));
            }

            return ChildStructures.ToArray();
        }

        private Structure[] FindMissingLinkedStructures(AnnotateStructuresClient proxy, Structure[] ChildStructures)
        { 
            List<long> ListAbsentLinkPartners = new List<long>(ChildStructures.Length);

            //Find missing structures and populate the list
            foreach (Structure child in ChildStructures)
            {
                if (!IDToStructure.ContainsKey(child.ID))
                {
                    IDToStructure.Add(child.ID, child); 
                }

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

            Structure[] LinkedStructurePartners = proxy.GetStructuresByIDs(ListAbsentLinkPartners.ToArray(), false);
            return LinkedStructurePartners;
        } 
        
    }



    /*
     * 
    public List<long> webService_GetHop(Graphx graph, long[] cellids)
        {
            if (cellids.Length == 0)
            {
                return new List<long>();
            }

            //Get the root structure
            List<Structure> RootStructures = new List<Structure>();

            // Store all them missing structure ids and call webservice
            List<long> MissingRootStructureIds = new List<long>();

            foreach (long id in cellids)
            {
                // Test to see if the RootStructure is already in the nodelist            
                if (!graph.NodeList.ContainsKey(id)) {
                    MissingRootStructureIds.Add(id);
                }
            }

            Structure[] MissingStructures = webService_GetStructures(graph, MissingRootStructureIds.ToArray());

            List<long> ListMissingChildrenIDs = new List<long>();

            foreach (Structure structure in MissingStructures)
            {
                foreach (long childID in structure.ChildIDs)
                {
                    if (graph.NodeList.ContainsKey(childID) == false)
                    {
                        ListMissingChildrenIDs.Add(childID);
                    }
                }
            }

            //Find all synapses and gap junctions
            Structure[] ChildStructObjs = webService_GetStructures(graph, ListMissingChildrenIDs.ToArray());

            List<long> ListAbsentSiblings = new List<long>();

            //Find missing structures and populate the list
            foreach (Structure child in ChildStructObjs)
            {
                //Temp Hack to skip desmosomes
                if (child.TypeID == 85)
                    continue;

                foreach (StructureLink link in child.Links)
                {

                    if (!graph.NodeList.ContainsKey(link.SourceID))
                    {
                        ListAbsentSiblings.Add(link.SourceID);

                    }

                    if (!graph.NodeList.ContainsKey(link.TargetID))
                    {
                        ListAbsentSiblings.Add(link.TargetID);

                    }
                }
            }

            Structure[] SiblingStructures = webService_GetStructures(graph, ListAbsentSiblings.ToArray());

            //Find missing structures and populate the list
            foreach (Structure child in ChildStructObjs)
            {
                foreach (StructureLink link in child.Links)
                {
                    if (!graph.NodeList.ContainsKey(link.SourceID))
                    {
                        continue;
                    }

                    if (!graph.NodeList.ContainsKey(link.TargetID))
                    {
                        continue;
                    }

                    //After this point both nodes are already in the graph and we can create an edge
                    Structure SourceCell = graph.NodeList[link.SourceID];
                    Structure TargetCell = graph.NodeList[link.TargetID];

                    if (TargetCell.ParentID != null && SourceCell.ParentID != null)
                    {
                        string SourceTypeName = "";
                        if (StructureTypesDictionary.ContainsKey(SourceCell.TypeID))
                        {
                            SourceTypeName = StructureTypesDictionary[SourceCell.TypeID].Name;
                        }

                        Edgex E = new Edgex(SourceCell.ParentID.Value, TargetCell.ParentID.Value, link, SourceTypeName);
                        graph.EdgeList.Add(E);
                    }
                }
            }

            List<long> ListAbsentParents = new List<long>(SiblingStructures.Length);

            //Find a list of the parentIDs we are missing, and add them to the graph, and return them
            //so we can easily make another hop later
            foreach (Structure sibling in SiblingStructures)
            {
                if (sibling.ParentID.HasValue == false)
                    continue;

                if (graph.NodeList.ContainsKey(sibling.ParentID.Value))
                    continue;

                if (ListAbsentParents.Contains(sibling.ParentID.Value) == false)
                    ListAbsentParents.Add(sibling.ParentID.Value);
            }



            return ListAbsentParents;
        }
     
    public Graphx getGraph(int cellID, int numHops)
        {
            // Create a new graph
            Graphx graph = new Graphx();

            // Get all the missing nodes
            List<long> MissingNodes = new List<long>(new long[] { cellID });

            // Get the nodes and build graph for numHops
            for (int i = 0; i < numHops; i++)
            {
                MissingNodes = webService_GetHop(graph, MissingNodes.ToArray());
            }

            //Tell the graph which cells are not fully populated
            graph.FrontierNodes = MissingNodes;

            var structLocations = db.ApproximateStructureLocations();

            foreach (ApproximateStructureLocationsResult result in structLocations)
            {
                if (result == null)
                    continue;

                if (graph.NodeList.ContainsKey(result.ParentID))
                {
                    Structure structure = graph.NodeList[result.ParentID];

                    if (structure.ParentID.HasValue)
                        graph.zLocationForSynapses.Add(result.ParentID, (long)Math.Round((double)result.Z));
                    else
                    {
                        graph.locationInfo.Add(result.ParentID, new LocationInfo((double)result.X, (double)result.Y, (double)result.Z, (double)result.Radius));
                        graph.InvolvedCells.Add(result.ParentID);
                    }
                }

                if (graph.FrontierNodes.Contains(result.ParentID))
                {
                    graph.locationInfo.Add(result.ParentID, new LocationInfo((double)result.X, (double)result.Y, (double)result.Z, (double)result.Radius));
                }
            }
           
            return graph;
        }
     */
}
