using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using AnnotationService.Types;

namespace Viking.AnnotationServiceTypes.Interfaces
{
    interface IChangeAction
    {
        DBACTION DBAction { get; set; }
    }
}
