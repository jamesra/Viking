using System;
using System.Globalization;
using System.Windows.Data;
using System.Windows.Media;

namespace WebAnnotation.WPF.Converters
{
    /// <summary>
    /// Converts a collection of IDs into StructureObj
    /// </summary>
    public class IntToBrushConverter : IValueConverter
    {
        public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
        {
            uint val;

            if (value is string)
            {
                string strVal = (string)value;
                val = System.Convert.ToUInt32(value);
            }
            else if (value is int)
            {
                val = (uint)(int)value;
            }  
            else if (value is uint)
            {
                val = (uint)value;
            }
            else{
                throw new NotImplementedException(string.Format("IntToBrush expects an int, but got {0}", value.ToString()));
            }

            byte a = (byte)((val & 0xFF000000) >> 24);
            byte r = (byte)((val & 0x00FF0000) >> 16);
            byte g = (byte)((val & 0x0000FF00) >> 8);
            byte b = (byte)((val & 0x000000FF) );

            if (targetType.IsAssignableFrom(typeof(System.Windows.Media.Brush)))
            {
                var brush = new SolidColorBrush(Color.FromArgb(a, r, g, b));
                return brush;
            }
            else if(targetType.IsAssignableFrom(typeof(System.Windows.Media.Color)))
            {
                return System.Windows.Media.Color.FromArgb(a, r, g, b);
            }

            throw new NotImplementedException($"IntToBrushConverter cannot conver {value} to {targetType}");
        }

        public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
        {
            if(value is System.Windows.Media.Color mediaColor)
            {
                uint a = ((uint)mediaColor.A) << 24;
                uint r = ((uint)mediaColor.R) << 16;
                uint g = ((uint)mediaColor.G) << 8;
                uint b = ((uint)mediaColor.B);

                uint output = a + r + g + b;

                if(targetType.IsAssignableFrom(typeof(System.Int32)))
                {
                    return (int)output;
                }
                else if(targetType.IsAssignableFrom(typeof(System.UInt32)))
                {
                    return output;
                }
            }

            throw new NotImplementedException($"IntToBrush not implemented for ConvertBack {value} to {targetType}");
        }
    }
}