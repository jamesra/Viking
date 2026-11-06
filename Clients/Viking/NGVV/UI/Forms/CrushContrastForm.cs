#nullable enable

using System;
using System.Collections.Generic;
using System.Drawing;
using System.Windows.Forms;
using Viking.VolumeModel;

namespace Viking.UI.Forms
{
    /// <summary>
    /// Edits the contrast window of one crushed section and channel. The black and white points are codes (0 to 255 for 8-bit
    /// tiles); every change is saved on this computer and redrawn at once.
    /// </summary>
    public sealed class CrushContrastForm : Form
    {
        private readonly CrushInfo _crush;
        private readonly string? _volume;
        private readonly int _section;
        private readonly string _channel;
        private readonly Action _redraw;
        private readonly HistogramPanel _histogram;
        private readonly TrackBar _black = new();
        private readonly TrackBar _white = new();
        private readonly Label _blackValue = new();
        private readonly Label _whiteValue = new();
        private bool _loading;

        internal CrushContrastForm(CrushInfo crush, string? volume, int section, string channel, Action redraw)
        {
            _crush = crush;
            _volume = volume;
            _section = section;
            _channel = channel;
            _redraw = redraw;

            int maxCode = (1 << Math.Min(crush.Bits, 8)) - 1;
            Text = $"Contrast - Section {section} {channel}";
            FormBorderStyle = FormBorderStyle.FixedToolWindow;
            StartPosition = FormStartPosition.CenterParent;
            ClientSize = new Size(420, 250);

            _histogram = new HistogramPanel(crush.CodeHistogram) { Location = new Point(10, 10), Size = new Size(400, 90) };

            ConfigureTrack(_black, maxCode, 108);
            ConfigureTrack(_white, maxCode, 160);
            AddRow("Black point", _black, _blackValue, 108);
            AddRow("White point", _white, _whiteValue, 160);

            Button reset = new() { Text = "Reset to volume range", Location = new Point(10, 212), Size = new Size(150, 28) };
            reset.Click += (_, _) => Reset();
            Button close = new() { Text = "Close", Location = new Point(330, 212), Size = new Size(80, 28), DialogResult = DialogResult.OK };
            AcceptButton = close;
            CancelButton = close;
            Controls.AddRange([_histogram, reset, close]);

            (double min, double max) = EffectiveWindow();
            Load(min, max);
        }

        private void ConfigureTrack(TrackBar bar, int maxCode, int top)
        {
            bar.Minimum = 0;
            bar.Maximum = maxCode;
            bar.TickFrequency = Math.Max(1, (maxCode + 1) / 16);
            bar.LargeChange = Math.Max(1, (maxCode + 1) / 32);
            bar.Location = new Point(90, top);
            bar.Size = new Size(260, 45);
            bar.ValueChanged += (_, _) => OnSlider();
        }

        private void AddRow(string caption, TrackBar bar, Label value, int top)
        {
            Controls.Add(new Label { Text = caption, Location = new Point(10, top + 6), Size = new Size(78, 20) });
            value.Location = new Point(355, top + 6);
            value.Size = new Size(55, 20);
            Controls.Add(bar);
            Controls.Add(value);
        }

        /// <summary>The window drawn now, in codes: the user's, else the volume's display range.</summary>
        private (double Min, double Max) EffectiveWindow()
        {
            if (CrushContrastStore.Default.Get(_volume, _section, _channel) is { } saved)
                return saved;

            (float min, float max) = _crush.DefaultWindow();
            return (min * CrushInfo.StorageMaximum, max * CrushInfo.StorageMaximum);
        }

        private void Load(double min, double max)
        {
            _loading = true;
            try
            {
                _black.Value = Math.Min(Math.Max((int)Math.Round(min), _black.Minimum), _black.Maximum);
                _white.Value = Math.Min(Math.Max((int)Math.Round(max), _white.Minimum), _white.Maximum);
                ShowValues();
            }
            finally
            {
                _loading = false;
            }
        }

        private void ShowValues()
        {
            _blackValue.Text = _black.Value.ToString();
            _whiteValue.Text = _white.Value.ToString();
            _histogram.SetWindow(_black.Value, _white.Value);
        }

        private void OnSlider()
        {
            if (_loading)
                return;

            // The white point stays above the black point so the window keeps a width.
            _loading = true;
            try
            {
                if (_white.Value <= _black.Value)
                {
                    if (_black.Focused || _black.Value >= _white.Maximum)
                        _black.Value = Math.Max(_white.Value - 1, 0);
                    else
                        _white.Value = Math.Min(_black.Value + 1, _white.Maximum);
                }
            }
            finally
            {
                _loading = false;
            }

            ShowValues();
            if (_white.Value > _black.Value)
            {
                CrushContrastStore.Default.Set(_volume, _section, _channel, _black.Value, _white.Value);
                _redraw();
            }
        }

        private void Reset()
        {
            CrushContrastStore.Default.Clear(_volume, _section, _channel);
            (double min, double max) = EffectiveWindow();
            Load(min, max);
            _redraw();
        }

        /// <summary>Counts per code on a log scale, with the black and white points marked.</summary>
        private sealed class HistogramPanel : Control
        {
            private readonly IReadOnlyList<long>? _counts;
            private int _black;
            private int _white = 255;

            public HistogramPanel(IReadOnlyList<long>? counts)
            {
                _counts = counts;
                DoubleBuffered = true;
                BackColor = Color.FromArgb(24, 24, 24);
            }

            public void SetWindow(int black, int white)
            {
                _black = black;
                _white = white;
                Invalidate();
            }

            protected override void OnPaint(PaintEventArgs e)
            {
                base.OnPaint(e);
                if (_counts is null || _counts.Count == 0)
                {
                    TextRenderer.DrawText(e.Graphics, "No histogram in the VikingXML", Font, ClientRectangle, Color.Gray);
                    return;
                }

                // Bin 0 holds the empty section edge, which would flatten every other bar.
                double top = 1;
                for (int i = 1; i < _counts.Count; i++)
                    top = Math.Max(top, Math.Log(1 + _counts[i]));

                float binWidth = (float)Width / _counts.Count;
                using Brush bar = new SolidBrush(Color.FromArgb(150, 150, 150));
                for (int i = 1; i < _counts.Count; i++)
                {
                    float h = (float)(Math.Log(1 + _counts[i]) / top * (Height - 2));
                    e.Graphics.FillRectangle(bar, i * binWidth, Height - h, Math.Max(binWidth, 1f), h);
                }

                using Pen black = new(Color.DeepSkyBlue, 2);
                using Pen white = new(Color.Orange, 2);
                e.Graphics.DrawLine(black, (_black + 0.5f) * binWidth, 0, (_black + 0.5f) * binWidth, Height);
                e.Graphics.DrawLine(white, (_white + 0.5f) * binWidth, 0, (_white + 0.5f) * binWidth, Height);
            }
        }
    }
}
