
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Text;

namespace AnnotationVizLib
{
    /// <summary>
    /// Tulip IDs every node with an index.  This is separate than the key for the source node we are viewing.
    /// </summary>
    /// <typeparam name="KEY"></typeparam>
    public class GMLViewNode : GraphViewNode<ulong>
    {
        public GMLViewNode(ulong id) : base(id)
        {
        }

        public GMLViewNode(ulong id, string lbl) : base(id, lbl)
        {
        }

        public System.Drawing.Color Color
        {
            set =>
                this.Attributes["Color"] = string.Format("({0},{1},{2},{3})", value.R,
                    value.G,
                    value.B,
                    value.A);
        }

        public string ToGMLElement()
        {
            if (this.Attributes.Count > 0)
            {
                StringBuilder ElementBuilder = new(string.Format("<node id=\"{0}\">", this.Key));
                foreach (string attribute in this.Attributes.Keys)
                {
                    ElementBuilder.AppendLine(AttributeToString(attribute));
                }
                ElementBuilder.AppendLine("</node>");
                return ElementBuilder.ToString();
            }
            else
            {
                return string.Format("<node id=\"{0}\"/>", this.Key);
            }
        }

        private string AttributeToString(string attrib_name)
        {
            if (!this.Attributes.ContainsKey(attrib_name))
                return null;

            return string.Format("\t<data key=\"{0}\">{1}</data>", attrib_name, this.Attributes[attrib_name]);
        }


    }

    /// <summary>
    /// Tulip IDs every edge with an index. The GMLViewEdge records the TO/FROM index of the linked nodes, and the unique index of the edge itself.
    /// </summary>
    /// <typeparam name="KEY"></typeparam>
    public class GMLViewEdge(ulong id) : GraphViewEdge<ulong>
    {
        /// <summary>
        /// GML file ID
        /// </summary>
        public ulong gml_id = id;

        public System.Drawing.Color Color
        {
            set =>
                this.Attributes["viewColor"] = string.Format("({0},{1},{2},{3})", value.R,
                    value.G,
                    value.B,
                    value.A);
        }

        public string ToGMLElement()
        {
            if (this.Attributes.Count > 0)
            {
                StringBuilder ElementBuilder = new(string.Format("<edge id=\"{0}\" source=\"{1}\" target=\"{2}\">", this.gml_id, this.from, this.to));
                foreach (string attribute in this.Attributes.Keys)
                {
                    ElementBuilder.AppendLine(AttributeToString(attribute));
                }
                ElementBuilder.AppendLine("</edge>");
                return ElementBuilder.ToString();
            }
            else
            {
                return string.Format("<edge id=\"{0}\" source=\"{1}\" target=\"{2}\"/>", this.gml_id, this.from, this.to);
            }
        }

        private string AttributeToString(string attrib_name)
        {
            if (!this.Attributes.ContainsKey(attrib_name))
                return null;

            return string.Format("\t<data key=\"{0}\">{1}</data>", attrib_name, this.Attributes[attrib_name]);
        }
    }

    internal static class GMLFile
    {
        public enum PropertyType
        {
            node,
            edge
        }
        public static string FileHeader
        {
            get
            {
                StringBuilder sb = new();
                sb.Append("<?xml version=\"1.0\" encoding=\"UTF-8\"?>");
                sb.Append("<graphml xmlns = \"http://graphml.graphdrawing.org/xmlns\"");
                sb.Append("\txmlns:xsi = \"http://www.w3.org/2001/XMLSchema-instance\"");
                sb.Append("\txsi:schemaLocation = \"http://graphml.graphdrawing.org/xmlns http://graphml.graphdrawing.org/xmlns/1.0/graphml.xsd\">");
                return sb.ToString();
            }
        }

        public static string FileFooter => GenericFooter;

        public static string GenericFooter => "</graphml>";

