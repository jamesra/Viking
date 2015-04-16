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

        protected MotifTLPView(MotifGraph graph, string VolumeURL) : base(VolumeURL)
        {
        }

        public TLPViewNode CreateTLPNode(MotifNode node)
        {
            TLPViewNode tlpnode = createNode(node.Key);  
            IDictionary<string, string> NodeAttribs = AttributeMapper.AttribsForLabel(node.Key, TLPAttributes.StandardLabelToNodeTLPAppearance);
            if(!NodeAttribs.ContainsKey("viewLabel"))
                NodeAttribs.Add("viewLabel", node.Key);

            NodeAttribs.Add("StructureIDs", SourceStructures(node));

            if (VolumeURL != null)
                NodeAttribs.Add("StructureURL", StructureLabelUrl(node));

            if (VolumeURL != null)
                NodeAttribs.Add("MorphologyURL", MorphologyUrl(node));

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

        public string StructureLabelUrl(MotifNode node)
        {
            if (this.VolumeURL != null)
            {
                return string.Format("{0}/OData/ConnectomeData.svc/Structures?$filter=startswith(Label,'{1}') eq true", VolumeURL, node.Key);
            }

            return null;  
        }

        public  string MorphologyUrl(MotifNode node)
        {
            if (VolumeURL != null)
            {
                return string.Format("{0}/Export/Morphology/Tlp?id={1}", VolumeURL, SourceStructures(node));
            }

            return null; 
        }

        private string SourceStructures(MotifNode node)
        {
            StringBuilder sb = new StringBuilder();

            bool first = false; 
            foreach(AnnotationService.Structure s in node.Structures)
            {
                if (!first)
                    first = true;
                else
                    sb.Append(", ");

                sb.Append(s.ID.ToString());
            }

            return sb.ToString(); 
        }

        private string EdgeStructuresString(IList<long> structIDs)
        {
            StringBuilder sb = new StringBuilder();
            bool first = false; 

            foreach (long sourceID in structIDs)
            {
                if (!first)
                    first = true;
                else
                    sb.Append(", ");

                sb.Append(sourceID.ToString()); 
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

        public static MotifTLPView ToTLP(MotifGraph graph, string ExportURLBase, bool IncludeUnlabeled = false)
        {
            MotifTLPView view = new MotifTLPView(graph, ExportURLBase);

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
