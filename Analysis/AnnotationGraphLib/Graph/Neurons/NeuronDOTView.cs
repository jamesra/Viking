using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using AnnotationUtils.AnnotationService; 

namespace AnnotationUtils
{
    public class NeuronDOTView : GraphVizEngine<long>
    {
        public static NeuronDOTView ToDOT(NeuronGraph graph)
        {
            string DOT = "";

            NeuronDOTView DotGraph = new NeuronDOTView();

            DotGraph.AddAttributes(AttributeMaps.StandardGraphAttributes);

            foreach (NeuronNode node in graph.Nodes.Values)
            {
                GraphVizNode<long> DOTNode = DotGraph.addNode(node.Key);
                DOTNode = GraphVizNodeFromNeuronNode(DOTNode, node);
            }

            DotGraph.Attributes.Add("nslimit", Math.Ceiling(Math.Sqrt(graph.Nodes.Count)).ToString());
            DotGraph.Attributes.Add("mclimit", Math.Ceiling(Math.Sqrt(graph.Nodes.Count)).ToString());

            List<NeuronEdge> UniqueEdges = new List<NeuronEdge>();
            foreach (NeuronEdge edge in graph.Edges)
            {
                bool AddEdge = true;
                foreach (NeuronEdge existingEdge in UniqueEdges)
                {
                    if (existingEdge.SourceNodeKey == edge.SourceNodeKey &&
                       existingEdge.TargetNodeKey == edge.TargetNodeKey &&
                       existingEdge.SynapseType == edge.SynapseType)
                    {
                        AddEdge = false;
                        existingEdge.Weight = existingEdge.Weight + 1;
                        break;
                    }
                }

                if (AddEdge)
                {
                    UniqueEdges.Add(edge);
                }
            }

            foreach (NeuronEdge edge in UniqueEdges)
            {
                GraphVizEdge<long> DOTEdge = GraphVizEdgeFromNeuronEdge(DotGraph, edge);
            }

            DotGraph.createDirectedGraph("Motif");

            return DotGraph;
        }

        private static GraphVizNode<long> GraphVizNodeFromNeuronNode(GraphVizNode<long> DotNode, NeuronNode node)
        {
            string nodelabel = node.Structure.Label;

            if (node.Structure.Label == null)
            {
                nodelabel = node.Key.ToString(); 
            }

            DotNode.label = nodelabel; 

            DotNode.AddAttributes(AttributeMaps.StandardNodeAttributes); 

            IDictionary<string, string> AttribsForLabel = AttributeMaps.AttribsForLabel(nodelabel, AttributeMaps.StandardLabelToNodeAppearance);

            string label = node.Key.ToString() + " " + node.Structure.Label;
              
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
                DotNode.AddAttributes(AttribsForLabel);
            }

            DotNode.Attributes.Add("label", label);

            return DotNode;
        }

        public static GraphVizEdge<long> GraphVizEdgeFromNeuronEdge(GraphVizEngine<long> DotEngine, NeuronEdge edge)
        {
            GraphVizEdge<long> DotEdge = new GraphVizEdge<long>();
            float additionFactor = 1f;
            float mulFactor = 0.5f;
            //Set the arrow properties
            string color = "black";
            string arrowhead = "";
            string arrowtail = "";
            string tooltip = "";
            string dir = "";
            float arrowsize = additionFactor;
            float pensize = additionFactor;

            dir = "";
            string StoredToolTip = "";

            DotEdge.from = edge.SourceNodeKey;
            DotEdge.to = edge.TargetNodeKey;

            IDictionary<string, string> EdgeAttribs = AttributeMaps.AttribsForLabel(edge.SynapseType.ToUpper(),
                                                                                     AttributeMaps.StandardEdgeLabelToAppearance);

            if (EdgeAttribs == null)
            {
                return null;
            }
            else
            {
                DotEdge.AddAttributes(EdgeAttribs);
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
            DotEdge.Attributes.Add("w", edge.Weight.ToString());
            //tempEdge.edgeAttributes.Add("weight", edge.Strength.ToString());
            DotEdge.Attributes.Add("penwidth", pensize.ToString());
            DotEdge.Attributes.Add("tooltip", tooltip.Length > 250 ? tooltip.Substring(0, 250) : tooltip);

            //If the edge is bidirectional clone it, reverse the direction, and make it invisible to help directional layout algorithms.
            if (DotEdge.Attributes["dir"] == "both")
            {
                GraphVizEdge<long> reverseTempEdge = DotEdge.Clone() as GraphVizEdge<long>;
                reverseTempEdge.to = DotEdge.from;
                reverseTempEdge.from = DotEdge.to;
                reverseTempEdge.Attributes.Add("style", "invis"); //invisible

                DotEngine.addEdge(reverseTempEdge);
            }

            DotEngine.addEdge(DotEdge);
            return DotEdge;
        }
    }
}
