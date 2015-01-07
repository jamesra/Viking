using System;
using System.Drawing;
using System.Collections.Generic;
using System.Linq;
using System.Text;


namespace AnnotationVizLib
{
    public class ColorMapImageData
    {
        Bitmap image;

        Scale scale;

        double alpha_scalar = 1.0;
        double red_scalar = 1.0;
        double green_scalar = 1.0;
        double blue_scalar = 1.0;

        public ColorMapImageData(System.IO.Stream ImageStream, Scale scale_data)
        {
            this.image = new Bitmap(ImageStream);
            this.scale = scale_data;
        }

        public Color GetColor(double X, double Y)
        {
            int bmp_X = (int)Math.Round(X * scale.X.Value);
            int bmp_Y = (int)Math.Round(Y * scale.Y.Value);
            Color color = Color.Empty;

            if (bmp_X < 0 || bmp_X >= image.Size.Width)
                return Color.Empty;

            if (bmp_Y < 0 || bmp_Y >= image.Size.Height)
                return Color.Empty;

            try
            {
                color = image.GetPixel(bmp_X, bmp_Y);
            }
            catch(ArgumentException)
            {
                return Color.Empty;
            }

            //Convert to a scalar, multiply, and convert back to color...
            return Color.FromArgb(ScaleColor(color.A, alpha_scalar),
                                  ScaleColor(color.R, red_scalar),
                                  ScaleColor(color.G, green_scalar),
                                  ScaleColor(color.B, blue_scalar));
        }

        private int ScaleColor(int color, double scalar)
        {
            int scaled_color = (int)Math.Floor((double)color * scalar);
            scaled_color = scaled_color > 255 ? 255 : scaled_color;
            scaled_color = scaled_color < 0 ? 0 : scaled_color;
            return scaled_color; 
        }
    }
    /// <summary>
    /// Maps a position in the volume to an RGB color
    /// </summary>
    class ColorMapping
    {
        public ColorMapping(string image_config_txt_full_path)
        {

        }

        private void ParseConfigText(string image_config_txt_full_path)
        {

        }

        public Color GetColor(double X, double Y, double Z)
        {

            return Color.Empty;
        }

        
    }
}
