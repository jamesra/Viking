using System;
using System.Data;
using System.Configuration;
using System.Collections.Generic; 
using System.Linq; 
using System.Xml.Linq;
using System.IO;
using System.Text;
using System.Diagnostics;
using System.Text.RegularExpressions;

namespace AnnotationUtils
{
    public class GraphEntity<KEY>
        where KEY : IComparable<KEY>
    {
        public string label;

        public SortedDictionary<string, string> Attributes = new SortedDictionary<string, string>();

        /// <summary>
        /// Add the list of attributes to the node
        /// </summary>
        /// <param name="node"></param>
        /// <param name="attribs"></param>
        /// <returns></returns>
        public void AddAttributes(System.Collections.Generic.IDictionary<string, string> attribs)
        {

            foreach (string key in attribs.Keys)
            {
                Trace.WriteLineIf(this.Attributes.ContainsKey(key),
                                  "AddAttributes replacing existing key: " + key + " in " + this.ToString());

                this.Attributes[key] = attribs[key];
            }
        }

        public override string ToString()
        {
            string str = label + "\n";
            foreach (string key in this.Attributes.Keys)
            {
                str = str + "\t" + key + " : " + this.Attributes[key];
            }

            return str;
        }
    }

    public class GraphVizNode<KEY> : GraphEntity<KEY>
        where KEY : IComparable<KEY>
    {
        public KEY ID;
        
        public GraphVizNode(KEY id, string lbl)
        {
            this.ID = id;
            this.label = lbl;
        }

        public GraphVizNode(KEY id)
        {
            this.ID = id;
            this.label = id.ToString();
        } 
    }

    public class GraphVizEdge<KEY> : GraphEntity<KEY>, ICloneable
        where KEY : IComparable<KEY>
    {
        public KEY from;
        public KEY to;
        
        #region ICloneable Members

        public object Clone()
        {
            GraphVizEdge<KEY> clone = new GraphVizEdge<KEY>();
            clone.label = label;
            clone.from = from;
            clone.to = to;
            foreach (string key in Attributes.Keys)
            {
                clone.Attributes.Add(key, Attributes[key]);
            }

            return clone; 
        }

        /// <summary>
        /// This string lists the parent structures connected, i.e. cells
        /// </summary>
        public string KeyString
        {
            get
            {
                return to + "->" + from;
            }
        }

        #endregion
    }

