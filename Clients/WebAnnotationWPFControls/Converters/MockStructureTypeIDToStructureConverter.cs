using Annotation.Interfaces;
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
            if (value == null)
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
            else if(value is IEnumerable)
            {
                List<MockStructureType> listTypes = new List<MockStructureType>();
                foreach(var obj in (IEnumerable)value)
                {
                    var result = this.Convert(obj, targetType, parameter, culture) as MockStructureType;
                    if (result != null)
                        listTypes.Add(result);
                     
                }

                return listTypes;
            }

            throw new ArgumentException(string.Format("No structure type with ID {0} in mock data", value));
        }

        public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
        {
            if (value == null)
                return null;

            
            var structType = value as IStructureType;
            if(structType == null)
            {
                throw new ArgumentException(string.Format("Expected object implementing IStructureType, got {0}", value));
       
            }

            return structType.ID;
        }
    }
}
