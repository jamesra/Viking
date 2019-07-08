using Geometry;
using Microsoft.Xna.Framework;
using System.Collections.Specialized;
using System.Linq;
using VikingXNA;

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

        PointSet _Points = new PointSet();
        private PointSetView PointsView = new PointSetView();
        private LineSetView VoronoiView = new LineSetView();
        ConvexHullView CVView = new ConvexHullView();

        public PointSetViewCollection(Color PointColor, Color VoronoiColor, Color CVViewColor)
        {
            PointsView.Color = PointColor;
            VoronoiView.color = VoronoiColor;
            CVView.color = CVViewColor;
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

            PointsView.Draw(window, scene);

            //       if (CVView.LineViews != null)
            //           LineView.Draw(window.GraphicsDevice, scene, window.lineManager, CVView.LineViews.ToArray());
        }
    }

}
