using Viking.AnnotationServiceTypes.Interfaces;
using System;
using System.Collections;
using System.Collections.Generic;
using System.Collections.Specialized;
using System.Globalization;
using System.Linq;
using System.Windows.Data;
using WebAnnotationModel;

namespace WebAnnotation.WPF.Converters
{
    /// <summary>
    /// Converts structure type IDs (a scalar ID, or a collection of IDs) into structure type objects from the store.
    /// Structure types, collections of them and any other collection pass through unchanged, so the structure type
    /// forms can bind it to IDs and to already resolved types alike.
    /// </summary>
    public class StructureTypeIDsToStructureTypeObjsConverter : IValueConverter
    {
        public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
        {
            if (value is null)
                return null;

            if (value.GetType() == targetType)
                return value;

            List<long> IDs = ToStructureTypeIDs(value);
            if (IDs is not null)
                return Store.StructureTypes.GetObjectsByIDs(IDs, true);

            if (value is IStructureTypeReadOnly or IEnumerable)
                return value;

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
                return Store.StructureTypes.GetObjectByID(ID, true);
            }
            catch (ArgumentException)
            {
                return string.Format("Invalid structure ID: {0}", value.ToString());
            }
        }

        /// <summary>
        /// Reads a collection of structure type IDs: strings (parsed in the current culture), <c>long</c>,
        /// <c>ulong</c> (reinterpreted, as the store keys are <c>long</c>) or <c>int</c>. Checked in that order, so a
        /// <c>ulong[]</c>, which the runtime also reports as <c>IEnumerable&lt;long&gt;</c>, is read as longs.
        /// </summary>
        /// <returns>The IDs, or null when <paramref name="value"/> is not one of those collections.</returns>
        /// <exception cref="FormatException">A string in a <see cref="StringCollection"/> is not an integer.</exception>
        internal static List<long> ToStructureTypeIDs(object value) => value switch
        {
            StringCollection strings => [.. strings.Cast<string>().Select(s => System.Convert.ToInt64(s))],
            IEnumerable<long> ids => [.. ids],
            IEnumerable<ulong> ids => [.. ids.Select(i => (long)i)],
            IEnumerable<int> ids => [.. ids.Select(i => (long)i)],
            _ => null
        };

        /// <summary>
        /// Returns the ID of a single structure type. Throws <see cref="NotImplementedException"/> for anything else,
        /// including a collection of structure types.
        /// </summary>
        public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
        {
            if (value is IStructureTypeReadOnly t && value is not IEnumerable<IStructureTypeReadOnly>)
                return t.ID;

            throw new NotImplementedException(string.Format("StructureIDToStructureObjConverter ConvertBack back expects a StructureObj, but got {0}", value.ToString()));
        }
    }
}
