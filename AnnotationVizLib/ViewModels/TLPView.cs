using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.IO;
using System.Diagnostics;

namespace AnnotationVizLib
{
    /// <summary>
    /// Tulip IDs every node with an index.  This is seperate than the key for the source node we are viewing.
    /// </summary>
    /// <typeparam name="KEY"></typeparam>
    public class TLPViewNode : GraphViewNode<ulong>
    {
        public TLPViewNode(ulong id) : base(id)
        {
        }

        public TLPViewNode(ulong id, string lbl) : base(id, lbl)
        {
        }

        public System.Drawing.Color Color
        {
            set
            {  
                this.Attributes["viewColor"] = string.Format("({0},{1},{2},{3})", value.R,
                                                                                 value.G,
                                                                                 value.B,
                                                                                 value.A); 
            }            
        }

        public string AttributeToString(string attrib_name)
        {
            if (!this.Attributes.ContainsKey(attrib_name))
                return null; 

            return string.Format("(node {0} \"{1}\")", this.Key, this.Attributes[attrib_name]);
        }
    }

    /// <summary>
    /// Tulip IDs every edge with an index. The TLPViewEdge records the TO/FROM index of the linked nodes, and the unique index of the edge itself.
    /// </summary>
    /// <typeparam name="KEY"></typeparam>
    public class TLPViewEdge : GraphViewEdge<ulong>
    {
        /// <summary>
        /// TLP file ID
        /// </summary>
        public ulong tulip_id;

        public TLPViewEdge(ulong id)
        {
            this.tulip_id = id;
        }

        public string DefinitionString()
        {
            return string.Format("(edge {0} {1} {2})", tulip_id, from, to);
        }

        public string AttributeToString(string attrib_name)
        {
            if (!this.Attributes.ContainsKey(attrib_name))
                return null;

            return string.Format("(edge {0} \"{1}\")", this.tulip_id, this.Attributes[attrib_name]);
        }
    }

    internal static class TLPFile
    {
        public static string FileHeader
        {
            get
            {
                return "(tlp \"2.0\"";
            }
        }

        public static string FileFooter
        {
            get
            {
                return GenericFooter;
            }
        }

        public static string GenericFooter
        {
            get
            {
                return ")";
            }
        } 

        public static string ClusterHeader(ulong id, string name)
        {
            return string.Format("(cluster {0} \"{1}\"", id, name);
        }

        public static string ClusterFooter()
        {
            return GenericFooter;
        }

        public static string PropertyHeader(string attribName)
        {
            string attribType = "string";
            if (TLPAttributes.TLPTypeForAttribute.ContainsKey(attribName))
                attribType = TLPAttributes.TLPTypeForAttribute[attribName];

            return string.Format("(property 0 {0} \"{1}\"", attribType, attribName);
        }

        public static string PropertyFooter
        {
            get
            {
                return GenericFooter;
            }
        }
         
    }

    public class TLPViewSubgraph
    {
        public readonly ulong ID; 
        public string Label;

        public SortedDictionary<string, string> SubgraphAttributes = new SortedDictionary<string, string>();
        public SortedDictionary<ulong, TLPViewSubgraph> SubGraphs = new SortedDictionary<ulong, TLPViewSubgraph>();

        SortedSet<ulong> NodeIDs = new SortedSet<ulong>();
        SortedSet<ulong> EdgeIDs = new SortedSet<ulong>();

        public TLPViewSubgraph(ulong subgraph_ID, string label)
        {
            this.ID = subgraph_ID;
            this.Label = label;
        }

        public void AddNode(ulong ID)
        {
            Debug.Assert(!NodeIDs.Contains(ID));
            NodeIDs.Add(ID);
        }

        public void AddEdge(ulong ID)
        {
            Debug.Assert(!EdgeIDs.Contains(ID));
            EdgeIDs.Add(ID);
        }

