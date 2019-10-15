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
    [Flags]
    public enum IndexLabelType
    {
        NONE = 0x0,
        MESH = 0x01, //The index of the vertex inside the mesh.
        POSITION = 0x02, //The position of the vertex
        POLYGON = 0x04, //The polygon indicies, with polygon index and vertex inside the polygon index
    }

    class PolygonSetView
    {
        private PointSetView[] PolyPointsView = null;
        private LineView[] PolyRingViews = null;
        private LabelView[] PolyIndexLabels = new LabelView[0];

        private List<GridPolygon> _Polygons = new List<GridPolygon>();

        public Color[] PolyLineColors;
        public Color[] PolyVertexColors;

        public IndexLabelType PointLabelType
        {
            get
            {
                IndexLabelType flags = IndexLabelType.NONE;
                flags |= this.LabelPosition ? IndexLabelType.POSITION : IndexLabelType.NONE;
                flags |= this.LabelIndex ? IndexLabelType.MESH : IndexLabelType.NONE;
                flags |= this.LabelPolygonIndex ? IndexLabelType.POLYGON : IndexLabelType.NONE;
                return flags; 
            }
            set
            {
                this.LabelIndex = (value & IndexLabelType.MESH) > 0;
                this.LabelPolygonIndex = (value & IndexLabelType.POLYGON) > 0;
                this.LabelPosition = (value & IndexLabelType.POSITION) > 0;
            }
        }

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

                if(value)
                {
                    this.LabelPosition = false;
                    this.LabelPolygonIndex = false; 
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

                if (value)
                {
                    this.LabelIndex = false;
                    this.LabelPolygonIndex = false;
                }
            }
        }

        private bool _LabelPolygonIndex = false;
        public bool LabelPolygonIndex
        {
            get
            {
                return _LabelPolygonIndex;
            }
            set
            {
                _LabelPolygonIndex = value;
                if(value)
                {
                    PolyIndexLabels = CreatePolyIndexLabels(_Polygons).ToArray();
                }

                if (value)
                {
                    this.LabelIndex = false;
                    this.LabelPosition = false;
                }
            }
        }

        private static List<LabelView> CreatePolyIndexLabels(List<GridPolygon> Polygons)
        {
            List<LabelView> listPointLabels = new List<LabelView>();

            foreach(PointIndex pi in new PolySetVertexEnum(Polygons))
            {
                GridVector2 point = pi.Point(Polygons);
                LabelView label = new LabelView(pi.ToString(), point);
                listPointLabels.Add(label);
                label.FontSize = 1;
            }

            return listPointLabels;
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

                polyRingViews.AddRange(p.AllSegments.Select(s => new LineView(s, 1, PolyLineColors[iPoly], LineStyle.Standard)));
            }

            PolyPointsView = listPointSetView.ToArray();
            PolyRingViews = polyRingViews.ToArray();
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

            if(((this.PointLabelType & (IndexLabelType.POLYGON)) > 0) && this.PolyIndexLabels != null)
            {
                LabelView.Draw(window.spriteBatch, window.fontArial, scene, this.PolyIndexLabels);
            }
        }
    }
}
