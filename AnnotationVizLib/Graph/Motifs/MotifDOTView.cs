using Annotation.Interfaces;
using System;
using System.Collections.Generic;

namespace AnnotationVizLib
{
    public class MotifDOTView : GraphVizEngine<string>
    {
        static MotifDOTView()
        {

        }

        public static MotifDOTView ToDOT(MotifGraph graph, bool IncludeUnlabeled = false)
        {
            MotifDOTView DotGraph = new MotifDOTView();

            DotGraph.AddStandardizedAttributes(DOTAttributes.StandardGraphDOTAttributes);

            foreach (MotifNode node in graph.Nodes.Values)
            {
                if (node.Key == "Unlabeled" && !IncludeUnlabeled)
                    continue;

                GraphViewNode<string> DOTNode = DotGraph.createNode(node.Key);
                DOTNode = GraphVizNodeFromMotifNode(DOTNode, node);
            }

            DotGraph.Attributes.Add("nslimit", Math.Ceiling(Math.Sqrt(graph.Nodes.Count)).ToString());
            DotGraph.Attributes.Add("mclimit", Math.Ceiling(Math.Sqrt(graph.Nodes.Count)).ToString());

            foreach (MotifEdge edge in graph.Edges.Values)
            {
                if (edge.TargetNodeKey == "Unlabeled" && !IncludeUnlabeled)
                    continue;

                if (edge.SourceNodeKey == "Unlabeled" && !IncludeUnlabeled)
                    continue;

                GraphViewEdge<string> DOTEdge = GraphVizEdgeFromMotifEdge(DotGraph, graph, edge);
            }

            DotGraph.createDirectedGraph("Motif");

            return DotGraph;
        }

        private static GraphViewNode<string> GraphVizNodeFromMotifNode(GraphViewNode<string> DotNode, MotifNode node)
        {
            string label = node.Key;

            DotNode.AddStandardizedAttributes(DOTAttributes.StandardNodeDOTAttributes);

            DotNode.Attributes.Add("label", node.Key);

            IDictionary<string, string> AttribsForLabel = AttributeMapper.AttribsForLabel(DotNode.label, DOTAttributes.StandardLabelToNodeDOTAppearance);

            if (AttribsForLabel == null)
            {
                if (label.Length > 0)
                    DotNode.Attributes.Add("fillcolor", "grey");
                else
                    DotNode.Attributes.Add("fillcolor", "white");

                DotNode.Attributes.Add("shape", "ellipse");
            }
            else
            {
                DotNode.AddStandardizedAttributes(AttribsForLabel);
            }

            string ToolTipStr = node.Structures.Count.ToString() + " " + node.Key + " instances: ";

            bool firstentry = true;
            foreach (IStructure s in node.Structures)
            {
                if (!firstentry)
                    ToolTipStr = ToolTipStr + ", ";

                firstentry = false;
                ToolTipStr = ToolTipStr + s.ID.ToString();
            }

            DotNode.Attributes.Add("tooltip", ToolTipStr);

            return DotNode;
        }

        public static GraphViewEdge<string> GraphVizEdgeFromMotifEdge(GraphViewEngine<string> DotEngine, MotifGraph graph, MotifEdge edge)
        {
            GraphVizEdge<string> DotEdge = new GraphVizEdge<string>();
            float additionFactor = 1f;
            float mulFactor = 0.5f;
            float arrowsize = additionFactor;
            float pensize = additionFactor;
            DotEdge.from = edge.SourceNodeKey;
            DotEdge.to = edge.TargetNodeKey;

            IDictionary<string, string> EdgeAttribs = AttributeMapper.AttribsForLabel(edge.SynapseType.ToUpper(),
                                                                                     DOTAttributes.StandardEdgeSourceLabelToDOTAppearance);

            if (EdgeAttribs == null)
            {
                return null;
            }
            else
            {
                DotEdge.AddStandardizedAttributes(EdgeAttribs);
            }

            arrowsize = arrowsize * (float)(Math.Sqrt(edge.Weight) * mulFactor);
            if (arrowsize < 1)
                arrowsize = 1;

            pensize = pensize * (float)Math.Sqrt(edge.Weight);

            DotEdge.Attributes.Add("tailclip", "true");
            //DotEdge.Attributes.Add("color", color);
            //DotEdge.edgeAttributes.Add("URL", edgeSections.Substring(0, edgeSections.Length - 1));
            //DotEdge.Attributes.Add("dir", dir);
            //tempEdge.edgeAttributes.Add("samehead", TypeName); 
            //DotEdge.Attributes.Add("arrowhead", arrowhead);
            //DotEdge.Attributes.Add("arrowtail", arrowtail);
            DotEdge.Attributes.Add("arrowsize", arrowsize.ToString());

            //tempEdge.edgeAttributes.Add("weight", edge.Strength.ToString());
            //DotEdge.Attributes.Add("penwidth", pensize.ToString());


            MotifNode SourceNode = graph.Nodes[edge.SourceNodeKey];
            MotifNode TargetNode = graph.Nodes[edge.SourceNodeKey];

            double SourceCoverage = (double)edge.SourceStructIDs.Count / (double)SourceNode.Structures.Count;
            double TargetCoverage = (double)edge.TargetStructIDs.Count / (double)TargetNode.Structures.Count;

            //double Weight = (SourceCoverage * 10.0) + (TargetCoverage * 10.0);
            double Weight = ((SourceCoverage + TargetCoverage) / 2) * 10.0;

            if (Weight < 1)
            {
                Weight = 1;
            }

            //Give small weights a boost in appearance. 
            if (Weight > 1 && Weight < 2)
            {
                Weight = 2;
            }

            if (edge.SourceStructIDs.Count == 1 ||
               edge.TargetStructIDs.Count == 1)
            {
                Weight = 1;
            }

            SourceCoverage *= 100;
            TargetCoverage *= 100;

            string toolTipStr = edge.SourceNodeKey + " connects: " + SourceCoverage.ToString("F0") + "%    " + edge.TargetNodeKey + " contacted: " + TargetCoverage.ToString("F0") + "%";
            DotEdge.Attributes.Add("tooltip", toolTipStr);


            DotEdge.Attributes.Add("penwidth", Weight.ToString());
            DotEdge.Attributes.Add("weight", (Weight * Weight).ToString());

            //If the edge is bidirectional clone it, reverse the direction, and make it invisible to help directional layout algorithms.
            if (DotEdge.Attributes.ContainsKey("dir") && DotEdge.Attributes["dir"] == "both")
            {
                GraphVizEdge<string> reverseTempEdge = DotEdge.Clone() as GraphVizEdge<string>;
                reverseTempEdge.Reverse();
                reverseTempEdge.Attributes.Add("style", "invis"); //invisible

                DotEngine.addEdge(reverseTempEdge);
            }

            DotEngine.addEdge(DotEdge);
            return DotEdge;
        }
    }
}