        public void AddSubgraph(ulong ID, TLPViewSubgraph subgraph)
        {
            Debug.Assert(!SubGraphs.ContainsKey(ID));
            SubGraphs.Add(ID, subgraph);
        }

        public System.Drawing.Color Color
        {
            set
            {
                this.SubgraphAttributes["viewColor"] = string.Format("({0},{1},{2},{3} {0},{1},{2},{3})", value.R,
                                                                                 value.G,
                                                                                 value.B,
                                                                                 value.A);
            }
        }
         
        private string NodesString(IEnumerable<ulong> node_ids)
        {
            if (node_ids.Count() == 0)
                return ""; 

            using (StringWriter sw = new StringWriter())
            {
                sw.Write("(nodes ");
                foreach(long id in node_ids)
                {
                    sw.Write(string.Format("{0} ", id));
                }
                sw.WriteLine(TLPFile.GenericFooter);

                return sw.ToString();
            }
        }

        private string EdgesString(IEnumerable<ulong> edge_ids)
        {
            if (edge_ids.Count() == 0)
                return ""; 

            using (StringWriter sw = new StringWriter())
            {
                sw.Write("(edges ");
                foreach (long id in edge_ids)
                {
                    sw.Write(string.Format("{0} ", id));
                }
                sw.WriteLine(TLPFile.GenericFooter);

                return sw.ToString();
            }
        }

        /// <summary>
        /// Cluster graph bodies are composed of properties and the (nodes # # #) and (edges # #) id numbers from the main tlp file
        /// </summary>
        /// <returns></returns>
        private string ClusterGraphBody()
        {
            using (StringWriter sw = new StringWriter())
            { 
                sw.Write(NodesString(this.NodeIDs));
                sw.Write(EdgesString(this.EdgeIDs)); 

                foreach (ulong subgraph_id in this.SubGraphs.Keys)
                {
                    sw.Write(this.SubGraphs[subgraph_id].ToClusterString());
                }

                return sw.ToString();
            }
        } 

        public string ToClusterString()
        {
            using (StringWriter sw = new StringWriter())
            {
                sw.WriteLine(TLPFile.ClusterHeader(this.ID, this.Label));

                sw.WriteLine(ClusterGraphBody());

                sw.WriteLine(TLPFile.GenericFooter);
                return sw.ToString();
            }
        }

        public override string ToString()
        {
            return string.Format("{0} {1}", this.ID, this.Label); 
        }
    }


