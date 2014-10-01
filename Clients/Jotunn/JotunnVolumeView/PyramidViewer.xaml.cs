using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Documents;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Media3D; 
using System.Windows.Media.Imaging;
using System.Windows.Navigation;
using System.Windows.Shapes;
using Viking.VolumeViewModel;
using Jotunn.Controls;
using Jotunn.Common;

namespace Viking.VolumeView
{
    /// <summary>
    /// Interaction logic for PyramidViewer.xaml
    /// </summary>
    [System.Windows.Markup.ContentProperty("TileMapping")]
    public partial class PyramidViewer : UserControl
    {

        #region Dependancy Property

        public static readonly DependencyProperty TileMappingProperty;

        public TileMappingViewModel TileMapping
        {
            get { return (TileMappingViewModel)GetValue(PyramidViewer.TileMappingProperty); }
            set { SetValue(PyramidViewer.TileMappingProperty, value); }
        }

        private static void OnTileMappingChanged(DependencyObject o, DependencyPropertyChangedEventArgs e) 
        {
            PyramidViewer v = o as PyramidViewer;
            
            
            //v.ChangedTileMapping(e.NewValue as TileMappingViewModel, e.OldValue as TileMappingViewModel);
        }

        public static readonly DependencyProperty VisibleRegionProperty;

        public VisibleRegionInfo VisibleRegion
        {
            get { return (VisibleRegionInfo)GetValue(PyramidViewer.VisibleRegionProperty); }
            set { SetCurrentValue(PyramidViewer.VisibleRegionProperty, value); }
        }

        private static void OnVisibleRegionChanged(DependencyObject o, DependencyPropertyChangedEventArgs e)
        {
            PyramidViewer v = o as PyramidViewer;
            VisibleRegionInfo r = e.NewValue as VisibleRegionInfo;

            v.Camera.Position = new Point3D(r.Center.X, r.Center.Y, v.Camera.Position.Z);
            if (v.LastValidWidth > 0)
            {
                v.Camera.Width = r.Downsample * v.LastValidWidth;
            }

            if (v.TileMapping != null)
            {
                v.TileMapping.VisibleRegion = r;
            }
        }
        
        static PyramidViewer()
        {
            PyramidViewer.TileMappingProperty = DependencyProperty.Register("TileMapping",
                                                                        typeof(TileMappingViewModel),
                                                                        typeof(PyramidViewer),
                                                                        new FrameworkPropertyMetadata(null,
                                                                            FrameworkPropertyMetadataOptions.AffectsRender,
                                                                            new PropertyChangedCallback(OnTileMappingChanged)));

            PyramidViewer.VisibleRegionProperty = DependencyProperty.Register("VisibleRegion",
                                                                        typeof(VisibleRegionInfo),
                                                                        typeof(PyramidViewer),
                                                                        new FrameworkPropertyMetadata(new VisibleRegionInfo(0, 0, 10000, 10000, 256),
                                                                            FrameworkPropertyMetadataOptions.AffectsRender |
                                                                            FrameworkPropertyMetadataOptions.BindsTwoWayByDefault |
                                                                            FrameworkPropertyMetadataOptions.Inherits,
                                                                            new PropertyChangedCallback(OnVisibleRegionChanged)));
        }

        private double LastValidWidth;
        private double LastValidHeight; 

        #endregion

        public PyramidViewer()
        {
            InitializeComponent();
        }
           
        /*
        public Rect ProjectionBounds
        {
            get
            {
                double aspect = LastValidHeight / LastValidWidth;

                double width = Camera.Width;
                double height = Camera.Width * aspect;

                Rect R = new Rect(CameraPosition.X - (width / 2), CameraPosition.Y - (height / 2), width, height); 
                
                return R;
            }
        }
         */
          

        private void Camera_Changed(object sender, EventArgs e)
        {
            if (TileMapping != null)
            {
                ;
                ; 
            }
        }

