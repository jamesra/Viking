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
        
        DBACTION _DBAction = DBACTION.NONE;
        DBACTION IChangeAction.DBAction { get => _DBAction; set => _DBAction = value; }
        long IDataObjectWithKey<long>.ID { get => Id; set => Id = value; }
        long? IDataObjectWithParent<long>.ParentID
        {
            get { return HasParentId ? this.ParentId : new long?(); }
            set
            {
                if (value.HasValue)
                {
                    ParentId = (long)value.Value;
                }
                else
                {
                    ClearParentId();
                }
            }
        }

        //string IStructureType.MarkupType { get => this.throw new NotImplementedException(); set => throw new NotImplementedException(); }
        //string[] IStructureType.Tags { get => this.Tags }
        string IStructureType.Attributes { get => this.Attributes; set => this.Attributes = value; }
        IPermittedStructureLink[] IStructureType.PermittedLinks { get => throw new NotImplementedException(); set => throw new NotImplementedException(); }

        //string[] IStructureType.StructureTags { get => throw new NotImplementedException(); set => throw new NotImplementedException(); }

        //IPermittedStructureLink[] IStructureType.PermittedLinks { get => this.P; set => throw new NotImplementedException(); }

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
