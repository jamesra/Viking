#nullable enable

using System;
using System.Collections.Generic;
using System.Globalization;
using System.Windows;
using System.Windows.Interop;
using System.Windows.Media;
using Viking.VolumeModel;

namespace Viking.UI.Forms
{
    /// <summary>
    /// Edits the contrast window of one crushed section and channel. The black and white points are codes (0 to 255 for 8-bit
    /// tiles); every change is saved on this computer and redrawn at once.
    /// </summary>
    public partial class CrushContrastWindow : Window
    {
        private readonly CrushInfo _crush;
        private readonly string? _volume;
        private readonly int _section;
        private readonly string _channel;
        private readonly Action _redraw;
        // True until the constructor has set the sliders, so the changes it makes are not saved.
        private bool _loading = true;

        internal CrushContrastWindow(CrushInfo crush, string? volume, int section, string channel, Action redraw, IntPtr ownerHandle)
        {
            InitializeComponent();

            _crush = crush;
            _volume = volume;
            _section = section;
            _channel = channel;
            _redraw = redraw;

            if (ownerHandle != IntPtr.Zero)
                new WindowInteropHelper(this).Owner = ownerHandle;

            int maxCode = (1 << Math.Min(crush.Bits, 8)) - 1;
            BlackSlider.Maximum = WhiteSlider.Maximum = maxCode;
            BlackSlider.TickFrequency = WhiteSlider.TickFrequency = Math.Max(1, (maxCode + 1) / 16);
            Histogram.Counts = crush.CodeHistogram;

            Title = $"Contrast - Section {section}";
            Heading.Text = $"Section {section}, channel {channel}";

            CrushContrastSetting now = EffectiveSetting();
            Load(now.Min, now.Max, now.Gamma);
        }

        /// <summary>What is drawn now: the user's setting, else the volume's display range at gamma 1.</summary>
        private CrushContrastSetting EffectiveSetting()
        {
            if (CrushContrastStore.Default.Get(_volume, _section, _channel) is { } saved)
                return saved;

            (float min, float max) = _crush.DefaultWindow();
            return new CrushContrastSetting(min * CrushInfo.StorageMaximum, max * CrushInfo.StorageMaximum, 1.0);
        }

        /// <summary>The gamma slider is on a log2 scale, so equal steps lift and darken equally and 1 sits in the middle.</summary>
        private double Gamma => Math.Pow(2, GammaSlider.Value);

        private void Load(double min, double max, double gamma)
        {
            _loading = true;
            try
            {
                BlackSlider.Value = Math.Min(Math.Max(Math.Round(min), 0), BlackSlider.Maximum);
                WhiteSlider.Value = Math.Min(Math.Max(Math.Round(max), 0), WhiteSlider.Maximum);
                GammaSlider.Value = Math.Log(CrushContrastSetting.ClampGamma(gamma), 2);
                ShowValues();
            }
            finally
            {
                _loading = false;
            }
        }

        private void ShowValues()
        {
            BlackText.Text = BlackSlider.Value.ToString("0", CultureInfo.CurrentCulture);
            WhiteText.Text = WhiteSlider.Value.ToString("0", CultureInfo.CurrentCulture);
            GammaText.Text = Gamma.ToString("0.00", CultureInfo.CurrentCulture);
            Histogram.SetWindow(BlackSlider.Value, WhiteSlider.Value, Gamma);
        }

        private void OnGammaChanged(object sender, RoutedPropertyChangedEventArgs<double> e)
        {
            if (_loading)
                return;

            ShowValues();
            Save();
        }

        private void OnSliderChanged(object sender, RoutedPropertyChangedEventArgs<double> e)
        {
            if (_loading)
                return;

            // The window keeps a width: the slider that was not moved gives way.
            _loading = true;
            try
            {
                if (WhiteSlider.Value <= BlackSlider.Value)
                {
                    if (ReferenceEquals(sender, BlackSlider))
                        WhiteSlider.Value = Math.Min(BlackSlider.Value + 1, WhiteSlider.Maximum);
                    else
                        BlackSlider.Value = Math.Max(WhiteSlider.Value - 1, 0);
                }
            }
            finally
            {
                _loading = false;
            }

            ShowValues();
            Save();
        }

        private void Save()
        {
            if (WhiteSlider.Value > BlackSlider.Value)
            {
                CrushContrastStore.Default.Set(_volume, _section, _channel, BlackSlider.Value, WhiteSlider.Value, Gamma);
                _redraw();
            }
        }

        private void OnAutoClick(object sender, RoutedEventArgs e)
        {
            (double min, double max) = _crush.AutoRange();
            Load(min, max, _crush.AutoGamma(Math.Round(min), Math.Round(max)));
            Save();
        }

        private void OnResetClick(object sender, RoutedEventArgs e)
        {
            CrushContrastStore.Default.Clear(_volume, _section, _channel);
            CrushContrastSetting now = EffectiveSetting();
            Load(now.Min, now.Max, now.Gamma);
            _redraw();
        }

