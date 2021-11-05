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

namespace WebAnnotationModel.gRPC.Converters
{
    public static class LocationConverterExtensions
    {
        public static IServiceCollection AddStandardLocationConverters(this IServiceCollection service)
        {
            service.AddSingleton<IObjectConverter<Location, LocationObj>, LocationServerToClientConverter>();
            service.AddSingleton<IObjectConverter<LocationObj, Location>, LocationClientToServerConverter>();
            service.AddTransient<IObjectUpdater<LocationObj, Location>, LocationServerToClientUpdater>();
            service.AddTransient<IBoundingBoxConverter<LocationObj>, LocationServerToMosaicShapeConverter>();
            return service;
        }
    }

    public class LocationServerToClientConverter : IObjectConverter<Location, LocationObj>
    {
        public LocationObj Convert(Location src)
        {
            LocationObj obj =
                new LocationObj(src.Id, src.ParentId)
                {
                    DBAction = DBACTION.NONE,
                    Section = src.Section,
                    MosaicShape = src.MosaicShape.Text.ParseWKT(),
                    VolumeShape = src.VolumeShape.Text.ParseWKT(),
                    TypeCode = (LocationType)src.TypeCode,
                    Terminal = src.Terminal,
                    OffEdge = src.OffEdge,
                    Width = src.Width,
                    Username = src.Username,
                    LastModified = src.LastModified.ToDateTime(),
                    
                };
            
            obj.SetAttributes(src.Attributes.ParseAttributes()).Wait();
            
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
                    ParentId = src.ParentID.Value,
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
              
            return obj;
        }
    }

    public class LocationClientToServerConverter : IObjectConverter<LocationObj, Location>
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
                    Section = src.Section
                };

            if (src.ParentID.HasValue)
                obj.ParentId = src.ParentID.Value;

            obj.Attributes = src.Attributes.ToXml();
            
            return obj;
        }
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
                obj.MosaicShape = update.MosaicShape.Text.ParseWKT();
                obj.VolumeShape = update.VolumeShape.Text.ParseWKT();
                obj.TypeCode = (LocationType)update.TypeCode;
                obj.Terminal = update.Terminal;
                obj.OffEdge = update.OffEdge;
                obj.Width = update.Width;
                obj.Username = update.Username;
                obj.LastModified = update.LastModified.ToDateTime();
                await obj.SetAttributes(update.Attributes.ParseAttributes());
            }
            finally
            {
                obj.PropertyChanged -= OnPropertyChanged;
            }

            return updated;
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