        public static string DeclareProperty(string attribName, PropertyType type)
        {
            string attribType = "string";
            string attribDefault = null;
            if (GMLAttributes.GMLTypeForAttribute.ContainsKey(attribName))
            {
                var meta = GMLAttributes.GMLTypeForAttribute[attribName];
                attribType = meta.Type;
                attribDefault = meta.Default;
            }

            string forType = type == PropertyType.node ? "node" : "edge";

            string Element = string.Format("<key id=\"{0}\" for=\"{1}\" attr.name=\"{0}\" attr.type=\"{2}\"", attribName, forType, attribType);

            if (attribDefault is null)
                Element += "/>";
            else
                Element += string.Format(">\n\t<default>{0}</default>\n</key>", attribDefault);

            return Element;
        }

        public static string PropertyFooter => GenericFooter;
    }

    public class GMLViewSubgraph(ulong subgraph_ID, string label)
    {
        public readonly ulong ID = subgraph_ID;
        public string Label = label;

        public SortedDictionary<string, string> SubgraphAttributes = [];
        public SortedDictionary<ulong, GMLViewSubgraph> SubGraphs = [];

        readonly SortedSet<ulong> NodeIDs = [];
        readonly SortedSet<ulong> EdgeIDs = [];

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

        public void AddSubgraph(ulong ID, GMLViewSubgraph subgraph)
        {
            Debug.Assert(!SubGraphs.ContainsKey(ID));
            SubGraphs.Add(ID, subgraph);
        }

        public System.Drawing.Color Color
        {
            set =>
                this.SubgraphAttributes["viewColor"] = string.Format("({0},{1},{2},{3} {0},{1},{2},{3})", value.R,
                    value.G,
                    value.B,
                    value.A);
        }

        private static string NodesString(IEnumerable<ulong> node_ids)
        {
            if (node_ids.Count() == 0)
                return "";

            using StringWriter sw = new();
            sw.Write("(nodes ");
            foreach (long id in node_ids.Select(v => (long)v))
            {
                sw.Write(string.Format("{0} ", id));
            }
            sw.WriteLine(GMLFile.GenericFooter);

            return sw.ToString();
        }

        private static string EdgesString(IEnumerable<ulong> edge_ids)
        {
            if (edge_ids.Count() == 0)
                return "";

            using StringWriter sw = new();
            sw.Write("(edges ");
            foreach (ulong id in edge_ids)
            {
                sw.Write(string.Format("{0} ", id));
            }
            sw.WriteLine(GMLFile.GenericFooter);

            return sw.ToString();
        }

        /// <summary>
        /// Cluster graph bodies are composed of properties and the (nodes # # #) and (edges # #) id numbers from the main GML file
        /// </summary>
        /// <returns></returns>
        private string ClusterGraphBody()
        {
            using StringWriter sw = new();
            sw.Write(GMLViewSubgraph.NodesString(this.NodeIDs));
            sw.Write(GMLViewSubgraph.EdgesString(this.EdgeIDs));

            foreach (ulong subgraph_id in this.SubGraphs.Keys)
            {
                sw.Write(this.SubGraphs[subgraph_id].ToClusterString());
            }

            return sw.ToString();
        }

        public string ToClusterString()
        {
            using StringWriter sw = new();
            //sw.WriteLine(GMLFile.ClusterHeader(this.ID, this.Label));

            sw.WriteLine(ClusterGraphBody());

            sw.WriteLine(GMLFile.GenericFooter);
            return sw.ToString();
        }

        public override string ToString() => string.Format("{0} {1}", this.ID, this.Label);
    }


