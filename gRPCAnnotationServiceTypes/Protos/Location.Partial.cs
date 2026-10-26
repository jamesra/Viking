using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Text;
using Geometry;
using Viking.AnnotationServiceTypes.Interfaces;
using CoordinateSpace = Viking.gRPC.Geometry.V1.CoordinateSpace;
using Shape = Viking.gRPC.Geometry.V1.Shape;

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
        /// <summary>The type the mosaic shape encodes (the volume shape when there is no mosaic shape). Setting it
        /// re-encodes both shapes as the new type.</summary>
        LocationType ILocation.TypeCode
        {
            get => (MosaicGeometry ?? VolumeGeometry)?.LocationTypeOf() ?? LocationType.POINT;
            set
            {
                double? width = MosaicGeometry.WidthOf();
                MosaicGeometry = MosaicGeometry.ToShape2D().ToShape(value, CoordinateSpace.Mosaic, width);
                VolumeGeometry = VolumeGeometry.ToShape2D().ToShape(value, CoordinateSpace.Volume, width);
            }
        }

        Vector3 ILocation.VolumePosition { get => ToVector3(this.VolumePosition); }

        Vector3 ILocation.MosaicPosition { get => ToVector3(this.MosaicPosition); }

        /// <summary>True for a closed line (a closed curve).</summary>
        bool ILocation.Closed => MosaicGeometry?.KindCase == Shape.KindOneofCase.Line && MosaicGeometry.Line.Closed;

        /// <summary>
        /// The mosaic radius as the database computes it: a circle's radius, 0 for a point, half the length of a line,
        /// and the radius of the circle of equal area for a polygon.
        /// </summary>
        double ILocation.Radius
        {
            get
            {
                switch (MosaicGeometry?.ToShape2D())
                {
                    case ICircle2D circle:
                        return circle.Radius;
                    case IPolyLine2D line:
                        return line.Length / 2.0;
                    case ILineSegment2D segment:
                        return segment.Length / 2.0;
                    case IPolygon2D polygon:
                        return Math.Sqrt(polygon.Area / Math.PI);
                    default:
                        return 0;
                }
            }
        }

        /// <summary>The mosaic line width; null for shapes other than lines. Setting it on a line sets both shapes'
        /// width in mosaic (the volume width is not stored).</summary>
        double? ILocation.Width
        {
            get => MosaicGeometry.WidthOf();
            set
            {
                if (MosaicGeometry?.KindCase == Shape.KindOneofCase.Line)
                    MosaicGeometry.Line.Width = value ?? 0;
            }
        }

        DateTime ILocation.Created { get => Created.ToDateTime(); }

        DateTime ILocation.LastModified { get => LastModified.ToDateTime(); }

        IList<long> ILocation.Links { get => this.Links; }
         
        long IDataObjectWithKey<long>.ID { get => this.Id; set => Id = value; }

        DBACTION _DBAction = DBACTION.NONE;
        DBACTION IChangeAction.DBAction { get => _DBAction; set => _DBAction = value; }

        string ILocation.MosaicGeometryWKT
        {
            get => ToWKT(MosaicGeometry);
            set => MosaicGeometry = FromWKT(value, ((ILocation)this).TypeCode, CoordinateSpace.Mosaic, MosaicGeometry.WidthOf());
        }

        string ILocation.VolumeGeometryWKT
        {
            get => ToWKT(VolumeGeometry);
            set => VolumeGeometry = FromWKT(value, ((ILocation)this).TypeCode, CoordinateSpace.Volume, MosaicGeometry.WidthOf());
        }

        private static Vector3 ToVector3(Viking.gRPC.Geometry.V1.Vector2 p) => p == null ? default : new Vector3(p.X, p.Y, 0);

        /// <summary>SQL Server WKT of a shape; circles as the CURVEPOLYGON (CIRCULARSTRING ...) Viking stores.</summary>
        private static string ToWKT(Shape shape)
        {
            if (shape == null || shape.KindCase == Shape.KindOneofCase.None)
                return null;

            if (shape.KindCase == Shape.KindOneofCase.Circle)
                return GeometryExtensions.ToCircleWKT(shape.Circle.Center?.X ?? 0, shape.Circle.Center?.Y ?? 0, shape.Circle.Radius);

            return shape.ToShape2D().ToWKT();
        }

        private static Shape FromWKT(string wkt, LocationType type, CoordinateSpace space, double? width)
        {
            if (string.IsNullOrWhiteSpace(wkt))
                return null;

            // ParseWKT reads a circle's CURVEPOLYGON only without the CIRCULARSTRING keyword.
            return wkt.Replace("CIRCULARSTRING", string.Empty).ParseWKT().ToShape(type, space, width);
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