    public abstract class TLPView<VIEWED_KEY>: GraphViewEngine<ulong>
        where VIEWED_KEY : IComparable<VIEWED_KEY>
    { 
        /*
        /// <summary>
        /// Map the motif label to the arbitrary id used by TLP
        /// </summary>
        SortedDictionary<MotifEdge, ulong> EdgeToIndex = new SortedDictionary<MotifEdge, ulong>();
        */

        /// <summary>
        /// Return a dictionary of default values for attributes
        /// </summary>
        protected abstract SortedDictionary<string, string> DefaultAttributes {  get; }

        /// <summary>
        /// Map the motif label to the arbitrary id used by TLP
        /// </summary>
        SortedDictionary<VIEWED_KEY, ulong> KeyToIndex = new SortedDictionary<VIEWED_KEY, ulong>();

        /// <summary>
        /// Map the motif label to the arbitrary id used by TLP
        /// </summary>
        SortedList<ulong, TLPViewSubgraph> Subgraphs = new SortedList<ulong, TLPViewSubgraph>();

        SortedDictionary<ulong, TLPViewEdge> TulipIDToEdge = new SortedDictionary<ulong, TLPViewEdge>();
        
        private ulong nextNodeIndex = 0;

        private ulong _nextSubgraphID = 1; 

        public ulong GenerateNextSubgraphID()
        {
            ulong next_id = _nextSubgraphID;
            _nextSubgraphID++;
            return next_id; 
        }

        protected TLPViewNode createNode(VIEWED_KEY key)
        {
            TLPViewNode tempNode = new TLPViewNode(nextNodeIndex);
            addNode(key, tempNode);
            nextNodeIndex += 1;
            return tempNode;
        }

        protected void addNode(VIEWED_KEY key, TLPViewNode node)
        {
            nodes.Add(node.Key, node);
            KeyToIndex.Add(key, node.Key);  
        }
        
        private ulong nextEdgeIndex = 0;
        protected TLPViewEdge addEdge(VIEWED_KEY source, VIEWED_KEY target)
        {
            TLPViewEdge edge = new TLPViewEdge(nextEdgeIndex);
            edge.to = KeyToIndex[target];
            edge.from = KeyToIndex[source];
            base.addEdge(edge);
            nextEdgeIndex += 1;

            TulipIDToEdge[edge.tulip_id] = edge; 
            return edge; 
        }

        protected void AddSubGraph(ulong id, TLPViewSubgraph subgraph)
        {
            Subgraphs.Add(id, subgraph);
        } 

        private static string NodesDefinitionString(ICollection<ulong> ids)
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

        private string EdgesDefinitionString()
        {
            StringBuilder s = new StringBuilder();
            foreach (TLPViewEdge edge in edges)
            {
                s.AppendLine(edge.DefinitionString());
            }

            return s.ToString();
        }


        private IList<string> EntityAttributeList()
        {
            SortedSet<string> attribNames = new SortedSet<string>();
            foreach (string attribName in this.DefaultAttributes.Keys)
            {
                if (!attribNames.Contains(attribName))
                {
                    attribNames.Add(attribName);
                }
            } 

            foreach(TLPViewEdge edge in edges)
            {
                foreach(string attribName in edge.Attributes.Keys)
                {
                    if(!attribNames.Contains(attribName))
                    {
                        attribNames.Add(attribName);
                    }
                }
            }

            foreach (TLPViewNode node in nodes.Values)
            {
                foreach (string attribName in node.Attributes.Keys)
                {
                    if (!attribNames.Contains(attribName))
                    {
                        attribNames.Add(attribName);
                    }
                }
            }
             

            return attribNames.ToList();
        }


        private string AttributeValueString()
        {
            StringBuilder sb = new StringBuilder();
            IList<string> knownAttributes = EntityAttributeList();
            foreach(string attribName in knownAttributes)
            {
                sb.AppendLine(WriteAttribute(attribName));
            }

            return sb.ToString();
        }
          
         
        private string WriteAttribute(string attribname)
        {
            StringBuilder sb = new StringBuilder();

            sb.AppendLine(TLPFile.PropertyHeader(attribname));

            if(this.DefaultAttributes.ContainsKey(attribname))
            {
                sb.AppendLine(string.Format("(default {0} )", DefaultAttributes[attribname]));
            }

            foreach(TLPViewNode node in nodes.Values)
            {
                string attrib = node.AttributeToString(attribname);
                if(attrib != null)
                    sb.AppendLine(attrib);
            }

            foreach(TLPViewEdge edge in edges)
            {
                string attrib = edge.AttributeToString(attribname);
                if (attrib != null)
                    sb.AppendLine(attrib);
            }

            sb.AppendLine(TLPFile.PropertyFooter);

            return sb.ToString(); 
        }
         

        public override string ToString()
        {
            using (StringWriter sw = new StringWriter())
            {
                sw.WriteLine(TLPFile.FileHeader);

                sw.WriteLine(this.GraphBody());

                sw.WriteLine(AttributeValueString());

                sw.WriteLine(TLPFile.FileFooter);
                return sw.ToString();
            }
        }

        private string GraphBody()
        {
            using (StringWriter sw = new StringWriter())
            {
                sw.WriteLine(NodesDefinitionString(nodes.Keys));

                sw.WriteLine(EdgesDefinitionString());
                
                foreach (ulong subgraph_id in this.Subgraphs.Keys)
                {
                    sw.WriteLine(this.Subgraphs[subgraph_id].ToClusterString());
                }

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
