using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using VikingXNA;
using VikingXNAGraphics;
using Geometry;
using Microsoft.Xna.Framework;
using TriangleNet;

namespace MonogameTestbed
{
    /// <summary>
    /// Uses Triangle.Net to triangulate a polygon. Also uses Viking's MedialAxis algorithm.
    /// </summary>
    public class PolygonView
    { 
        public LineSetView ExteriorRingView = new LineSetView();
        public LineSetView InteriorEdgeView = new LineSetView();
        public LineSetView MedialAxisView = new LineSetView();

        private Color _Color = Color.White;
        public Color Color
        {
            get { return _Color; }
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
            get { return _width; }
            set
            {
                _width = value;
                UpdateViews();
            }
        }

        private GridPolygon _Polygon;
        public GridPolygon Polygon
        {
            get
            {
                return _Polygon;
            }
            set
            {
                _Polygon = value;
                UpdateViews();
            }
        }

        private void UpdateViews()
        {
            if(_Polygon == null)
            {
                ExteriorRingView = null;
                InteriorEdgeView = null;
            }

            ExteriorRingView = new LineSetView();
            ExteriorRingView.LineViews = Polygon.ExteriorSegments.Select(s => new LineView(s, this.width, this.Color, LineStyle.Standard)).ToList();

            TriangleNet.Meshing.IMesh mesh = _Polygon.Triangulate();

            int[] indicies = mesh.IndiciesForPointsXY(Polygon.ExteriorRing);

            InteriorEdgeView = new LineSetView();
            List<GridLineSegment> lines = mesh.ToLines();
            InteriorEdgeView.LineViews = lines.Where(l => !Polygon.ExteriorSegments.Contains(l)).Select(s => new LineView(s, this.width, this.Color, LineStyle.Ladder)).ToList();

            MedialAxisView = new LineSetView();
            GridLineSegment[] MedialAxis = MorphologyMesh.MedialAxisFinder.ApproximateMedialAxis(_Polygon).Segments;
            MedialAxisView.LineViews = MedialAxis.Select(s => new LineView(s, this.width, this.Color, LineStyle.Glow)).ToList();
        }

        public void Draw(MonoTestbed window)
        {
            if (ExteriorRingView != null)
                LineView.Draw(window.GraphicsDevice, window.Scene, window.lineManager, ExteriorRingView.LineViews.ToArray());
                 
            if (InteriorEdgeView != null)
                LineView.Draw(window.GraphicsDevice, window.Scene, window.lineManager, InteriorEdgeView.LineViews.ToArray());

            if (MedialAxisView != null)
                LineView.Draw(window.GraphicsDevice, window.Scene, window.lineManager, MedialAxisView.LineViews.ToArray()); 
        }
    }

    public class VikingDelaunayView
    {
        public LineSetView ExteriorRingView = new LineSetView();
        public LineSetView InteriorEdgeView = new LineSetView();
        public LineSetView MedialAxisView = new LineSetView();

        private Color _Color = Color.White;
        public Color Color
        {
            get { return _Color; }
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
            get { return _width; }
            set
            {
                _width = value;
                UpdateViews();
            }
        }

        private GridPolygon _Polygon;
        public GridPolygon Polygon
        {
            get
            {
                return _Polygon;
            }
            set
            {
                _Polygon = value;
                UpdateViews();
            }
        }

        private void UpdateViews()
        {
            if (_Polygon == null)
            {
                ExteriorRingView = null;
                InteriorEdgeView = null;
            }

            ExteriorRingView = new LineSetView();
            ExteriorRingView.LineViews = Polygon.ExteriorSegments.Select(s => new LineView(s, this.width, this.Color, LineStyle.Standard)).ToList();

            TriangleNet.Meshing.IMesh mesh = _Polygon.Triangulate();

            int[] indicies = mesh.IndiciesForPointsXY(Polygon.ExteriorRing);

            InteriorEdgeView = new LineSetView();
            List<GridLineSegment> lines = mesh.ToLines();
            InteriorEdgeView.LineViews = lines.Where(l => !Polygon.ExteriorSegments.Contains(l)).Select(s => new LineView(s, this.width, this.Color, LineStyle.Ladder)).ToList();

            MedialAxisView = new LineSetView();
            GridLineSegment[] MedialAxis = MorphologyMesh.MedialAxisFinder.ApproximateMedialAxis(_Polygon).Segments;
            MedialAxisView.LineViews = MedialAxis.Select(s => new LineView(s, this.width, this.Color, LineStyle.Glow)).ToList();
        }

        public void Draw(MonoTestbed window)
        {
            if (ExteriorRingView != null)
                LineView.Draw(window.GraphicsDevice, window.Scene, window.lineManager, ExteriorRingView.LineViews.ToArray());

            if (InteriorEdgeView != null)
                LineView.Draw(window.GraphicsDevice, window.Scene, window.lineManager, InteriorEdgeView.LineViews.ToArray());

            if (MedialAxisView != null)
                LineView.Draw(window.GraphicsDevice, window.Scene, window.lineManager, MedialAxisView.LineViews.ToArray());
        }
    }
}
