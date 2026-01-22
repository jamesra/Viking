using AnnotationService.Types;
using AnnotationVizLib.WCFClient.AnnotationClient;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;

namespace AnnotationVizLib.WCFClient
{
    public class WCFMotifFactory
    {
        public static MotifGraph BuildGraph(string Endpoint, System.Net.NetworkCredential userCredentials)
        {
            SortedDictionary<long, StructureType> TypeIDToType;
            SortedDictionary<long, Structure> ChildIDToParent = [];
            SortedDictionary<long, Structure> IDToStructure = [];
            SortedList<string, List<Structure>> LabelToStructures = null;

            ConnectionFactory.SetConnection(Endpoint, userCredentials);

            MotifGraph graph = new();

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
                Structure[] linkedStructures = Queries.GetStructuresByIDs(proxy, [.. StructIDToLinks.Keys]);
                List<long> ParentIDs = new(linkedStructures.Count());
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

                Structure[] ParentStructures = Queries.GetStructuresByIDs(proxy, [.. ParentIDs]); //Don't query child structures because we know the linked ones
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
                        List<long> children = Parent.ChildIDs is null ? [] : [.. Parent.ChildIDs];
                        children.Add(s.ID);
                        Parent.ChildIDs = [.. children];
                    }
                }

                foreach (string Label in LabelToStructures.Keys)
                {
                    List<Structure> StructuresForLabel = LabelToStructures[Label];
                    MotifNode node = new(Label, StructuresForLabel.ConvertAll(s => new WCFStructureAdapter(s)));
                    graph.AddNode(node);
                }

                //OK, build some edges
                SortedDictionary<MotifEdge, MotifEdge> dictEdges = [];
                foreach (StructureLink link in AllStructureLinks)
                {
                    try
                    {
                        Structure SourceStructure = IDToStructure[link.SourceID];

                        StructureType type = TypeIDToType[SourceStructure.TypeID];
                        string ConnectionLabel = type.Name;

                        if (!ChildIDToParent.ContainsKey(link.SourceID))
                            continue;
                        if (!ChildIDToParent.ContainsKey(link.TargetID))
                            continue;

                        Structure ParentOfSource = ChildIDToParent[link.SourceID];
                        Structure ParentOfTarget = ChildIDToParent[link.TargetID];

                        string SourceLabel = Queries.BaseLabel(ParentOfSource.Label);
                        string TargetLabel = Queries.BaseLabel(ParentOfTarget.Label);

                        MotifEdge edge = new(SourceLabel, TargetLabel, ConnectionLabel);

                        if (dictEdges.TryGetValue(edge, out var result))
                            edge = result;
                        else
                            dictEdges.Add(edge, edge);

                        edge.AddEdgeInstance(ParentOfSource.ID, link.SourceID, ParentOfTarget.ID, link.TargetID);
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
