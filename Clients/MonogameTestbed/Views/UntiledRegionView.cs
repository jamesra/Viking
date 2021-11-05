using Geometry;
using Microsoft.Xna.Framework;
using System;
using System.Collections.Generic;
using System.Linq;
using TriangleNet;
using VikingXNA;
using VikingXNAGraphics;

namespace MonogameTestbed
{
    /// <summary>
    /// Given a set of polygons, renders the medial axis between the polygons
    /// </summary>
    class UntiledRegionView
    {
        public List<PointSet> Sets = new List<PointSet>();

        public List<GridPolygon> Shapes = new List<GridPolygon>();

        public TriangleNet.Voronoi.VoronoiBase Voronoi;

        public List<LineSetView> PolygonViews = new List<LineSetView>();
        public LineSetView VoronoiView = new LineSetView();
        public LineSetView DelaunayView = new LineSetView();
        public LineSetView BoundaryView = new LineSetView();

        public List<LabelView> listLabels = new List<LabelView>();

        public Color Color
        {
            get { return BoundaryView.color; }
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
            LineSetView newView = new MonogameTestbed.LineSetView();
            newView.color = new Color().Random();
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
            int[] original_indicies;

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
                Shapes[i] = new GridPolygon(ps.Points.EnsureClosedRing().ToArray());//ConvexHullExtension.ConvexHull(ps.Points.ToArray(), out original_indicies);
            }
            else
            {
                Shapes[i] = null;
            }

            PolygonViews[i].UpdateViews(Shapes[i]);

            GridPolygon[] ShapeArray = Shapes.Where(s => s != null).ToArray();

            TriangleNet.Meshing.IMesh mesh = null;
            try
            {
                mesh = ShapeArray.Triangulate();
            }
            catch (ArgumentException)
            {

            }

            //List<GridLineSegment> LinesBetweenShapes = SelectLinesBetweenShapes(mesh, Shapes);

            if (mesh == null)
                DelaunayView.UpdateViews(Array.Empty<GridVector2>());
            else
                DelaunayView.UpdateViews(mesh.ToLines());

            Voronoi = Shapes.Voronoi();

            List<GridLineSegment> listVoronoiLines = BoundaryFinder.StripNonBoundaryLines(Voronoi, ShapeArray);
            VoronoiView.UpdateViews(listVoronoiLines);

            //DetermineBoundary
            List<GridLineSegment> listBoundaryLines = BoundaryFinder.DetermineBoundary(ShapeArray);
            BoundaryView.UpdateViews(listBoundaryLines);

            listLabels = listBoundaryLines.Select(line => new LabelView(line.A.ToLabel(), line.A)).ToList();
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
        private TriangleNet.Meshing.IMesh TriangulatePolygons(List<GridVector2[]> PointSets)
        {
            GridVector2[] AllPoints = PointSets.SelectMany(ps => ps.EnsureOpenRing()).ToArray();

            if (AllPoints.Length < 3)
                return null;

            int[] original_indicies;
            GridVector2[] EntireSetConvexHull = AllPoints.ConvexHull(out original_indicies);

            TriangleNet.Geometry.Polygon poly = TriangleExtensions.CreatePolygon(EntireSetConvexHull);

            foreach (GridVector2[] points in PointSets)
            {
                if (points == null || points.Length < 4)
                    continue;

                poly.AppendCountour(points);
            }

            if (poly.Count < 3)
                return null;

            TriangleNet.Meshing.IMesh mesh = TriangleNet.Geometry.ExtensionMethods.Triangulate(poly);
            return mesh;
        }

        private List<LabelView> LabelDistances(IReadOnlyList<GridPolygon> shapes)
        {
            List<LabelView> labels = new List<LabelView>();
            for (int i = 0; i < shapes.Count; i++)
            {
                GridPolygon iPoly = shapes[i];
                if (iPoly == null)
                    continue;

                for (int j = i + 1; j < shapes.Count; j++)
                {
                    GridPolygon jPoly = shapes[j];
                    if (jPoly == null)
                        continue;

                    double minDistance = iPoly.Distance(jPoly);

                    LabelView newLabel = new LabelView(minDistance.ToString(), (iPoly.Centroid + jPoly.Centroid) / 2.0);
                    newLabel.FontSize /= 4.0;

                    labels.Add(newLabel);
                }
            }

            return labels;
        }

        public void Draw(MonoTestbed window, Scene scene)
        {
            if (BoundaryView.LineViews != null)
                LineView.Draw(window.GraphicsDevice, scene, window.lineManager, BoundaryView.LineViews.ToArray());

            if (DelaunayView.LineViews != null)
                LineView.Draw(window.GraphicsDevice, scene, window.lineManager, DelaunayView.LineViews.ToArray());

            if (VoronoiView.LineViews != null)
                LineView.Draw(window.GraphicsDevice, scene, window.lineManager, VoronoiView.LineViews.ToArray());

            LineView.Draw(window.GraphicsDevice, scene, window.lineManager, PolygonViews.Where(poly => poly.LineViews != null).SelectMany(poly => poly.LineViews).ToArray());

            if (listLabels != null)
                LabelView.Draw(window.spriteBatch, window.fontArial, scene, listLabels);
        }
    }
}
