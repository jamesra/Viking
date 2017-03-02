using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using AnnotationVizLib;
using AnnotationVizLib.WCFClient.AnnotationService;
using AnnotationVizLib.WCFClient;
using System.Diagnostics;

namespace AnnotationVizLib.WCFClient
{
    public class WCFMotifFactory
    { 
        public static MotifGraph BuildGraph(string Endpoint, System.Net.NetworkCredential userCredentials)
        {
            SortedDictionary<long, StructureType> TypeIDToType;
            SortedDictionary<long, Structure> ChildIDToParent = new SortedDictionary<long, Structure>();
            SortedDictionary<long, Structure> IDToStructure = new SortedDictionary<long, Structure>();
            SortedList<string, List<Structure>> LabelToStructures = null;

            ConnectionFactory.SetConnection(Endpoint, userCredentials);

            MotifGraph graph = new MotifGraph();

            using (AnnotateStructureTypesClient proxy = ConnectionFactory.CreateStructureTypesClient())
            {
                TypeIDToType = Queries.GetStructureTypes(proxy);
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
                        IDToStructure.Add(s.ID, s);
                        if (!ParentIDs.Contains(s.ParentID.Value))
                            ParentIDs.Add(s.ParentID.Value);
                    }
                }

                ParentIDs.Sort();

                Structure[] ParentStructures = Queries.GetStructuresByIDs(proxy, ParentIDs.ToArray()); //Don't query child structures because we know the linked ones
                foreach (Structure s in ParentStructures)
                {

                    if (IDToStructure.ContainsKey(s.ID))
                    {
                        Trace.WriteLine(s.ID.ToString() + " uses another child structure as a parent");
                        continue;
                    }

                    IDToStructure.Add(s.ID, s);
                }

                LabelToStructures = Queries.LabelToStructuresMap(ParentStructures);

                foreach (Structure s in linkedStructures)
                {
                    if (s.ParentID.HasValue)
                    {
                        long ParentID = s.ParentID.Value;
                        Debug.Assert(IDToStructure.ContainsKey(ParentID));
                        Structure Parent = IDToStructure[ParentID];
                        ChildIDToParent[s.ID] = Parent;
                        List<long> children = Parent.ChildIDs == null ? new List<long>() : new List<long>(Parent.ChildIDs);
                        children.Add(s.ID);
                        Parent.ChildIDs = children.ToArray();
                    }
                }

                foreach (string Label in LabelToStructures.Keys)
                {
                    List<Structure> StructuresForLabel = LabelToStructures[Label];
                    MotifNode node = new MotifNode(Label, StructuresForLabel.ConvertAll(s => new WCFStructureAdapter(s)));
                    graph.AddNode(node);
                }

                //OK, build some edges
                SortedDictionary<MotifEdge, MotifEdge> dictEdges = new SortedDictionary<MotifEdge, MotifEdge>();
                foreach (StructureLink link in AllStructureLinks)
                {
                    try
                    {
                        Structure SourceStructure = IDToStructure[link.SourceID];

                        StructureType type = TypeIDToType[SourceStructure.TypeID];
                        string ConnectionLabel = type.Name;

                        Structure ParentOfSource = ChildIDToParent[link.SourceID];
                        Structure ParentOfTarget = ChildIDToParent[link.TargetID];

                        string SourceLabel = Queries.BaseLabel(ParentOfSource.Label);
                        string TargetLabel = Queries.BaseLabel(ParentOfTarget.Label);

                        MotifEdge edge = new MotifEdge(SourceLabel, TargetLabel, ConnectionLabel);

                        if (!dictEdges.ContainsKey(edge))
                            dictEdges.Add(edge, edge);
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

                foreach (MotifEdge edge in dictEdges.Values)
                    graph.AddEdge(edge);
            }

            return graph;
        }

    }
}
