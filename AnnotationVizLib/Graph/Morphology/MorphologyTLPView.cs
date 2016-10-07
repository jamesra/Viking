using System;
using System.Diagnostics;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace AnnotationVizLib
{
    public class MorphologyTLPView : TLPView<ulong>
    {
        public readonly Geometry.Scale scale;
        public readonly System.Drawing.Color structure_color;

        public SortedDictionary<MorphologyEdge, ulong> MorphologyEdgeToTulipID = new SortedDictionary<MorphologyEdge, ulong>();

        protected override SortedDictionary<string, string> DefaultAttributes
        {
            get { return TLPAttributes.DefaultForMorphologyAttribute; }
        }

        public MorphologyTLPView(Geometry.Scale scale, System.Drawing.Color ColorMap, string VolumeURL) : base(VolumeURL)
        {
            this.scale = scale;
            this.structure_color = ColorMap;

            if (ColorMap.IsEmpty)
                this.structure_color = System.Drawing.Color.Gray;
        }

        protected new TLPViewNode createNode(ulong ID)
        {
            TLPViewNode tempNode = new TLPViewNode(ID);
            addNode(ID, tempNode);
            return tempNode; 
        }

        /// <summary>
        /// Does not populate attributes since they are inherited
        /// </summary>
        public TLPViewNode CreateTLPSubgraphNode(MorphologyNode node)
        {
            return createNode(node.Key); 
        }
          
        public TLPViewNode CreateTLPNode(MorphologyNode node, System.Drawing.Color color)
        {
            TLPViewNode tlpnode = createNode(node.Key);
            Dictionary<string, string> NodeAttribs = new Dictionary<string, string>();
            /*IDictionary<string, string> NodeAttribs = AttributeMapper.AttribsForLabel(node..Label, TLPAttributes.StandardLabelToNodeTLPAppearance);

            if(NodeAttribs.Count == 0)
            {
                //Add default node properties 
                AttributeMapper.CopyAttributes(TLPAttributes.UnknownTLPNodeAttributes, NodeAttribs);
            }
            */

            tlpnode.Color = color; 
            NodeAttribs.Add("viewSize", NodeSize(node, scale));
            NodeAttribs.Add("viewLayout", NodeLayout(node, scale));
            NodeAttribs.Add("LocationID", node.Location.ID.ToString());
            NodeAttribs.Add("ParentID", node.Location.ParentID.ToString());
            NodeAttribs.Add("LocationInViking", NodeVikingLocation(node));
            NodeAttribs.Add("Terminal", node.Location.Terminal ? "true" : "false");
            NodeAttribs.Add("OffEdge", node.Location.OffEdge ? "true" : "false");
            NodeAttribs.Add("Untraceable", node.Location.IsUntraceable() ? "true" : "false");
            NodeAttribs.Add("Vericosity Cap", node.Location.IsVericosityCap() ? "true" : "false");
            NodeAttribs.Add("StructureTags", node.Graph.AttributesToString());
            NodeAttribs.Add("Tags", ObjAttribute.AttributesToString(node.Location.AttributesXml));

            NodeAttribs.Add("StructureURL", string.Format("{0}/OData/ConnectomeData.svc/Locations({1}L)", this.VolumeURL, node.Location.ID));            

            if(node.Graph.structureType != null)
                NodeAttribs.Add("Type", node.Graph.structureType.Name); 

            if(NodeShape(node) != null)
            {
                NodeAttribs.Add("viewShape", NodeShape(node)); 
            }

            if (node.Graph != null && node.Graph.structure.Links != null && node.Graph.structure.Links.Count() > 0)
            {
                NodeAttribs.Add("NumLinkedStructures", node.Graph.structure.Links.Count().ToString());
            } 

            tlpnode.AddAttributes(NodeAttribs);

            return tlpnode;
        }

        public static string NodeShape(MorphologyNode node)
        {
            if (node.Graph != null && node.Graph.structure.Links != null && node.Graph.structure.Links.Count() > 0)
                return TLPAttributes.IntForShape(TLPAttributes.NodeShapes.GlowSphere);

            return null; 
        }
         

        public static string NodeVikingLocation(MorphologyNode node)
        {
            AnnotationService.AnnotationPoint pos = node.Location.VolumePosition;
            return string.Format("X:{0} Y:{1} Z:{2}", pos.X, pos.Y, node.Location.Section);
        }

        public static string NodeLayout(MorphologyNode node, Geometry.Scale scale)
        {
            AnnotationService.AnnotationPoint pos = node.Location.VolumePosition;
            return string.Format("({0},{1},{2})", pos.X * scale.X.Value, pos.Y * scale.Y.Value, node.Location.Section * scale.Z.Value);
        }

        public static string NodeSize(MorphologyNode node, Geometry.Scale scale)
        {
            //OK, tulip treats the location property as the center of the shape.  The size is centered on the origin.  So if a cell is centered on 0, and the radius is 50.  We need to use the diamater to ensure the size is correct.
            return string.Format("({0},{1},{2})", node.Location.Radius * 2.0 * scale.X.Value, node.Location.Radius * 2.0 * scale.Y.Value, 1 * scale.Z.Value);
        }

        public string LabelForNode(MorphologyNode node)
        {
            return node.Key.ToString();
        }

        public string LabelForStructure(AnnotationService.Structure s)
        {
            if (s == null)
                return "";

            if (s.Label == null || s.Label.Length == 0)
            {
                //TODO: Return StructureTypeID
                return string.Format("{0} #{1}", s.TypeID, s.ID);
            }

            return string.Format("{0} #{1}", s.ID, s.Label);
        }

        public static string LinkString(AnnotationService.StructureLink link)
        {
            return link.SourceID + " -> " + link.TargetID;
        }

        /// <summary>
        /// Does not populate attributes since they are inherited
        /// </summary>
        public TLPViewEdge CreateTLPSubgraphEdge(MorphologyEdge edge)
        {
            TLPViewEdge tlpedge = null;
            try
            {
                tlpedge = this.addEdge(edge.SourceNodeKey, edge.TargetNodeKey);
            }
            catch (KeyNotFoundException)
            {
                Trace.WriteLine(string.Format("Nodes missing for edge {0}", edge.ToString()));
                return null;
            }
            return tlpedge;
        }

        /// <summary>
        /// Create an edge between two nodes.  Returns null if the nodes do not exist
        /// </summary>
        /// <param name="edge"></param>
        /// <returns></returns>
        public TLPViewEdge CreateTLPEdge(MorphologyEdge edge, System.Drawing.Color color)
        {
            TLPViewEdge tlpedge = null;
            try
            {
                tlpedge = this.addEdge(edge.SourceNodeKey, edge.TargetNodeKey);
                tlpedge.Color = color;

                MorphologyEdgeToTulipID.Add(edge, tlpedge.tulip_id); 

            }
            catch(KeyNotFoundException)
            {
                Trace.WriteLine(string.Format("Nodes missing for edge {0}", edge.ToString()));
                return null;
            }
            
            /*
            IDictionary<string, string> EdgeAttribs = AttributeMapper.AttribsForLabel(edge.SynapseType, TLPAttributes.StandardEdgeSourceLabelToTLPAppearance);

            if (EdgeAttribs.Count == 0)
            {
                //Add default node properties 
                AttributeMapper.CopyAttributes(TLPAttributes.UnknownTLPEdgeAttributes, EdgeAttribs);
            }

            EdgeAttribs.Add("viewLabel", edge.SynapseType);
            EdgeAttribs.Add("edgeType", edge.SynapseType);
            EdgeAttribs.Add("LinkedStructures", LinkedStructures(edge));
            
            tlpedge.AddAttributes(EdgeAttribs);
            
            */

            return tlpedge;
        }

        public static System.Drawing.Color GetStructureColor(MorphologyGraph graph, StructureMorphologyColorMap colorMap)
        {
            if (colorMap == null)
                return System.Drawing.Color.Empty;

            return colorMap.GetColor(graph); 
        }

        public static MorphologyTLPView ToTLP(MorphologyGraph graph, Geometry.Scale scale, StructureMorphologyColorMap colorMap, string VolumeURL)
        {
            MorphologyTLPView view = new MorphologyTLPView(scale, GetStructureColor(graph, colorMap), VolumeURL);

            AddAllSubgraphNodesAndEdges(view, graph, colorMap);

            foreach(ulong subgraph_key in graph.Subgraphs.Keys)
            {
                ulong subgraph_tlp_id = view.GenerateNextSubgraphID();
                MorphologyGraph subgraph = graph.Subgraphs[subgraph_key];
                TLPViewSubgraph subgraph_view = AssignNodesToSubgraphs(view, subgraph, colorMap);
                view.AddSubGraph(subgraph_tlp_id, subgraph_view);
            }
             
            return view;
        }

        private static void AddAllSubgraphNodesAndEdges(MorphologyTLPView view, MorphologyGraph structuregraph, StructureMorphologyColorMap colorMap)
        {
            System.Drawing.Color color = colorMap.GetColor(structuregraph);

            foreach (MorphologyNode node in structuregraph.Nodes.Values)
            {
                view.CreateTLPNode(node, color);
            }

            foreach (MorphologyEdge edge in structuregraph.Edges.Values)
            {
                view.CreateTLPEdge(edge, color);
            }

            foreach (ulong subgraph_id in structuregraph.Subgraphs.Keys)
            { 
                MorphologyGraph subgraph = structuregraph.Subgraphs[subgraph_id];
                //MorphologyTLPView subgraph_view = new MorphologyTLPView(view.scale, GetStructureColor(subgraph, colorMap));
                
                //CreateSubgraph(subgraph_view,subgraph); 

                AddAllSubgraphNodesAndEdges(view, subgraph, colorMap);
                //view.AddSubGraph(subgraph_id, subgraph_view);
            } 
        }

        private static TLPViewSubgraph AssignNodesToSubgraphs(MorphologyTLPView view, MorphologyGraph structuregraph, StructureMorphologyColorMap colorMap)
        {
            TLPViewSubgraph subgraph_view = new TLPViewSubgraph(view.GenerateNextSubgraphID(),
                                                                    view.LabelForStructure(structuregraph.structure));
            subgraph_view.Color = GetStructureColor(structuregraph, colorMap);

            foreach (MorphologyNode node in structuregraph.Nodes.Values)
            {
                subgraph_view.AddNode(node.Key);
            }

            foreach (MorphologyEdge edge in structuregraph.Edges.Values)
            {
                subgraph_view.AddEdge(view.MorphologyEdgeToTulipID[edge]);
            }

            foreach (ulong subgraph_id in structuregraph.Subgraphs.Keys)
            {
                MorphologyGraph subgraph = structuregraph.Subgraphs[subgraph_id];
                   
                subgraph_view.AddSubgraph(view.GenerateNextSubgraphID(),
                                          AssignNodesToSubgraphs(view, subgraph, colorMap));
            }

            return subgraph_view;
        }
    }
}
