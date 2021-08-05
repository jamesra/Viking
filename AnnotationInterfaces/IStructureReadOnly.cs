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

        ICollection<IStructureLinkReadOnly> Links
        {
            get;
        }

        IStructureTypeReadOnly Type
        {
            get;
        }

        string TagsXML { get; }
    }

    public interface IStructure : IEquatable<IStructure>, IDataObjectWithParent<long>
    {
        long TypeID { get; set; }
          
        string Label { get; set; }

        string Attributes { get; set; }

        string Notes { get; set; }

        double Confidence { get; set; }

        string Username { get; }

        long[] ChildIDs { get; }

        bool Verified { get; }

        IStructureLink[] Links { get; }
    }
}
