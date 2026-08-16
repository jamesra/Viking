using System;
using System.Collections.Generic;
using System.Text;
using System.Threading.Tasks;
using WebAnnotationModel.Objects;
using WebAnnotationModel.ServerInterface;
using Viking.AnnotationServiceTypes.gRPC.V1.Protos;
using Viking.AnnotationServiceTypes.Interfaces;
using System.ComponentModel;
using System.Data.Common;
using Geometry;
using Google.Protobuf.WellKnownTypes;
using Geometry = Viking.AnnotationServiceTypes.gRPC.V1.Protos.Geometry;
using Microsoft.Extensions.DependencyInjection;
using WebAnnotationModel;

namespace WebAnnotationModel.gRPC.Converters
{ 

    public class LocationServerToClientConverter : IObjectConverter<Location, LocationObj>,
        IObjectConverter<ILocation, LocationObj>
    {
        public LocationObj Convert(Location src)
        {
            var obj = new LocationObj(src.Id, src.ParentId)
            {
                DBAction = DBACTION.NONE,
                Section = src.Section,
                MosaicShape = LocationShapeConversion.ShapeFromCircleOrWkt(src, mosaic: true),
                VolumeShape = LocationShapeConversion.ShapeFromCircleOrWkt(src, mosaic: false),
                TypeCode = (LocationType)src.TypeCode,
                Terminal = src.Terminal,
                OffEdge = src.OffEdge,
                Width = src.Width,
                Username = src.Username,
                LastModified = src.LastModified?.ToDateTime() ?? default,
            };

            obj.SetAttributes(ObjAttributeParser.ParseAttributes(src.Attributes ?? string.Empty)).ConfigureAwait(false).GetAwaiter().GetResult();
            obj.SetLinksFromServerAsync(src.Links).ConfigureAwait(false).GetAwaiter().GetResult();
            return obj;
        }

        public LocationObj Convert(ILocation src)
        {
            if (src is Location concrete)
                return Convert(concrete);

            var obj = new LocationObj(src.ID, src.ParentID ?? 0)
            {
                DBAction = DBACTION.NONE,
                Section = src.SectionNumber,
                MosaicShape = LocationShapeConversion.ShapeFromCircleOrWkt(src, mosaic: true),
                VolumeShape = LocationShapeConversion.ShapeFromCircleOrWkt(src, mosaic: false),
                TypeCode = src.TypeCode,
                Terminal = src.Terminal,
                OffEdge = src.OffEdge,
                Width = src.Width,
                Username = src.Username,
                LastModified = src.LastModified,
            };

            obj.SetAttributes(ObjAttributeParser.ParseAttributes(src.TagsXml ?? string.Empty)).ConfigureAwait(false).GetAwaiter().GetResult();
            obj.SetLinksFromServerAsync(src.Links).ConfigureAwait(false).GetAwaiter().GetResult();
            return obj;
        }
    }

    public class LocationToLocationServerConverter : IObjectConverter<ILocation, Location>
    {
        public Location Convert(ILocation src)
        {
            if (src is Location loc)
                return loc;

            Location obj =
                new Location
                {
                    Attributes = src.Attributes,
                    Id = src.ID,
                    Section = src.SectionNumber,
                    MosaicShape = new Viking.AnnotationServiceTypes.gRPC.V1.Protos.Geometry { Text = src.MosaicGeometryWKT },
                    VolumeShape = new Viking.AnnotationServiceTypes.gRPC.V1.Protos.Geometry { Text = src.VolumeGeometryWKT },
                    Closed = false,
                    TypeCode = (AnnotationType)src.TypeCode,
                    VolumePosition = src.VolumePosition,
                    MosaicPosition = src.MosaicPosition,
                    Terminal = src.Terminal,
                    OffEdge = src.OffEdge,
                    Radius = src.Radius,
                    Width = src.Width,
                    Created = src.Created.ToTimestamp(),
                    Username = src.Username,
                    LastModified = src.LastModified.ToTimestamp(),

                };

            if (src.ParentID.HasValue)
                obj.ParentId = src.ParentID.Value;
              
            return obj;
        }
    }

