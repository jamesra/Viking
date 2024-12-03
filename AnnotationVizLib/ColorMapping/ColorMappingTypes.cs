using System;
using System.Drawing;
using UnitsAndScale;


namespace AnnotationVizLib
{
    [Serializable]
    public readonly struct ColorScalars
    {
        public readonly double alpha;
        public readonly double red;
        public readonly double green;
        public readonly double blue;

        public ColorScalars(double a, double r, double g, double b)
        {
            this.alpha = a;
            this.red = r;
            this.green = g;
            this.blue = b;
        }
    }


    /// <summary>
    /// Used to store offsets into color map images
    /// </summary>
    [Serializable]
    public readonly struct ColorImageOffset
    {
        public readonly double X;
        public readonly double Y;

        public ColorImageOffset(double x, double y)
        {
            this.X = x;
            this.Y = y;
        }
    }

    public class ColorMapImageData
    {
        public readonly int SectionNumber;
        readonly Bitmap image;
        readonly IScale scale;
        readonly ColorScalars color_scalar = new ColorScalars(1, 1, 1, 1);
        readonly ColorImageOffset offset = new ColorImageOffset(0, 0);

        public ColorMapImageData(System.IO.Stream ImageStream, int section_number, IScale scale_data)
        {
            this.SectionNumber = section_number;
            this.image = new Bitmap(ImageStream);
            this.scale = scale_data;
        }

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

            if (bmp_X < 0 || bmp_X >= image.Size.Width)
                return Color.Empty;

            if (bmp_Y < 0 || bmp_Y >= image.Size.Height)
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
            return Color.FromArgb(ScaleColor(color.A, color_scalar.alpha),
                                  ScaleColor(color.R, color_scalar.red),
                                  ScaleColor(color.G, color_scalar.green),
                                  ScaleColor(color.B, color_scalar.blue));
        }

        private int ScaleColor(int color, double scalar)
        {
            int scaled_color = (int)Math.Floor((double)color * scalar);
            scaled_color = scaled_color > 255 ? 255 : scaled_color;
            scaled_color = scaled_color < 0 ? 0 : scaled_color;
            return scaled_color;
        }
    }
}
