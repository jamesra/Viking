using System;

namespace Viking.AnnotationServiceTypes.Interfaces
{
    public interface IPermittedStructureLink : IEquatable<IPermittedStructureLink>
    {
        ulong SourceTypeID { get; }
        ulong TargetTypeID { get; }
        bool Directional { get; }
    }
}
