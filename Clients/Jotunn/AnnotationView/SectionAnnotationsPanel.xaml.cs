using AnnotationViewModel;
using Jotunn.Common;
using System;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using Viking.VolumeViewModel;

namespace Jotunn.AnnotationView
{
    /// <summary>
    /// Interaction logic for SectionAnnotationsPanel.xaml
    /// </summary>
    public partial class SectionAnnotationsPanel : UserControl
    {  
        public static readonly DependencyProperty SectionNumberProperty;

        public long SectionNumber
        {
            get { return (long)GetValue(SectionAnnotationsPanel.SectionNumberProperty); }
            set { SetCurrentValue(SectionAnnotationsPanel.SectionNumberProperty, value); }
        }

        public static readonly DependencyProperty VisibleRegionProperty;

        public VisibleRegionInfo VisibleRegion
        {
            get { return (VisibleRegionInfo)GetValue(SectionAnnotationsPanel.VisibleRegionProperty); }
            set { SetCurrentValue(SectionAnnotationsPanel.VisibleRegionProperty, value); }
        }

        private static void OnSectionNumberChanged(DependencyObject o, DependencyPropertyChangedEventArgs e)
        {
            SectionAnnotationsPanel sap = o as SectionAnnotationsPanel;

            AnnotationViewModel.VisibleRectLocations vlr = new AnnotationViewModel.VisibleRectLocations(sap.TileMapping, sap.SectionNumber);
            sap.VisibleLocations = vlr;
        }

        public static readonly DependencyProperty TileMappingProperty;

        public TileMappingViewModel TileMapping
        {
            get { return (TileMappingViewModel)GetValue(SectionAnnotationsPanel.TileMappingProperty); }
            set { SetValue(SectionAnnotationsPanel.TileMappingProperty, value); }
        }

        private static void OnTileMappingChanged(DependencyObject o, DependencyPropertyChangedEventArgs e)
        {
            SectionAnnotationsPanel sap = o as SectionAnnotationsPanel;

            AnnotationViewModel.VisibleRectLocations vlr = new AnnotationViewModel.VisibleRectLocations(sap.TileMapping, sap.SectionNumber);

            sap.VisibleLocations = vlr; 
        }

        private static readonly DependencyProperty VisibleLocationsProperty;
        public  VisibleRectLocations VisibleLocations
        {
            get { return (VisibleRectLocations)GetValue(SectionAnnotationsPanel.VisibleLocationsProperty); }
            set { SetValue(SectionAnnotationsPanel.VisibleLocationsProperty, value); }
        }

        private static void OnVisibleLocationsChanged(DependencyObject o, DependencyPropertyChangedEventArgs e)
        {
            SectionAnnotationsPanel v = o as SectionAnnotationsPanel;

            v.DataContext = e.NewValue;
        }

        protected override Size MeasureOverride(Size constraint)
        {
            if (this.TileMapping == null)
            {
                return constraint;
            }
            else
            {
                return base.MeasureOverride(constraint);
            }
        }
          
        static SectionAnnotationsPanel()
        { 

            SectionAnnotationsPanel.VisibleRegionProperty = DependencyProperty.Register("VisibleRegion",
                                                                        typeof(VisibleRegionInfo),
                                                                        typeof(SectionAnnotationsPanel),
                                                                        new FrameworkPropertyMetadata(new VisibleRegionInfo(0, 0, 10000, 10000, 256),
                                                                            FrameworkPropertyMetadataOptions.AffectsRender |
                                                                            FrameworkPropertyMetadataOptions.BindsTwoWayByDefault ));

            SectionAnnotationsPanel.TileMappingProperty = DependencyProperty.Register("TileMapping",
                                                                      typeof(TileMappingViewModel),
                                                                      typeof(SectionAnnotationsPanel),
                                                                      new FrameworkPropertyMetadata(null,
                                                                          FrameworkPropertyMetadataOptions.AffectsRender,
                                                                          new PropertyChangedCallback(OnTileMappingChanged)));

            SectionAnnotationsPanel.SectionNumberProperty = DependencyProperty.Register("SectionNumber",
                                                                      typeof(long),
                                                                      typeof(SectionAnnotationsPanel));

            SectionAnnotationsPanel.VisibleLocationsProperty = DependencyProperty.Register("VisibleLocations",
                                                                        typeof(VisibleRectLocations),
                                                                        typeof(SectionAnnotationsPanel),
                                                                        new FrameworkPropertyMetadata(null,
                                                                            FrameworkPropertyMetadataOptions.AffectsRender |
                                                                            FrameworkPropertyMetadataOptions.BindsTwoWayByDefault,
                                                                            new PropertyChangedCallback(OnVisibleLocationsChanged)));
        }
        /*
        public SectionAnnotationsPanel()
        {
            Viking.VolumeViewModel.VolumeViewModel volume = Microsoft.Practices.ServiceLocation.ServiceLocator.Current.GetInstance<Viking.VolumeViewModel.VolumeViewModel>();
            this.DataContext = volume;
        }*/

        public SectionAnnotationsPanel()
        {
            this.InitializeComponent();
        }

        protected override void OnMouseMove(MouseEventArgs e)
        {
            e.Handled = false;
        }

        protected override void OnMouseDown(MouseButtonEventArgs e)
        {
            e.Handled = false; 
        }
        
        protected override void OnInitialized(EventArgs e)
        {

            base.OnInitialized(e);

           // AnnotationViewModel.VisibleRectLocations vlr = new AnnotationViewModel.VisibleRectLocations(this.TileMapping, this.SectionNumber);
            //this.VisibleLocations = vlr; 
        }
    }
}