    public class GraphVizEngine<KEY> : GraphEntity<KEY>
        where KEY : IComparable<KEY>
    {
        public string graphType;
        private string graphDefinition;
        public string connector = "->"; 
        public string graphLabel;
        public string completePath_URL; 
        public SortedDictionary<KEY, GraphVizNode<KEY>> nodes = new SortedDictionary<KEY, GraphVizNode<KEY>>();
        public SortedDictionary<string, List<KEY>> subgraphs = new SortedDictionary<string, List<KEY>>(); 
        public List<GraphVizEdge<KEY>> edges = new List<GraphVizEdge<KEY>>();
        public string layout = "dot";
        public List<string> outputFormats = new List<string>();
        public bool minimize; 

        public string virtualRoot {get;set;}
   
        public void createDirectedGraph(string name)
        {
            graphType = "Directed";
            connector = "->";
            graphLabel = name;
            graphDefinition = "DiGraph "+name+"{\n";
        }
        
        public void createUndirectedGraph(string name)
        {
            graphType = "Undirected";
            connector = "--";
            graphLabel = name;
            graphDefinition = "Graph "+name+"{\n";
        }

        public void AssignNodeToSubgraph(string subgraphName, KEY NodeID)
        {
            //Determine if we have an entry in our subgraph or if we need to add it.
            if (!this.subgraphs.ContainsKey(subgraphName))
            {
                List<KEY> listSubgraphNodes = new List<KEY>();
                listSubgraphNodes.Add(NodeID);
                subgraphs.Add(subgraphName, listSubgraphNodes);
            }
            else
            {
                subgraphs[subgraphName].Add(NodeID);
            }
        }


        public GraphVizNode<KEY> addNode(KEY ID)
        {
            GraphVizNode<KEY> tempNode = new GraphVizNode<KEY>(ID);
            nodes.Add(ID,tempNode);
            //tempNode.nodeAttributes.Add("style", "filled");
            //tempNode.nodeAttributes.Add("target", "_top");
            //tempNode.nodeAttributes.Add("penwidth", "0.0");
            //tempNode.nodeAttributes.Add("fontsize", "8");
            //tempNode.nodeAttributes.Add("fontname", "Helvetica");
            return tempNode;
        }

        public void removeNode(KEY label)
        {
            if (nodes.ContainsKey(label))
                nodes.Remove(label);
        }
        public void addEdge(GraphVizEdge<KEY> edge)
        {
            edges.Add(edge); 
        }

        public void removeEdge(GraphVizEdge<KEY> edge)
        {
            if(edges.Contains(edge))
                edges.Remove(edge);
        }

        private static void WriteAttributesToDOT(StringWriter sw, IDictionary<string, string> dict)
        {
            sw.Write('[');
            sw.Write('\n');
            bool first = true;

            int LongestKey = 0;
            int LongestVal = 0; 
            foreach (KeyValuePair<string, string> attribute in dict)
            {
                if (attribute.Key.Length > LongestKey)
                    LongestKey = attribute.Key.Length;

                if (attribute.Value.Length > LongestVal)
                    LongestVal = attribute.Value.Length;

            }

            string FormatStr = "\t{0," + (-LongestKey).ToString() + "} = {1," + (-(LongestVal + 1)).ToString() + "}";

            foreach (KeyValuePair<string, string> attribute in dict)
            {
                if (!first)
                {
                    sw.Write(",\n");
                }

                first = false;
                sw.Write(FormatStr, attribute.Key, "\"" + attribute.Value + "\"");
            }
            sw.Write("];\n"); 
        }

        public override string ToString()
        {
            StringWriter sw = null; 

            try
            {
                sw = new StringWriter();
            
                //StreamWriter sw = new StreamWriter(fs);
                sw.Write(graphDefinition);
                 
                bool first = true;
                sw.Write("graph");
                WriteAttributesToDOT(sw, this.Attributes); 
                sw.Write("\n"); 

                //Draw nodes
                foreach (KeyValuePair<KEY, GraphVizNode<KEY>> node in this.nodes)
                {
                    sw.Write("\"" + node.Key + "\"" + " ");
                    WriteAttributesToDOT(sw, node.Value.Attributes); 
                    sw.Write('\n');
                }

                sw.Write('\n'); 

                //Draw Edges
                foreach (GraphVizEdge<KEY> edge in this.edges)
                {
                    sw.Write("\"" + edge.from + "\"" + connector + "\"" + edge.to + "\"");
                    WriteAttributesToDOT(sw, edge.Attributes);
                    sw.Write('\n'); 
                }

                sw.Write('\n'); 

                //Assign nodes to subgraphs
                foreach (KeyValuePair<string, List<KEY>> subgraph in this.subgraphs)
                {
                    
                    //No need for a subgraph if only one node included 
                    if (subgraph.Value.Count < 2)
                        continue;

                    if (subgraph.Key.Length < 1)
                        continue;

                    StringBuilder sb = new StringBuilder(subgraph.Value.Count * 16);

                    sb.Append("\nsubgraph \"" + subgraph.Key + "\" {");

                    int NumValidKeys = 0; 
                    foreach (KEY nodeID in subgraph.Value)
                    {
                        if (this.nodes.ContainsKey(nodeID)) //Don't add unless we've created a node already or it will be a featureless white node
                        {
                            sb.Append(" " + nodeID.ToString());
                            NumValidKeys += 1; 
                        }
                    }

                    sb.Append(" }\n");

                    if (NumValidKeys > 1)
                    {
                        sw.Write(sb.ToString());

                    }
                }


                sw.Write("}");

                return sw.ToString(); 
            }
            finally
            {
                if (sw != null)
                {
                    sw.Close();
                    sw = null;
                }
            }
        }

        static public IList<string> Convert(string GraphVizExe, string DotFileFullPath, string[] OutputExtensions)
        { 
            int length = OutputExtensions.Count();
            string DotFilePath = System.IO.Path.GetDirectoryName(DotFileFullPath);
            string DotFileNameNoExtension = System.IO.Path.GetFileNameWithoutExtension(DotFileFullPath);
             
            DateTime DotFileLastWrite = System.IO.File.GetLastWriteTimeUtc(DotFileFullPath);

            List<Process> ProcessList = new List<Process>(length);
            List<string> listOutputFiles = new List<string>();

            string layout = System.IO.Path.GetFileNameWithoutExtension(GraphVizExe);

            for (int i = 0; i < length; i++)
            {
                string type = OutputExtensions[i];
                string OutputFile = DotFilePath + "\\" + DotFileNameNoExtension + "_" + layout + "." + type;

                listOutputFiles.Add(OutputFile); 

                //Don't generate files unless they are older than the .dot file we use as a source
                if (System.IO.File.Exists(OutputFile))
                {
                    if (System.IO.File.GetLastWriteTimeUtc(OutputFile) > DotFileLastWrite)
                    {
                        continue;
                    }
                } 
                
                Process p = new Process();
                ProcessList.Add(p);

                p.StartInfo.FileName = GraphVizExe;
                p.StartInfo.Arguments = "-T" + type + " " + DotFileFullPath + " -o " + OutputFile;
                p.StartInfo.UseShellExecute = true;

                //p.StartInfo.RedirectStandardOutput = true;
                p.StartInfo.WindowStyle = ProcessWindowStyle.Hidden;
                p.Start();

                //output = p.StandardOutput.ReadToEnd();
                //p.WaitForExit();
            }

            foreach (Process p in ProcessList)
            {
                p.WaitForExit();
            } 

            return listOutputFiles;
        }
       
    }
}
