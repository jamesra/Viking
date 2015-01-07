using System;
using System.Drawing;
using System.Diagnostics;
using System.Collections.Generic;
using System.Linq;
using System.Text;


namespace AnnotationVizLib
{
    public class ColorScalars
    {
        public double alpha = 1.0;
        public double red = 1.0;
        public double green = 1.0;
        public double blue = 1.0;

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
    public class ColorImageOffset
    {
        public double X = 0.0;
        public double Y = 0.0;

        public ColorImageOffset(double x, double y)
        {
            this.X = x;
            this.Y = y; 
        }
    }

    public class ColorMapImageData
    {
        public readonly int SectionNumber; 
        Bitmap image;  
        Scale scale;
        ColorScalars color_scalar = new ColorScalars(1, 1, 1, 1);
        ColorImageOffset offset = new ColorImageOffset(0,0);

        public ColorMapImageData(System.IO.Stream ImageStream, int section_number, Scale scale_data)
        {
            this.SectionNumber = section_number; 
            this.image = new Bitmap(ImageStream);
            this.scale = scale_data;
        }

        public ColorMapImageData(System.IO.Stream ImageStream, int section_number, Scale scale_data, ColorScalars color_scalars, ColorImageOffset offset)
            : this(ImageStream, section_number, scale_data)
        {
            this.color_scalar = color_scalars;
            this.offset = offset; 
        }

        public Color GetColor(double X, double Y)
        {
            X += offset.X;
            Y += offset.Y;

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


    /// <summary>
    /// Maps a position in the volume to an RGB color
    /// </summary>
    public class ColorMapping
    {  
        SortedList<int, List<ColorMapImageData>> ColorMapTable = new SortedList<int,List<ColorMapImageData>>();
 
        protected ColorMapping()
        {

        }

        private void ParseConfigText(string image_config_txt_full_path)
        {

        }

        public void AddColorMapImage(int SectionNumber, ColorMapImageData data)
        {
            if (ColorMapTable.ContainsKey(SectionNumber))
            {
                Debug.Assert(!ColorMapTable[SectionNumber].Contains(data));
                List<ColorMapImageData> listColorMapImageData = ColorMapTable[SectionNumber];
                listColorMapImageData.Add(data);
            }
            else
            {
                ColorMapTable.Add(SectionNumber, new List<ColorMapImageData>(new ColorMapImageData[] { data }));
            }

            return;
        }

        public Color GetColor(double X, double Y, int Z)
        {
            if(!ColorMapTable.ContainsKey(Z))
                return Color.Empty;

            List<ColorMapImageData> colormapimages = ColorMapTable[Z];
            List<Color> colors = new List<Color>(colormapimages.Count);

            for (int i = 0; i < colormapimages.Count; i++ )
            {
                Color color = colormapimages[i].GetColor(X, Y);
                if (color != Color.Empty)
                {
                    colors.Add(color);
                }
            }

            //Average the colors together
            return AverageColors(colors); 
        }

        public Color AverageColors(IList<Color> colors)
        {
            int R = 0; 
            int G = 0;
            int B = 0;
            int A = 0;

            foreach(Color c in colors)
            {
                A += System.Convert.ToInt32(c.A);
                R += System.Convert.ToInt32(c.R);
                G += System.Convert.ToInt32(c.G);
                B += System.Convert.ToInt32(c.B);
            }

            R /= colors.Count; 
            G /= colors.Count; 
            B /= colors.Count; 
            A /= colors.Count;

            return Color.FromArgb(A, R, G, B);
        }

        /// <summary>
        /// Reads a text file with this format:
        /// 
        /// Section	XOffset	YOffset	XScale	YScale	Red	Green	Blue	Filename	
        ///1	0	0	128	128	1	1	1	0001_5604_CMPv2.png
        ///30	0	0	32	32	0	1	0	0030_Glycine_32.png
        ///61	0	0	32	32	1	0	0	0061_GABA_32.png
        ///90	0	0	32	32	0	0	1	0090_AGB_32.png
        ///184	0	0	32	32	1	0	0	0184_GABA_32.png
        ///277	0	0	32	32	0	1	0	0277_Glycine_32.png
        ///371	0	0	32	32	1	1	1	0371_PCAImage.png
        /// </summary>
        /// <param name="config"></param>
        public static ColorMapping Create(string config_data, string ImageDir)
        {
            ColorMapping mapping = new ColorMapping(); 

            SortedList<int, List<ColorMapImageData>> ColorMapList = new SortedList<int, List<ColorMapImageData>>();
            string[] lines = config_data.Split(new char[] { '\n' });
            foreach(string line in lines)
            {
                try
                {
                    ColorMapImageData colormapimagedata = ParseConfigLine(line, ImageDir);
                    mapping.AddColorMapImage(colormapimagedata.SectionNumber, colormapimagedata);
                }
                catch(System.FormatException e)
                {
                    System.Diagnostics.Trace.WriteLine("Unable to parse Color Map Config line: " + line);
                } 
                catch(System.ArgumentException e)
                {
                    Trace.WriteLine(e.Message);
                    continue; 
                }
            }

            return mapping; 
        }

        /// <summary>
        /// Parses a line in the format:
        /// 
        ///Section	XOffset	YOffset	XScale	YScale	Red	Green	Blue	Filename	
        ///1	0	0	128	128	1	1	1	0001_5604_CMPv2.png
        ///
        /// If the header row is passed to the function it throws an argument exception
        ///
        /// </summary>
        /// <param name="line"></param>
        /// <returns></returns>
        private static ColorMapImageData ParseConfigLine(string line, string ImageDir)
        {
            line = line.Trim().ToLower();
            string[] parts = line.Split();

            if (parts[0].Trim() == "section")
                throw new FormatException("Attempting to parse header row");

            int SectionNumber = System.Convert.ToInt32(parts[0]);

            ColorImageOffset offset = new ColorImageOffset(System.Convert.ToInt32(parts[1]), System.Convert.ToInt32(parts[2]));
            
            AxisUnits X_Scale = new AxisUnits(System.Convert.ToDouble(parts[3]), "");
            AxisUnits Y_Scale = new AxisUnits(System.Convert.ToDouble(parts[4]), "");
            AxisUnits Z_Scale = new AxisUnits(0, "");

            Scale scale = new Scale(X_Scale, Y_Scale, Z_Scale);
            
            ColorScalars scalars = new ColorScalars(System.Convert.ToDouble(parts[5]),
                                                    System.Convert.ToDouble(parts[6]),
                                                    System.Convert.ToDouble(parts[7]),
                                                    System.Convert.ToDouble(parts[8]));

            string Filename = parts[9];  
            if(ImageDir != null)
            {
                Filename = System.IO.Path.Combine(ImageDir, Filename);
            }

            if(!System.IO.File.Exists(Filename))
            {
                throw new ArgumentException("File specified in ColorMap config file does not exist: " + Filename);
            }

            using (System.IO.Stream stream = System.IO.File.OpenRead(Filename))
            {
                ColorMapImageData image = new ColorMapImageData(stream, SectionNumber, scale, scalars, offset);
                return image; 
            }
        } 
        
    }
}
