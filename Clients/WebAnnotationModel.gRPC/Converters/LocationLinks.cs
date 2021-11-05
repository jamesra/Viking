using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Text;
using System.Threading.Tasks;
using WebAnnotationModel.Objects;
using WebAnnotationModel.ServerInterface;
using Viking.AnnotationServiceTypes.gRPC.V1.Protos;
using Microsoft.Extensions.DependencyInjection;

namespace WebAnnotationModel.gRPC.Converters
{
    public static class LocationLinkConverterExtensions
    {
        public static IServiceCollection AddStandardLocationLinkConverters(this IServiceCollection service)
        {
            service.AddSingleton<IObjectConverter<LocationLink, LocationLinkObj>, LocationLinkServerToClientConverter>();
            service.AddSingleton<IObjectConverter<LocationLinkObj, LocationLink>, LocationLinkClientToServerConverter>();
            return service;
        }
    }

    public class LocationLinkServerToClientConverter : IObjectConverter<LocationLink, LocationLinkObj>
    {
        public LocationLinkObj Convert(LocationLink src)
        { 
            return new LocationLinkObj(src.SourceId, src.TargetId); ;
        }
    }

    public class LocationLinkClientToServerConverter : IObjectConverter<LocationLinkObj, LocationLink>
    {
        public LocationLink Convert(LocationLinkObj src)
        {
            return new LocationLink
                {
                    SourceId = src.A,
                    TargetId = src.B
                }; 
        }
    }

    public class LocationLinkServerToClientUpdater : IObjectUpdater<LocationLinkObj, LocationLink>
    {
        public Task<bool> Update(LocationLinkObj obj, LocationLink update)
        {
            throw new NotImplementedException();
        }
    }
}