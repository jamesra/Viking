using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace AnnotationVizLib
{
    public interface IStructureLink : IEquatable<IStructureLink>
    {
        ulong SourceID { get; }

        ulong TargetID { get; }

        bool Directional { get; }
    }
}
