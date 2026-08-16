using Geometry;
using Microsoft.Xna.Framework;
using System.Collections.Generic;
using System.Linq;
using TriangleNet;
using VikingXNAGraphics;
using Vector2 = Microsoft.Xna.Framework.Vector2;
using Vector3 = Microsoft.Xna.Framework.Vector3;

namespace MonogameTestbed
{

    public class LineSetView
    {
        public List<LineView> LineViews = [];
        public List<LabelView> LineLabels = [];
        public double LineRadius = 1;
        public Color color;
        public LineStyle Style = LineStyle.Standard;

        /// <summary>
        /// Optional
        /// </summary>
        public string Name = "";

        public void UpdateViews(ICollection<Geometry.Vector2> Points)
        {
            if (Points.Count >= 3)
            {
                TriangleNet.Voronoi.VoronoiBase v = Points.Voronoi();
                UpdateViews(v);
            }
            else
            {
                LineViews = [];
            }
        }

        public void UpdateViews(TriangleNet.Voronoi.VoronoiBase v) => LineViews = v != null ? ToLines(v, color) : [];

        public void UpdateViews(ICollection<LineSegment> lines) => LineViews = lines != null ? [.. lines.Select(l => new LineView(l.A, l.B, LineRadius, color, Style))] : [];

        public void UpdateViews(Polygon polygon) => LineViews = polygon is null ? [] : [.. polygon.ExteriorSegments.Select(l => new LineView(l.A, l.B, LineRadius, color, Style))];

        private List<LineView> ToLines(TriangleNet.Topology.DCEL.DcelMesh mesh, Color color)
        {
            List<LineView> listLines = [];
            //Create a map of Vertex ID's to DRMesh ID's
            int[] IndexMap = [.. mesh.Vertices.Select(v => v.ID)];

            foreach (var e in mesh.Edges)
            {
                listLines.Add(new LineView(mesh.Vertices[e.P0].ToVector2(),
                                           mesh.Vertices[e.P1].ToVector2(),
                                           LineRadius,
                                           color,
                                           Style));
            }

            return listLines;
        }
    }

}
