using System;
using System.Diagnostics;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace AnnotationVizLib
{
    public class NeuronGMLView : GMLView<long>
    {
        public NeuronGMLView(string VolumeURL) : base(VolumeURL)
        {
        }

        public GMLViewNode CreateGMLNode(NeuronNode node)
        {
            GMLViewNode GMLnode = createNode(node.Key);
            IDictionary<string, string> NodeAttribs = new Dictionary<string, string>();

            NodeAttribs.Add("Label", LabelForNode(node));
            
            NodeAttribs.Add("StructureURL", string.Format("{0}/OData/ConnectomeData.svc/Structures({1}L)", this.VolumeURL, node.Key));

            GMLnode.AddAttributes(NodeAttribs);

            return GMLnode;
        }

        public string LabelForNode(NeuronNode node)
        {
            if (node.Structure.Label != null && node.Structure.Label.Length > 0)
                return node.Structure.Label + "\n" + node.Key.ToString();

            return node.Key.ToString();
        }

        public static string LinkedStructures(NeuronEdge edge)
        {
            StringBuilder sb = new StringBuilder();
            //sb.Append(edge.SynapseType);
            foreach (AnnotationService.StructureLink link in edge.Links)
            {
                sb.Append("\t" + LinkString(link));
            }

            return sb.ToString();
        }

        public static string LinkString(AnnotationService.StructureLink link)
        {
            return link.SourceID + " -> " + link.TargetID;
        }

        /// <summary>
        /// Create an edge between two nodes.  Returns null if the nodes do not exist
        /// </summary>
        /// <param name="edge"></param>
        /// <returns></returns>
        public void CreateGMLEdge(NeuronEdge edge)
        {
            GMLViewEdge GMLedge = null;
            GMLViewEdge GMLReverseEdge = null;
            try
            {
                GMLedge = this.addEdge(edge.SourceNodeKey, edge.TargetNodeKey);
                if(!edge.Directional && !edge.IsLoop)
                {
                    GMLReverseEdge = this.addEdge(edge.TargetNodeKey, edge.SourceNodeKey);
                }                
            }
            catch (KeyNotFoundException)
            {
                Trace.WriteLine(string.Format("Nodes missing for edge {0}", edge.ToString()));
                return;
            }

            IDictionary<string, string> EdgeAttribs = AttributesForEdge(edge);
            GMLedge.AddAttributes(EdgeAttribs);

            if (GMLReverseEdge != null)
                GMLReverseEdge.AddAttributes(EdgeAttribs);

            return;
        }

        protected static IDictionary<string, string> AttributesForEdge(NeuronEdge edge)
        {
            Dictionary<string, string> EdgeAttribs = new Dictionary<string, string>();
            EdgeAttribs.Add("edgeType", edge.SynapseType);
            EdgeAttribs.Add("Directional", (edge.Directional).ToString());
            return EdgeAttribs;
        }

        public static NeuronGMLView ToGML(NeuronGraph graph, string VolumeURL, bool IncludeUnlabeled = false)
        {
            NeuronGMLView view = new NeuronGMLView(VolumeURL);

            foreach (NeuronNode node in graph.Nodes.Values)
            {
                view.CreateGMLNode(node);
            }

            foreach (NeuronEdge edge in graph.Edges.Values.Where(e => view.HaveNodesForEdge(e.SourceNodeKey, e.TargetNodeKey)))
            {
                view.CreateGMLEdge(edge);
            }

            return view;
        }
    }
}
