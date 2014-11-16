using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.IO;
using System.Diagnostics;

namespace AnnotationVizLib
{
    public class MotifTLPView
    {
        MotifGraph motifGraph = null;

        private MotifTLPView(MotifGraph graph)
        {
            motifGraph = graph;
        }

        private static string Header()
        {
            return "(tlp \"2.0\"";
        }

        private static string Footer()
        {
            return ")";
        }

        /// <summary>
        /// Map the motif edge to an arbitrary id used by TLP
        /// </summary>
        SortedDictionary<ulong, MotifEdge> edges = new SortedDictionary<ulong, MotifEdge>();


        /// <summary>
        /// Map the motif label to the arbitrary id used by TLP
        /// </summary>
        SortedDictionary<MotifEdge, ulong> EdgeToIndex = new SortedDictionary<MotifEdge, ulong>();


        /// <summary>
        /// Map the motif node to an arbitrary id used by TLP
        /// </summary>
        SortedDictionary<ulong, MotifNode> nodes = new SortedDictionary<ulong, MotifNode>();


        /// <summary>
        /// Map the motif label to the arbitrary id used by TLP
        /// </summary>
        SortedDictionary<string, ulong> LabelToIndex = new SortedDictionary<string, ulong>();


        private ulong nextNodeIndex = 0;
        private void addNode(MotifNode node)
        {
            LabelToIndex.Add(node.Key, nextNodeIndex); 
            nodes.Add(nextNodeIndex++, node); 
        }


        private ulong nextEdgeIndex = 0;
        private void addEdge(MotifEdge edge)
        {
            EdgeToIndex.Add(edge, nextEdgeIndex);
            edges.Add(nextEdgeIndex++, edge);
        }

        

        public static MotifTLPView ToTLP(MotifGraph graph, bool IncludeUnlabeled = false)
        {
            MotifTLPView view = new MotifTLPView(graph);

            foreach (MotifNode node in graph.Nodes.Values)
            {
                if (node.Key == "Unlabeled" && !IncludeUnlabeled)
                    continue;

                view.addNode(node);
            }

            foreach (MotifEdge edge in graph.Edges.Values)
            {
                if (edge.SourceNodeKey == "Unlabeled" || edge.TargetNodeKey == "Unlabeled")
                    continue; 

                view.addEdge(edge);
            }

            return view; 
        }

        private static string NodesToString(ICollection<ulong> ids)
        {
            StringBuilder s = new StringBuilder();
            s.Append("(nodes ");
            foreach (ulong id in ids)
            {
                s.Append(id.ToString() + " ");
            }
            s.Append(")");

            return s.ToString();
        }

        private string EdgesToString()
        {
            StringBuilder s = new StringBuilder();
            foreach (MotifEdge edge in EdgeToIndex.Keys)
            {
                s.AppendLine(EdgeToString(edge));
            }

            return s.ToString();
        }

        private string EdgeToString(MotifEdge edge)
        {
            StringBuilder s = new StringBuilder();
            s.Append("(edge ");
            ulong edgeID = EdgeToIndex[edge];
            ulong sourceID = LabelToIndex[edge.SourceNodeKey];
            ulong targetID = LabelToIndex[edge.TargetNodeKey];

            return string.Format("(edge {0} {1} {2})", new object[] { edgeID.ToString(), sourceID.ToString(), targetID.ToString() });
        }

        private string LabelProperties()
        {
            StringBuilder s = new StringBuilder();
            s.AppendLine("(property 0 string \"viewLabel\"");
            s.AppendLine("\t(default \"\" \"\")");
            foreach (ulong id in nodes.Keys)
            {
                MotifNode node = nodes[id];
                s.AppendLine("\t(node " + id.ToString() + " \"" + node.Key + "\")");
            }
            s.AppendLine(")");

            return s.ToString(); 
        }

        public override string ToString()
        {
            using(StringWriter sw = new StringWriter())
            {
                sw.WriteLine(MotifTLPView.Header());

                sw.WriteLine(MotifTLPView.NodesToString(nodes.Keys));

                sw.WriteLine(EdgesToString());

                sw.WriteLine(LabelProperties()); 

                sw.WriteLine(MotifTLPView.Footer());
                return sw.ToString();
            } 
        }

        public void SaveTLP(string FullPath)
        {
            using (StreamWriter write = new StreamWriter(FullPath, false))
            {
                write.Write(this.ToString());
                write.Close();
            }

            Debug.Assert(System.IO.File.Exists(FullPath), "Dot file we just wrote does not exist"); 
        }
    }
}
