using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Viking.AnnotationServiceTypes.Interfaces
{
    public enum DBACTION : Int32
    {
        NONE = 0,
        INSERT = 1,
        UPDATE = 2,
        DELETE = 3
    };

    interface IChangeAction
    {
       DBACTION DBAction { get; set; }
    }
}
