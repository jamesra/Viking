using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Drawing;
using System.Linq; 
using Geometry;
using SqlGeometryUtils;
using UnitsAndScale;
using Viking.AnnotationServiceTypes.Interfaces;

namespace AnnotationVizLib
{
    /// <summary>
    /// Maps a position in the volume to an RGB color based on the X,Y,Z coordinates.
    /// Averages color values when multiple images overlap the target coordinates
    /// </summary>
    public class ColorMapWithImages
    {
        readonly SortedList<int, List<ColorMapImageData>> ColorMapTable = new SortedList<int, List<ColorMapImageData>>();

        protected ColorMapWithImages()
        {

        }

        /// <summary>
        /// Read the images form the image config text file.  This function expects image paths to be relative to the text file path
        /// </summary>
        /// <param name="image_config_txt_full_path"></param>
        /// <returns></returns>
        public static ColorMapWithImages CreateFromConfigFile(string image_config_txt_full_path)
        {
            string config = System.IO.File.ReadAllText(image_config_txt_full_path);
            return ColorMapWithImages.Create(config, System.IO.Path.GetDirectoryName(image_config_txt_full_path));
        }

        public void AddColorMapImage(int SectionNumber, ColorMapImageData data)
        {
            if (ColorMapTable.TryGetValue(SectionNumber, out var colormap))
            {
                Debug.Assert(!colormap.Contains(data));
                colormap.Add(data);
            }
            else
            {
                ColorMapTable.Add(SectionNumber, new List<ColorMapImageData>(new ColorMapImageData[] { data }));
            }

            return;
        }

        public IList<int> SectionNumbers => ColorMapTable.Keys;

        public Color GetColor(double X, double Y, int Z)
        {
            if (!ColorMapTable.TryGetValue(Z, out var colormapimages))
                return Color.Empty;

            List<Color> colors = new List<Color>(colormapimages.Count);

            for (int i = 0; i < colormapimages.Count; i++)
            {
                Color color = colormapimages[i].GetColor(X, Y);
                if (color != Color.Empty)
                {
                    colors.Add(color);
                }
            }

            if (colors.Count == 0)
                return Color.Empty;

            //Average the colors together
            return AverageColors(colors);
        }

        /// <summary>
        /// Get average color for all locations based on position
        /// </summary>
        /// <param name="locations"></param>
        /// <returns></returns>
        public Color GetColor(ICollection<ILocation> locations)
        {
            //Remove locations with Z values not in our lookup list to save a lot of time
            List<GridVector3> listPoints = locations.Where(l => ColorMapTable.ContainsKey((int)l.UnscaledZ)).ToList().ConvertAll(loc => loc.Geometry.Centroid().ToGridVector3(loc.UnscaledZ));

            return GetColor(listPoints);
        }

        /// <summary>
        /// Get average color for all locations based on position
        /// </summary>
        /// <param name="locations"></param>
        /// <returns></returns>
        public Color GetColor(ICollection<GridVector3> points)
        {
            if (points.Count == 0)
                return Color.Empty;

            IEnumerable<GridVector3> filteredPoints = points.Where(p => ColorMapTable.ContainsKey((int)p.Z));
            IList<Color> colors = filteredPoints.Select<GridVector3, Color>(p => GetColor(p.X, p.Y, (int)p.Z)).ToList();
            return AverageColors(colors);
        }

        public Color AverageColors(ICollection<Color> colors)
        {
            if (colors.Count == 0)
                return Color.Empty;

            if (colors.Count == 1)
                return colors.First();

            int R = 0;
            int G = 0;
            int B = 0;
            int A = 0;

            foreach (Color c in colors)
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
        public static ColorMapWithImages Create(string config_data, string ImageDir)
        {
            ColorMapWithImages mapping = new ColorMapWithImages();

            SortedList<int, List<ColorMapImageData>> ColorMapList = new SortedList<int, List<ColorMapImageData>>();
            string[] lines = config_data.Split(new char[] { '\n' });
            foreach (string line in lines)
            {
                try
                {
                    ColorMapImageData colormapimagedata = ParseConfigLine(line, ImageDir);
                    mapping.AddColorMapImage(colormapimagedata.SectionNumber, colormapimagedata);
                }
                catch (System.FormatException)
                {
                    System.Diagnostics.Trace.WriteLine("Unable to parse Color Map Config line: " + line);
                }
                catch (System.ArgumentException e)
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

            if (ConfigStringHelper.StartsWithComment(line))
                throw new ArgumentException("Skipping comment");

            string[] parts = line.Split();
            if (parts.Length < 10)
                throw new ArgumentException("Not enough arguments in line:\n" + line);

            if (!ConfigStringHelper.StartsWithNumber(parts[0]))
                throw new FormatException("Attempting to parse header row");

            int SectionNumber = System.Convert.ToInt32(parts[0]);

            ColorImageOffset offset = new ColorImageOffset(System.Convert.ToInt32(parts[1]), System.Convert.ToInt32(parts[2]));

            AxisUnits X_Scale = new AxisUnits(System.Convert.ToDouble(parts[3]), "");
            AxisUnits Y_Scale = new AxisUnits(System.Convert.ToDouble(parts[4]), "");
            AxisUnits Z_Scale = new AxisUnits(0, "");

            Scale scale = new Scale(X_Scale, Y_Scale, Z_Scale);

            ColorScalars scalars = new ColorScalars(ConfigStringHelper.NormalizedStringToByte(parts[5]),
                ConfigStringHelper.NormalizedStringToByte(parts[6]),
                ConfigStringHelper.NormalizedStringToByte(parts[7]),
                ConfigStringHelper.NormalizedStringToByte(parts[8]));

            string Filename = parts[9];
            if (ImageDir != null)
            {
                Filename = System.IO.Path.Combine(ImageDir, Filename);
            }

            if (!System.IO.File.Exists(Filename))
            {
                throw new ArgumentException("File specified in ColorMap config file does not exist: " + Filename);
            }

            using (System.IO.Stream stream = System.IO.File.OpenRead(Filename))
            {
                if (stream != null)
                {
                    ColorMapImageData image = new ColorMapImageData(stream, SectionNumber, scale, scalars, offset);
                    return image;
                }
            }

            throw new ArgumentException("Could not open file: " + Filename);
        }
    }
}