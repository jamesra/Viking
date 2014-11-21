using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using GraphLib;
using AnnotationVizLib.AnnotationService;
using System.Diagnostics;


namespace AnnotationVizLib
{
    public class MotifEdge : Edge<string>, IComparer<MotifEdge>, IComparable<MotifEdge>
    {
        public string SynapseType;

        /// <summary>
        /// A list of unique values indicating which structures have this type of connection
        /// </summary>
        public List<long> SourceStructIDs = new List<long>();

        /// <summary>
        /// A list of unique values indicating which structures have this type of connection
        /// </summary>
        public List<long> TargetStructIDs = new List<long>();

        public void AddEdgeInstance(long SourceStructID, long TargetStructID)
        {
            if (!SourceStructIDs.Contains(SourceStructID))
                SourceStructIDs.Add(SourceStructID);

            if (!TargetStructIDs.Contains(TargetStructID))
                TargetStructIDs.Add(TargetStructID); 

        }

        public MotifEdge(string SourceKey, string TargetKey, string SynapseType)
            : base(SourceKey, TargetKey)
        {
            this.SynapseType = SynapseType;
        }

        public override string ToString()
        {
            return this.SourceNodeKey + " -> " + this.TargetNodeKey + " via " + this.SynapseType;
        }

        public override int GetHashCode()
        {
            return this.SourceNodeKey.GetHashCode();
        }

        public int Compare(MotifEdge x, MotifEdge y)
        {
            if (x == null && y == null)
                return 0;

            if (x == null)
                return -1;
            if (y == null)
                return 1;

            return x.CompareTo(y);
        }

        public int CompareTo(MotifEdge other)
        {
            int SourceComparison = this.SourceNodeKey.CompareTo(other.SourceNodeKey);
            int TargetComparison = this.TargetNodeKey.CompareTo(other.TargetNodeKey);
            int SynapseTypeComparison = this.SynapseType.CompareTo(other.SynapseType);

            if (SourceComparison == 0 && TargetComparison == 0)
                return SynapseTypeComparison;

            if (SourceComparison != 0)
                return SourceComparison;

            return TargetComparison;
        }
    }

    public class MotifNode : Node<string, MotifEdge>
    {
        //Structures that belong to this node
        public List<Structure> Structures;

        public MotifNode(string key, IEnumerable<Structure> value)
            : base(key)
        {
            this.Structures = new List<Structure>();
            this.Structures.AddRange(value);
        }

        public override string ToString()
        {
            string Label = this.Key;

            foreach (Structure s in Structures)
            {
                Label = Label + ", " + s.ID.ToString();
            }

            return Label;
        }
    } 
    

    public class MotifGraph : Graph<string, MotifNode, MotifEdge>
    {
         
        public SortedList<string, List<AnnotationService.Structure>> LabelToStructures = null; 

        SortedDictionary<long, Structure> ChildIDToParent = new SortedDictionary<long,Structure>();
        SortedDictionary<long, Structure> IDToStructure = new SortedDictionary<long,Structure>();

        SortedDictionary<long, StructureType> TypeIDToType;

        public MotifGraph()
        {
            
        }

        public override string ToString()
        {
            List<string> AlreadyAdded = new List<string>();
            
            foreach (MotifEdge e in this.Edges.Values)
            {
                string EdgeLabel = e.ToString();
                if (!AlreadyAdded.Contains(EdgeLabel))
                {
                    
                    AlreadyAdded.Add(EdgeLabel);
                }
            }

            AlreadyAdded.Sort(); 

            string Label = "";
            foreach (string l in AlreadyAdded)
            {
                Label = Label + l + '\n'; 
            }

            return Label; 
        }

