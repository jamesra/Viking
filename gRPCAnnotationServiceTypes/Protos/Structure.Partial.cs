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

        DBACTION _DBAction = DBACTION.NONE;
        DBACTION IChangeAction.DBAction { get => _DBAction; set => _DBAction = value; }

        long? IDataObjectWithParent<long>.ParentID
        {
            get => HasParentId ? this.ParentId : new long?();
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
         
        string IStructure.Label { get => this.Label; set => Label = value; }

        long IStructure.TypeID { get => TypeId; set => TypeId = value; }

        string IStructure.Attributes { get => this.Attributes; set => this.Attributes = value; }
            
        long IDataObjectWithKey<long>.ID { get => this.Id; set => this.Id = value; }

        long[] IStructure.ChildIDs => this.ChildIds.ToArray();

        IStructureLink[] IStructure.Links  => this.Links.Cast<IStructureLink>().ToArray(); 

        bool IEquatable<IStructure>.Equals(IStructure other)
        {
            if (ReferenceEquals(this, other))
                return true;

            if (ReferenceEquals(other, null))
                return false;

            return this.Id == (long)other.ID;
        }
    }
}
