using System; 

namespace Viking.AnnotationServiceTypes.Interfaces
{
    public interface IStructureLinkKey : IEquatable<IStructureLinkKey>, IComparable<IStructureLinkKey>
    {
        ulong SourceID { get; }

        ulong TargetID { get; }

        bool Directional { get; }
    }

    public interface IStructureLink : IEquatable<IStructureLinkKey>, IEquatable<IStructureLink>, IDataObjectWithKey<IStructureLinkKey>, IDataObjectWithKey<StructureLinkKey>
    { 
        ulong SourceID { get; set; }

        ulong TargetID { get; set; }

        bool Directional { get; set; }
    }
}
