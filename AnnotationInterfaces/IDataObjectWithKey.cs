using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.ComponentModel;

namespace Viking.AnnotationServiceTypes.Interfaces
{
    /// <summary>
    /// Generic interface to a database row with a key of the given type
    /// </summary>
    public interface IDataObjectWithKey<T>
         where T : IComparable<T>, IEquatable<T>
    {
        T ID { get; set; }
    }
}
