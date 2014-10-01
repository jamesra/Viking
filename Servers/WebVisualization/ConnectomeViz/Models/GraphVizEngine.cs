using System;
using System.Data;
using System.Configuration;
using System.Collections.Generic; 
using System.Linq;
using System.Web;
using System.Web.Security;
using System.Web.UI;
using System.Web.UI.HtmlControls;
using System.Web.UI.WebControls;
using System.Web.UI.WebControls.WebParts;
using System.Xml.Linq;
using System.IO;
using System.Text;
using System.Diagnostics;
using System.Text.RegularExpressions;

namespace ConnectomeViz.Models
{
    public class Nodes
    {
        public long ID;
        public string label;
        public Dictionary<string, string> nodeAttributes = new Dictionary<string, string>();

        public Nodes(long id, string lbl)
        {
            this.ID = id;
            this.label = lbl;
        }

        public Nodes(long id)
        {
            this.ID = id;
            this.label = id.ToString();
        }

    }
    public class Edges : ICloneable
    {
        public string label;
        public long from;
        public long to;
        public Dictionary<string, string> edgeAttributes = new Dictionary<string, string>();


        #region ICloneable Members

        public object Clone()
        {
            Edges clone = new Edges();
            clone.label = label;
            clone.from = from;
            clone.to = to;
            foreach (string key in edgeAttributes.Keys)
            {
                clone.edgeAttributes.Add(key, edgeAttributes[key]);
            }

            return clone; 
        }

        #endregion
    }

    public class GraphVizEngine
    {
        public string graphType;
        private string graphDefinition;
        public string connector; 
        public string graphLabel;
        public string completePath_URL;
        public Dictionary<string, string> graphAttribites = new Dictionary<string,string>();
        public Dictionary<long, Nodes> nodes = new Dictionary<long, Nodes>();
        public Dictionary<string, List<long>> subgraphs = new Dictionary<string, List<long>>(); 
        public List<Edges> edges = new List<Edges>();
        public string layout;
        public List<string> outputFormats = new List<string>();
        public bool minimize;
        public string completePath_local
        {get;set;}

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

        public void AssignNodeToSubgraph(string subgraphName, long NodeID)
        {
            //Determine if we have an entry in our subgraph or if we need to add it.
            if (!this.subgraphs.ContainsKey(subgraphName))
            {
                List<long> listSubgraphNodes = new List<long>();
                listSubgraphNodes.Add(NodeID);
                subgraphs.Add(subgraphName, listSubgraphNodes);
            }
            else
            {
                subgraphs[subgraphName].Add(NodeID);
            }
        }


        public Nodes addNode(long ID)
        {
            Nodes tempNode = new Nodes(ID);
            nodes.Add(ID,tempNode);
            //tempNode.nodeAttributes.Add("style", "filled");
            //tempNode.nodeAttributes.Add("target", "_top");
            //tempNode.nodeAttributes.Add("penwidth", "0.0");
            //tempNode.nodeAttributes.Add("fontsize", "8");
            //tempNode.nodeAttributes.Add("fontname", "Helvetica");
            return tempNode;
        }

        public void removeNode(long label)
        {
            if (nodes.ContainsKey(label))
                nodes.Remove(label);
        }
        public void addEdge(Edges edge)
        {
            edges.Add(edge); 
        }

        public void removeEdge(Edges edge)
        {
            if(edges.Contains(edge))
                edges.Remove(edge);
        }

        private static void WriteAttributesToDOT(StringWriter sw, Dictionary<string, string> dict)
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
                WriteAttributesToDOT(sw, this.graphAttribites); 
                sw.Write("\n"); 

                //Draw nodes
                foreach (KeyValuePair<long, Nodes> node in this.nodes)
                {
                    sw.Write(node.Key + " ");
                    WriteAttributesToDOT(sw, node.Value.nodeAttributes); 
                    sw.Write('\n');
                }

                sw.Write('\n'); 

                //Draw Edges
                foreach (Edges edge in this.edges)
                {
                    sw.Write(edge.from + connector + edge.to);
                    WriteAttributesToDOT(sw, edge.edgeAttributes);
                    sw.Write('\n'); 
                }

                sw.Write('\n'); 

                //Assign nodes to subgraphs
                foreach (KeyValuePair<string, List<long>> subgraph in this.subgraphs)
                {
                    
                    //No need for a subgraph if only one node included 
                    if (subgraph.Value.Count < 2)
                        continue;

                    if (subgraph.Key.Length < 1)
                        continue;

                    StringBuilder sb = new StringBuilder(subgraph.Value.Count * 16);

                    sb.Append("\nsubgraph \"" + subgraph.Key + "\" {");

                    int NumValidKeys = 0; 
                    foreach (long nodeID in subgraph.Value)
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

        public string Output()

        {
            String dotString = this.ToString();

            using (StreamWriter fs = new StreamWriter(this.completePath_local + ".dot", false, Encoding.ASCII, dotString.Length))
            {
                fs.Write(dotString);
            }

            string svgfile = this.completePath_local + graphLabel + ".svg";
            if (System.IO.File.Exists(svgfile))
            {
                File.Delete(svgfile);
            }
            
            string output="blank";
            bool failed = false;
            int length = outputFormats.Count();
            //foreach(string type in outputFormats)
            //{
            //    p.StartInfo.FileName = layout+".exe";
            //    p.StartInfo.Arguments = "-T"+type+" "+this.outputPath+graphLabel+".dot"+ " -o " + this.outputPath+graphLabel+ "."+type;
            //    p.StartInfo.UseShellExecute = false;
                
            //    p.StartInfo.RedirectStandardOutput = true;
            //    p.Start();
            //    output = p.StandardOutput.ReadToEnd();
            //    p.WaitForExit();
            //    if (p.ExitCode != 0)
            //    {
            //        failed = true;
            //    }
            //}


            //FileStream ft = new FileStream(completePath_local + "query.txt", FileMode.OpenOrCreate);
            //StreamWriter wr = new StreamWriter(ft);
            //wr.Write("came".ToString() + ", " + DateTime.Now);
            //wr.Close();
            //ft.Close();

            for(int i=0;i< length;i++)
            {
                Process p = new Process();
                string type = outputFormats[i];
                 
                if (type == "svg")
                {
                   State.svgProcessReference = p;
                }
                
                p.StartInfo.FileName = layout + ".exe";
                p.StartInfo.Arguments = "-T" + type + " " + this.completePath_local+ ".dot" + " -o " + this.completePath_local + "." + type;
                p.StartInfo.UseShellExecute = false;
                

                p.StartInfo.RedirectStandardOutput = true;
                p.StartInfo.WindowStyle = ProcessWindowStyle.Hidden;
                p.Start();
                                
                //output = p.StandardOutput.ReadToEnd();
                //p.WaitForExit();
            }

            try
            {
                State.svgProcessReference.WaitForExit();
            }
            catch (Exception e)
            {
                return e.Message; 
            }

            svgfile = this.completePath_local + ".svg";

            SVG.InjectSVGViewer(svgfile, virtualRoot);

            return output;

        }
       
    }
}
