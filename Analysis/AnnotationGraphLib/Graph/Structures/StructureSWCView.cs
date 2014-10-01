using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.IO;

namespace AnnotationUtils.Graph.Structures
{

    public enum SWCType
        {
            UNDEFINED = 0,
            SOMA = 1, 
            AXON = 2, 
            DENDRITE = 3, 
            APICAL_DENDRITE = 4,
            FORK_POINT = 5, 
            END_POINT = 6,
            CUSTOM = 7
        };

    class SWCStackData
    {
        public LocationNode node { get; private set; }
        public SWCType Type { get; private set; }
        public long ParentID { get; private set; }
        public List<long> KnownLocations {get; private set;}

        public SWCStackData(LocationNode node, SWCType type, long ParentID)
        {
            this.node = node;
            this.Type = type;
            this.ParentID = ParentID;
        }

        public override string ToString()
        {
            return string.Format("{0}: {1} {2} {3}", node.Location.ID.ToString(),
                                                     Type.ToString(),
                                                     ParentID.ToString());
        }
    }


    public class StructureSWCView
    {

        List<long> MasterKnownLocationList;
        Queue<SWCStackData> queue = new Queue<SWCStackData>();

        public StructureSWCView(StructureGraph graph)
        {

            Stack<LocationNode> segmentStack = new Stack<LocationNode>();
            MasterKnownLocationList = new List<long>(graph.Nodes.Count);
            LocationNode somaNode = StructureSWCView.LargestNode(graph);

            queue.Enqueue(new SWCStackData(somaNode, SWCType.SOMA, -1));
            ProcessSWCStack(graph, queue);
        }

        private static Structures.LocationNode LargestNode(StructureGraph graph)
        {
            double maxradius = 0.0; 
            LocationNode largestNode = null;
            foreach (LocationNode node in graph.Nodes.Values)
            {
                if(node.Location.Radius > maxradius)
                {
                    maxradius = node.Location.Radius;
                    largestNode = node;
                }
            }

            return largestNode;
        }

        private int NextLineNumber = 1;
        private int NextSegmentNumber = 1;

        private StringBuilder sb = new StringBuilder();

        private int WriteLocationEntry(StringBuilder sb, LocationNode node, SWCType Type, long parent)
        {
            const string FormatString = "{0} {1} {2} {3} {4} {5} {6}";



            string outString = string.Format(FormatString, new object[] {NextLineNumber,
                                                      Type.ToString("D"),
                                                      node.Location.VolumePosition.X * 2.18,
                                                      node.Location.VolumePosition.Y * 2.18,
                                                      node.Location.VolumePosition.Z * 90,
                                                      node.Location.Radius,
                                                      parent});
            sb.AppendLine(outString);

            NextLineNumber++;

            return NextLineNumber - 1;
        }

        

        private void AddLinks(StructureGraph graph, LocationNode loc, SWCType Type, long SWCID)
        {
            foreach(long locID in loc.Location.Links)
            {
                if (MasterKnownLocationList.Contains(locID))
                    continue; 

                LocationNode node = graph.Nodes[locID];

                queue.Enqueue(new SWCStackData(node, Type, SWCID));
            }
        }

        

        private List<long> AddKnownBranchesToList(LocationNode loc)
        {
            List<long> listKnownBranches = new List<long>(loc.Location.Links.Length);

            listKnownBranches.Add(loc.Location.ID);
            foreach (long locID in loc.Location.Links)
            {
                if (MasterKnownLocationList.Contains(locID))
                {
                    listKnownBranches.Add(locID);
                    continue;
                }

            }

            return listKnownBranches;
        }

        private void TraceLocation(StructureGraph graph, LocationNode loc, SWCType Type, long ParentID)
        {
            MasterKnownLocationList.Add(loc.Location.ID);

            int SWCID;

            if (loc.Edges.Count == 1)
            {
                SWCID = WriteLocationEntry(sb, loc, SWCType.END_POINT, ParentID);
            }
            else if (loc.Edges.Count == 2)
            {
                SWCID = WriteLocationEntry(sb, loc, Type, ParentID);
                AddLinks(graph, loc, Type, SWCID);
            }
            else
            {
                SWCID = WriteLocationEntry(sb, loc, SWCType.FORK_POINT, ParentID);

                List<long> NewAddedLocations = AddKnownBranchesToList(loc);
                AddLinks(graph, loc, SWCType.AXON, SWCID);
            }
        }

        private void ProcessSWCStack(StructureGraph graph, Queue<SWCStackData> queue)
        {
            while(queue.Count > 0)
            {
                SWCStackData entry = queue.Dequeue();

                TraceLocation(graph, entry.node, entry.Type, entry.ParentID);
            }
        }


       

        public override string ToString()
        {
            return sb.ToString();
        }

        public void Save(string FileFullPath)
        {
            using (FileStream fl = new FileStream(FileFullPath, FileMode.Create, FileAccess.Write))
            {
                using (StreamWriter write = new StreamWriter(fl))
                {
                    write.Write(this.ToString());
                    write.Close();
                }
                fl.Close();
            }
        }
    }
}
