using System;
using System.Diagnostics;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace AnnotationVizLib
{
    public class NeuronTLPView : TLPView<long>
    {
        protected override SortedDictionary<string, string> DefaultAttributes
        {
            get { return TLPAttributes.DefaultForAttribute; }
        }

        public NeuronTLPView(string VolumeURL) : base(VolumeURL)
        {
        }

        public TLPViewNode CreateTLPNode(NeuronNode node)
        {
            TLPViewNode tlpnode = createNode(node.Key);
            IDictionary<string, string> NodeAttribs = AttributeMapper.AttribsForLabel(node.Structure.Label, TLPAttributes.StandardLabelToNodeTLPAppearance);

            if(NodeAttribs.Count == 0)
            {
                //Add default node properties 
                AttributeMapper.CopyAttributes(TLPAttributes.UnknownTLPNodeAttributes, NodeAttribs);
            }

            if (!NodeAttribs.ContainsKey("viewLabel"))
                NodeAttribs.Add("viewLabel", LabelForNode(node));

            NodeAttribs.Add("StructureURL", string.Format("{0}/OData/ConnectomeData.svc/Structures({1}L)", this.VolumeURL, node.Key));
            NodeAttribs.Add("ID", string.Format("{0}", node.Key));
            NodeAttribs.Add("Tags", node.Structure.AttributesXml);

            tlpnode.AddAttributes(NodeAttribs);

            return tlpnode;
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
        public TLPViewEdge CreateTLPEdge(NeuronEdge edge)
        {
            TLPViewEdge tlpedge = null;
            try
            {
                tlpedge = this.addEdge(edge.SourceNodeKey, edge.TargetNodeKey);
            }
            catch(KeyNotFoundException)
            {
                Trace.WriteLine(string.Format("Nodes missing for edge {0}", edge.ToString()));
                return null;
            }
            
            IDictionary<string, string> EdgeAttribs = AttributeMapper.AttribsForLabel(edge.SynapseType, TLPAttributes.StandardEdgeSourceLabelToTLPAppearance);

            if (EdgeAttribs.Count == 0)
            {
                //Add default node properties 
                AttributeMapper.CopyAttributes(TLPAttributes.UnknownTLPEdgeAttributes, EdgeAttribs);
            }

            EdgeAttribs.Add("viewLabel", edge.SynapseType);
            EdgeAttribs.Add("edgeType", edge.SynapseType);
            EdgeAttribs.Add("LinkedStructures", LinkedStructures(edge));

            /*
            foreach(long sourceID in edge.SourceStructIDs)
            {
                string key = string.Format("Source_{0}", sourceID);
                EdgeAttribs.Add(key, string.Format("http://connectomes.utah.edu/Services/RC1/ConnectomeData.svc/Structures({0}L)", sourceID));
            }

            foreach(long targetID in edge.TargetStructIDs)
            {
                string key = string.Format("Target_{0}", targetID);
                EdgeAttribs.Add(key, string.Format("http://connectomes.utah.edu/Services/RC1/ConnectomeData.svc/Structures({0}L)", targetID));
            }
            */

            tlpedge.AddAttributes(EdgeAttribs);

            return tlpedge;
        }

        public static NeuronTLPView ToTLP(NeuronGraph graph, string VolumeURL, bool IncludeUnlabeled = false)
        {
            NeuronTLPView view = new NeuronTLPView(VolumeURL);

            foreach (NeuronNode node in graph.Nodes.Values)
            {
                view.CreateTLPNode(node);
            }

            foreach (NeuronEdge edge in graph.Edges.Values.Where(e => view.HaveNodesForEdge(e.SourceNodeKey, e.TargetNodeKey)))
            {
                view.CreateTLPEdge(edge);
            }

            return view;
        } 
    }
}
