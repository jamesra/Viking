using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace AnnotationVizLib
{
    public class NeuronTLPView : TLPView<long>
    {
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

            NodeAttribs.Add("StructureURL", string.Format("https://connectomes.utah.edu/Services/RC1/ConnectomeData.svc/Structures({0}L)", node.Key));

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

        public TLPViewEdge CreateTLPEdge(NeuronEdge edge)
        {
            TLPViewEdge tlpedge = this.addEdge(edge.SourceNodeKey, edge.TargetNodeKey);
            IDictionary<string, string> EdgeAttribs = AttributeMapper.AttribsForLabel(edge.SynapseType, TLPAttributes.StandardEdgeSourceLabelToTLPAppearance);

            if (EdgeAttribs.Count == 0)
            {
                //Add default node properties 
                AttributeMapper.CopyAttributes(TLPAttributes.UnknownTLPEdgeAttributes, EdgeAttribs);
            }

            EdgeAttribs.Add("viewLabel", edge.SynapseType);
            EdgeAttribs.Add("LinkedStructures", LinkedStructures(edge));

            /*
            foreach(long sourceID in edge.SourceStructIDs)
            {
                string key = string.Format("Source_{0}", sourceID);
                EdgeAttribs.Add(key, string.Format("https://connectomes.utah.edu/Services/RC1/ConnectomeData.svc/Structures({0}L)", sourceID));
            }

            foreach(long targetID in edge.TargetStructIDs)
            {
                string key = string.Format("Target_{0}", targetID);
                EdgeAttribs.Add(key, string.Format("https://connectomes.utah.edu/Services/RC1/ConnectomeData.svc/Structures({0}L)", targetID));
            }
            */

            tlpedge.AddAttributes(EdgeAttribs);

            return tlpedge;
        }

        public static NeuronTLPView ToTLP(NeuronGraph graph, bool IncludeUnlabeled = false)
        {
            NeuronTLPView view = new NeuronTLPView();

            foreach (NeuronNode node in graph.Nodes.Values)
            {
                view.CreateTLPNode(node);
            }

            foreach (NeuronEdge edge in graph.Edges.Values)
            {
                view.CreateTLPEdge(edge);
            }

            return view;
        } 
    }
}
