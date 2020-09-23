using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using VikingXNAGraphics;
using VikingXNA; 
using Geometry;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;

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

        private double _PointRadius;
        public double PointRadius
        {
            get
            {
                return _PointRadius;
            }
            set {

                if (_PointRadius != value)
                { 
                    foreach (PointSetView psv in PolyPointsView)
                    {
                        psv.PointRadius = value;
                    }

                    _PointRadius = value;
                }
            }
        }

        private IndexLabelType _PointLabelTypes = IndexLabelType.NONE;
        public IndexLabelType PointLabelType
        {
            get
            {
                return _PointLabelTypes;
            }
            set
            {
                _PointLabelTypes = value; 
                this.LabelIndex = (value & IndexLabelType.MESH) > 0;
                this.LabelPolygonIndex = (value & IndexLabelType.POLYGON) > 0;
                this.LabelPosition = (value & IndexLabelType.POSITION) > 0;
            }
        }

        public bool LabelIndex
        {
            get
            {
                return (_PointLabelTypes & IndexLabelType.MESH) > 0;
            }
            private set
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
                return (_PointLabelTypes & IndexLabelType.POSITION) > 0;
            }
            private set
            {
                foreach (PointSetView psv in PolyPointsView)
                {
                    psv.LabelPosition = value;
                }
            }
        }

        public bool LabelPolygonIndex
        {
            get
            {
                return (_PointLabelTypes & IndexLabelType.POLYGON) > 0;
            }
            private set
            {
                if(true == value)
                {
                    PolyIndexLabels = CreatePolyIndexLabels(_Polygons, this.PointRadius).ToArray();
                }
            }
        }

        private static List<LabelView> CreatePolyIndexLabels(List<GridPolygon> Polygons, double pointradius)
        {
            List<LabelView> listPointLabels = new List<LabelView>();

            //Figure out if we have duplicate points and offset labels as needed
            var pointEnum = new PolySetVertexEnum(Polygons);
            GridVector2[] point_array = pointEnum.Select(i => i.Point(Polygons)).ToArray();

            Dictionary<GridVector2, int> DuplicatePointsAddedCount = new Dictionary<GridVector2, int>(); //Track the number of times we've hit a specific duplicate point and move the label accordingly
            HashSet<GridVector2> KnownPoints = new HashSet<GridVector2>();
            foreach (GridVector2 p in point_array)
            {
                if (KnownPoints.Contains(p))
                {
                    DuplicatePointsAddedCount.Add(p, 0); //Set the counter to 0 for when we use it later
                }
                else
                {
                    KnownPoints.Add(p);
                }
            }

            foreach (PointIndex pi in new PolySetVertexEnum(Polygons))
            {
                GridVector2 point = pi.Point(Polygons);
                GridVector2 offset_point = point - new GridVector2(0, (pointradius * 2));
                LabelView label = new LabelView(pi.ToString(), offset_point);
                listPointLabels.Add(label);
                label.FontSize = pointradius * 2.0;

                if (DuplicatePointsAddedCount.ContainsKey(point))
                {
                    //label.Position = label.Position + label.
                    //label.Position = label.Position + new GridVector2(0, pointradius * (DuplicatePointsAddedCount[point]-1));

                    
                    string prepended_newlines = "";
                    for (int iLine = 0; iLine < DuplicatePointsAddedCount[point]; iLine++)
                        prepended_newlines += "|\n\r";

                    label.Text = prepended_newlines + label.Text; //Prepend a line
                    
                    DuplicatePointsAddedCount[point] = DuplicatePointsAddedCount[point] + 1;
                }
            }

            return listPointLabels;
        }


        public PolygonSetView(IEnumerable<GridPolygon> polys, double PointRadius=1.0)
        {
            this._PointRadius = PointRadius;

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
                psv.PointRadius = this.PointRadius;
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
                    psv.Draw(window.GraphicsDevice, scene, OverlayStyle.Alpha);
                }
            }

            window.GraphicsDevice.Clear(ClearOptions.DepthBuffer | ClearOptions.Stencil, Color.Black, float.MaxValue, 0);

            if (((this.PointLabelType & (IndexLabelType.POLYGON)) > 0) && this.PolyIndexLabels != null)
            {
                LabelView.Draw(window.spriteBatch, window.fontArial, scene, this.PolyIndexLabels);
            }
        }
    }
}