    public class LocationClientToServerConverter : IObjectConverter<LocationObj, Location>,
        IObjectConverter<LocationObj, ILocation>
    {
        public Location Convert(LocationObj src)
        {
            var mosaicshape = new Viking.AnnotationServiceTypes.gRPC.V1.Protos.Geometry();
            mosaicshape.Text = src.MosaicShape.ToWKT();

            var volumeshape = new Viking.AnnotationServiceTypes.gRPC.V1.Protos.Geometry();
            volumeshape.Text = src.VolumeShape.ToWKT();

            Location obj =
                new Location
                {
                    Id = src.ID,
                    MosaicShape = mosaicshape,
                    VolumeShape = volumeshape,
                    TypeCode = (AnnotationType)src.TypeCode,
                    Terminal = src.Terminal,
                    OffEdge = src.OffEdge,
                    Width = src.Width,
                    Section = src.Section,
                    Username = src.Username ?? string.Empty,
                    Attributes = LocationAttributesXml(src),
                };

            if (src.ParentID.HasValue)
                obj.ParentId = src.ParentID.Value;

            ((IChangeAction)obj).DBAction = src.DBAction;

            return obj;
        }

        private static string LocationAttributesXml(LocationObj src)
        {
            try
            {
                return src.Attributes.ToXml() ?? string.Empty;
            }
            catch (NullReferenceException)
            {
                return string.Empty;
            }
        }

        ILocation IObjectConverter<LocationObj, ILocation>.Convert(LocationObj src) => Convert(src);
    }

    public class LocationServerToClientUpdater : IObjectUpdater<LocationObj, Location>
    {
        public async Task<bool> Update(LocationObj obj, Location update)
        {
            bool updated = false;
            void OnPropertyChanged(object s, PropertyChangedEventArgs e) => updated = true;
            try
            {
                obj.PropertyChanged += OnPropertyChanged; //Record change events so we know if an update occurred.

                obj.Section = update.Section;
                obj.MosaicShape = LocationShapeConversion.ShapeFromCircleOrWkt(update, mosaic: true);
                obj.VolumeShape = LocationShapeConversion.ShapeFromCircleOrWkt(update, mosaic: false);
                obj.TypeCode = (LocationType)update.TypeCode;
                obj.Terminal = update.Terminal;
                obj.OffEdge = update.OffEdge;
                obj.Width = update.Width;
                obj.Username = update.Username;
                obj.LastModified = update.LastModified.ToDateTime();
                await obj.SetAttributes(update.Attributes.ParseAttributes());
                await obj.SetLinksFromServerAsync(update.Links);
            }
            finally
            {
                obj.PropertyChanged -= OnPropertyChanged;
            }

            return updated;
        } 
    }

    /// <summary>
    /// Server circle WKT is CURVEPOLYGON (CIRCULARSTRING (...)), which client ParseWKT
    /// does not understand. Reconstruct circles from proto scalars; volume radius on
    /// read is the mosaic Radius placeholder.
    /// </summary>
    internal static class LocationShapeConversion
    {
        /// <summary>
        /// Called by gRPC location converters. Circles use MosaicPosition/VolumePosition
        /// and Radius; other types fall back to WKT.
        /// </summary>
        public static IShape2D ShapeFromCircleOrWkt(Location src, bool mosaic)
        {
            if (src.TypeCode == AnnotationType.Circle && src.Radius > 0)
            {
                var pos = mosaic ? src.MosaicPosition : src.VolumePosition;
                if (pos != null)
                    return new Circle(pos.X, pos.Y, src.Radius);
            }

            ILocation asIface = src;
            return ParseShape(mosaic ? asIface.MosaicGeometryWKT : asIface.VolumeGeometryWKT);
        }

        /// <summary>
        /// Same as the proto overload for ILocation callers that are not a proto Location.
        /// </summary>
        public static IShape2D ShapeFromCircleOrWkt(ILocation src, bool mosaic)
        {
            if (src.TypeCode == LocationType.CIRCLE && src.Radius > 0)
            {
                var pos = mosaic ? src.MosaicPosition : src.VolumePosition;
                return new Circle(pos.X, pos.Y, src.Radius);
            }

            return ParseShape(mosaic ? src.MosaicGeometryWKT : src.VolumeGeometryWKT);
        }

        private static IShape2D ParseShape(string wkt)
        {
            if (string.IsNullOrWhiteSpace(wkt))
                return null;

            // netstandard2.0 has no Replace(string, string, StringComparison).
            var normalized = wkt.Replace("CIRCULARSTRING", string.Empty);
            return normalized.ParseWKT();
        }
    }

    public class LocationServerToMosaicShapeConverter : IBoundingBoxConverter<LocationObj>
    {
        public RTree.Rectangle BoundingRect(LocationObj obj)
        {
            return obj.MosaicShape.BoundingBox.ToRTreeRect(obj.Z);
        }
    }

    public class LocationServerToVolumeShapeConverter : IBoundingBoxConverter<LocationObj>
    {
        public RTree.Rectangle BoundingRect(LocationObj obj)
        {
            return obj.VolumeShape.BoundingBox.ToRTreeRect(obj.Z);
        }
    }
}
