﻿using Geometry;
using Microsoft.Xna.Framework;
using System.Collections.Generic;
using System.Linq;
using TriangleNet;
using VikingXNAGraphics;

namespace MonogameTestbed
{

    public class LineSetView
    {
        public List<LineView> LineViews = new List<LineView>();
        public List<LabelView> LineLabels = new List<LabelView>();
        public double LineRadius = 1;
        public Color color;
        public LineStyle Style = LineStyle.Standard;

        /// <summary>
        /// Optional
        /// </summary>
        public string Name = "";

        public void UpdateViews(ICollection<GridVector2> Points)
        {
            if (Points.Count >= 3)
            {
                TriangleNet.Voronoi.VoronoiBase v = Points.Voronoi();
                UpdateViews(v);
            }
            else
            {
                LineViews = new List<LineView>();
            }
        }

        public void UpdateViews(TriangleNet.Voronoi.VoronoiBase v)
        {
            if (v != null)
            {
                LineViews = ToLines(v, color);
            }
            else
            {
                LineViews = new List<LineView>();
            }
        }

        public void UpdateViews(ICollection<GridLineSegment> lines)
        {
            if (lines != null)
            {
                LineViews = lines.Select(l => new LineView(l.A, l.B, LineRadius, color, Style)).ToList();

            }
            else
            {
                LineViews = new List<LineView>();
            }
        }

        public void UpdateViews(GridPolygon polygon)
        {
            if (polygon == null)
            {
                LineViews = new List<LineView>();
            }
            else
            {
                LineViews = polygon.ExteriorSegments.Select(l => new LineView(l.A, l.B, LineRadius, color, Style)).ToList();
            }
        }

        private List<LineView> ToLines(TriangleNet.Topology.DCEL.DcelMesh mesh, Color color)
        {
            List<LineView> listLines = new List<LineView>();
            //Create a map of Vertex ID's to DRMesh ID's
            int[] IndexMap = mesh.Vertices.Select(v => v.ID).ToArray();

            foreach (var e in mesh.Edges)
            {
                listLines.Add(new LineView(mesh.Vertices[e.P0].ToGridVector2(),
                                           mesh.Vertices[e.P1].ToGridVector2(),
                                           LineRadius,
                                           color,
                                           Style));
            }

            return listLines;
        }
    }

}
