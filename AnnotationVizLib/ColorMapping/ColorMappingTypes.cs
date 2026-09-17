using System;
using System.Drawing;
using UnitsAndScale;


namespace AnnotationVizLib
{
    [Serializable]
    public readonly struct ColorScalars(double a, double r, double g, double b)
    {
        public readonly double alpha = a;
        public readonly double red = r;
        public readonly double green = g;
        public readonly double blue = b;
    }


    /// <summary>
    /// Used to store offsets into color map images
    /// </summary>
    [Serializable]
    public readonly struct ColorImageOffset(double x, double y)
    {
        public readonly double X = x;
        public readonly double Y = y;
    }

    public class ColorMapImageData(System.IO.Stream ImageStream, int section_number, IScale scale_data) : IDisposable
    {
        public readonly int SectionNumber = section_number;
        readonly CrossPlatformImage image = new(ImageStream);
        readonly IScale scale = scale_data;
        readonly ColorScalars color_scalar = new(1, 1, 1, 1);
        readonly ColorImageOffset offset = new(0, 0);

        public ColorMapImageData(System.IO.Stream ImageStream, int section_number, IScale scale_data, ColorScalars color_scalars, ColorImageOffset offset)
            : this(ImageStream, section_number, scale_data)
        {
            this.color_scalar = color_scalars;
            this.offset = offset;
        }

        public Color GetColor(double X, double Y)
        {
            X += offset.X;
            Y += offset.Y;

            int bmp_X = (int)Math.Round(X / scale.X.Value);
            int bmp_Y = (int)Math.Round(Y / scale.Y.Value);
            Color color = Color.Empty;

            if (bmp_X < 0 || bmp_X >= image.Width)
                return Color.Empty;

            if (bmp_Y < 0 || bmp_Y >= image.Height)
                return Color.Empty;

            try
            {
                color = image.GetPixel(bmp_X, bmp_Y);
            }
            catch (ArgumentException)
            {
                return Color.Empty;
            }

            //Convert to a scalar, multiply, and convert back to color...
            return Color.FromArgb(ColorMapImageData.ScaleColor(color.A, color_scalar.alpha),
                                  ColorMapImageData.ScaleColor(color.R, color_scalar.red),
                                  ColorMapImageData.ScaleColor(color.G, color_scalar.green),
                                  ColorMapImageData.ScaleColor(color.B, color_scalar.blue));
        }

        private static int ScaleColor(int color, double scalar)
        {
            int scaled_color = (int)Math.Floor((double)color * scalar);
            scaled_color = scaled_color > 255 ? 255 : scaled_color;
            scaled_color = scaled_color < 0 ? 0 : scaled_color;
            return scaled_color;
        }

        void IDisposable.Dispose() => image?.Dispose();
    }
}
