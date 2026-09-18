using Geometry;
using SixLabors.Fonts;
using SixLabors.ImageSharp;
using SixLabors.ImageSharp.Drawing;
using SixLabors.ImageSharp.Drawing.Processing;
using SixLabors.ImageSharp.PixelFormats;
using SixLabors.ImageSharp.Processing;
using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using Viking.SectionCorrection;
using Path = System.IO.Path;

namespace Viking.SectionCorrectionBuilder
{
    static class QuiverRenderer
    {
        const int MinLongSide = 512;
        const int MaxLongSide = 2048;
        const double TargetNmPerPixel = 100.0;

        public static void WriteSectionQuivers(
            string previewDirectory,
            IEnumerable<SectionVectorField> sections,
            double displayPitchNm,
            IReadOnlyDictionary<long, Geometry.Rectangle> sectionBoundsNm)
        {
            List<SectionVectorField> list = [.. sections.Where(s => s.NodeCount > 0).OrderBy(s => s.Z)];
            foreach (SectionVectorField field in list)
            {
                string path = Path.Combine(previewDirectory, field.Z.ToString(CultureInfo.InvariantCulture) + ".quiver.svg");
                Geometry.Rectangle? frame = sectionBoundsNm.TryGetValue(field.Z, out Geometry.Rectangle bounds) ? bounds : null;
                WriteQuiverSvg(path, field, displayPitchNm, frame);
            }

            WriteContactSheet(previewDirectory, list, displayPitchNm, sectionBoundsNm);
        }

        /// <summary>
        /// Writes quiver SVGs and the contact sheet that are not already in <paramref name="previewDirectory"/>.
        /// Does not touch published measurement files.
        /// </summary>
        public static int WriteMissingQuivers(
            string previewDirectory,
            IEnumerable<SectionVectorField> sections,
            double displayPitchNm,
            IReadOnlyDictionary<long, Geometry.Rectangle> sectionBoundsNm)
        {
            Directory.CreateDirectory(previewDirectory);
            List<SectionVectorField> list = [.. sections.Where(s => s.NodeCount > 0).OrderBy(s => s.Z)];
            int written = 0;
            foreach (SectionVectorField field in list)
            {
                string path = Path.Combine(previewDirectory, field.Z.ToString(CultureInfo.InvariantCulture) + ".quiver.svg");
                if (File.Exists(path) && SvgHasAxes(path))
                    continue;

                Geometry.Rectangle? frame = sectionBoundsNm.TryGetValue(field.Z, out Geometry.Rectangle bounds) ? bounds : null;
                WriteQuiverSvg(path, field, displayPitchNm, frame);
                written++;
            }

            string index = Path.Combine(previewDirectory, "index.png");
            if (list.Count > 0 && (written > 0 || !File.Exists(index)))
            {
                WriteContactSheet(previewDirectory, list, displayPitchNm, sectionBoundsNm);
                written++;
            }

            return written;
        }

