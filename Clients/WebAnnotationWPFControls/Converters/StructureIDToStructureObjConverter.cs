using Annotation.Interfaces;
using System;
using System.Collections;
using System.Collections.Generic;
using System.Collections.Specialized;
using System.Globalization;
using System.Linq;
using System.Windows.Data;
using WebAnnotation.WPF.MockData;
using WebAnnotationModel;

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

            if (value == null)
                return null;

            if (value.GetType() == targetType)
                return value; 

            List<long> IDs = new List<long>(); 
            if(value is IStructureType s)
            {
                return s; 
            }
            else if(value is IEnumerable<IStructureType>)
            { 
                return value as IEnumerable<IStructureType>;
            }
            else if(value is IEnumerable<MockStructureType>)
            {
                return value as IEnumerable<MockStructureType>;
            }
            else if (value is MockStructureTypes)
            {
                return value as MockStructureTypes;
            }
            else if (value is  MockStructureType)
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

                return Store.StructureTypes.GetObjectsByIDs(IDs, true);
            }
            else if (value is IEnumerable<long>)
            {
                IEnumerable<long> values = value as IEnumerable<long>;
                return Store.StructureTypes.GetObjectsByIDs(values.ToArray(), true);
            }
            else if (value is IEnumerable<ulong>)
            {
                IEnumerable<ulong> values = value as IEnumerable<ulong>;
                return Store.StructureTypes.GetObjectsByIDs(values.Select(i => (long)i).ToArray(), true);
            }
            else if (value is IEnumerable<int> )
            {
                return Store.StructureTypes.GetObjectsByIDs(((IEnumerable<int>)IDs).Select(i=> (long)i).ToArray(), true);
            }
            else if (value is IEnumerable<IStructureType>)
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
            else if(value is StructureTypeObj)
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
                catch(ArgumentException e)
                {
                    throw new NotImplementedException(string.Format("StructureIDToStructureObjConverter Convert expects a StructureID, but got {0}\n{1}", value.ToString(), e)); 
                }

                try
                { 
                    return Store.StructureTypes.GetObjectByID(ID, true);
                }
                catch (ArgumentException)
                {
                    return string.Format("Invalid structure ID: {0}", value.ToString());
                }
            }
        }

        public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
        {
            if (value is IStructureType == false || value is IEnumerable<IStructureType>)
                throw new NotImplementedException(string.Format("StructureIDToStructureObjConverter ConvertBack back expects a StructureObj, but got {0}", value.ToString()));

            if (value is IStructureType t)
            { 
                return t.ID;
            }
            else if(value is IEnumerable<StructureTypeObj>)
            {
                var values = (IEnumerable < StructureTypeObj >) value;
                List<ulong> IDs = new List<ulong>(); 
                foreach(var obj in values)
                {
                    IDs.Add((ulong)obj.ID);
                }

                return IDs;
            }
            else if (value is IEnumerable<IStructureType>)
            {
                var values = (IEnumerable<IStructureType>)value;
                List<ulong> IDs = new List<ulong>();
                foreach (var obj in values)
                {
                    IDs.Add((ulong)obj.ID);
                }

                return IDs;
            }

            throw new NotImplementedException(string.Format("StructureIDToStructureObjConverter ConvertBack expects a StructureObj, but got {0}", value.ToString()));
        }
    }
}
