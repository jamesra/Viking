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
        public GMLViewEdge CreateGMLEdge(NeuronEdge edge)
        {
            GMLViewEdge GMLedge = null;
            try
            {
                GMLedge = this.addEdge(edge.SourceNodeKey, edge.TargetNodeKey);
            }
            catch (KeyNotFoundException)
            {
                Trace.WriteLine(string.Format("Nodes missing for edge {0}", edge.ToString()));
                return null;
            }

            IDictionary<string, string> EdgeAttribs = new Dictionary<string, string>();
            
            
            EdgeAttribs.Add("edgeType", edge.SynapseType);
            
            GMLedge.AddAttributes(EdgeAttribs);

            return GMLedge;
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
