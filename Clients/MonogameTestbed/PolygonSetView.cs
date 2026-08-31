using Geometry;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using System;
using System.Collections.Generic;
using System.Linq;
using VikingXNA;
using VikingXNAGraphics;
using Vector2 = Microsoft.Xna.Framework.Vector2;
using Vector3 = Microsoft.Xna.Framework.Vector3;

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

    /// <summary>
    /// Displays a set of polygons with indicies labeled.  If there are null values in the polygon array they are skipped, but the index number of the shape is still advanced.s
    /// </summary>
    class PolygonSetView
    {
        private PointSetView[] PolyPointsView = null;
        private LineView[] PolyRingViews = null;
        private LabelView[] PolyIndexLabels = [];

        private readonly List<Polygon> _Polygons;

        public Color[] PolyLineColors;
        public Color[] PolyVertexColors;

        private double _PointRadius;
        public double PointRadius
        {
            get => _PointRadius;
            set
            {

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
            get => _PointLabelTypes;
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
            get => (_PointLabelTypes & IndexLabelType.MESH) > 0;
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
            get => (_PointLabelTypes & IndexLabelType.POSITION) > 0;
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
            get => (_PointLabelTypes & IndexLabelType.POLYGON) > 0;
            private set
            {
                if (true == value)
                {
                    PolyIndexLabels = [.. CreatePolyIndexLabels(_Polygons, this.PointRadius)];
                }
            }
        }

        /// <summary>
        /// Applies vertex, ring, and label sizes in world units.  All three are world measurements, so their
        /// apparent size depends entirely on the camera zoom: the constructor defaults fall well below one pixel
        /// once the camera is fitted to a whole slice, and the vertices, rings, and labels all disappear.
        /// </summary>
        public void SetDrawScale(double pointRadius, double lineWidth, double labelFontSize)
        {
            PointRadius = pointRadius;

            foreach (LineView line in PolyRingViews ?? [])
                line.LineWidth = (float)lineWidth;

            foreach (LabelView label in PolyIndexLabels)
                label.FontSize = labelFontSize;

            //The per-polygon point sets rebuild their labels from PointRadius, so size those after the assignment
            //above rather than letting the marker radius decide how large the index text is.
            foreach (PointSetView psv in PolyPointsView ?? [])
                foreach (LabelView label in psv.LabelViews ?? [])
                    label.FontSize = labelFontSize;
        }

        private static List<LabelView> CreatePolyIndexLabels(List<Polygon> Polygons, double pointradius)
        {
            List<LabelView> listPointLabels = [];

            //Figure out if we have duplicate points and offset labels as needed
            PolySetVertexEnum pointEnum = new(Polygons);
            Geometry.Vector2[] point_array = [.. pointEnum.Select(i => i.Point(Polygons))];

            QuadTree<int> DuplicatePointsAddedCount = new(); //Track the number of times we've hit a specific duplicate point and move the label accordingly
            HashSet<Geometry.Vector2> KnownPoints = [];
            foreach (Geometry.Vector2 p in point_array)
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

            foreach (PolygonIndex pi in new PolySetVertexEnum(Polygons))
            {
                Geometry.Vector2 point = pi.Point(Polygons);
                Geometry.Vector2 offset_point = point - new Geometry.Vector2(0, (pointradius * 2));
                LabelView label = new(pi.ToString(), offset_point);
                listPointLabels.Add(label);
                label.FontSize = pointradius * 2.0;

                if (DuplicatePointsAddedCount.Contains(point))
                {
                    //label.Position = label.Position + label.
                    //label.Position = label.Position + new Geometry.Vector2(0, pointradius * (DuplicatePointsAddedCount[point]-1));

                    string prepended_newlines = "";
                    for (int iLine = 0; iLine < DuplicatePointsAddedCount[point]; iLine++)
                        prepended_newlines += "|\n\r";

                    label.Text = prepended_newlines + label.Text; //Prepend a line

                    DuplicatePointsAddedCount[point] = DuplicatePointsAddedCount[point] + 1;
                }
            }

            return listPointLabels;
        }


        public static readonly Color[] DefaultColorMapping =
        [
            Color.Green,
            Color.Yellow,
            Color.Red,
            Color.Blue
        ];

        /// <summary>
        /// 
        /// </summary>
        /// <param name="polys"></param>
        /// <param name="colors">Colors can be null and does not need to match the length of the polys array.  If an entry does not exist a random color is selected.</param>
        /// <param name="PointRadius"></param>
        public PolygonSetView(IEnumerable<Polygon> polys, IReadOnlyList<Color> colors = null, double PointRadius = 1.0)
        {
            this._PointRadius = PointRadius;

            _Polygons = [.. polys];
            PolyLineColors = [.. polys.Select((_,i) => colors != null && colors.Count > i ?
                colors[i] : Color.Black.Random())];
            PolyVertexColors = [.. PolyLineColors.Select(c => c.SetAlpha(0.5f))];

            UpdatePolyViews();
        }

        private void UpdatePolyViews()
        {
            List<PointSetView> listPointSetView = [];

            List<LineView> polyRingViews = [];

            for (int iPoly = 0; iPoly < _Polygons.Count; iPoly++)
            {
                Polygon p = _Polygons[iPoly];
                if (p is null)
                    continue;

                PointSetView psv = new();

                List<Geometry.Vector2> points = [.. p.ExteriorRing];
                foreach (Polygon innerPoly in p.InteriorPolygons)
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

            PolyPointsView = [.. listPointSetView];
            PolyRingViews = [.. polyRingViews];
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

            window.GraphicsDevice.Clear(ClearOptions.DepthBuffer | ClearOptions.Stencil, Color.Black, 1.0f, 0);

            if (((this.PointLabelType & (IndexLabelType.POLYGON)) > 0) && this.PolyIndexLabels != null)
            {
                LabelView.Draw(window.spriteBatch, window.fontArial, scene, this.PolyIndexLabels);
            }
        }
    }
}
