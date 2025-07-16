using System;

namespace Viking.AnnotationServiceTypes.Interfaces
{
    public interface IPermittedStructureLinkReadOnly : IEquatable<IPermittedStructureLinkReadOnly>
    {
        ulong SourceTypeID { get; }
        ulong TargetTypeID { get; }
        bool Directional { get; }
    }
}