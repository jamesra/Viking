using System.Collections.Generic;
using System.Drawing;
using System.Drawing.Drawing2D;

namespace WebAnnotation.UI
{
    /// <summary>
    /// Toolbar glyphs for Pen Mode and Auto Polygonize. Drawn at runtime on a 16-unit
    /// design grid and scaled to the requested pixel size so they stay sharp on high-DPI forms.
    /// </summary>
    internal static class AnnotationModeToolbarIcons
    {
        private const float DesignSize = 16f;

        private static readonly Dictionary<int, Image> PenModeBySize = [];
        private static readonly Dictionary<int, Image> AutoPolygonizeBySize = [];

        /// <summary>Stylus / pen tip for Pen Mode at <paramref name="pixelSize"/> square.</summary>
        public static Image PenMode(int pixelSize) =>
            GetOrCreate(PenModeBySize, pixelSize, DrawPenMode);

        /// <summary>Circle with a polygon ring for Auto Polygonize at <paramref name="pixelSize"/> square.</summary>
        public static Image AutoPolygonize(int pixelSize) =>
            GetOrCreate(AutoPolygonizeBySize, pixelSize, DrawAutoPolygonize);

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

        private static void DrawPenMode(Graphics g)
        {
            using Pen body = new(Color.FromArgb(40, 40, 45), 2.2f)
            {
                StartCap = LineCap.Round,
                EndCap = LineCap.Round
            };
            g.DrawLine(body, 3.5f, 12.5f, 11.5f, 4.5f);

            PointF[] tip =
            [
                new(11.2f, 2.8f),
                new(13.5f, 2.5f),
                new(13.2f, 4.8f)
            ];
            using SolidBrush tipBrush = new(Color.FromArgb(30, 30, 35));
            g.FillPolygon(tipBrush, tip);

            using Pen band = new(Color.FromArgb(0, 120, 70), 1.6f);
            g.DrawLine(band, 5.5f, 10.2f, 7.2f, 8.5f);
        }

        private static void DrawAutoPolygonize(Graphics g)
        {
            using Pen circlePen = new(Color.FromArgb(140, 140, 150), 1.4f);
            g.DrawEllipse(circlePen, 2.5f, 2.5f, 11f, 11f);

            PointF[] hex =
            [
                new(8f, 2.2f),
                new(12.8f, 5f),
                new(12.8f, 11f),
                new(8f, 13.8f),
                new(3.2f, 11f),
                new(3.2f, 5f)
            ];
            using Pen polyPen = new(Color.FromArgb(0, 110, 60), 1.6f);
            g.DrawPolygon(polyPen, hex);
        }
    }
}
