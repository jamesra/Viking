using System;
using System.Collections.Generic;
using System.Collections.Concurrent;
using System.Linq;
using System.Text;
using GraphLib;
using System.Diagnostics;
using SqlGeometryUtils;
using AnnotationVizLib.AnnotationService;
using Geometry;

namespace AnnotationVizLib
{ 
    public enum LocationType
    {
        POINT = 0,
        CIRCLE = 1,
        ELLIPSE = 2,
        POLYLINE = 3,
        POLYGON = 4,
        OPENCURVE = 5,
        CLOSEDCURVE = 6
    };

    public class MorphologyEdge : Edge<ulong>
    {
        public override bool Directional
        {
            get
            {
                return false;
            }
        }

        public MorphologyEdge(ulong A, ulong B)
            : base(A < B ? A : B, A < B ? B : A, false)
        {
        }

        public MorphologyEdge(long A, long B)
            : this((ulong)A, (ulong)B)
        {
        }

        /// <summary>
        /// Return the other node connected by the edge
        /// </summary>
        /// <param name="key"></param>
        /// <returns></returns>
        public ulong OtherNode(ulong key)
        {
            return SourceNodeKey == key ? TargetNodeKey : SourceNodeKey;
        }
          
        public override string ToString()
        {
            return this.SourceNodeKey.ToString() + "-" + this.TargetNodeKey.ToString();
        }    
    }
     

    public partial class MorphologyGraph : Graph<ulong, MorphologyNode, MorphologyEdge>
    {
        
        /// <summary>
        /// ID of the structure graph, zero for root or StructureID of structure
        /// </summary>
        public readonly ulong StructureID = 0;

        public IStructure structure = null;

        public readonly Geometry.Scale scale = null;

        public virtual double SectionThickness
        {
            get { return scale.Z.Value; }
        }

        public IStructureType structureType
        {
            get { return structure.Type; }
        } 

        /// <summary>
        /// Map the motif label to the arbitrary id used by TLP
        /// </summary>
        public ConcurrentDictionary<ulong, MorphologyGraph> Subgraphs = new ConcurrentDictionary<ulong, MorphologyGraph>();

        public MorphologyGraph(ulong subgraph_id, Geometry.Scale scale)
        {
            this.StructureID = subgraph_id;
            this.structure = null;
            this.scale = scale;
        }

        public MorphologyGraph(ulong subgraph_id, Geometry.Scale scale, IStructure structure)
        {
            this.StructureID = subgraph_id;
            this.structure = structure;
            this.scale = scale;
        }

        /// <summary>
        /// Remove the node, for any edges create new links between the remaining nodes
        /// </summary>
        /// <param name="key"></param>
        private SortedSet<MorphologyEdge> EdgesForRemovedNode(ulong key)
        {
            //Move all of my edges to the nearest node
            double min_distance;
            MorphologyNode node_to_remove = Nodes[key];
            SortedSet<ulong> other_nodes = new SortedSet<ulong>(node_to_remove.Edges.Keys);

            ulong nearest_id = NearestNode(key, other_nodes, out min_distance);

            other_nodes.Remove(nearest_id); //Do not link nearest_node to itself

            SortedSet<MorphologyEdge> new_edges = new SortedSet<AnnotationVizLib.MorphologyEdge>();
            foreach (ulong relink_id in other_nodes)
            {
                MorphologyEdge new_edge = new AnnotationVizLib.MorphologyEdge(nearest_id, relink_id);
                new_edges.Add(new_edge);
            }

            return new_edges;
        }

        private void RemoveNodePreserveEdges(ulong key)
        {
            SortedSet<MorphologyEdge> new_edges = EdgesForRemovedNode(key);

            RemoveNode(key);

            foreach (MorphologyEdge edge in new_edges)
            {
                if(this.Edges.ContainsKey(edge) == false)
                    this.AddEdge(edge);
            }
        } 

        public Geometry.GridBox BoundingBox
        {
            get
            {
                GridBox bbox_accumulator = null;

                if (this.Nodes.Count > 0)
                {
                    bbox_accumulator = this.Nodes.First().Value.BoundingBox;

                    foreach (MorphologyNode node in this.Nodes.Values)
                    {
                        bbox_accumulator.Union(node.BoundingBox);
                    }
                }              
                    
                foreach (MorphologyGraph graph in Subgraphs.Values)
                {
                    if (bbox_accumulator == null)
                        bbox_accumulator = graph.BoundingBox;
                    else
                        bbox_accumulator.Union(graph.BoundingBox);
                }

                return bbox_accumulator;
            }
        }

        protected SortedDictionary<ulong, SortedSet<ulong>> BuildEdgeLookup()
        {
            SortedDictionary<ulong, SortedSet<ulong>> Links = new SortedDictionary<ulong, SortedSet<ulong>>();

            foreach (MorphologyEdge edge in Edges.Values)
            {
                if (!Links.ContainsKey(edge.SourceNodeKey))
                {
                    Links[edge.SourceNodeKey] = new SortedSet<ulong>(new ulong[] { edge.TargetNodeKey });
                }
                else
                {
                    Links[edge.SourceNodeKey].Add(edge.TargetNodeKey);
                }

                if (!Links.ContainsKey(edge.TargetNodeKey))
                {
                    Links[edge.TargetNodeKey] = new SortedSet<ulong>(new ulong[] { edge.SourceNodeKey });
                }
                else
                {
                    Links[edge.TargetNodeKey].Add(edge.SourceNodeKey);
                }
            }

            return Links;
        }

        /// <summary>
        /// Locations with 3 or more edges, branch points in a process
        /// </summary>
        /// <returns></returns>
        public ulong[] GetBranchPoints()
        {
            return this.Nodes.Values.Where(n => n.Edges.Count > 2).Select(n => n.Key).ToArray();
        }

        /// <summary>
        /// Locations with 1 or fewer links, the tip of a process
        /// </summary>
        /// <returns></returns>
        public ulong[] GetTerminals()
        {
            return this.Nodes.Values.Where(n => n.Edges.Count == 1 && !n.Location.IsVericosityCap).Select(n => n.Key).ToArray();
        }

        /// <summary>
        /// Locations with 2 links, the middle of a process
        /// </summary>
        /// <returns></returns>
        public ulong[] GetProcess()
        {
            return this.Nodes.Values.Where(n => n.Edges.Count == 2).Select(n => n.Key).ToArray();
        }

    }
}

