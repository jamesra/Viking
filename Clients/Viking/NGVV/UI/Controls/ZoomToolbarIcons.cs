using System.Collections.Generic;
using System.Drawing;
using System.Drawing.Drawing2D;

namespace Viking.UI.Controls
{
    /// <summary>
    /// Toolbar glyphs for Zoom In, Zoom Out, and Home (snap mag to power of two).
    /// Drawn on a 16-unit design grid and scaled to the requested pixel size for high-DPI.
    /// </summary>
    internal static class ZoomToolbarIcons
    {
        private const float DesignSize = 16f;

        private static readonly Dictionary<int, Image> ZoomInBySize = [];
        private static readonly Dictionary<int, Image> ZoomOutBySize = [];
        private static readonly Dictionary<int, Image> HomeBySize = [];

        /// <summary>Magnifier with a plus for Zoom In at <paramref name="pixelSize"/> square.</summary>
        public static Image ZoomIn(int pixelSize) =>
            GetOrCreate(ZoomInBySize, pixelSize, DrawZoomIn);

        /// <summary>Magnifier with a minus for Zoom Out at <paramref name="pixelSize"/> square.</summary>
        public static Image ZoomOut(int pixelSize) =>
            GetOrCreate(ZoomOutBySize, pixelSize, DrawZoomOut);

        /// <summary>Simple house for Home (nearest power-of-two mag) at <paramref name="pixelSize"/> square.</summary>
        public static Image Home(int pixelSize) =>
            GetOrCreate(HomeBySize, pixelSize, DrawHome);

        private static Image GetOrCreate(Dictionary<int, Image> cache, int pixelSize, System.Action<Graphics> draw)
        {
            pixelSize = System.Math.Max(16, pixelSize);
            if (cache.TryGetValue(pixelSize, out Image? existing))
                return existing;

            Bitmap bmp = new(pixelSize, pixelSize);
            using (Graphics g = Graphics.FromImage(bmp))
            {
                g.SmoothingMode = SmoothingMode.AntiAlias;
                g.Clear(Color.Transparent);
                g.ScaleTransform(pixelSize / DesignSize, pixelSize / DesignSize);
                draw(g);
            }

            cache[pixelSize] = bmp;
            return bmp;
        }

        private static void DrawZoomIn(Graphics g)
        {
            DrawMagnifier(g);
            using Pen plus = new(Color.FromArgb(30, 30, 35), 1.6f)
            {
                StartCap = LineCap.Round,
                EndCap = LineCap.Round
            };
            g.DrawLine(plus, 5.2f, 7f, 8.8f, 7f);
            g.DrawLine(plus, 7f, 5.2f, 7f, 8.8f);
        }

        private static void DrawZoomOut(Graphics g)
        {
            DrawMagnifier(g);
            using Pen minus = new(Color.FromArgb(30, 30, 35), 1.6f)
            {
                StartCap = LineCap.Round,
                EndCap = LineCap.Round
            };
            g.DrawLine(minus, 5.2f, 7f, 8.8f, 7f);
        }

        private static void DrawMagnifier(Graphics g)
        {
            using Pen rim = new(Color.FromArgb(40, 40, 45), 1.5f);
            g.DrawEllipse(rim, 2.2f, 2.2f, 9.6f, 9.6f);

            using Pen handle = new(Color.FromArgb(40, 40, 45), 2f)
            {
                StartCap = LineCap.Round,
                EndCap = LineCap.Round
            };
            g.DrawLine(handle, 10.2f, 10.2f, 13.8f, 13.8f);
        }

        private static void DrawHome(Graphics g)
        {
            PointF[] roof =
            [
                new(2.5f, 8f),
                new(8f, 2.5f),
                new(13.5f, 8f)
            ];
            using Pen stroke = new(Color.FromArgb(40, 40, 45), 1.5f)
            {
                LineJoin = LineJoin.Round
            };
            g.DrawLines(stroke, roof);

            using Pen wall = new(Color.FromArgb(40, 40, 45), 1.5f);
            g.DrawRectangle(wall, 4f, 8f, 8f, 5.5f);

            using SolidBrush door = new(Color.FromArgb(0, 110, 60));
            g.FillRectangle(door, 6.8f, 10f, 2.4f, 3.5f);
        }
    }
}
