using System;

namespace Viking.AnnotationServiceTypes.Interfaces
{
    public interface IPermittedStructureLinkReadOnly : IEquatable<IPermittedStructureLinkReadOnly>
    {
        ulong SourceTypeID { get; }
        ulong TargetTypeID { get; }
        bool Directional { get; }
    }

    public interface IPermittedStructureLink : IEquatable<IPermittedStructureLink>
    {
        ulong SourceTypeID { get; set; }
        ulong TargetTypeID { get; set; }
        bool Directional { get; set; }
    }
}
