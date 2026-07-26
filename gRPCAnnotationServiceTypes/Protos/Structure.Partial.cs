using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using System.Text;
using Geometry;
using Viking.AnnotationServiceTypes.Interfaces;

namespace Viking.AnnotationServiceTypes.gRPC.V1.Protos
{
    public partial class Structure : IStructureReadOnly, IChangeAction
    {
        // IStructureReadOnly interface implementation
        ulong IStructureReadOnly.ID => (ulong)this.Id;

        ulong? IStructureReadOnly.ParentID => this.HasParentId ? (ulong?)this.ParentId : null;

        ulong IStructureReadOnly.TypeID => (ulong)this.TypeId;

        string IStructureReadOnly.Label => this.Label;

        ICollection<IStructureLink> IStructureReadOnly.Links => 
            this.Links?.Cast<IStructureLink>().ToList() ?? new List<IStructureLink>();

        IStructureTypeReadOnly IStructureReadOnly.Type => 
            new StructureTypeProxy((ulong)this.TypeId, this.HasParentId ? (ulong?)this.ParentId : null);

        // TagsXML field doesn't exist in protobuf, use attributes instead
        string IStructureReadOnly.TagsXML => this.Attributes ?? string.Empty;

        // IChangeAction implementation
        DBACTION _DBAction = DBACTION.NONE;
        DBACTION IChangeAction.DBAction { get => _DBAction; set => _DBAction = value; }

        // IEquatable implementation
        bool IEquatable<IStructureReadOnly>.Equals(IStructureReadOnly other)
        {
            if (ReferenceEquals(this, other))
                return true;

            if (ReferenceEquals(other, null))
                return false;

            return this.Id == (long)other.ID;
        }
    }

    // Helper class to implement IStructureTypeReadOnly
    internal class StructureTypeProxy : IStructureTypeReadOnly
    {
        public ulong ID { get; }
        public ulong? ParentID { get; }
        public string Name => "Unknown"; // Placeholder
        public string Code => "UNK"; // Placeholder
        public string[] Tags => Array.Empty<string>();

        public StructureTypeProxy(ulong id, ulong? parentId)
        {
            ID = id;
            ParentID = parentId;
        }

        public bool Equals(IStructureTypeReadOnly other)
        {
            return other != null && ID == other.ID;
        }
    }
}