        public static MotifGraph BuildGraph(string Endpoint, System.Net.NetworkCredential userCredentials)
        {
            ConnectionFactory.SetConnection(Endpoint, userCredentials);          

            MotifGraph graph = new MotifGraph(); 

            using (AnnotateStructureTypesClient proxy = ConnectionFactory.CreateStructureTypesClient())
            {
                graph.TypeIDToType = Queries.GetStructureTypes(proxy);
                //graph.LabelToStructures = Queries.LabelToStructuresMap(proxy);

                //UnmappedStructures = new List<long>(graph.LabelToStructures.Count * 4);
            }

            using (AnnotateStructuresClient proxy = ConnectionFactory.CreateStructuresClient())
            {
                StructureLink[] AllStructureLinks = proxy.GetLinkedStructures();
                SortedDictionary<long, List<StructureLink>> StructIDToLinks = Queries.GetLinkedStructures(AllStructureLinks);

                //Find the parents of the linked structures, if they exist
                Structure[] linkedStructures = Queries.GetStructuresByIDs(proxy, StructIDToLinks.Keys.ToArray());
                List<long> ParentIDs = new List<long>(linkedStructures.Count());
                foreach (Structure s in linkedStructures)
                {
                    if (s.ParentID.HasValue)
                    {
                        graph.IDToStructure.Add(s.ID, s);
                        if (!ParentIDs.Contains(s.ParentID.Value))
                            ParentIDs.Add(s.ParentID.Value);
                    }
                }

                ParentIDs.Sort();

                Structure[] ParentStructures = Queries.GetStructuresByIDs(proxy, ParentIDs.ToArray()); //Don't query child structures because we know the linked ones
                foreach (Structure s in ParentStructures)
                {

                    if (graph.IDToStructure.ContainsKey(s.ID))
                    {
                        Trace.WriteLine(s.ID.ToString() + " uses another child structure as a parent");
                        continue;
                    }

                    graph.IDToStructure.Add(s.ID, s);
                }

                graph.LabelToStructures = Queries.LabelToStructuresMap(ParentStructures);

                foreach (Structure s in linkedStructures)
                {
                    if (s.ParentID.HasValue)
                    {
                        long ParentID = s.ParentID.Value;
                        Debug.Assert(graph.IDToStructure.ContainsKey(ParentID));
                        Structure Parent = graph.IDToStructure[ParentID];
                        graph.ChildIDToParent[s.ID] = Parent;
                        List<long> children = new List<long>(Parent.ChildIDs);
                        children.Add(s.ID);
                        Parent.ChildIDs = children.ToArray();
                    }
                }

                foreach (string Label in graph.LabelToStructures.Keys)
                {
                    List<Structure> StructuresForLabel = graph.LabelToStructures[Label];
                    MotifNode node = new MotifNode(Label, StructuresForLabel);
                    graph.AddNode(node);
                }
                 
                //OK, build some edges
                SortedDictionary<MotifEdge, MotifEdge> dictEdges = new SortedDictionary<MotifEdge, MotifEdge>(); 
                foreach (StructureLink link in AllStructureLinks)
                { 
                    try
                    {
                        Structure SourceStructure = graph.IDToStructure[link.SourceID];

                        StructureType type = graph.TypeIDToType[SourceStructure.TypeID];
                        string ConnectionLabel = type.Name;

                        Structure ParentOfSource = graph.ChildIDToParent[link.SourceID];
                        Structure ParentOfTarget = graph.ChildIDToParent[link.TargetID];

                        string SourceLabel = Queries.BaseLabel(ParentOfSource.Label);
                        string TargetLabel = Queries.BaseLabel(ParentOfTarget.Label);

                        MotifEdge edge = new MotifEdge(SourceLabel, TargetLabel, ConnectionLabel);

                        if (!dictEdges.ContainsKey(edge))
                            dictEdges.Add(edge,edge);
                        else
                            edge = dictEdges[edge];

                        edge.AddEdgeInstance(ParentOfSource.ID, ParentOfTarget.ID); 
                    }

                    catch (System.Collections.Generic.KeyNotFoundException e)
                    {
                        //Add it to the UnmappedStructures pile
                        Trace.WriteLine(e.Message); 
                        //Debug.Fail("Why do we not have a mapping for this object, DB change during query? " + e.Message);
                        continue;
                    }
                }

                foreach( MotifEdge edge in dictEdges.Values)
                    graph.AddEdge(edge);
            }

            return graph; 
        }


    }
}
