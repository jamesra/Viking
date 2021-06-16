using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace AnnotationService.Interfaces
{
    interface IDataObjectWithKey<T>
        where T : IComparable<T>
    {
        Int64 ID { get; set; }
    }

    interface IDataObjectWithParent<T> : IDataObjectWithKey<T>
        where T : struct, IComparable<T>
    {
        Nullable<T> ParentID { get; set; }
    }
}
