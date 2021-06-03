using System;
using System.Windows;
using System.Windows.Data;

namespace Viking.VolumeView
{
    class RectToCenterPointConverter : IValueConverter
    {
        #region IValueConverter Members

        object IValueConverter.Convert(object value, Type targetType, object parameter, System.Globalization.CultureInfo culture)
        {
            if (value.GetType() != typeof(Rect))
                throw new InvalidOperationException("RectToCenterPointConverter: target type must be a Rect!");

            Rect rect = (Rect)value;

            Point p = new Point(rect.X + (rect.Width / 2), rect.Y + (rect.Height / 2));

            if (parameter as string != null)
            {
                string strParameter = ((string)parameter).ToLower();
                if (strParameter == "x")
                    return p.X.ToString();
                if (strParameter == "y")
                    return p.Y.ToString(); 
            }

            return p; 
        }

        object IValueConverter.ConvertBack(object value, Type targetType, object parameter, System.Globalization.CultureInfo culture)
        {
            throw new NotImplementedException();
        }

        #endregion
    }
}
