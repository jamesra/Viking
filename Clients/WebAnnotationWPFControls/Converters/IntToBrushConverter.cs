using System;
using System.Collections;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows.Data;
using WebAnnotationModel;
using System.Collections.Specialized;
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
            else
            {
                if ((value is int || value is uint) == false)
                    throw new NotImplementedException(string.Format("IntToBrush expects an int, but got {0}", value.ToString()));

                val = (uint)value;
            }

            byte a = (byte)((val & 0xFF000000) >> 24);
            byte r = (byte)((val & 0x00FF0000) >> 16);
            byte g = (byte)((val & 0x0000FF00) >> 8);
            byte b = (byte)((val & 0x000000FF) );
            SolidColorBrush brush = new SolidColorBrush(Color.FromArgb(a, r, g, b));

            return brush;
        }

        public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
        {
            throw new NotImplementedException();
        }
    }
}