using System.Linq;

namespace AnnotationVizLib
{
    public static class MotifExtensions
    {
        public static void AddEdgeStatistics(this MotifGraph graph)
        {
            foreach (MotifEdge edge in graph.Edges.Values)
            {
                graph.AddEdgeStatistics(edge);
            }
        }

        private static void AddEdgeStatistics(this MotifGraph graph, MotifEdge edge)
        {
            MotifNode sourceNode = graph.Nodes[edge.SourceNodeKey];
            MotifNode targetNode = graph.Nodes[edge.TargetNodeKey];

            double PercentOccurenceInSourceCells = ((double)edge.SourceCellCount / (double)sourceNode.StructureCount) * 100.0;
            edge.Attributes.Add("%OccurenceInSourceCells", PercentOccurenceInSourceCells.ToString());

            double PercentOccurenceInTargetCells = ((double)edge.TargetCellCount / (double)targetNode.StructureCount) * 100.0;
            edge.Attributes.Add("%OccurenceInTargetCells", PercentOccurenceInTargetCells.ToString());

            if (edge.Directional)
            {
                double PercentOfSourceOutput = ((double)edge.SourceConnectionCount / (double)sourceNode.OutputEdgesCount) * 100.0;
                double PercentOfTargetInput = ((double)edge.TargetConnectionCount / (double)targetNode.InputEdgesCount) * 100.0;

                edge.Attributes.Add("%ofSourceTypeOutput", PercentOfSourceOutput.ToString());
                edge.Attributes.Add("%ofTargetTypeInput", PercentOfTargetInput.ToString());
            }
            else
            {
                double PercentOfSourceBidirectionalOutput = ((double)edge.SourceConnectionCount / (double)sourceNode.BidirectionalEdgesCount) * 100.0;
                double PercentOfTargetBidirectionalOutput = ((double)edge.TargetConnectionCount / (double)targetNode.BidirectionalEdgesCount) * 100.0;

                edge.Attributes.Add("%ofSourceTypeBidirectional", PercentOfSourceBidirectionalOutput.ToString());
                edge.Attributes.Add("%ofTargetTypeBidirectional", PercentOfTargetBidirectionalOutput.ToString());
            }

            double AvgSourceLinks = edge.SourceStructIDs.Average(source => source.Value.Count);
            double AvgTargetLinks = edge.TargetStructIDs.Average(target => target.Value.Count);

            edge.Attributes.Add("Avg#OfOutputsPerSource", AvgSourceLinks.ToString());
            edge.Attributes.Add("Avg#OfInputsPerTarget", AvgTargetLinks.ToString());

            if (edge.SourceCellCount > 1)
            {

                double StdDevSourceLinks = edge.SourceStructIDs.Select(source => (double)source.Value.Count).StdDev();
                edge.Attributes.Add("StdDevOfOutputsPerSource", StdDevSourceLinks.ToString());
            }

            if (edge.TargetCellCount > 1)
            {
                double StdDevTargetLinks = edge.TargetStructIDs.Select(target => (double)target.Value.Count).StdDev();
                edge.Attributes.Add("StdDevOfInputsPerTarget", StdDevTargetLinks.ToString());
            }
        }
    }
}
