using System;
using System.Collections;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows.Data;
using WebAnnotationModel;
using System.Collections.Specialized;

namespace WebAnnotation.WPF.Converters
{
    /// <summary>
    /// Converts a collection of IDs into StructureObj
    /// </summary>
    public class StructureIDsToStructureObjsConverter : IValueConverter
    {
        public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
        {
            if ((value is long || value is int || value is ulong || value is uint || 
                 value is string || value is StringCollection || value is IEnumerable<long> ||
                 value is IEnumerable<int>) == false)
                throw new NotImplementedException(string.Format("StructureIDToStructureObjConverter expects a StructureID, but got {0}", value.ToString()));

            List<long> IDs = new List<long>(); 
            if (value is StringCollection)
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
                return Store.StructureTypes.GetObjectsByIDs(((IEnumerable<long>)IDs).ToArray(), true);
            }
            else if (value is IEnumerable<int> )
            {
                return Store.StructureTypes.GetObjectsByIDs(((IEnumerable<int>)IDs).Select(i=> (long)i).ToArray(), true);
            }
            else
            {
                long ID;
                try
                {
                    ID = System.Convert.ToInt64(value);
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
            if (value is StructureTypeObj == false || value is IEnumerable<StructureTypeObj>)
                throw new NotImplementedException(string.Format("StructureIDToStructureObjConverter convert back expects a StructureObj, but got {0}", value.ToString()));

            if (value is StructureTypeObj)
            {
                StructureTypeObj obj = (StructureTypeObj)value;
                return obj.ID;
            }
            else if(value is IEnumerable<StructureTypeObj>)
            {
                var values = (IEnumerable < StructureTypeObj >) value;
                List<long> IDs = new List<long>(); 
                foreach(var obj in values)
                {
                    IDs.Add(obj.ID);
                }

                return IDs;
            }

            throw new NotImplementedException(string.Format("StructureIDToStructureObjConverter convert back expects a StructureObj, but got {0}", value.ToString()));
        }
    }
}