        public static Image<Rgba32> Render(SectionVectorField field, double displayPitchNm, Geometry.Rectangle? sectionBoundsNm = null)
        {
            (double originX, double originY, double extentX, double extentY, bool sectionFrame) = Frame(field, displayPitchNm, sectionBoundsNm);
            double maxExtent = Math.Max(extentX, extentY);
            double nmPerPixel = TargetNmPerPixel;
            double longSide = maxExtent / nmPerPixel;
            if (longSide < MinLongSide)
                nmPerPixel = maxExtent / MinLongSide;
            if (maxExtent / nmPerPixel > MaxLongSide)
                nmPerPixel = maxExtent / MaxLongSide;

            int margin = sectionFrame ? 0 : 40;
            int pxW = Math.Max(64, (int)Math.Ceiling(extentX / nmPerPixel) + margin * 2);
            int pxH = Math.Max(64, (int)Math.Ceiling(extentY / nmPerPixel) + margin * 2);
            Image<Rgba32> image = new(pxW, pxH, new Rgba32(12, 12, 16));
            double pitch = displayPitchNm > 0 ? displayPitchNm : field.PitchNm * 0.5;
            double cellPx = pitch / nmPerPixel;
            double legend500 = 500.0 / nmPerPixel;

            image.Mutate(ctx =>
            {
                foreach ((Vector2 origin, Vector2 offset) in field.EnumerateTrustedDisplaySamples(pitch))
                {
                    double mag = offset.Magnitude;
                    float px = (float)((origin.X - originX) / nmPerPixel + margin);
                    float py = (float)((origin.Y - originY) / nmPerPixel + margin);
                    double arrowLen = Math.Min(cellPx * 0.9, Math.Max(3, mag / 500.0 * Math.Max(legend500, cellPx * 0.75)));
                    Vector2 dir = offset * (arrowLen / mag);
                    float x1 = px + (float)dir.X;
                    float y1 = py + (float)dir.Y;
                    Color color = ColorForMagnitude(mag);
                    ctx.DrawLine(color, 2f, new PointF(px, py), new PointF(x1, y1));
                    DrawHead(ctx, color, px, py, x1, y1);
                }

                if (sectionFrame)
                {
                    Color border = Color.FromRgb(176, 176, 184);
                    float right = pxW - 1.5f;
                    float bottom = pxH - 1.5f;
                    ctx.DrawLine(border, 3f, new PointF(1.5f, 1.5f), new PointF(right, 1.5f));
                    ctx.DrawLine(border, 3f, new PointF(right, 1.5f), new PointF(right, bottom));
                    ctx.DrawLine(border, 3f, new PointF(right, bottom), new PointF(1.5f, bottom));
                    ctx.DrawLine(border, 3f, new PointF(1.5f, bottom), new PointF(1.5f, 1.5f));
                }

                DrawLegend(ctx, pxW);
            });

            return image;
        }

        static void DrawHead(IImageProcessingContext ctx, Color color, float x0, float y0, float x1, float y1)
        {
            float dx = x1 - x0;
            float dy = y1 - y0;
            float len = MathF.Sqrt(dx * dx + dy * dy);
            if (len < 1)
                return;
            dx /= len;
            dy /= len;
            float hx = x1 - dx * 10;
            float hy = y1 - dy * 10;
            float px = -dy * 4;
            float py = dx * 4;
            ctx.DrawLine(color, 2f, new PointF(x1, y1), new PointF(hx + px, hy + py));
            ctx.DrawLine(color, 2f, new PointF(x1, y1), new PointF(hx - px, hy - py));
        }

        static Color ColorForMagnitude(double magNm)
        {
            double t = Math.Clamp(magNm / 1000.0, 0, 1);
            byte r = (byte)(40 + 200 * t);
            byte g = (byte)(180 * (1 - t));
            byte b = (byte)(220 * (1 - t));
            return Color.FromRgb(r, g, b);
        }

        static void DrawLegend(IImageProcessingContext ctx, int width)
        {
            try
            {
                Font font;
                try
                {
                    font = SystemFonts.CreateFont("DejaVu Sans", 28);
                }
                catch (FontException)
                {
                    font = SystemFonts.CreateFont("Arial", 28);
                }
                ctx.DrawText("100 nm / 500 nm / 1 µm", font, Color.White, new PointF(20, 12));
            }
            catch (FontException)
            {
                ctx.DrawLine(Color.White, 3f, new PointF(20, 20), new PointF(20 + 100, 20));
            }

            ctx.DrawLine(ColorForMagnitude(100), 3f, new PointF(width - 360, 24), new PointF(width - 300, 24));
            ctx.DrawLine(ColorForMagnitude(500), 3f, new PointF(width - 240, 24), new PointF(width - 160, 24));
            ctx.DrawLine(ColorForMagnitude(1000), 3f, new PointF(width - 120, 24), new PointF(width - 20, 24));
        }