    public abstract class GMLView<VIEWED_KEY> : GraphViewEngine<ulong>
        where VIEWED_KEY : IComparable<VIEWED_KEY>
    {
        /// <summary>
        /// URL to build custom properties which query the export service
        /// </summary>
        protected string VolumeURL = null;

        /// <summary>
        /// Map the motif label to the arbitrary id used by GML
        /// </summary>
        readonly SortedDictionary<VIEWED_KEY, ulong> KeyToIndex = [];

        /// <summary>
        /// Map the motif label to the arbitrary id used by GML
        /// </summary>
        readonly SortedList<ulong, GMLViewSubgraph> Subgraphs = [];

        readonly SortedDictionary<ulong, GMLViewEdge> GML_IDToEdge = [];

        private ulong nextNodeIndex = 0;

        private ulong _nextSubgraphID = 1;

        public ulong GenerateNextSubgraphID()
        {
            ulong next_id = _nextSubgraphID;
            _nextSubgraphID++;
            return next_id;
        }

        public GMLView()
        {
        }

        public GMLView(string VolumeURL)
            : base()
        {
            this.VolumeURL = VolumeURL;
        }

        protected GMLViewNode createNode(VIEWED_KEY key)
        {
            GMLViewNode tempNode = new(nextNodeIndex);
            addNode(key, tempNode);
            nextNodeIndex += 1;
            return tempNode;
        }

        protected void addNode(VIEWED_KEY key, GMLViewNode node)
        {
            nodes.Add(node.Key, node);
            KeyToIndex.Add(key, node.Key);
        }

        private ulong nextEdgeIndex = 0;
        protected GMLViewEdge addEdge(VIEWED_KEY source, VIEWED_KEY target)
        {
            GMLViewEdge edge = new(nextEdgeIndex)
            {
                to = KeyToIndex[target],
                from = KeyToIndex[source]
            };
            base.addEdge(edge);
            nextEdgeIndex += 1;

            GML_IDToEdge[edge.gml_id] = edge;
            return edge;
        }

        protected bool HaveNodesForEdge(VIEWED_KEY source, VIEWED_KEY target) => KeyToIndex.ContainsKey(source) && KeyToIndex.ContainsKey(target);

        protected void AddSubGraph(ulong id, GMLViewSubgraph subgraph) => Subgraphs.Add(id, subgraph);

        private IList<string> NodeAttributeList()
        {
            SortedSet<string> attribNames = [];

            foreach (GMLViewNode node in nodes.Values.Cast<GMLViewNode>())
            {
                foreach (string attribName in node.Attributes.Keys)
                {
                    if (!attribNames.Contains(attribName))
                    {
                        attribNames.Add(attribName);
                    }
                }
            }

            return [.. attribNames];
        }

        private string NodesDefinitionString()
        {
            StringBuilder nodeElements = new();
            foreach (GMLViewNode node in nodes.Values.Cast<GMLViewNode>())
            {
                nodeElements.Append(node.ToGMLElement());
            }

            return nodeElements.ToString();
        }

        private string EdgesDefinitionString()
        {
            StringBuilder edgeElements = new();
            foreach (GMLViewEdge edge in edges.Cast<GMLViewEdge>())
            {
                edgeElements.Append(edge.ToGMLElement());
            }

            return edgeElements.ToString();
        }

        private IList<string> EdgeAttributeList()
        {
            SortedSet<string> attribNames = [];

            foreach (GMLViewEdge edge in edges.Cast<GMLViewEdge>())
            {
                foreach (string attribName in edge.Attributes.Keys)
                {
                    if (!attribNames.Contains(attribName))
                    {
                        attribNames.Add(attribName);
                    }
                }
            }

            return [.. attribNames];
        }


        public string DefineAttributes()
        {
            using StringWriter sw = new();
            foreach (string NodeProperty in NodeAttributeList())
            {
                sw.WriteLine(GMLFile.DeclareProperty(NodeProperty, GMLFile.PropertyType.node));
            }

            foreach (string NodeProperty in EdgeAttributeList())
            {
                sw.WriteLine(GMLFile.DeclareProperty(NodeProperty, GMLFile.PropertyType.edge));
            }

            return sw.ToString();

        }


        public override string ToString()
        {
            using StringWriter sw = new();
            sw.WriteLine(GMLFile.FileHeader);

            sw.WriteLine(DefineAttributes());

            sw.WriteLine("<graph edgedefault=\"directed\">");

            sw.WriteLine(NodesDefinitionString());

            sw.WriteLine(EdgesDefinitionString());

            sw.WriteLine("</graph>");

            sw.WriteLine(GMLFile.FileFooter);
            return sw.ToString();
        }

        public void SaveGML(string FullPath)
        {
            using (StreamWriter write = new(FullPath, false))
            {
                write.Write(this.ToString());
                write.Close();
            }

            Debug.Assert(System.IO.File.Exists(FullPath), "Dot file we just wrote does not exist");
        }
    }
}
