using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Annotation.Interfaces
{
    public interface IPermittedStructureLink : IEquatable<IPermittedStructureLink>
    {
        ulong SourceTypeID { get; }
        ulong TargetTypeID { get; }
        bool Directional { get; }
    }
}
