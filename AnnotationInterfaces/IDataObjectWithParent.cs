using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Viking.AnnotationServiceTypes.Interfaces
{
    interface IDataObjectWithParent<T> : IDataObjectWithKey<T>
        where T : struct, IComparable<T>, IEquatable<T>
    {
        T? ParentID { get; set; }
    }
}
