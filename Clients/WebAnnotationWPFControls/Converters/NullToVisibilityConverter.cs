using System;
using System.Globalization;
using System.Windows.Data;

namespace WebAnnotation.WPF.Converters
{
    /// <summary>
    /// Converts a null object to Hidden Visibility.  Otherwise Visible
    /// </summary>
    class NullToVisibilityConverter : IValueConverter
    {
        public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
        {
            return value == null ? System.Windows.Visibility.Hidden : System.Windows.Visibility.Visible;
        }

        public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
        {
            throw new NotImplementedException();
        }
    }
}
