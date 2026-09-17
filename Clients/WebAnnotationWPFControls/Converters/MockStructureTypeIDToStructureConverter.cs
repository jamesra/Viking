using Viking.AnnotationServiceTypes.Interfaces;
using System;
using System.Collections;
using System.Collections.Generic;
using System.Globalization;
using System.Windows.Data;

namespace WebAnnotation.WPF.MockData
{
    public class MockStructureTypeIDToStructureConverter : IValueConverter
    {
        public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
        {
            if (value is null)
                return null;

            if (value is long || value is int || value is uint || value is ulong)
            {
                ulong ID = System.Convert.ToUInt64(value);

                if (MockData.StructureTypes.ContainsKey(ID) == false)
                {
                    throw new ArgumentException(string.Format("No structure type with ID {0} in mock data", ID));
                }

                return MockData.StructureTypes[ID];
            }
            else if (value is IEnumerable enumerable)
            {
                List<MockStructureType> listTypes = [];
                foreach (var obj in enumerable)
                {
                    if (this.Convert(obj, targetType, parameter, culture) is MockStructureType result)
                        listTypes.Add(result);

                }

                return listTypes;
            }

            throw new ArgumentException(string.Format("No structure type with ID {0} in mock data", value));
        }

        public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
        {
            if (value is null)
                return null;


            if (value is not IStructureTypeReadOnly structType)
            {
                throw new ArgumentException(string.Format("Expected object implementing IStructureType, got {0}", value));

            }

            return structType.ID;
        }
    }
}
