using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using System.Text;
using Geometry;
using Viking.AnnotationServiceTypes.Interfaces;

namespace Viking.AnnotationServiceTypes.gRPC.V1.Protos
{
    public partial class Structure : IStructure, IChangeAction
    {
        // IStructure interface implementation
        ulong IStructure.ID => (ulong)this.Id;

        ulong? IStructure.ParentID => this.HasParentId ? (ulong?)this.ParentId : null;

        ulong IStructure.TypeID => (ulong)this.TypeId;

        string IStructure.Label => this.Label;

        ICollection<IStructureLink> IStructure.Links => 
            this.Links?.Cast<IStructureLink>().ToList() ?? new List<IStructureLink>();

        IStructureType IStructure.Type => 
            new StructureTypeProxy((ulong)this.TypeId, this.HasParentId ? (ulong?)this.ParentId : null);

        // TagsXML field doesn't exist in protobuf, use attributes instead
        string IStructure.TagsXML => this.Attributes ?? string.Empty;

        // IChangeAction implementation
        DBACTION _DBAction = DBACTION.NONE;
        DBACTION IChangeAction.DBAction { get => _DBAction; set => _DBAction = value; }

        // IEquatable implementation
        bool IEquatable<IStructure>.Equals(IStructure other)
        {
            if (ReferenceEquals(this, other))
                return true;

            if (ReferenceEquals(other, null))
                return false;

            return this.Id == (long)other.ID;
        }
    }

    // Helper class to implement IStructureType
    internal class StructureTypeProxy : IStructureType
    {
        public ulong ID { get; }
        public ulong? ParentID { get; }
        public string Name => "Unknown"; // Placeholder
        public string Code => "UNK"; // Placeholder
        public string[] Tags => new string[0];

        public StructureTypeProxy(ulong id, ulong? parentId)
        {
            ID = id;
            ParentID = parentId;
        }

        public bool Equals(IStructureType other)
        {
            return other != null && ID == other.ID;
        }
    }
}
