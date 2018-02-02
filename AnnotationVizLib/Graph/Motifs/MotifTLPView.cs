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
            NodeAttribs.Add("NumberOfCells", node.Structures.Count.ToString());
            NodeAttribs.Add("InputTypeCount", node.InputEdgesCount.ToString());
            NodeAttribs.Add("OutputTypeCount", node.OutputEdgesCount.ToString());
            NodeAttribs.Add("BidirectionalTypeCount", node.BidirectionalEdgesCount.ToString());
            

            if (VolumeURL != null)
                NodeAttribs.Add("StructureURL", StructureLabelUrl(node));

            if (VolumeURL != null)
                NodeAttribs.Add("MorphologyURL", MorphologyUrl(node));

            tlpnode.AddAttributes(NodeAttribs);

            return tlpnode;
        }

        public TLPViewEdge CreateTLPEdge(MotifGraph graph, MotifEdge edge)
        {
            TLPViewEdge tlpedge = this.addEdge(edge.SourceNodeKey, edge.TargetNodeKey);
            IDictionary<string, string> EdgeAttribs = AttributeMapper.AttribsForLabel(edge.SynapseType, TLPAttributes.StandardEdgeSourceLabelToTLPAppearance);
                        
            EdgeAttribs.Add("SourceParentStructures", EdgeStructuresString(edge.SourceStructIDs.Keys));
            EdgeAttribs.Add("ConnectionSourceStructures", EdgeStructuresString(edge.SourceStructIDs.SelectMany(s => s.Value)));
            EdgeAttribs.Add("TargetParentStructures", EdgeStructuresString(edge.TargetStructIDs.Keys));
            EdgeAttribs.Add("ConnectionTargetStructures", EdgeStructuresString(edge.TargetStructIDs.SelectMany(s => s.Value)));
              
            EdgeAttribs.Add("viewLabel", EdgeLabel(edge));
            EdgeAttribs.Add("edgeType", edge.SynapseType);

            EdgeAttribs.Add("SourceLabel", edge.SourceNodeKey);
            EdgeAttribs.Add("TargetLabel", edge.TargetNodeKey);

            AppendEdgeStatistics(graph, edge, ref EdgeAttribs);

            tlpedge.AddAttributes(EdgeAttribs);

            return tlpedge;
        }
        
        /// <summary>
        /// Append statistics about the edge to edge attributes
        /// </summary>
        /// <param name="edge"></param>
        private void AppendEdgeStatistics(MotifGraph graph, MotifEdge edge, ref IDictionary<string, string> EdgeAttribs)
        {
            MotifNode sourceNode = graph.Nodes[edge.SourceNodeKey];
            MotifNode targetNode = graph.Nodes[edge.TargetNodeKey];

            double PercentOccurenceInSourceCells = ((double)edge.SourceCellCount / (double)sourceNode.StructureCount) * 100.0;
            EdgeAttribs.Add("%OccurenceInSourceCells", PercentOccurenceInSourceCells.ToString());

            double PercentOccurenceInTargetCells = ((double)edge.TargetCellCount / (double)targetNode.StructureCount) * 100.0;
            EdgeAttribs.Add("%OccurenceInTargetCells", PercentOccurenceInTargetCells.ToString());
             
            if (edge.Directional)
            {
                double PercentOfSourceOutput = ((double)edge.SourceConnectionCount / (double)sourceNode.OutputEdgesCount) * 100.0;
                double PercentOfTargetInput = ((double)edge.TargetConnectionCount / (double)targetNode.InputEdgesCount) * 100.0;

                EdgeAttribs.Add("%ofSourceTypeOutput", PercentOfSourceOutput.ToString());
                EdgeAttribs.Add("%ofTargetTypeInput", PercentOfTargetInput.ToString()); 
            }
            else
            {
                double PercentOfSourceBidirectionalOutput = ((double)edge.SourceConnectionCount / (double)sourceNode.BidirectionalEdgesCount) * 100.0;
                double PercentOfTargetBidirectionalOutput = ((double)edge.TargetConnectionCount / (double)targetNode.BidirectionalEdgesCount) * 100.0;

                EdgeAttribs.Add("%ofSourceTypeBidirectional", PercentOfSourceBidirectionalOutput.ToString());
                EdgeAttribs.Add("%ofTargetTypeBidirectional", PercentOfTargetBidirectionalOutput.ToString());
            }

            double AvgSourceLinks = edge.SourceStructIDs.Average(source => source.Value.Count);
            double AvgTargetLinks = edge.TargetStructIDs.Average(target => target.Value.Count);

            EdgeAttribs.Add("Avg#OfOutputsPerSource", AvgSourceLinks.ToString());
            EdgeAttribs.Add("Avg#OfInputsPerTarget", AvgTargetLinks.ToString());

            if (edge.SourceCellCount > 1)
            { 

                double StdDevSourceLinks = StdDev(edge.SourceStructIDs.Select(source => (double)source.Value.Count).ToList());
                EdgeAttribs.Add("StdDevOfOutputsPerSource", StdDevSourceLinks.ToString());
            }

            if (edge.TargetCellCount > 1)
            {
                double StdDevTargetLinks = StdDev(edge.TargetStructIDs.Select(target => (double)target.Value.Count).ToList());
                EdgeAttribs.Add("StdDevOfInputsPerTarget", StdDevTargetLinks.ToString());
            } 
        }

        private double StdDev(IReadOnlyList<double> values)
        {
            double average = values.Average();
            double sumOfSquaresOfDifferences = values.Select(val => (val - average) * (val - average)).Sum();
            double sd = Math.Sqrt(sumOfSquaresOfDifferences / values.Count);
            return sd; 
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
            foreach(IStructure s in node.Structures)
            {
                if (!first)
                    first = true;
                else
                    sb.Append(", ");

                sb.Append(s.ID.ToString());
            }

            return sb.ToString(); 
        }

        private string EdgeStructuresString(IEnumerable<long> structIDs)
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

                view.CreateTLPEdge(graph, edge);
            }

            return view; 
        } 
    }
}
