using Viking.AnnotationServiceTypes.Interfaces;
using System;
using System.Globalization;
using System.Windows.Data;
using WebAnnotationModel;
using WebAnnotationModel.Objects;

namespace WebAnnotation.WPF.Converters
{
    internal class PermittedSourceStructureLinkTypeToStructureTypeConverters : IValueConverter
    {
        public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
        {
            IPermittedStructureLinkKey link = value as IPermittedStructureLinkKey;
            if (link == null)
                throw new ArgumentException(string.Format("Expected an IPermittedStructureLink, got {0}", value));

            return Store.StructureTypes.GetObjectByID((long)link.SourceTypeID);
        }

        public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
        {
            throw new NotImplementedException();
        }
    }

    internal class PermittedTargetStructureLinkTypeToStructureTypeConverters : IValueConverter
    {
        public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
        {
            IPermittedStructureLinkKey link = value as IPermittedStructureLinkKey;
            if (link == null)
                throw new ArgumentException(string.Format("Expected an IPermittedStructureLink, got {0}", value));

            return Store.StructureTypes.GetObjectByID((long)link.TargetTypeID);
        }

        public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
        {
            throw new NotImplementedException();
        }
    }

    internal class PermittedBidirectionalStructureLinkTypeToStructureTypeConverters : IValueConverter
    {
        public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
        {
            IPermittedStructureLinkKey link = value as IPermittedStructureLinkKey;
            if (link == null)
                throw new ArgumentException(string.Format("Expected an IPermittedStructureLink, got {0}", value));

            ulong myTypeID = System.Convert.ToUInt64(parameter);
            ulong otherTypeID = link.SourceTypeID == myTypeID ? link.TargetTypeID : link.SourceTypeID;
            return Store.StructureTypes.GetObjectByID((long)otherTypeID);
        }

        public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
        {
            throw new NotImplementedException();
        }
    }

    internal class StructureTypeObjToPermittedStructureLinksViewModelConverters : IValueConverter
    {
        public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
        {
            if (value is null)
                return null;

            StructureTypeObj typeObj = value as StructureTypeObj;
            if(typeObj == null && (value is long || value is int || value is ulong || value is uint))
            {
                long ID = System.Convert.ToInt64(value);
                typeObj = Store.StructureTypes.GetObjectByID(ID,true);
            }

            if(typeObj == null)
                throw new ArgumentException(string.Format("Expected a StructureTypeObj, got {0}", value));

            return new Annotation.ViewModels.PermittedStructureLinkViewModel(typeObj);
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
