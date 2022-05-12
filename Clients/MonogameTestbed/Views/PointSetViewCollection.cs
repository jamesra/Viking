using Geometry;
using Microsoft.Xna.Framework;
using System.Collections.Specialized;
using System.Linq;
using VikingXNA;
using VikingXNAGraphics;

namespace MonogameTestbed
{
    public class PointSetViewCollection
    {
        public PointSet Points
        {
            get
            {
                return _Points;
            }
            set
            {
                if (_Points != null)
                {
                    _Points.CollectionChanged -= this.OnPointCollectionChanged;
                }

                _Points = value;

                if (_Points != null)
                {
                    _Points.CollectionChanged += this.OnPointCollectionChanged;
                }

                PointsView.Points = _Points;
                UpdateViews();
            }
        }

        PointSet _Points = null;
        private readonly PointSetView PointsView = new PointSetView();
        private readonly LineSetView VoronoiView = new LineSetView();
        readonly ConvexHullView CVView = new ConvexHullView();

        public PointSetViewCollection(Color PointColor, Color VoronoiColor, Color CVViewColor) : this (new PointSet(), PointColor, VoronoiColor, CVViewColor)
        {
        }

        public PointSetViewCollection(PointSet points, Color PointColor, Color VoronoiColor, Color CVViewColor)
        {
            Points = points;
            PointsView.Color = PointColor;
            VoronoiView.color = VoronoiColor;
            CVView.color = CVViewColor;
        }

        public void TogglePoint(GridVector2 p)
        {
            Points.Toggle(p);
        }

        private void UpdateViews()
        {
            PointsView.UpdateViews();
            VoronoiView.UpdateViews(Points.Points);
            CVView.UpdateViews(Points.Points.ToArray());
        }

        public void OnPointCollectionChanged(object obj, NotifyCollectionChangedEventArgs e)
        {
            UpdateViews();
        }

        public void Draw(MonoTestbed window, Scene scene)
        {

            //          if(VoronoiView.LineViews != null)
            //              LineView.Draw(window.GraphicsDevice, scene, window.lineManager, VoronoiView.LineViews.ToArray());

            PointsView.Draw(window.GraphicsDevice, scene, OverlayStyle.Alpha);

            //       if (CVView.LineViews != null)
            //           LineView.Draw(window.GraphicsDevice, scene, window.lineManager, CVView.LineViews.ToArray());
        }
    }

}
