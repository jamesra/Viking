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
                _geometry ??= Location.Geometry;

                return _geometry;
            }
            set => _geometry = value;
        }
        public double Z => Location.Z;

        public double UnscaledZ => Location.UnscaledZ;

        public override string ToString() => this.Key.ToString();

        public GridBox BoundingBox
        {
            get
            {
                GridRectangle rect = Geometry.BoundingBox();
                GridVector3 botleft = new(rect.Left, rect.Bottom, Z - Graph.SectionThickness / 2.0);
                GridVector3 topright = new(rect.Right, rect.Top, Z + Graph.SectionThickness / 2.0);

                GridBox bbox = new(botleft, topright);
                return bbox;
            }
        }

        public GridVector3 Center
        {
            get
            {
                GridVector2 c = Geometry.Centroid();
                return new GridVector3(c.X, c.Y, Z);
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
        /// Return edges conneted to nodes above this node in Z
        /// </summary>
        public IEnumerable<IGrouping<double, MorphologyNode>> GetConnectedNodesGroupedByZ(MorphologyGraph graph = null)
        {
            graph ??= this.Graph;

            return this.Edges.Keys.Select(other => graph.Nodes[other]).GroupBy(other => other.Z);
        }

    }
}
