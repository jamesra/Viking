using System;

namespace Viking.AnnotationServiceTypes.Interfaces
{
    public interface IStructureLinkReadOnly : IEquatable<IStructureLinkReadOnly>
    {
        ulong SourceID { get; }

        ulong TargetID { get; }

        bool Directional { get; }
    }

    public interface IStructureLink : IStructureLinkReadOnly, IEquatable<IStructureLinkReadOnly>
    {
        ulong SourceID { get; set; }

        ulong TargetID { get; set; }

        bool Directional { get; set; }
    }
}
