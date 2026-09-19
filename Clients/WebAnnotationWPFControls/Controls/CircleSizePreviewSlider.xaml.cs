using System;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;

namespace WebAnnotation.WPF.Controls
{
    /// <summary>
    /// Slider plus a preview of the selected cutoff. Pixel modes draw only the circle
    /// (on-screen size). NanometerRadius stores Value in nanometers and sizes the
    /// preview with <see cref="NanometersPerPixel"/>. PercentOfScreen draws a circle
    /// whose area is Value% of the live Viking view, at that same pixel size on this page.
    /// Pixel and nanometer modes cap the slider at the control width so the circle cannot exceed the page.
    /// </summary>
    public partial class CircleSizePreviewSlider : UserControl
    {
        public static readonly DependencyProperty HeaderProperty =
            DependencyProperty.Register(nameof(Header), typeof(string), typeof(CircleSizePreviewSlider), new PropertyMetadata(string.Empty));

        public static readonly DependencyProperty DescriptionProperty =
            DependencyProperty.Register(nameof(Description), typeof(string), typeof(CircleSizePreviewSlider), new PropertyMetadata(string.Empty));

        public static readonly DependencyProperty ValueProperty =
            DependencyProperty.Register(
                nameof(Value),
                typeof(double),
                typeof(CircleSizePreviewSlider),
                new FrameworkPropertyMetadata(0.0, FrameworkPropertyMetadataOptions.BindsTwoWayByDefault, OnPreviewInputsChanged));

        public static readonly DependencyProperty MinimumProperty =
            DependencyProperty.Register(nameof(Minimum), typeof(double), typeof(CircleSizePreviewSlider), new PropertyMetadata(0.0));

        public static readonly DependencyProperty MaximumProperty =
            DependencyProperty.Register(nameof(Maximum), typeof(double), typeof(CircleSizePreviewSlider), new PropertyMetadata(10.0, OnPreviewInputsChanged));

        public static readonly DependencyProperty TickFrequencyProperty =
            DependencyProperty.Register(nameof(TickFrequency), typeof(double), typeof(CircleSizePreviewSlider), new PropertyMetadata(0.1));

        public static readonly DependencyProperty PreviewModeProperty =
            DependencyProperty.Register(nameof(PreviewMode), typeof(CircleSizePreviewMode), typeof(CircleSizePreviewSlider), new PropertyMetadata(CircleSizePreviewMode.PixelDiameter, OnPreviewModeChanged));

        public static readonly DependencyProperty ViewportPixelWidthProperty =
            DependencyProperty.Register(nameof(ViewportPixelWidth), typeof(double), typeof(CircleSizePreviewSlider), new PropertyMetadata(0.0, OnPreviewInputsChanged));

        public static readonly DependencyProperty ViewportPixelHeightProperty =
            DependencyProperty.Register(nameof(ViewportPixelHeight), typeof(double), typeof(CircleSizePreviewSlider), new PropertyMetadata(0.0, OnPreviewInputsChanged));

        public static readonly DependencyProperty NanometersPerPixelProperty =
            DependencyProperty.Register(nameof(NanometersPerPixel), typeof(double), typeof(CircleSizePreviewSlider), new PropertyMetadata(0.0, OnPreviewInputsChanged));

        public static readonly DependencyProperty ValueTextProperty =
            DependencyProperty.Register(nameof(ValueText), typeof(string), typeof(CircleSizePreviewSlider), new PropertyMetadata(string.Empty));

        private bool updatingFromLayout;
        private bool updatingFromValue;

        public CircleSizePreviewSlider()
        {
            InitializeComponent();
        }

        public string Header
        {
            get => (string)GetValue(HeaderProperty);
            set => SetValue(HeaderProperty, value);
        }

        public string Description
        {
            get => (string)GetValue(DescriptionProperty);
            set => SetValue(DescriptionProperty, value);
        }

        public double Value
        {
            get => (double)GetValue(ValueProperty);
            set => SetValue(ValueProperty, value);
        }

        public double Minimum
        {
            get => (double)GetValue(MinimumProperty);
            set => SetValue(MinimumProperty, value);
        }

        public double Maximum
        {
            get => (double)GetValue(MaximumProperty);
            set => SetValue(MaximumProperty, value);
        }

        public double TickFrequency
        {
            get => (double)GetValue(TickFrequencyProperty);
            set => SetValue(TickFrequencyProperty, value);
        }

        public CircleSizePreviewMode PreviewMode
        {
            get => (CircleSizePreviewMode)GetValue(PreviewModeProperty);
            set => SetValue(PreviewModeProperty, value);
        }

        /// <summary>Viking view width in device pixels. 0 means the live view size is unknown.</summary>
        public double ViewportPixelWidth
        {
            get => (double)GetValue(ViewportPixelWidthProperty);
            set => SetValue(ViewportPixelWidthProperty, value);
        }

        /// <summary>Viking view height in device pixels. 0 means the live view size is unknown.</summary>
        public double ViewportPixelHeight
        {
            get => (double)GetValue(ViewportPixelHeightProperty);
            set => SetValue(ViewportPixelHeightProperty, value);
        }

        /// <summary>
        /// Nanometers represented by one Viking screen pixel at the current zoom.
        /// PixelRadius mode stores Value in pixels and labels it with this scale.
        /// </summary>
        public double NanometersPerPixel
        {
            get => (double)GetValue(NanometersPerPixelProperty);
            set => SetValue(NanometersPerPixelProperty, value);
        }

        public string ValueText
        {
            get => (string)GetValue(ValueTextProperty);
            private set => SetValue(ValueTextProperty, value);
        }

