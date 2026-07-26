using Geometry;
using Microsoft.Xna.Framework;
using System.Collections.Generic;
using System.Linq;
using TriangleNet;
using VikingXNAGraphics;

namespace MonogameTestbed
{
    /// <summary>
    /// Uses Triangle.Net to triangulate a polygon. Also uses Viking's MedialAxis algorithm.
    /// </summary>
    public class PolygonContourView
    {
        public LineSetView ExteriorRingView = new();
        public LineSetView InteriorEdgeView = new();
        public LineSetView MedialAxisView = new();

        private Color _Color = Color.White;
        public Color Color
        {
            get => _Color;
            set
            {
                _Color = value;
                ExteriorRingView.color = value;
                InteriorEdgeView.color = value;
                MedialAxisView.color = value;
            }
        }

        private double _width = 2.0;
        public double width
        {
            get => _width;
            set
            {
                _width = value;
                UpdateViews();
            }
        }

        private GridPolygon _Polygon;
        public GridPolygon Polygon
        {
            get => _Polygon;
            set
            {
                _Polygon = value;
                UpdateViews();
            }
        }

        private void UpdateViews()
        {
            if (_Polygon is null)
            {
                ExteriorRingView = null;
                InteriorEdgeView = null;
            }

            ExteriorRingView = new LineSetView
            {
                LineViews = [.. Polygon.ExteriorSegments.Select(s => new LineView(s, this.width, this.Color, LineStyle.Standard))]
            };

            TriangleNet.Meshing.IMesh mesh = _Polygon.Triangulate();

            int[] indicies = mesh.IndiciesForPointsXY(Polygon.ExteriorRing);

            InteriorEdgeView = new LineSetView();
            List<GridLineSegment> lines = mesh.ToLines();
            InteriorEdgeView.LineViews = [.. lines.Where(l => !Polygon.ExteriorSegments.Contains(l)).Select(s => new LineView(s, this.width, this.Color, LineStyle.Ladder))];

            MedialAxisView = new LineSetView();
            var MedialAxis = MedialAxisFinder.ApproximateMedialAxisChordal(_Polygon, extendToApex: true);
            GridLineSegment[] MedialAxisSegments = MedialAxis.Segments;
            MedialAxisView.LineViews = [.. MedialAxisSegments.Select(s => new LineView(s, this.width, this.Color, LineStyle.Glow))];
            var NewVerts = MedialAxis.Nodes.Values.ToArray();
            System.Diagnostics.Debug.Assert(NewVerts.All(v => _Polygon.GetRelation(v.Key) == ShapeRelation.CONTAINED), "Interior points must be inside Face");

        }

        public void Draw(MonoTestbed window)
        {
            if (ExteriorRingView != null)
                LineView.Draw(window.GraphicsDevice, window.Scene, window.lineManager, [.. ExteriorRingView.LineViews]);

            if (InteriorEdgeView != null)
                LineView.Draw(window.GraphicsDevice, window.Scene, window.lineManager, [.. InteriorEdgeView.LineViews]);

            if (MedialAxisView != null)
                LineView.Draw(window.GraphicsDevice, window.Scene, window.lineManager, [.. MedialAxisView.LineViews]);
        }
    }

    public class VikingDelaunayView
    {
        public LineSetView ExteriorRingView = new();
        public LineSetView InteriorEdgeView = new();
        public LineSetView MedialAxisView = new();

        private Color _Color = Color.White;
        public Color Color
        {
            get => _Color;
            set
            {
                _Color = value;
                ExteriorRingView.color = value;
                InteriorEdgeView.color = value;
                MedialAxisView.color = value;
            }
        }

        private double _width = 2.0;
        public double width
        {
            get => _width;
            set
            {
                _width = value;
                UpdateViews();
            }
        }

        private GridPolygon _Polygon;
        public GridPolygon Polygon
        {
            get => _Polygon;
            set
            {
                _Polygon = value;
                UpdateViews();
            }
        }

        private void UpdateViews()
        {
            if (_Polygon is null)
            {
                ExteriorRingView = null;
                InteriorEdgeView = null;
                return;
            }

            ExteriorRingView = new LineSetView();
            if (Polygon != null)
            {
                ExteriorRingView.LineViews = [.. Polygon.ExteriorSegments.Select(s => new LineView(s, this.width, this.Color, LineStyle.Standard))];
            }

            TriangleNet.Meshing.IMesh mesh = _Polygon.Triangulate();

            int[] indicies = mesh.IndiciesForPointsXY(Polygon.ExteriorRing);

            InteriorEdgeView = new LineSetView();
            List<GridLineSegment> lines = mesh.ToLines();
            InteriorEdgeView.LineViews = [.. lines.Where(l => !Polygon.ExteriorSegments.Contains(l)).Select(s => new LineView(s, this.width, this.Color, LineStyle.Ladder))];

            MedialAxisView = new LineSetView();
            GridLineSegment[] MedialAxis = MedialAxisFinder.ApproximateMedialAxisChordal(_Polygon, extendToApex: true).Segments;
            MedialAxisView.LineViews = [.. MedialAxis.Select(s => new LineView(s, this.width, this.Color, LineStyle.Glow))];
        }

        public void Draw(MonoTestbed window)
        {
            if (ExteriorRingView != null)
                LineView.Draw(window.GraphicsDevice, window.Scene, window.lineManager, [.. ExteriorRingView.LineViews]);

            if (InteriorEdgeView != null)
                LineView.Draw(window.GraphicsDevice, window.Scene, window.lineManager, [.. InteriorEdgeView.LineViews]);

            if (MedialAxisView != null)
                LineView.Draw(window.GraphicsDevice, window.Scene, window.lineManager, [.. MedialAxisView.LineViews]);
        }
    }
}
