using Viking.AnnotationServiceTypes.Interfaces;
using Geometry;
using GraphLib;
using Microsoft.SqlServer.Types;
using SqlGeometryUtils;
using System;
using System.Collections.Generic;
using System.Linq;

namespace AnnotationVizLib
{
    [Serializable]
    public class MorphologyNode(ulong key, ILocationReadOnly Location, MorphologyGraph parent) : Node<ulong, MorphologyEdge>(key), IGeometry
    {
        public ILocationReadOnly Location = Location;

        public ulong ID => this.Location.ID;

        //Structure this node represents 
        public MorphologyGraph Graph = parent;
        private SqlGeometry _geometry = null;
        public SqlGeometry Geometry
        {
            get
            {
                /*if(Location.Geometry.GeometryType() == SupportedGeometryType.POLYLINE)
{
    var bbox = Location.Geometry.BoundingBox();
    _geometry = Location.Geometry.ToCircle().ToSqlGeometry(); 
}
else
{*/
                _geometry ??= Location.Geometry();

                return _geometry;
            }
            set => _geometry = value;
        }
        public double Z => Location.Z;

        public double UnscaledZ => Location.UnscaledZ;

        public override string ToString() => this.Key.ToString();

        public Box BoundingBox
        {
            get
            {
                Rectangle rect = Geometry.BoundingBox();
                Vector3 botleft = new(rect.Left, rect.Bottom, Z - Graph.SectionThickness / 2.0);
                Vector3 topright = new(rect.Right, rect.Top, Z + Graph.SectionThickness / 2.0);

                Box bbox = new(botleft, topright);
                return bbox;
            }
        }

        public Vector3 Center
        {
            get
            {
                Vector2 c = Geometry.Centroid();
                return new Vector3(c.X, c.Y, Z);
            }
        }

        private readonly SortedSet<ulong> _Subgraphs = [];

        internal void AddSubgraph(ulong StructureID) => _Subgraphs.Add(StructureID);

        internal void RemoveSubgraph(ulong StructureID) => _Subgraphs.Remove(StructureID);

        /// <summary>
        /// List the subgraphs that are nearest to this node, if any
        /// </summary>
        public IReadOnlyList<MorphologyGraph> Subgraphs => [.. _Subgraphs.Select(sid => Graph.Subgraphs[sid])];

        public bool IsNodeAbove(MorphologyNode other) => other.Z > this.Z;

        public bool IsNodeBelow(MorphologyNode other) => other.Z < this.Z;

        /// <summary>
        /// Return edges conneted to nodes above this node in Z
        /// </summary>
        public ulong[] GetEdgesAbove(MorphologyGraph graph = null)
        {
            graph ??= this.Graph;

            return [.. this.Edges.Where(e => this.IsNodeAbove(graph.Nodes[e.Key])).Select(e => e.Key)];
        }

        /// <summary>
        /// Return edges conneted to nodes above this node in Z
        /// </summary>
        public ulong[] GetEdgesBelow(MorphologyGraph graph = null)
        {
            graph ??= this.Graph;

            return [.. this.Edges.Where(e => this.IsNodeBelow(graph.Nodes[e.Key])).Select(e => e.Key)];
        }

        /// <summary>
        /// Unbranched Z-traveling shaft: exactly one LocationLink to the section above and one to the section below.
        /// <see cref="MorphologyGraph.SmoothProcesses"/> translates only these nodes. Degree-2 nodes whose both
        /// links share a Z are branches, not processes.
        /// </summary>
        public bool IsUnbranchedProcess(MorphologyGraph graph = null)
        {
            graph ??= this.Graph;
            return GetEdgesAbove(graph).Length == 1
                && GetEdgesBelow(graph).Length == 1
                && !IsSameSectionBranch(graph);
        }

        /// <summary>
        /// Exactly one LocationLink. Curve endpoint; SmoothProcesses never translates these.
        /// </summary>
        public bool IsProcessTerminal() => Edges.Count == 1;

        /// <summary>
        /// Two or more LocationLinks to the same adjacent Z (Y-junction / Bajaj branch). Pinned; never translated.
        /// </summary>
        public bool IsSameSectionBranch(MorphologyGraph graph = null)
        {
            graph ??= this.Graph;
            return GetConnectedNodesGroupedByZ(graph).Any(g => g.Count() > 1);
        }

        /// <summary>
        /// Return edges conneted to nodes above this node in Z
        /// </summary>
        public IEnumerable<IGrouping<double, MorphologyNode>> GetConnectedNodesGroupedByZ(MorphologyGraph graph = null)
        {
            graph ??= this.Graph;

            return this.Edges.Keys.Select(other => graph.Nodes[other]).GroupBy(other => other.Z);
        }

    }
}
