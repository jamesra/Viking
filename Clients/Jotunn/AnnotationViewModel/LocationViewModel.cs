using System.Windows;
using WebAnnotationModel;
using WebAnnotationModel.Objects;

namespace AnnotationViewModel
{
    public class LocationViewModel : DependencyObject
    {
        private readonly LocationObj obj; 
        
        public static readonly DependencyProperty ScreenPositionProperty;

        public Point ScreenPosition
        {
            get { return (Point)GetValue(LocationViewModel.SectionPositionProperty); }
            set { SetCurrentValue(LocationViewModel.SectionPositionProperty, value); }
        }

        public static readonly DependencyProperty VolumePositionProperty;

        public Point VolumePosition
        {
            get { return (Point)GetValue(LocationViewModel.VolumePositionProperty); }
            set { SetCurrentValue(LocationViewModel.VolumePositionProperty, value); }
        }
        
        public static readonly DependencyProperty SectionPositionProperty;

        public Point SectionPosition
        {
            get { return (Point)GetValue(LocationViewModel.SectionPositionProperty); }
            set { SetCurrentValue(LocationViewModel.SectionPositionProperty, value); }
        }

        private static void OnSectionPositionChanged(DependencyObject o, DependencyPropertyChangedEventArgs e)
        {
           /* LocationViewModel lvm = o as LocationViewModel;
            if (o == null)
                return;

            Point p = (Point)e.NewValue;
            lvm.obj.Position = new Geometry.GridVector2(p.X, p.Y);*/
        }

        private static void OnVolumePositionChanged(DependencyObject o, DependencyPropertyChangedEventArgs e)
        {
            /* LocationViewModel lvm = o as LocationViewModel;
             if (o == null)
                 return;

             Point p = (Point)e.NewValue;
             lvm.obj.Position = new Geometry.GridVector2(p.X, p.Y);*/
        }

        private static void OnScreenPositionChanged(DependencyObject o, DependencyPropertyChangedEventArgs e)
        {
            /* LocationViewModel lvm = o as LocationViewModel;
             if (o == null)
                 return;

             Point p = (Point)e.NewValue;
             lvm.obj.Position = new Geometry.GridVector2(p.X, p.Y);*/
        }

        public static readonly DependencyProperty RadiusProperty;

        public double Radius
        {
            get { return (double)GetValue(LocationViewModel.RadiusProperty); }
            set { SetCurrentValue(LocationViewModel.RadiusProperty, value); }
        }

        private static void OnRadiusChanged(DependencyObject o, DependencyPropertyChangedEventArgs e)
        {
            LocationViewModel lvm = o as LocationViewModel;
            if (o == null)
                return;
             
        }

        static LocationViewModel()
        {
            LocationViewModel.RadiusProperty = DependencyProperty.Register("Radius",
                                                                       typeof(double),
                                                                       typeof(LocationViewModel),
                                                                       new FrameworkPropertyMetadata(double.NaN,
                                                                           FrameworkPropertyMetadataOptions.AffectsRender,
                                                                           new PropertyChangedCallback(OnRadiusChanged)));

            LocationViewModel.SectionPositionProperty = DependencyProperty.Register("SectionPosition",
                                                                        typeof(Point),
                                                                        typeof(LocationViewModel),
                                                                        new FrameworkPropertyMetadata(new Point(),
                                                                            FrameworkPropertyMetadataOptions.AffectsRender,
                                                                            new PropertyChangedCallback(OnSectionPositionChanged)));

            LocationViewModel.VolumePositionProperty = DependencyProperty.Register("VolumePosition",
                                                                        typeof(Point),
                                                                        typeof(LocationViewModel),
                                                                        new FrameworkPropertyMetadata(new Point(),
                                                                            FrameworkPropertyMetadataOptions.AffectsRender,
                                                                            new PropertyChangedCallback(OnVolumePositionChanged)));

            LocationViewModel.SectionPositionProperty = DependencyProperty.Register("ScreenPosition",
                                                                        typeof(Point),
                                                                        typeof(LocationViewModel),
                                                                        new FrameworkPropertyMetadata(new Point(),
                                                                            FrameworkPropertyMetadataOptions.AffectsRender,
                                                                            new PropertyChangedCallback(OnScreenPositionChanged)));
        }

        public LocationViewModel(LocationObj loc)
        {
            obj = loc; 
            this.SectionPosition = new Point(loc.Position.X, loc.Position.Y); 
            this.Radius = loc.Radius;
        }

    }
}
