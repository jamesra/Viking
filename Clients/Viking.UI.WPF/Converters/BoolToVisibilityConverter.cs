using System;
using System.Globalization;
using System.Windows;
using System.Windows.Data;

namespace Viking.UI.WPF.Converters
{
    public class BoolToVisibilityConverter : IValueConverter
    {
        public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
        {
            if (value is bool boolValue)
            {
                return boolValue ? Visibility.Visible : Visibility.Collapsed;
            }
            return Visibility.Collapsed;
        }

        public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
        {
            if (value is Visibility visibility)
            {
                return visibility == Visibility.Visible;
            }
            return false;
        }
    }

    public class BoolToFontWeightConverter : IValueConverter
    {
        public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
        {
            if (value is bool boolValue && boolValue)
            {
                return FontWeights.Bold;
            }
            return FontWeights.Normal;
        }

        public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture) => throw new NotImplementedException();
    }

    public class StringToVisibilityConverter : IValueConverter
    {
        public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
        {
            if (value is string stringValue && !string.IsNullOrWhiteSpace(stringValue))
            {
                return Visibility.Visible;
            }
            return Visibility.Collapsed;
        }

        public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture) => throw new NotImplementedException();
    }

    /// <summary>
    /// Converts between int and string for two-way binding to TextBox.Text (e.g. SectionNumberOverlayCount).
    /// On parse failure, returns Binding.DoNothing so the previous value is preserved.
    /// </summary>
    public class Int32Converter : IValueConverter
    {
        public int Minimum { get; set; } = int.MinValue;
        public int Maximum { get; set; } = int.MaxValue;

        public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
        {
            if (value is int i)
            {
                return i.ToString(culture ?? CultureInfo.CurrentCulture);
            }
            return string.Empty;
        }

        public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
        {
            if (value is string s && int.TryParse(s, NumberStyles.Integer, culture ?? CultureInfo.InvariantCulture, out int result))
            {
                int clamped = Math.Max(Minimum, Math.Min(Maximum, result));
                return clamped;
            }
            return Binding.DoNothing;
        }
    }
}

