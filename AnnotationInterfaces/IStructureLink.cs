using System;

namespace Annotation.Interfaces
{
    public interface IStructureLink : IEquatable<IStructureLink>
    {
        ulong SourceID { get; }

        ulong TargetID { get; }

        bool Directional { get; }
    }
}
