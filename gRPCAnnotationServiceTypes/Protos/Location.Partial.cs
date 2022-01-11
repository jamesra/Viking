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

        DateTime ILocation.Created { get => Created.ToDateTime(); }

        DateTime ILocation.LastModified { get => LastModified.ToDateTime(); }

        IList<long> ILocation.Links { get => this.Links; }
         
        long IDataObjectWithKey<long>.ID { get => this.Id; set => Id = value; }

        DBACTION _DBAction = DBACTION.NONE;
        DBACTION IChangeAction.DBAction { get => _DBAction; set => _DBAction = value; }

        string ILocation.MosaicGeometryWKT
        {
            get => ToWKT(MosaicShape) ?? this.ToMosaicCircleWKT();
            set => this.MosaicShape.Text = value;
        }

        string ILocation.VolumeGeometryWKT { get => ToWKT(VolumeShape) ?? this.ToVolumeCircleWKT(); set => this.VolumeShape.Text = value; }

        private string ToWKT(Geometry g)
        {
            switch (g.EncodingCase)
            {
                case Geometry.EncodingOneofCase.None:
                    return null;
                case Geometry.EncodingOneofCase.Text:
                    return g.Text;
                case Geometry.EncodingOneofCase.Binary:
                {
                    var r = new NetTopologySuite.IO.WKBReader();
                    var rdr = new NetTopologySuite.IO.WKBReader
                    {
                        HandleOrdinates = NetTopologySuite.Geometries.Ordinates.AllOrdinates,
                        HandleSRID = false
                    };

                    var ptAUR = rdr.Read(g.Binary.ToByteArray());
                    return ptAUR.ToText();
                }
            }

            throw new NotImplementedException("Unexpected geometry encoding");
        }

        bool IEquatable<ILocation>.Equals(ILocation other)
        {
            if (ReferenceEquals(this, other))
                return true;

            if (other is null)
                return false;

            return this.Id == other.ID;
        }

        public static explicit operator LocationChangeRequest(Location src)
        {
            var value = new LocationChangeRequest();
            switch (src._DBAction)
            {
                case DBACTION.NONE:
                    return null;
                case DBACTION.INSERT:
                    value.Create = src;
                    break;
                case DBACTION.UPDATE:
                    value.Update = src;
                    break;
                case DBACTION.DELETE:
                    value.Delete = src.Id;
                    break;
            }
            return value;
        }
    }
}
