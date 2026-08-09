using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Viking.AnnotationServiceTypes.Interfaces
{  
    public interface IChangeAction
    {
       DBACTION DBAction { get; set; }
    }
}
