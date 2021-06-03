using System;
using System.Collections.Generic;

namespace Annotation.Interfaces
{
    public interface IStructure : IEquatable<IStructure>
    {
        ulong ID { get; }

        ulong? ParentID { get; }

        ulong TypeID { get; }

        string Label { get; }

        ICollection<IStructureLink> Links
        {
            get;
        }

        IStructureType Type
        {
            get;
        }

        string TagsXML { get; }
    }
}