        /// <summary>
        /// ActualWidth and ActualHeight are only valid after layout completes
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="e"></param>
        private void Viewport_LayoutUpdated(object sender, EventArgs e)
        {
            LastValidHeight = Viewport.ActualHeight;
            LastValidWidth = Viewport.ActualWidth;

            Camera.Width = LastValidWidth * VisibleRegion.Downsample; 
        }

        MouseEventArgs OldMouseEventArgs;
        Point OldScreenPosition;
        private void UserControl_MouseMove(object sender, MouseEventArgs e)
        {
            if (TileMapping == null)
                return; 
           

            if (e.LeftButton == MouseButtonState.Pressed)
            {
                if (OldMouseEventArgs == null)
                {
                    OldMouseEventArgs = e;
                    OldScreenPosition = e.GetPosition(Viewport);
                    return;
                }

                Point ScreenPosition = e.GetPosition(Viewport);

                //Rect newVisibleRect = VisibleRegion.VisibleRect; 
                Point newPosition = VisibleRegion.Center; //new Point3D(CameraPosition.X, CameraPosition.Y, CameraPosition.Z); 
                newPosition.X += (OldScreenPosition.X - ScreenPosition.X) * VisibleRegion.Downsample;
                newPosition.Y -= (OldScreenPosition.Y - ScreenPosition.Y) * VisibleRegion.Downsample;
                //TileMapping.VisibleRect = newVisibleRect; 
                //CameraPosition = newPosition;

                OldScreenPosition = ScreenPosition;

                OldMouseEventArgs = e;
            }
        }

        private void PanelInputOverlay_MouseUp(object sender, MouseButtonEventArgs e)
        {
            OldMouseEventArgs = null;
            OldScreenPosition = new Point(); 
        }
         
        protected override void OnMouseWheel(MouseWheelEventArgs e)
        {
            float multiplier = ((float)e.Delta / 120.0f);

            StepCameraDistance(multiplier);

            base.OnMouseWheel(e);
        }

        protected void StepCameraDistance(float multiplier)
        {
            double ds = VisibleRegion.Downsample;
            if (multiplier > 0)
                ds *= 0.86956521739130434782608695652174;
            else
                ds *= 1.15;

            if (ds < 0.25)
                ds = 0.25;

            //double Aspect = LastValidCellHeight / LastValidCellWidth;
            //double visWidth = LastValidCellWidth * ds;
            //double visHeight = LastValidCellWidth * Aspect * ds;
            double Aspect = this.ActualHeight / this.ActualWidth;
            double visWidth = this.ActualWidth * ds;
            double visHeight = visWidth * Aspect;

            Rect visRect = new Rect(VisibleRegion.Center.X - (visWidth / 2),
                                    VisibleRegion.Center.Y - (visHeight / 2),
                                    visWidth,
                                    visHeight);

            VisibleRegion = new VisibleRegionInfo(visRect, ds);
        }
         
        protected override void OnMouseMove(MouseEventArgs e)
        {
            if (e.LeftButton == MouseButtonState.Pressed)
            {
                if (OldMouseEventArgs == null)
                {
                    OldMouseEventArgs = e;
                    OldScreenPosition = e.GetPosition(this);
                    return;
                }

                Point ScreenPosition = e.GetPosition(this);

                Point newPosition = VisibleRegion.Center;
                newPosition.X += (OldScreenPosition.X - ScreenPosition.X) * VisibleRegion.Downsample;
                newPosition.Y -= (OldScreenPosition.Y - ScreenPosition.Y) * VisibleRegion.Downsample;
                Size VisibleArea = new Size(this.ActualWidth * VisibleRegion.Downsample, this.ActualHeight * VisibleRegion.Downsample);
                VisibleRegion = new VisibleRegionInfo(newPosition, VisibleArea, VisibleRegion.Downsample);

                OldScreenPosition = ScreenPosition;

                OldMouseEventArgs = e;
            }

            base.OnMouseMove(e);
        }

        protected override void OnMouseUp(MouseButtonEventArgs e)
        {
            OldMouseEventArgs = null;
            OldScreenPosition = new Point();

            base.OnMouseUp(e);
        }

    }
}