        static void WriteContactSheet(
            string previewDirectory,
            List<SectionVectorField> sections,
            double displayPitchNm,
            IReadOnlyDictionary<long, Geometry.Rectangle> sectionBoundsNm)
        {
            if (sections.Count == 0)
                return;

            const int thumb = 512;
            int take = Math.Min(64, sections.Count);
            int step = Math.Max(1, sections.Count / take);
            List<SectionVectorField> sample = [];
            for (int i = 0; i < sections.Count && sample.Count < take; i += step)
                sample.Add(sections[i]);

            int cols = (int)Math.Ceiling(Math.Sqrt(sample.Count));
            int rows = (int)Math.Ceiling(sample.Count / (double)cols);
            using Image<Rgba32> sheet = new(cols * thumb, rows * thumb, new Rgba32(8, 8, 10));
            for (int i = 0; i < sample.Count; i++)
            {
                Geometry.Rectangle? frame = sectionBoundsNm.TryGetValue(sample[i].Z, out Geometry.Rectangle bounds) ? bounds : null;
                using Image<Rgba32> full = Render(sample[i], displayPitchNm, frame);
                full.Mutate(c => c.Resize(thumb, thumb));
                int cx = (i % cols) * thumb;
                int cy = (i / cols) * thumb;
                sheet.Mutate(c => c.DrawImage(full, new Point(cx, cy), 1f));
            }

            sheet.SaveAsPng(Path.Combine(previewDirectory, "index.png"));
        }

        /// <summary>
        /// Writes one section quiver in nanometers so arrows stay sharp when zoomed.
        /// Arrow length matches the PNG preview at 100 nm per pixel, without the raster size clamp.
        /// </summary>
        static void WriteQuiverSvg(string path, SectionVectorField field, double displayPitchNm, Geometry.Rectangle? sectionBoundsNm)
        {
            (double minX, double minY, double vbW, double vbH, bool sectionFrame) = Frame(field, displayPitchNm, sectionBoundsNm);
            double pitch = displayPitchNm > 0 ? displayPitchNm : field.PitchNm * 0.5;
            if (!sectionFrame)
            {
                const double padNm = 4000;
                minX -= padNm;
                minY -= padNm;
                vbW += padNm * 2;
                vbH += padNm * 2;
            }

            using StreamWriter writer = new(path, false, new System.Text.UTF8Encoding(encoderShouldEmitUTF8Identifier: false));
            writer.Write("<?xml version=\"1.0\" encoding=\"UTF-8\"?>");
            writer.Write("<!-- quiver-axes -->");
            writer.Write("<svg xmlns=\"http://www.w3.org/2000/svg\" viewBox=\"");
            writer.Write(F(minX));
            writer.Write(' ');
            writer.Write(F(minY));
            writer.Write(' ');
            writer.Write(F(vbW));
            writer.Write(' ');
            writer.Write(F(vbH));
            writer.Write("\" width=\"");
            writer.Write(F(vbW / TargetNmPerPixel));
            writer.Write("\" height=\"");
            writer.Write(F(vbH / TargetNmPerPixel));
            writer.Write("\">");

            double maxY = minY + vbH;
            double flip = minY + maxY;
            writer.Write("<g transform=\"matrix(1 0 0 -1 0 ");
            writer.Write(F(flip));
            writer.Write(")\">");

            writer.Write("<rect x=\"");
            writer.Write(F(minX));
            writer.Write("\" y=\"");
            writer.Write(F(minY));
            writer.Write("\" width=\"");
            writer.Write(F(vbW));
            writer.Write("\" height=\"");
            writer.Write(F(vbH));
            writer.Write("\" fill=\"#0C0C10\"/>");
            if (sectionFrame)
            {
                writer.Write("<rect x=\"");
                writer.Write(F(minX));
                writer.Write("\" y=\"");
                writer.Write(F(minY));
                writer.Write("\" width=\"");
                writer.Write(F(vbW));
                writer.Write("\" height=\"");
                writer.Write(F(vbH));
                writer.Write("\" fill=\"none\" stroke=\"#B0B0B8\" stroke-width=\"2\" vector-effect=\"non-scaling-stroke\"/>");
            }
            writer.Write("<g fill=\"none\" stroke-width=\"200\" stroke-linecap=\"round\" stroke-linejoin=\"round\">");

            foreach ((Vector2 origin, Vector2 offset) in field.EnumerateTrustedDisplaySamples(pitch))
            {
                double mag = offset.Magnitude;
                if (mag <= 1e-6)
                    continue;

                double arrowLen = Math.Min(pitch * 0.9, Math.Max(300, mag / 500.0 * Math.Max(500, pitch * 0.75)));
                Vector2 dir = offset * (arrowLen / mag);
                double x0 = origin.X;
                double y0 = origin.Y;
                double x1 = x0 + dir.X;
                double y1 = y0 + dir.Y;

                writer.Write("<path stroke=\"");
                writer.Write(Hex(mag));
                writer.Write("\" d=\"M");
                writer.Write(F(x0));
                writer.Write(' ');
                writer.Write(F(y0));
                writer.Write(" L");
                writer.Write(F(x1));
                writer.Write(' ');
                writer.Write(F(y1));

                double len = Math.Sqrt(dir.X * dir.X + dir.Y * dir.Y);
                if (len >= 100)
                {
                    double ux = dir.X / len;
                    double uy = dir.Y / len;
                    double head = Math.Min(1000, len * 0.35);
                    double half = head * 0.4;
                    double hx = x1 - ux * head;
                    double hy = y1 - uy * head;
                    double px = -uy * half;
                    double py = ux * half;
                    writer.Write(" M");
                    writer.Write(F(x1));
                    writer.Write(' ');
                    writer.Write(F(y1));
                    writer.Write(" L");
                    writer.Write(F(hx + px));
                    writer.Write(' ');
                    writer.Write(F(hy + py));
                    writer.Write(" M");
                    writer.Write(F(x1));
                    writer.Write(' ');
                    writer.Write(F(y1));
                    writer.Write(" L");
                    writer.Write(F(hx - px));
                    writer.Write(' ');
                    writer.Write(F(hy - py));
                }

                writer.Write("\"/>");
            }

            writer.Write("</g>");
            double step = NiceStep(Math.Max(vbW, vbH));
            double tick = Math.Min(vbW, vbH) * 0.012;
            WriteAxisTicks(writer, minX, minY, vbW, vbH, step, tick);
            writer.Write("</g>");

            double font = Math.Max(Math.Min(vbW, vbH) * 0.018, 1);
            WriteAxisLabels(writer, field.Z, minX, minY, vbW, vbH, step, font);
            writer.Write("</svg>");
        }

