using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Drawing;
using System.Linq;

namespace AnnotationVizLib
{
    /// <summary>
    /// Return a color based on a key value
    /// </summary>
    public class ColorMapWithLong
    {
        readonly SortedList<long, Color> _colorMapTable = [];

        private static long ConvertKey(string str) => System.Convert.ToInt64(str);

        public void Add(long key, Color color) => _colorMapTable.Add(key, color);

        public bool ContainsKey(long key) => this._colorMapTable.ContainsKey(key);

        public Color GetColor(long key) => this._colorMapTable[key];

        public Color this[long key] => this._colorMapTable[key];

        public static ColorMapWithLong CreateFromConfigFile(string config_txt_full_path)
        {
            string full_path = System.IO.Path.GetFullPath(config_txt_full_path);
            if (!System.IO.File.Exists(full_path))
            {
                throw new System.IO.FileNotFoundException("Color mapping file not found " + full_path);
            }

            string config = System.IO.File.ReadAllText(full_path);
            return ColorMapWithLong.Create(config);
        }

        public static ColorMapWithLong Create(string config_data)
        {
            ColorMapWithLong mapping = new();

            string[] lines = config_data.Split(['\n']);
            foreach (string line in lines)
            {
                string trim_line = line.Trim();
                if (!trim_line.Any())
                    continue;

                try
                {
                    Color color = ColorMapWithLong.TryParseConfigLine(trim_line, out long Key);

                    if (color != Color.Empty)
                        mapping.Add(Key, color);

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

        private static Color TryParseConfigLine(string line, out long Key)
        {
            if (ConfigStringHelper.StartsWithComment(line))
                throw new ArgumentException("Skipping comment");

            if (!ConfigStringHelper.StartsWithNumber(line))
                throw new FormatException("Attempting to parse header row");

            line = line.Trim();
            string[] parts = line.Split();

            if (parts.Length < 4)
                throw new ArgumentException("Not enough parameters in line:\n" + line);

            Key = ConvertKey(parts[0]);

            try
            {
                Color color = Color.FromArgb(ConfigStringHelper.NormalizedStringToByte(parts[4]),
                    ConfigStringHelper.NormalizedStringToByte(parts[1]),
                    ConfigStringHelper.NormalizedStringToByte(parts[2]),
                    ConfigStringHelper.NormalizedStringToByte(parts[3]));

                return color;
            }
            catch (FormatException e)
            {
                throw new FormatException("Unable to parse line:\n" + line, e);
            }
        }
    }
}