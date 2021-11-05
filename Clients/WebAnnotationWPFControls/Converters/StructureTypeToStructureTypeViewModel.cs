using System;
using System.Globalization;
using System.Windows.Data;
using WebAnnotationModel;
using WebAnnotationModel.Objects;

namespace WebAnnotation.WPF.Converters
{
    internal class StructureTypeObjToStructureObjViewModelConverters : IValueConverter
    {
        public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
        {
            if (value is null)
                return null;

            StructureTypeObj typeObj = value as StructureTypeObj;
            if (typeObj == null && (value is long || value is int || value is ulong || value is uint))
            {
                long ID = System.Convert.ToInt64(value);
                typeObj = Store.StructureTypes.GetObjectByID(ID, true);
            }

            if (typeObj == null)
                throw new ArgumentException(string.Format("Expected a StructureTypeObj, got {0}", value));

            return new Annotation.ViewModels.StructureTypeObjViewModel(typeObj);
        }

        public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
        {
            if (value is null)
                return null;

            if (false == value is Annotation.ViewModels.PermittedStructureLinkViewModel)
                throw new ArgumentException(string.Format("Expected a Annotation.ViewModels.PermittedStructureLinkViewModel, got {0}", value));

            if (targetType == typeof(StructureTypeObj))
                return ((Annotation.ViewModels.PermittedStructureLinkViewModel)value).Model;
            else if (targetType == typeof(long))
                return ((Annotation.ViewModels.PermittedStructureLinkViewModel)value).Model.ID;

            throw new ArgumentException(string.Format("Unknown type requested: {0}", targetType));

        }
    }
}