        static double SvgY(double worldY, double minY, double height) => (minY + minY + height) - worldY;

        static void WriteAxisTicks(StreamWriter writer, double minX, double minY, double width, double height, double step, double tick)
        {
            writer.Write("<g fill=\"none\" stroke=\"#E6E6EA\" stroke-width=\"2\" vector-effect=\"non-scaling-stroke\">");
            double maxX = minX + width;
            double maxY = minY + height;
            for (double x = Math.Ceiling(minX / step) * step; x <= maxX + step * 0.01; x += step)
            {
                if (x < minX - 1 || x > maxX + 1)
                    continue;
                writer.Write("<line x1=\"");
                writer.Write(F(x));
                writer.Write("\" y1=\"");
                writer.Write(F(minY));
                writer.Write("\" x2=\"");
                writer.Write(F(x));
                writer.Write("\" y2=\"");
                writer.Write(F(minY + tick));
                writer.Write("\"/>");
            }

            for (double y = Math.Ceiling(minY / step) * step; y <= maxY + step * 0.01; y += step)
            {
                if (y < minY - 1 || y > maxY + 1)
                    continue;
                writer.Write("<line x1=\"");
                writer.Write(F(minX));
                writer.Write("\" y1=\"");
                writer.Write(F(y));
                writer.Write("\" x2=\"");
                writer.Write(F(minX + tick));
                writer.Write("\" y2=\"");
                writer.Write(F(y));
                writer.Write("\"/>");
            }

            double bar = step;
            double barX = minX + tick * 2;
            double barY = minY + tick * 3;
            writer.Write("<line x1=\"");
            writer.Write(F(barX));
            writer.Write("\" y1=\"");
            writer.Write(F(barY));
            writer.Write("\" x2=\"");
            writer.Write(F(barX + bar));
            writer.Write("\" y2=\"");
            writer.Write(F(barY));
            writer.Write("\" stroke=\"#FFFFFF\" stroke-width=\"4\"/>");
            writer.Write("</g>");
        }

