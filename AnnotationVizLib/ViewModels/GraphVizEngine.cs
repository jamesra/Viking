using System;
using System.Diagnostics;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.IO;

namespace AnnotationVizLib
{
    public class GraphVizNode<KEY> : GraphViewNode<KEY>
        where KEY : IComparable<KEY>
    {
        public GraphVizNode(KEY key) : base(key) { }
    }

    public class GraphVizEdge<KEY> : GraphViewEdge<KEY>, ICloneable
        where KEY : IComparable<KEY>
    {  

        public void Reverse()
        {
            KEY temp = this.from;
            this.from = this.to;
            this.to = temp; 
        }

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

        #endregion

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

        
    }

    public class GraphVizEngine<KEY> : GraphViewEngine<KEY>
        where KEY : IComparable<KEY>
    {
        public string graphType;
        private string graphDefinition;
        public string connector = "->";
        public string graphLabel;
        public string completePath_URL;
        public string layout = "dot";
        public List<string> outputFormats = new List<string>();
        public bool minimize;

        protected void createDirectedGraph(string name)
        {
            graphType = "directed";
            connector = "->";
            graphLabel = name;
            graphDefinition = "digraph " + name + "{\n";
        }

        protected void createUndirectedGraph(string name)
        {
            graphType = "undirected";
            connector = "--";
            graphLabel = name;
            graphDefinition = "graph " + name + "{\n";
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
                sw.Write("graph");
                WriteAttributesToDOT(sw, this.Attributes);
                sw.Write("\n");

                //Draw nodes
                foreach (KeyValuePair<KEY, GraphViewNode<KEY>> node in this.nodes)
                {
                    sw.Write("\"" + node.Key + "\"" + " ");
                    WriteAttributesToDOT(sw, node.Value.Attributes);
                    sw.Write('\n');
                }

                sw.Write('\n');

                //Draw Edges
                foreach (GraphViewEdge<KEY> edge in this.edges)
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

        public void SaveDOT(string DotFileFullPath)
        {
            using (StreamWriter write = new StreamWriter(DotFileFullPath, false))
            {
                write.Write(this.ToString());
                write.Close();
            }

            Debug.Assert(System.IO.File.Exists(DotFileFullPath), "Dot file we just wrote does not exist");
        }

        /// <summary>
        /// Run the GraphViz program on a dot file to create a layout for nodes. 
        /// </summary>
        /// <param name="GraphVizExe">Path to GraphViz .exe</param>
        /// <param name="DotFileFullPath">Input .dot file for GraphViz</param>
        /// <param name="OutputExtensions">File extensions for output</param>
        /// <returns></returns>
        static public IList<string> Convert(string GraphVizExe, string DotFileFullPath, string[] OutputExtensions)
        {
            Debug.Assert(System.IO.File.Exists(DotFileFullPath), "Input dot file does not exist");

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
                p.StartInfo.Arguments = "-T" + type + " \"" + DotFileFullPath + "\" -o \"" + OutputFile + "\"";
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
