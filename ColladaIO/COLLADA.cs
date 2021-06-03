
using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;
using System.Xml;
using System.Xml.Serialization;


namespace ColladaIO
{

    /// <summary>
    ///   Extend COLLADA class to provide convertion helpers
    ///   https://code4k.blogspot.com/2010/08/import-and-export-3d-collada-files-with.html
    /// </summary>
    public partial class COLLADA
    {
        private static Regex regex = new Regex(@"\s+");

        public static string ConvertFromArray<T>(IList<T> array)
        {
            if (array == null)
                return null;

            StringBuilder text = new StringBuilder();
            if (typeof(T) == typeof(double))
            {
                // If type is double, then use a plain ToString with no exponent
                for (int i = 0; i < array.Count; i++)
                {
                    object value1 = array[i];
                    double value = (double)value1;
                    text.Append(
                        value.ToString(
                            "0.000000",
                            NumberFormatInfo.InvariantInfo));
                    if ((i + 1) < array.Count)
                        text.Append(" ");
                }
            }
            else if(typeof(T) == typeof(Byte))
            {
                return Encoding.UTF8.GetString(((IList<Byte>)array).ToArray());
            }
            else
            {
                for (int i = 0; i < array.Count; i++)
                {
                    text.Append(Convert.ToString(array[i], NumberFormatInfo.InvariantInfo));
                    if ((i + 1) < array.Count)
                        text.Append(" ");
                }
            }
            return text.ToString();
        }

        internal static string[] ConvertStringArray(string arrayStr)
        {
            string[] elements = regex.Split(arrayStr.Trim());
            string[] ret = new string[elements.Length];
            for (int i = 0; i < ret.Length; i++)
                ret[i] = elements[i];
            return ret;
        }

        internal static int[] ConvertIntArray(string arrayStr)
        {
            string[] elements = regex.Split(arrayStr.Trim());
            int[] ret = new int[elements.Length];
            for (int i = 0; i < ret.Length; i++)
                ret[i] = int.Parse(elements[i]);
            return ret;
        }

        internal static long[] ConvertLongIntArray(string arrayStr)
        {
            string[] elements = regex.Split(arrayStr.Trim());
            long[] ret = new long[elements.Length];
            for (long i = 0; i < ret.Length; i++)
                ret[i] = long.Parse(elements[i]);
            return ret;
        }

        internal static ulong[] ConvertULongArray(string arrayStr)
        {
            string[] elements = regex.Split(arrayStr.Trim());
            ulong[] ret = new ulong[elements.Length];
            for (long i = 0; i < ret.Length; i++)
                ret[i] = ulong.Parse(elements[i]);
            return ret;
        }

        internal static double[] ConvertDoubleArray(string arrayStr)
        {
            string[] elements = regex.Split(arrayStr.Trim());
            double[] ret = new double[elements.Length];
            try
            {
                for (int i = 0; i < ret.Length; i++)
                    ret[i] = double.Parse(elements[i], NumberStyles.Float, CultureInfo.InvariantCulture);
            }
            catch (Exception ex)
            {
                Console.WriteLine(ex);
            }
            return ret;
        }

        internal static bool[] ConvertBoolArray(string arrayStr)
        {
            string[] elements = regex.Split(arrayStr.Trim());
            bool[] ret = new bool[elements.Length];
            for (int i = 0; i < ret.Length; i++)
                ret[i] = bool.Parse(elements[i]);
            return ret;
        }

        internal static byte[] ConvertByteArray(string arrayStr)
        {
            return Encoding.UTF8.GetBytes(arrayStr);
        }
        
        public static COLLADA Load(string fileName)
        {
            FileStream stream = new FileStream(fileName, FileMode.Open);
            COLLADA result;
            try
            {
                result = Load(stream);
            }
            finally
            {
                stream.Close();
            }
            return result;
        }

        public static COLLADA Load(Stream stream)
        {
            StreamReader str = new StreamReader(stream);
            XmlSerializer xSerializer = new XmlSerializer(typeof(COLLADA));

            return (COLLADA)xSerializer.Deserialize(str);
        }

        public void Save(string Filename)
        {
            if (System.IO.File.Exists(Filename))
                System.IO.File.Delete(Filename);

            using (Stream stream = File.Open(Filename, FileMode.Create))
            {
                XmlSerializer serializer = new XmlSerializer(typeof(COLLADA));
                serializer.Serialize(stream, this);
                stream.Flush();
            }
        }

        public void Save(Stream stream)
        {
            using (XmlTextWriter writer = new XmlTextWriter(stream, Encoding.UTF8))
            {
                writer.Formatting = Formatting.Indented;

                XmlSerializer xSerializer = new XmlSerializer(typeof(COLLADA));
                xSerializer.Serialize(writer, this);
            }
        }
    }
}
