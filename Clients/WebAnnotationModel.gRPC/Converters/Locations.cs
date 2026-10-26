using System;
using System.Collections.Generic;
using System.Text;
using System.Threading.Tasks;
using WebAnnotationModel.Objects;
using WebAnnotationModel.ServerInterface;
using Viking.AnnotationServiceTypes;
using Viking.AnnotationServiceTypes.gRPC.V1.Protos;
using Viking.AnnotationServiceTypes.Interfaces;
using System.ComponentModel;
using System.Data.Common;
using Geometry;
using Google.Protobuf.WellKnownTypes;
using Viking.AnnotationServiceTypes.gRPC.V1;
using CoordinateSpace = Viking.gRPC.Geometry.V1.CoordinateSpace;
using Microsoft.Extensions.DependencyInjection;
using WebAnnotationModel;

namespace WebAnnotationModel.gRPC.Converters
{ 

    public class LocationServerToClientConverter : IObjectConverter<Location, LocationObj>,
        IObjectConverter<ILocation, LocationObj>
    {
        public LocationObj Convert(Location src)
        {
            // DBAction is assigned last: every property setter marks the object UPDATE, and a location loaded from the
            // server must start NONE so it is neither sent back on Save nor skipped by later refreshes.
            var obj = new LocationObj(src.Id, src.ParentId)
            {
                Section = src.Section,
                MosaicShape = src.MosaicGeometry.ToShape2D(),
                VolumeShape = src.VolumeGeometry.ToShape2D(),
                TypeCode = LocationShapeConversion.TypeOf(src),
                Terminal = src.Terminal,
                OffEdge = src.OffEdge,
                Width = src.MosaicGeometry.WidthOf(),
                Username = src.Username,
                LastModified = src.LastModified?.ToDateTime() ?? default,
                DBAction = DBACTION.NONE,
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
                Section = src.SectionNumber,
                MosaicShape = LocationShapeConversion.ParseShape(src.MosaicGeometryWKT),
                VolumeShape = LocationShapeConversion.ParseShape(src.VolumeGeometryWKT),
                TypeCode = src.TypeCode,
                Terminal = src.Terminal,
                OffEdge = src.OffEdge,
                Width = src.Width,
                Username = src.Username,
                LastModified = src.LastModified,
                DBAction = DBACTION.NONE,
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
                    MosaicGeometry = LocationShapeConversion.ParseShape(src.MosaicGeometryWKT).ToShape(src.TypeCode, CoordinateSpace.Mosaic, src.Width),
                    VolumeGeometry = LocationShapeConversion.ParseShape(src.VolumeGeometryWKT).ToShape(src.TypeCode, CoordinateSpace.Volume, src.Width),
                    VolumePosition = src.VolumePosition.XY().ToProtoVector2(),
                    MosaicPosition = src.MosaicPosition.XY().ToProtoVector2(),
                    Terminal = src.Terminal,
                    OffEdge = src.OffEdge,
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
            // The type travels as the mosaic shape's case and fit; a line's width travels in the shape. Circles carry
            // their own radius in each space (the volume circle is the drawn one, the mosaic circle mapped from it).
            Location obj =
                new Location
                {
                    Id = src.ID,
                    MosaicGeometry = src.MosaicShape.ToShape(src.TypeCode, CoordinateSpace.Mosaic, src.Width),
                    VolumeGeometry = src.VolumeShape.ToShape(src.TypeCode, CoordinateSpace.Volume, src.Width),
                    Terminal = src.Terminal,
                    OffEdge = src.OffEdge,
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

    /// <summary>
    /// Copies a server refresh onto a cached location. Applied values are not local edits, so a location that mirrors the
    /// server stays <see cref="DBACTION.NONE"/> and the next Save does not send it back under the current user's name.
    /// A location with an unsaved local edit (DBAction not NONE) keeps the user's values until that edit is saved.
    /// </summary>
    public class LocationServerToClientUpdater : IObjectUpdater<LocationObj, Location>,
        IObjectUpdater<LocationObj, ILocation>
    {
        public Task<bool> Update(LocationObj obj, ILocation update)
        {
            if (update is Location loc)
                return Update(obj, loc);

            return UpdateFromInterface(obj, update);
        }

        public async Task<bool> Update(LocationObj obj, Location update)
        {
            if (obj.DBAction != DBACTION.NONE)
                return false;

            // The row is unchanged since the cached copy was made: skip parsing its shapes again. Links are a separate
            // table and can change without touching the row, so they are still synced.
            DateTime serverModified = update.LastModified?.ToDateTime() ?? default;
            if (serverModified != default && obj.LastModified == serverModified && obj.Section == update.Section)
            {
                await obj.SetLinksFromServerAsync(update.Links);
                return false;
            }

            bool updated = false;
            void OnPropertyChanged(object s, PropertyChangedEventArgs e) => updated = true;
            try
            {
                obj.PropertyChanged += OnPropertyChanged;

                obj.ApplyServerValues(() =>
                {
                    obj.Section = update.Section;
                    obj.MosaicShape = update.MosaicGeometry.ToShape2D();
                    obj.VolumeShape = update.VolumeGeometry.ToShape2D();
                    obj.TypeCode = LocationShapeConversion.TypeOf(update);
                    obj.Terminal = update.Terminal;
                    obj.OffEdge = update.OffEdge;
                    obj.Width = update.MosaicGeometry.WidthOf();
                    obj.Username = update.Username;
                    obj.LastModified = update.LastModified.ToDateTime();
                });
                await obj.SetAttributes(update.Attributes.ParseAttributes());
                await obj.SetLinksFromServerAsync(update.Links);
            }
            finally
            {
                obj.PropertyChanged -= OnPropertyChanged;
            }

            return updated;
        }

        static async Task<bool> UpdateFromInterface(LocationObj obj, ILocation update)
        {
            if (obj.DBAction != DBACTION.NONE)
                return false;

            if (update.LastModified != default && obj.LastModified == update.LastModified && obj.Section == update.SectionNumber)
            {
                await obj.SetLinksFromServerAsync(update.Links);
                return false;
            }

            bool updated = false;
            void OnPropertyChanged(object s, PropertyChangedEventArgs e) => updated = true;
            try
            {
                obj.PropertyChanged += OnPropertyChanged;

                obj.ApplyServerValues(() =>
                {
                    obj.Section = update.SectionNumber;
                    obj.MosaicShape = LocationShapeConversion.ParseShape(update.MosaicGeometryWKT);
                    obj.VolumeShape = LocationShapeConversion.ParseShape(update.VolumeGeometryWKT);
                    obj.TypeCode = update.TypeCode;
                    obj.Terminal = update.Terminal;
                    obj.OffEdge = update.OffEdge;
                    obj.Width = update.Width;
                    obj.Username = update.Username;
                    obj.LastModified = update.LastModified;
                });
                await obj.SetAttributes(ObjAttributeParser.ParseAttributes(update.Attributes ?? string.Empty));
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
    /// Shape helpers for the gRPC location converters. Proto locations carry <c>viking.geometry.v1.Shape</c> (converted by
    /// <see cref="AnnotationShapes"/>); other <see cref="ILocation"/> sources carry WKT.
    /// </summary>
    internal static class LocationShapeConversion
    {
        /// <summary>The location's type, from its mosaic shape (its volume shape when it has none).</summary>
        public static LocationType TypeOf(Location src) =>
            (src.MosaicGeometry ?? src.VolumeGeometry)?.LocationTypeOf() ?? LocationType.POINT;

        /// <summary>
        /// Parses Viking WKT. A circle's CURVEPOLYGON (CIRCULARSTRING (...)) is read without the CIRCULARSTRING keyword,
        /// which the client parser does not know.
        /// </summary>
        public static IShape2D ParseShape(string wkt)
        {
            if (string.IsNullOrWhiteSpace(wkt))
                return null;

            // netstandard2.0 has no Replace(string, string, StringComparison).
            var normalized = wkt.Replace("CIRCULARSTRING", string.Empty);
            return normalized.ParseWKT();
        }

        /// <summary>The XY of a position.</summary>
        public static Vector2 XY(this Vector3 p) => new Vector2(p.X, p.Y);
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
