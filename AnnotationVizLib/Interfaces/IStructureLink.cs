using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace AnnotationVizLib
{
    public interface IStructureLink
    {
        ulong SourceID { get; }

        ulong TargetID { get; }
    }
}
