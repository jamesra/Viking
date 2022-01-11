using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Text;
using Geometry;
using Viking.AnnotationServiceTypes.Interfaces;

namespace Viking.AnnotationServiceTypes.gRPC.V1.Protos
{
    public partial class Location : ILocation, IChangeAction
    {
        long? ILocation.ParentID { get => this.HasParentId ? this.ParentId : new long?();
            set {
                if (value.HasValue)
                    this.ParentId = value.Value;
                else
                    this.ClearParentId();
            }
        }

        string ILocation.Attributes { get => this.Attributes; set => this.Attributes = value; }

        long ILocation.SectionNumber { get => this.Section; set => Section = value; }
        string ILocation.TagsXml { get => this.Attributes; set => this.Attributes = value;}
        LocationType ILocation.TypeCode { get => (LocationType)(int)this.TypeCode; set => TypeCode = (Viking.AnnotationServiceTypes.gRPC.V1.Protos.AnnotationType)(int)value;}

        GridVector3 ILocation.VolumePosition { get => this.VolumePosition; }

        GridVector3 ILocation.MosaicPosition { get => this.MosaicPosition; }

        DateTime ILocation.LastModified { get => LastModified.ToDateTime(); }

        IList<long> ILocation.Links { get => this.Links; }
         
        long IDataObjectWithKey<long>.ID { get => this.Id; set => Id = value; }

        DBACTION _DBAction = DBACTION.NONE;
        DBACTION IChangeAction.DBAction { get => _DBAction; set => _DBAction = value; }
        string ILocation.MosaicGeometryWKT { get => this.MosaicShape.Text; set => this.MosaicShape.Text = value; }
        string ILocation.VolumeGeometryWKT { get => this.VolumeShape.Text; set => this.VolumeShape.Text = value; }

        bool IEquatable<ILocation>.Equals(ILocation other)
        {
            if (ReferenceEquals(this, other))
                return true;

            if (ReferenceEquals(other, null))
                return false;

            return (ulong)this.Id == other.ID;
        }
    }
}
