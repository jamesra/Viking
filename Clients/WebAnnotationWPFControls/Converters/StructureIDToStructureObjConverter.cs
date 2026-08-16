using Viking.AnnotationServiceTypes.Interfaces;
using System;
using System.Collections;
using System.Collections.Generic;
using System.Collections.Specialized;
using System.Globalization;
using System.Linq;
using System.Threading;
using System.Windows.Data;
using WebAnnotation.WPF.MockData;
using WebAnnotationModel;
using WebAnnotationModel.Objects;

namespace WebAnnotation.WPF.Converters
{
    /// <summary>
    /// Converts a collection of IDs into StructureObj
    /// </summary>
    public class StructureTypeIDsToStructureTypeObjsConverter : IValueConverter
    {
        public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
        {
            /* if ((value is long || value is int || value is ulong || value is uint || 
                  value is string || value is StringCollection || value is IEnumerable<long> ||
                  value is IEnumerable<int> || value is IStructureType || value is IEnumerable<IStructureType> || value is MockData.MockStructureTypes) == false)
                 throw new NotImplementedException(string.Format("StructureIDToStructureObjConverter expects a StructureID, but got {0}", value.ToString()));
                 */

            if (value is null)
                return null;

            if (value.GetType() == targetType)
                return value;

            List<long> IDs = [];
            if (value is IStructureTypeReadOnly s)
            {
                return s;
            }
            else if (value is IEnumerable<IStructureTypeReadOnly>)
            {
                return value as IEnumerable<IStructureTypeReadOnly>;
            }
            else if (value is IEnumerable<MockStructureType>)
            {
                return value as IEnumerable<MockStructureType>;
            }
            else if (value is MockStructureTypes)
            {
                return value as MockStructureTypes;
            }
            else if (value is MockStructureType)
            {
                return value as MockStructureType;
            }
            else if (value is StringCollection)
            {
                StringCollection collection = value as StringCollection;

                foreach (string val in collection)
                {
                    long ID;
                    try
                    {
                        ID = System.Convert.ToInt64(val);
                        IDs.Add(ID);
                    }
                    catch (ArgumentException)
                    {
                        return string.Format("Invalid structure ID: {0}", value.ToString());
                    }
                }

                Store.StructureTypes.TryGetObjectsByIDs(IDs, out var found, out _);
                return found;
            }
            else if (value is IEnumerable<long>)
            {
                IEnumerable<long> values = value as IEnumerable<long>;
                Store.StructureTypes.TryGetObjectsByIDs([.. values], out var foundLong, out _);
                return foundLong;
            }
            else if (value is IEnumerable<ulong>)
            {
                IEnumerable<ulong> values = value as IEnumerable<ulong>;
                Store.StructureTypes.TryGetObjectsByIDs([.. values.Select(i => (long)i)], out var foundUlong, out _);
                return foundUlong;
            }
            else if (value is IEnumerable<int> intIds)
            {
                Store.StructureTypes.TryGetObjectsByIDs([.. intIds.Select(i => (long)i)], out var foundInt, out _);
                return foundInt;
            }
            else if (value is IEnumerable<IStructureTypeReadOnly>)
            {
                return value;
            }
            else if (value is IEnumerable)
            {
                return value;
                /*
                try
                {
                    List<IStructureType> listTypes = new List<IStructureType>();
                    foreach(object item in (IEnumerable)value)
                    {
                        IStructureType obj = item as IStructureType;
                        if (obj != null)
                            listTypes.Add(obj);
                    }

                    return listTypes;
                }
                catch
                { 
                    throw new NotImplementedException(string.Format("StructureIDToStructureObjConverter Convert got unknown IEnumerable {0}", value.ToString()));
                }*/
            }
            else if (value is StructureTypeObj)
            {
                return value;
            }
            else
            {
                long ID;
                try
                {
                    ID = System.Convert.ToInt64(value);
                }
                catch (ArgumentException e)
                {
                    throw new NotImplementedException(string.Format("StructureIDToStructureObjConverter Convert expects a StructureID, but got {0}\n{1}", value.ToString(), e));
                }

                try
                {
                    Store.StructureTypes.TryGetObjectByID(ID, out var type);
                    return type;
                }
                catch (ArgumentException)
                {
                    return string.Format("Invalid structure ID: {0}", value.ToString());
                }
            }
        }

        public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
        {
            if (value is IStructureTypeReadOnly == false || value is IEnumerable<IStructureTypeReadOnly>)
                throw new NotImplementedException(string.Format("StructureIDToStructureObjConverter ConvertBack back expects a StructureObj, but got {0}", value.ToString()));

            if (value is IStructureTypeReadOnly t)
            {
                return t.ID;
            }
            else if (value is IEnumerable<StructureTypeObj>)
            {
                IEnumerable<StructureTypeObj> values = (IEnumerable<StructureTypeObj>)value;
                List<ulong> IDs = [];
                foreach (var obj in values)
                {
                    IDs.Add((ulong)obj.ID);
                }

                return IDs;
            }
            else if (value is IEnumerable<IStructureTypeReadOnly> values)
            {
                List<ulong> IDs = [];
                foreach (var obj in values)
                {
                    IDs.Add((ulong)obj.ID);
                }

                return IDs;
            }

            throw new NotImplementedException($"StructureIDToStructureObjConverter ConvertBack expects a StructureObj, but got {value}");
        }
    }
}
