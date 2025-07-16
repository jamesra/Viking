using System;
using System.Collections.Generic;

namespace Viking.AnnotationServiceTypes.Interfaces
{
    public interface IStructureReadOnly : IEquatable<IStructureReadOnly>
    {
        ulong ID { get; }

        ulong? ParentID { get; }

        ulong TypeID { get; }

        string Label { get; }

        ICollection<IStructureLink> Links
        {
            get;
        }

        IStructureTypeReadOnly Type
        {
            get;
        }

        string TagsXML { get; }
    }
}