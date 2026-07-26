using System;

namespace Viking.AnnotationServiceTypes.Interfaces
{
    public interface IPermittedStructureLinkKey : IEquatable<IPermittedStructureLinkKey>, IComparable<IPermittedStructureLinkKey>
    {
        ulong SourceTypeID { get; }
        ulong TargetTypeID { get; }
        bool Directional { get; }
    }

    public interface IPermittedStructureLink : IEquatable<IPermittedStructureLink>, IDataObjectWithKey<IPermittedStructureLinkKey>, IDataObjectWithKey<PermittedStructureLinkKey>
    {
        ulong SourceTypeID { get; set; }
        ulong TargetTypeID { get; set; }
        bool Directional { get; set; }
    }
}
