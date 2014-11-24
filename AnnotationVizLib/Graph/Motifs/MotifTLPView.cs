using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.IO;
using System.Diagnostics;

namespace AnnotationVizLib
{
    public class MotifTLPView : TLPView<string>
    { 

        private MotifTLPView(MotifGraph graph)
        { 
        }

        public TLPViewNode CreateTLPNode(MotifNode node)
        {
            TLPViewNode tlpnode = createNode(node.Key);  
            IDictionary<string, string> NodeAttribs = AttributeMapper.AttribsForLabel(node.Key, TLPAttributes.StandardLabelToNodeTLPAppearance);
            if(!NodeAttribs.ContainsKey("viewLabel"))
                NodeAttribs.Add("viewLabel", node.Key);

            NodeAttribs.Add("StructureIDs", SourceStructures(node));

            tlpnode.AddAttributes(NodeAttribs);

            return tlpnode;
        }

        public TLPViewEdge CreateTLPEdge(MotifEdge edge)
        {
            TLPViewEdge tlpedge = this.addEdge(edge.SourceNodeKey, edge.TargetNodeKey);
            IDictionary<string, string> EdgeAttribs = AttributeMapper.AttribsForLabel(edge.SynapseType, TLPAttributes.StandardEdgeSourceLabelToTLPAppearance);

            EdgeAttribs.Add("SourceStructures", EdgeStructuresString(edge.SourceStructIDs));
            EdgeAttribs.Add("TargetStructures", EdgeStructuresString(edge.TargetStructIDs));
            EdgeAttribs.Add("viewLabel", EdgeLabel(edge));

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

        private string SourceStructures(MotifNode node)
        {
            StringBuilder sb = new StringBuilder();
            
            foreach(AnnotationService.Structure s in node.Structures)
            {
                sb.AppendLine(s.ID.ToString());
            }

            return sb.ToString(); 
        }

        private string EdgeStructuresString(IList<long> structIDs)
        {
            StringBuilder sb = new StringBuilder();
            foreach (long sourceID in structIDs)
            {
                sb.AppendLine(sourceID.ToString());
            }

            return sb.ToString();
        }

        private string EdgeLabel(MotifEdge edge)
        {
            return edge.SynapseType;
        }

        public void PopulateTLPNode(MotifNode node, TLPViewNode tlpnode)
        {
            
        }

        public static MotifTLPView ToTLP(MotifGraph graph, bool IncludeUnlabeled = false)
        {
            MotifTLPView view = new MotifTLPView(graph);

            foreach (MotifNode node in graph.Nodes.Values)
            {
                if (node.Key == "Unlabeled" && !IncludeUnlabeled)
                    continue;

                view.CreateTLPNode(node);
            }

            foreach (MotifEdge edge in graph.Edges.Values)
            {
                if (edge.SourceNodeKey == "Unlabeled" || edge.TargetNodeKey == "Unlabeled")
                    continue;

                view.CreateTLPEdge(edge);
            }

            return view; 
        } 
    }
}
