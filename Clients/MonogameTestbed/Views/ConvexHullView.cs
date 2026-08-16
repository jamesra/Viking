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
    public class ConvexHullView
    {
        public List<LineView> LineViews = [];
        public double LineRadius = 1;
        public Color color;

        public List<LineView> UpdateViews(IReadOnlyList<Geometry.Vector2> Points)
        {
            Geometry.Vector2[] cv_points = ConvexHullExtension.ConvexHull(Points, out int[] originalIndices);

            List<LineView> listLines = [];

            for (int i = 0; i < cv_points.Length - 1; i++)
            {
                listLines.Add(new LineView(cv_points[i],
                                           cv_points[i + 1],
                                           LineRadius,
                                           color,
                                           LineStyle.Standard));
            }

            LineViews = listLines;
            return listLines;
        }

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
                                           LineStyle.Standard));
            }

            return listLines;
        }
    }
}
