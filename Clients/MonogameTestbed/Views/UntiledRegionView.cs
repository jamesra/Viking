using Geometry;
using Microsoft.Xna.Framework;
using System;
using System.Collections.Generic;
using System.Linq;
using TriangleNet;
using VikingXNA;
using VikingXNAGraphics;
using Vector2 = Microsoft.Xna.Framework.Vector2;
using Vector3 = Microsoft.Xna.Framework.Vector3;

namespace MonogameTestbed
{
    /// <summary>
    /// Given a set of polygons, renders the medial axis between the polygons
    /// </summary>
    class UntiledRegionView
    {
        public List<PointSet> Sets = [];

        public List<Polygon> Shapes = [];

        public TriangleNet.Voronoi.VoronoiBase Voronoi;

        public List<LineSetView> PolygonViews = [];
        public LineSetView VoronoiView = new();
        public LineSetView DelaunayView = new();
        public LineSetView BoundaryView = new();

        public List<LabelView> listLabels = [];

        public Color Color
        {
            get => BoundaryView.color;
            set
            {
                BoundaryView.color = value;
                VoronoiView.color = value;
            }
        }

        public int AddSet(PointSet set)
        {
            Sets.Add(set);
            Shapes.Add(null);
            LineSetView newView = new()
            {
                color = new Color().Random()
            };
            PolygonViews.Add(newView);
            UpdateSet(set, Sets.Count - 1);
            return Sets.Count - 1;
        }

        public UntiledRegionView()
        {
            VoronoiView.LineRadius = 1;
            DelaunayView.LineRadius = 1;
            BoundaryView.LineRadius = 2;
        }

        /// <summary>
        /// When a pointset changes we need to recalculate the dividing line between convex hulls
        /// </summary>
        public void UpdateSet(PointSet ps, int i)
        {
            int[] originalIndices;

            Sets[i] = ps;

            //Algorithm:
            //1. Triangulate and remove points that do not have an edge to the other polygon.
            //2. Create a voronoi diagram of the remaining points
            //3. Loop
            // a. If the voronoi edge does not intersect either polygon it is part of the border
            // b. If the edge does intersect we create a new line from any point connected to the border and the intersection of the Delaunay edge
            //    and the voronoi edge
            // c. ??? 

            if (ps.Points.Count >= 3)
            {
                Shapes[i] = new Polygon(ps.Points.EnsureClosedRing().ToArray());//ConvexHullExtension.ConvexHull(ps.Points.ToArray(), out originalIndices);
            }
            else
            {
                Shapes[i] = null;
            }

            PolygonViews[i].UpdateViews(Shapes[i]);

            Polygon[] ShapeArray = [.. Shapes.Where(s => s != null)];

            TriangleNet.Meshing.IMesh mesh = null;
            try
            {
                mesh = ShapeArray.Triangulate();
            }
            catch (ArgumentException)
            {

            }

            //List<LineSegment> LinesBetweenShapes = SelectLinesBetweenShapes(mesh, Shapes);

            if (mesh is null)
                DelaunayView.UpdateViews(Array.Empty<Geometry.Vector2>());
            else
                DelaunayView.UpdateViews(mesh.ToLines());

            Voronoi = Shapes.Voronoi();

            List<LineSegment> listVoronoiLines = BoundaryFinder.StripNonBoundaryLines(Voronoi, ShapeArray);
            VoronoiView.UpdateViews(listVoronoiLines);

            //DetermineBoundary
            List<LineSegment> listBoundaryLines = BoundaryFinder.DetermineBoundary(ShapeArray);
            BoundaryView.UpdateViews(listBoundaryLines);

            listLabels = [.. listBoundaryLines.Select(line => new LabelView(line.A.ToLabel(), line.A))];
            listLabels.ForEach(label =>
            {
                label.FontSize = 2;
                label.Color = Color.Green;
            });
        }



        /// <summary>
        /// This function creates the triangulation of a set of polygons returning the set of edges between polygons and the external polygon borders.
        /// </summary>
        /// <param name="PointSets"></param>
        /// <returns></returns>
        private static TriangleNet.Meshing.IMesh TriangulatePolygons(List<Geometry.Vector2[]> PointSets)
        {
            Geometry.Vector2[] AllPoints = [.. PointSets.SelectMany(ps => ps.EnsureOpenRing())];

            if (AllPoints.Length < 3)
                return null;

            Geometry.Vector2[] EntireSetConvexHull = AllPoints.ConvexHull(out int[] originalIndices);

            TriangleNet.Geometry.Polygon poly = TriangleExtensions.CreatePolygon(EntireSetConvexHull);

            foreach (Geometry.Vector2[] points in PointSets)
            {
                if (points is null || points.Length < 4)
                    continue;

                poly.AppendCountour(points);
            }

            if (poly.Count < 3)
                return null;

            TriangleNet.Meshing.IMesh mesh = TriangleNet.Geometry.ExtensionMethods.Triangulate(poly);
            return mesh;
        }

        private static List<LabelView> LabelDistances(IReadOnlyList<Polygon> shapes)
        {
            List<LabelView> labels = [];
            for (int i = 0; i < shapes.Count; i++)
            {
                Polygon iPoly = shapes[i];
                if (iPoly is null)
                    continue;

                for (int j = i + 1; j < shapes.Count; j++)
                {
                    Polygon jPoly = shapes[j];
                    if (jPoly is null)
                        continue;

                    double minDistance = iPoly.Distance(jPoly);

                    LabelView newLabel = new(minDistance.ToString(), (iPoly.Centroid + jPoly.Centroid) / 2.0);
                    newLabel.FontSize /= 4.0;

                    labels.Add(newLabel);
                }
            }

            return labels;
        }

        public void Draw(MonoTestbed window, Scene scene)
        {
            if (BoundaryView.LineViews != null)
                LineView.Draw(window.GraphicsDevice, scene, window.lineManager, [.. BoundaryView.LineViews]);

            if (DelaunayView.LineViews != null)
                LineView.Draw(window.GraphicsDevice, scene, window.lineManager, [.. DelaunayView.LineViews]);

            if (VoronoiView.LineViews != null)
                LineView.Draw(window.GraphicsDevice, scene, window.lineManager, [.. VoronoiView.LineViews]);

            LineView.Draw(window.GraphicsDevice, scene, window.lineManager, [.. PolygonViews.Where(poly => poly.LineViews != null).SelectMany(poly => poly.LineViews)]);

            if (listLabels != null)
                LabelView.Draw(window.spriteBatch, window.fontArial, scene, listLabels);
        }
    }
}
