using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using System.Text;
using Geometry;
using Viking.AnnotationServiceTypes.Interfaces;

namespace Viking.AnnotationServiceTypes.gRPC.V1.Protos 
{
    public partial class StructureType : IStructureType, IChangeAction
    {
        // IStructureType interface implementation
        ulong IStructureType.ID => (ulong)this.Id;

        ulong? IStructureType.ParentID => this.HasParentId ? (ulong?)this.ParentId : null;

        string IStructureType.Name => this.Name;

        string IStructureType.Code => this.Code;

        // Tags field doesn't exist in protobuf, return empty array
        string[] IStructureType.Tags => new string[0];

        // IChangeAction implementation
        DBACTION _DBAction = DBACTION.NONE;
        DBACTION IChangeAction.DBAction { get => _DBAction; set => _DBAction = value; }

        // IEquatable implementation
        bool IEquatable<IStructureType>.Equals(IStructureType other)
        {
            if (ReferenceEquals(this, other))
                return true;

            if (ReferenceEquals(other, null))
                return false;

            return this.Id == (long)other.ID;
        }
    }
}
