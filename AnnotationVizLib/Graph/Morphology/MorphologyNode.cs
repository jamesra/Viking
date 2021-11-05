﻿using Viking.AnnotationServiceTypes.Interfaces;
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
    public class MorphologyNode : Node<ulong, MorphologyEdge>, IGeometry
    {
        public ILocationReadOnly Location = null;

        public ulong ID { get { return this.Location.ID; } }

        //Structure this node represents 
        public MorphologyGraph Graph;

        public MorphologyNode(ulong key, ILocationReadOnly Location, MorphologyGraph parent)
            : base(key)
        {
            this.Graph = parent;
            this.Location = Location;
        }

        private IShape2D _geometry = null;
        public IShape2D Geometry
        {
            get
            {
                if (_geometry != null)
                    return _geometry;

                _geometry = Location.VolumeGeometryWKT.ParseWKT();
                return _geometry;
            }
            set => _geometry = value; 
        }

        public double Z { get { return Location.Z; } }

        public double UnscaledZ { get { return Location.UnscaledZ; } }

        public override string ToString()
        {
            return this.Key.ToString();
        }

        public GridBox BoundingBox
        {
            get
            {
                GridRectangle rect = Geometry.BoundingBox;
                GridVector3 botleft = new GridVector3(rect.Left, rect.Bottom, Z - Graph.SectionThickness / 2.0);
                GridVector3 topright = new GridVector3(rect.Right, rect.Top, Z + Graph.SectionThickness / 2.0);

                GridBox bbox = new GridBox(botleft, topright);
                return bbox;
            }
        }

        public GridVector3 Center
        {
            get
            {
                GridVector2 c = Geometry.Centroid;
                return new GridVector3(c.X, c.Y, Z);
            }
        }

        private SortedSet<ulong> _Subgraphs = new SortedSet<ulong>();

        internal void AddSubgraph(ulong StructureID)
        {
            _Subgraphs.Add(StructureID);
        }

        internal void RemoveSubgraph(ulong StructureID)
        {
            _Subgraphs.Remove(StructureID);
        }

        /// <summary>
        /// List the subgraphs that are nearest to this node, if any
        /// </summary>
        public IReadOnlyList<MorphologyGraph> Subgraphs
        {
            get
            {
                return _Subgraphs.Select(sid => Graph.Subgraphs[sid]).ToList();
            }
        }

        public bool IsNodeAbove(MorphologyNode other)
        {
            return other.Z > this.Z;
        }

        public bool IsNodeBelow(MorphologyNode other)
        {
            return other.Z < this.Z;
        }

        /// <summary>
        /// Return edges conneted to nodes above this node in Z
        /// </summary>
        public ulong[] GetEdgesAbove(MorphologyGraph graph = null)
        {
            if (graph == null)
            {
                graph = this.Graph;
            }

            return this.Edges.Where(e => this.IsNodeAbove(graph.Nodes[e.Key])).Select(e => e.Key).ToArray();
        }

        /// <summary>
        /// Return edges conneted to nodes above this node in Z
        /// </summary>
        public ulong[] GetEdgesBelow(MorphologyGraph graph = null)
        {
            if (graph == null)
            {
                graph = this.Graph;
            }

            return this.Edges.Where(e => this.IsNodeBelow(graph.Nodes[e.Key])).Select(e => e.Key).ToArray();
        }

        /// <summary>
        /// Return edges conneted to nodes above this node in Z
        /// </summary>
        public IEnumerable<IGrouping<double, MorphologyNode>> GetConnectedNodesGroupedByZ(MorphologyGraph graph = null)
        {
            if (graph == null)
            {
                graph = this.Graph;
            }

            return this.Edges.Keys.Select(other => graph.Nodes[other]).GroupBy(other => other.Z);
        }

    }
}
