using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Viking.AnnotationServiceTypes.Interfaces
{
    /// <summary>
    /// Generic interface to a database row with a key of the given type
    /// </summary>
    interface IDataObjectWithKey<T>
         where T : struct, IComparable<T>, IEquatable<T>
    {
        T ID { get; set; }
    }
}
