using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using System.Text;
using Geometry;
using Viking.AnnotationServiceTypes.Interfaces;

namespace Viking.AnnotationServiceTypes.gRPC.V1.Protos 
{
    public partial class StructureType : IStructureTypeReadOnly, IChangeAction
    {
        // IStructureTypeReadOnly interface implementation
        ulong IStructureTypeReadOnly.ID => (ulong)this.Id;

        ulong? IStructureTypeReadOnly.ParentID => this.HasParentId ? (ulong?)this.ParentId : null;

        string IStructureTypeReadOnly.Name => this.Name;

        string IStructureTypeReadOnly.Code => this.Code;

        // Tags field doesn't exist in protobuf, return empty array
        string[] IStructureTypeReadOnly.Tags => Array.Empty<string>();

        // IChangeAction implementation
        DBACTION _DBAction = DBACTION.NONE;
        DBACTION IChangeAction.DBAction { get => _DBAction; set => _DBAction = value; }

        // IEquatable implementation
        bool IEquatable<IStructureTypeReadOnly>.Equals(IStructureTypeReadOnly other)
        {
            if (ReferenceEquals(this, other))
                return true;

            if (ReferenceEquals(other, null))
                return false;

            return this.Id == (long)other.ID;
        }
    }
}
