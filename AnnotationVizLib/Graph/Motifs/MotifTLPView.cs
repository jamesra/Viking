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
        protected override SortedDictionary<string, string> DefaultAttributes
        {
            get { return TLPAttributes.DefaultForAttribute; }
        }

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
            NodeAttribs.Add("StructureURL", StructureLabelUrl(node));

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
            EdgeAttribs.Add("edgeType", edge.SynapseType);
              
            tlpedge.AddAttributes(EdgeAttribs);

            return tlpedge;
        }

        public static string StructureLabelUrl(MotifNode node)
        {
            return string.Format("http://connectomes.utah.edu/Services/RC1/ConnectomeData.svc/Structures?$filter=startswith(Label,'{0}') eq true", node.Key);
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