        private void OnCloseClick(object sender, RoutedEventArgs e) => Close();
    }

    /// <summary>Counts per code on a log scale, with the codes outside the black and white points dimmed.</summary>
    internal sealed class HistogramView : FrameworkElement
    {
        private static readonly Brush Background = Frozen(new SolidColorBrush(Color.FromRgb(24, 24, 24)));
        private static readonly Brush Bars = Frozen(new SolidColorBrush(Color.FromRgb(170, 170, 170)));
        private static readonly Brush Dim = Frozen(new SolidColorBrush(Color.FromArgb(150, 0, 0, 0)));
        private static readonly Pen BlackPen = FrozenPen(Colors.DeepSkyBlue);
        private static readonly Pen WhitePen = FrozenPen(Colors.Orange);
        private static readonly Pen CurvePen = FrozenPen(Color.FromArgb(220, 255, 255, 255), 1.5);
        private static readonly Pen MidPen = FrozenDashedPen(Colors.LimeGreen);

        private IReadOnlyList<long>? _counts;
        private double _black;
        private double _white = 255;
        private double _gamma = 1;

        public IReadOnlyList<long>? Counts
        {
            get => _counts;
            set
            {
                _counts = value;
                InvalidateVisual();
            }
        }

        public void SetWindow(double black, double white, double gamma)
        {
            _black = black;
            _white = white;
            _gamma = gamma;
            InvalidateVisual();
        }

        protected override void OnRender(DrawingContext dc)
        {
            double width = ActualWidth;
            double height = ActualHeight;
            dc.DrawRectangle(Background, null, new Rect(0, 0, width, height));

            if (_counts is not { Count: > 0 })
            {
                FormattedText text = new("No histogram in the VikingXML", CultureInfo.CurrentUICulture, FlowDirection.LeftToRight,
                    new Typeface("Segoe UI"), 14, Brushes.Gray, VisualTreeHelper.GetDpi(this).PixelsPerDip);
                dc.DrawText(text, new Point((width - text.Width) / 2, (height - text.Height) / 2));
                return;
            }

            // Bin 0 holds the empty section edge, which would flatten every other bar.
            double top = 1;
            for (int i = 1; i < _counts.Count; i++)
                top = Math.Max(top, Math.Log(1 + _counts[i]));

            double binWidth = width / _counts.Count;
            for (int i = 1; i < _counts.Count; i++)
            {
                double h = Math.Log(1 + _counts[i]) / top * (height - 2);
                dc.DrawRectangle(Bars, null, new Rect(i * binWidth, height - h, Math.Max(binWidth, 1), h));
            }

            double blackX = (_black + 0.5) * binWidth;
            double whiteX = (_white + 0.5) * binWidth;
            dc.DrawRectangle(Dim, null, new Rect(0, 0, Math.Max(blackX, 0), height));
            dc.DrawRectangle(Dim, null, new Rect(whiteX, 0, Math.Max(width - whiteX, 0), height));
            dc.DrawLine(BlackPen, new Point(blackX, 0), new Point(blackX, height));
            dc.DrawLine(WhitePen, new Point(whiteX, 0), new Point(whiteX, height));

            // The tone curve: how bright each code between the points is shown, from black at the bottom to white at the top.
            // The green line marks the code that is shown at mid grey, which is where the gamma places the middle of the window.
            if (whiteX > blackX + 2)
            {
                StreamGeometry curve = new();
                using (StreamGeometryContext ctx = curve.Open())
                {
                    const int steps = 48;
                    for (int i = 0; i <= steps; i++)
                    {
                        double t = (double)i / steps;
                        Point p = new(blackX + (t * (whiteX - blackX)), height - (Math.Pow(t, 1.0 / _gamma) * (height - 2)) - 1);
                        if (i == 0)
                            ctx.BeginFigure(p, false, false);
                        else
                            ctx.LineTo(p, true, false);
                    }
                }

                curve.Freeze();
                dc.DrawGeometry(null, CurvePen, curve);

                double midX = blackX + ((whiteX - blackX) * Math.Pow(0.5, _gamma));
                dc.DrawLine(MidPen, new Point(midX, 0), new Point(midX, height));
            }
        }

        private static Brush Frozen(SolidColorBrush brush)
        {
            brush.Freeze();
            return brush;
        }

        private static Pen FrozenPen(Color color, double thickness = 2)
        {
            Pen pen = new(Frozen(new SolidColorBrush(color)), thickness);
            pen.Freeze();
            return pen;
        }

        private static Pen FrozenDashedPen(Color color)
        {
            Pen pen = new(Frozen(new SolidColorBrush(color)), 1.5) { DashStyle = new DashStyle([4, 3], 0) };
            pen.Freeze();
            return pen;
        }
    }
}
