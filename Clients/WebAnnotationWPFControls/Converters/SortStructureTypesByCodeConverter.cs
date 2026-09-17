using System;
using System.Collections;
using System.Globalization;
using System.Linq;
using System.Windows.Data;
using Viking.AnnotationServiceTypes.Interfaces;

namespace WebAnnotation.WPF.Converters
{
    /// <summary>
    /// Sorts a structure-type collection by Code. Used for tree children; roots are sorted in StructureTypeTree.
    /// </summary>
    public class SortStructureTypesByCodeConverter : IValueConverter
    {
        public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
        {
            if (value is not IEnumerable items)
                return value;

            return items.Cast<IStructureTypeReadOnly>()
                .OrderBy(t => t.Code ?? string.Empty, StringComparer.OrdinalIgnoreCase)
                .ToList();
        }

        public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture) =>
            throw new NotSupportedException();
    }
}