        static void WriteAxisLabels(StreamWriter writer, long z, double minX, double minY, double width, double height, double step, double font)
        {
            double maxX = minX + width;
            writer.Write("<g fill=\"#E6E6EA\" font-family=\"sans-serif\" font-size=\"");
            writer.Write(F(font));
            writer.Write("\">");
            writer.Write("<text x=\"");
            writer.Write(F(minX + font * 0.4));
            writer.Write("\" y=\"");
            writer.Write(F(minY + font * 1.2));
            writer.Write("\">Z ");
            writer.Write(z.ToString(CultureInfo.InvariantCulture));
            writer.Write("  volume nm  +X right  +Y up</text>");

            for (double x = Math.Ceiling(minX / step) * step; x <= maxX + step * 0.01; x += step)
            {
                if (x < minX - 1 || x > maxX + 1)
                    continue;
                writer.Write("<text x=\"");
                writer.Write(F(x));
                writer.Write("\" y=\"");
                writer.Write(F(SvgY(minY, minY, height) - font * 0.35));
                writer.Write("\" text-anchor=\"middle\">");
                writer.Write(FormatNm(x));
                writer.Write("</text>");
            }

            double maxY = minY + height;
            for (double y = Math.Ceiling(minY / step) * step; y <= maxY + step * 0.01; y += step)
            {
                if (y < minY - 1 || y > maxY + 1)
                    continue;
                writer.Write("<text x=\"");
                writer.Write(F(minX + font * 0.3));
                writer.Write("\" y=\"");
                writer.Write(F(SvgY(y, minY, height)));
                writer.Write("\" dominant-baseline=\"middle\">");
                writer.Write(FormatNm(y));
                writer.Write("</text>");
            }

            double tick = Math.Min(width, height) * 0.012;
            double barX = minX + tick * 2;
            double barY = minY + tick * 3;
            writer.Write("<text x=\"");
            writer.Write(F(barX));
            writer.Write("\" y=\"");
            writer.Write(F(SvgY(barY, minY, height) - font * 0.35));
            writer.Write("\">");
            writer.Write(FormatNm(step));
            writer.Write("</text>");
            writer.Write("</g>");
        }

        static bool SvgHasAxes(string path)
        {
            try
            {
                using StreamReader reader = new(path);
                char[] buffer = new char[180];
                int n = reader.Read(buffer, 0, buffer.Length);
                return n > 0 && new string(buffer, 0, n).Contains("quiver-axes", StringComparison.Ordinal);
            }
            catch (IOException)
            {
                return false;
            }
        }

        static double NiceStep(double extent)
        {
            if (extent <= 0 || double.IsNaN(extent) || double.IsInfinity(extent))
                return 1000;
            double raw = extent / 5.0;
            double pow = Math.Pow(10, Math.Floor(Math.Log10(Math.Max(raw, 1e-9))));
            double mantissa = raw / pow;
            double nice = mantissa < 1.5 ? 1 : mantissa < 3.5 ? 2 : mantissa < 7.5 ? 5 : 10;
            return nice * pow;
        }

        static string FormatNm(double nm)
        {
            double abs = Math.Abs(nm);
            if (abs >= 1_000_000)
                return string.Create(CultureInfo.InvariantCulture, $"{nm / 1_000_000:0.###} mm");
            if (abs >= 1_000)
                return string.Create(CultureInfo.InvariantCulture, $"{nm / 1_000:0.#} µm");
            return string.Create(CultureInfo.InvariantCulture, $"{nm:0.#} nm");
        }

        static (double MinX, double MinY, double Width, double Height, bool SectionFrame) Frame(
            SectionVectorField field,
            double displayPitchNm,
            Geometry.Rectangle? sectionBoundsNm)
        {
            if (sectionBoundsNm is Geometry.Rectangle box && box.Width > 0 && box.Height > 0)
                return (box.Left, box.Bottom, box.Width, box.Height, true);

            (double originX, double originY, int width, int height) = field.OccupiedRasterBounds();
            double extentX = Math.Max(width * field.PitchNm, displayPitchNm);
            double extentY = Math.Max(height * field.PitchNm, displayPitchNm);
            return (originX, originY, extentX, extentY, false);
        }

        static string Hex(double magNm)
        {
            double t = Math.Clamp(magNm / 1000.0, 0, 1);
            int r = (int)(40 + 200 * t);
            int g = (int)(180 * (1 - t));
            int b = (int)(220 * (1 - t));
            return string.Create(CultureInfo.InvariantCulture, $"#{r:X2}{g:X2}{b:X2}");
        }

        static string F(double value) => value.ToString("0.###", CultureInfo.InvariantCulture);
    }
}
