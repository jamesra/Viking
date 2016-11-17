using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace AnnotationVizLib
{
    public interface IStructureType
    {
        ulong ID { get; }
        ulong ParentID { get; }
        string Name { get; } 
        string[] Tags { get; }
    }
}
