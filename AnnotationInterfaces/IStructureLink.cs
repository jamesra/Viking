using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Annotation.Interfaces
{
    public interface IStructureLink : IEquatable<IStructureLink>
    {
        ulong SourceID { get; }

        ulong TargetID { get; }

        bool Directional { get; }
    }
}
