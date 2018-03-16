using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using VikingXNAGraphics;
using VikingXNA; 
using Geometry;
using Microsoft.Xna.Framework;

namespace MonogameTestbed
{
    class PolygonSetView
    {
        private PointSetView[] PolyPointsView = null;
        private LineView[] PolyRingViews = null;

        private List<GridPolygon> _Polygons = new List<GridPolygon>();

        public Color[] PolyLineColors;
        public Color[] PolyVertexColors;

        public bool LabelIndex
        {
            get
            {
                return PolyPointsView[0].LabelIndex;
            }
            set
            {
                foreach (PointSetView psv in PolyPointsView)
                {
                    psv.LabelIndex = value;
                }
            }
        }

        public bool LabelPosition
        {
            get
            {
                return PolyPointsView[0].LabelPosition;
            }
            set
            {
                foreach (PointSetView psv in PolyPointsView)
                {
                    psv.LabelPosition = value;
                }
            }
        }

        public PolygonSetView(IEnumerable<GridPolygon> polys)
        {
            _Polygons = polys.ToList();
            PolyLineColors = polys.Select(p => Color.Black.Random()).ToArray();
            PolyVertexColors = PolyLineColors.Select(c => c.SetAlpha(0.5f)).ToArray();

            UpdatePolyViews();
        }

        private void UpdatePolyViews()
        {
            List<PointSetView> listPointSetView = new List<PointSetView>();

            List<LineView> polyRingViews = new List<LineView>();

            for (int iPoly = 0; iPoly < _Polygons.Count; iPoly++)
            {
                GridPolygon p = _Polygons[iPoly];
                PointSetView psv = new PointSetView();

                List<GridVector2> points = p.ExteriorRing.ToList();
                foreach (GridPolygon innerPoly in p.InteriorPolygons)
                {
                    points.AddRange(innerPoly.ExteriorRing);
                }

                psv.Points = points;

                psv.Color = PolyVertexColors[iPoly];
                psv.LabelIndex = false;

                psv.UpdateViews();
                listPointSetView.Add(psv);

                polyRingViews.AddRange(p.AllSegments.Select(s => new LineView(s, 1, PolyLineColors[iPoly], LineStyle.Standard, false)));
            }

            PolyPointsView = listPointSetView.ToArray();
            PolyRingViews = PolyRingViews.ToArray();
        }

        public void Draw(MonoTestbed window, Scene scene)
        {
            if (PolyRingViews != null)
            {
                LineView.Draw(window.GraphicsDevice, window.Scene, window.lineManager, PolyRingViews);
            }

            if (PolyPointsView != null)
            {
                foreach (PointSetView psv in PolyPointsView)
                {
                    psv.Draw(window, scene);
                }
            }
        }
    }
}
