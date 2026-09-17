using System;
using System.Windows;
using System.Windows.Controls;

namespace WebAnnotation.WPF.Controls
{
    /// <summary>
    /// Slider plus a circle preview whose on-page size matches the selected cutoff.
    /// Pixel modes cap the slider at the control width so the circle cannot exceed the property page.
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
                Maximum = PreviewMode == CircleSizePreviewMode.PixelRadius
                    ? ActualWidth / 2.0
                    : ActualWidth;
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

            double diameter = ComputePreviewDiameter();
            if (ActualWidth > 0)
                diameter = Math.Min(diameter, ActualWidth);

            if (diameter < 1)
                diameter = Value > 0 ? 1 : 0;

            PreviewCircle.Width = diameter;
            PreviewCircle.Height = diameter;
            PreviewHost.Height = Math.Max(8, diameter);
            ValueText = FormatValueText();
        }

        private double ComputePreviewDiameter()
        {
            switch (PreviewMode)
            {
                case CircleSizePreviewMode.PercentOfScreen:
                    if (ActualWidth < 1 || Value <= 0)
                        return 0;
                    double width = ActualWidth;
                    double height = width * 9.0 / 16.0;
                    double area = (Value / 100.0) * width * height;
                    return 2.0 * Math.Sqrt(area / Math.PI);
                case CircleSizePreviewMode.PixelRadius:
                    return Math.Max(0, Value) * 2.0;
                default:
                    return Math.Max(0, Value);
            }
        }

        private string FormatValueText()
        {
            if (PreviewMode == CircleSizePreviewMode.PercentOfScreen)
                return $"{Value:F1}% of screen";
            return Value.ToString("F1");
        }
    }
}