        private static void OnPreviewModeChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
        {
            if (d is CircleSizePreviewSlider slider)
            {
                slider.ApplyPageWidthMaximum();
                slider.SyncSliderFromValue();
                slider.UpdatePreview();
            }
        }

        private static void OnPreviewInputsChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
        {
            if (d is CircleSizePreviewSlider slider)
            {
                slider.SyncSliderFromValue();
                slider.UpdatePreview();
            }
        }

        private void OnControlSizeChanged(object sender, SizeChangedEventArgs e)
        {
            ApplyPageWidthMaximum();
            SyncSliderFromValue();
            UpdatePreview();
        }

        private void OnSliderValueChanged(object sender, RoutedPropertyChangedEventArgs<double> e)
        {
            if (updatingFromLayout || updatingFromValue)
                return;

            Value = e.NewValue;
        }

        private void ApplyPageWidthMaximum()
        {
            if (ActualWidth < 1)
                return;

            if (PreviewMode == CircleSizePreviewMode.PercentOfScreen)
                return;

            updatingFromLayout = true;
            try
            {
                if (PreviewMode == CircleSizePreviewMode.NanometerRadius && NanometersPerPixel > 0)
                    Maximum = (ActualWidth / 2.0) * NanometersPerPixel;
                else if (PreviewMode == CircleSizePreviewMode.PixelRadius ||
                         PreviewMode == CircleSizePreviewMode.NanometerRadius)
                    Maximum = ActualWidth / 2.0;
                else
                    Maximum = ActualWidth;
            }
            finally
            {
                updatingFromLayout = false;
            }
        }

        private void SyncSliderFromValue()
        {
            if (SizeSlider is null)
                return;

            updatingFromValue = true;
            try
            {
                double displayed = Math.Max(Minimum, Math.Min(Value, Maximum));
                if (Math.Abs(SizeSlider.Value - displayed) > 0.0001)
                    SizeSlider.Value = displayed;
            }
            finally
            {
                updatingFromValue = false;
            }
        }

        private void UpdatePreview()
        {
            if (PreviewCircle is null || PreviewHost is null)
                return;

            if (PreviewMode == CircleSizePreviewMode.PercentOfScreen)
            {
                if (PreviewScreen is not null)
                    PreviewScreen.Visibility = Visibility.Collapsed;

                double pixelDiameter = ComputeCutoffDiameterPixels();
                double dpi = GetDpiScale();
                double dipDiameter = dpi > 0 ? pixelDiameter / dpi : pixelDiameter;
                if (dipDiameter < 1 && Value > 0)
                    dipDiameter = 1;

                PreviewCircle.Width = dipDiameter;
                PreviewCircle.Height = dipDiameter;
                PreviewHost.Width = double.NaN;
                PreviewHost.Height = Math.Max(8, dipDiameter);
                ValueText = FormatValueText(pixelDiameter);
                return;
            }

            if (PreviewScreen is not null)
                PreviewScreen.Visibility = Visibility.Collapsed;

            double styleDiameter = ComputePreviewDiameter();
            if (ActualWidth > 0)
                styleDiameter = Math.Min(styleDiameter, ActualWidth);

            if (styleDiameter < 1)
                styleDiameter = Value > 0 ? 1 : 0;

            PreviewCircle.Width = styleDiameter;
            PreviewCircle.Height = styleDiameter;
            PreviewHost.Width = double.NaN;
            PreviewHost.Height = Math.Max(8, styleDiameter);
            ValueText = FormatValueText();
        }

        /// <summary>
        /// On-screen diameter of the min-size cutoff using the live Viking view when available.
        /// Same formula as <c>MeetsMinScreenArea</c>: area is Value% of the view.
        /// </summary>
        private double ComputeCutoffDiameterPixels()
        {
            double width = ViewportPixelWidth > 1 ? ViewportPixelWidth : 0;
            double height = ViewportPixelHeight > 1 ? ViewportPixelHeight : 0;
            if (width < 1 || height < 1 || Value <= 0)
                return 0;

            return 2.0 * Math.Sqrt((Value / 100.0) * width * height / Math.PI);
        }

        private double GetDpiScale()
        {
            try
            {
                double scale = VisualTreeHelper.GetDpi(this).DpiScaleX;
                return scale > 0 ? scale : 1.0;
            }
            catch (InvalidOperationException)
            {
                return 1.0;
            }
        }

        private double ComputePreviewDiameter()
        {
            if (PreviewMode == CircleSizePreviewMode.NanometerRadius)
            {
                if (NanometersPerPixel <= 0)
                    return 0;
                return Math.Max(0, Value / NanometersPerPixel) * 2.0;
            }

            return PreviewMode == CircleSizePreviewMode.PixelRadius
                ? Math.Max(0, Value) * 2.0
                : Math.Max(0, Value);
        }

        private string FormatValueText()
        {
            return FormatValueText(ComputeCutoffDiameterPixels());
        }

        private string FormatValueText(double pixelDiameter)
        {
            if (PreviewMode == CircleSizePreviewMode.PercentOfScreen)
            {
                if (pixelDiameter > 0)
                    return $"{Value:F1}% · {pixelDiameter:F0} px";
                return $"{Value:F1}% of view area";
            }

            if (PreviewMode == CircleSizePreviewMode.NanometerRadius)
                return $"{Value:F0} nm";

            if (PreviewMode == CircleSizePreviewMode.PixelRadius && NanometersPerPixel > 0)
                return $"{Value * NanometersPerPixel:F0} nm";

            return Value.ToString("F1");
        }
    }
}
