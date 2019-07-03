using Geometry;
using Microsoft.Xna.Framework;
using System.Collections.Generic;
using System.Linq;
using TriangleNet;
using VikingXNAGraphics;

namespace MonogameTestbed
{
    public class ConvexHullView
    {
        public List<LineView> LineViews = new List<LineView>();
        public double LineRadius = 1;
        public Color color;

        public List<LineView> UpdateViews(IReadOnlyList<GridVector2> Points)
        {
            int[] original_indicies;
            GridVector2[] cv_points = ConvexHullExtension.ConvexHull(Points, out original_indicies);

            List<LineView> listLines = new List<LineView>();

            for (int i = 0; i < cv_points.Length - 1; i++)
            {
                listLines.Add(new LineView(cv_points[i],
                                           cv_points[i + 1],
                                           LineRadius,
                                           color,
                                           LineStyle.Standard,
                                           false));
            }

            LineViews = listLines;
            return listLines;
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
                                           LineStyle.Standard,
                                           false));
            }

            return listLines;
        }
    }
}
