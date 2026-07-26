using System;
using Grpc.Net.Client;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Hosting;
using WebAnnotationModel.gRPC.Converters;
using WebAnnotationModel.Objects;
using WebAnnotationModel.ServerInterface;
using Viking.AnnotationServiceTypes.gRPC.V1.Protos;
using WebAnnotationModel;
using WebAnnotationModel.gRPC;

namespace Microsoft.Extensions.DependencyInjection
{
    public static class DependencyInjectionExtensions
    {
        public static IServiceCollection AddStandardLocationConverters(this IServiceCollection service)
        {
            service.AddTransient<IObjectConverter<Location, LocationObj>, LocationServerToClientConverter>();
            service.AddTransient<IObjectConverter<LocationObj, Location>, LocationClientToServerConverter>();
            service.AddTransient<IObjectUpdater<LocationObj, Location>, LocationServerToClientUpdater>();
            service.AddTransient<IBoundingBoxConverter<LocationObj>, LocationServerToMosaicShapeConverter>();
            return service;
        } 

        public static IServiceCollection AddStandardQueryConverters(this IServiceCollection service)
        {
            service.AddTransient<IServerQuerySingleAddOrUpdateHandler<AnnotationSet>, ProcessorForServerAnnotationSets>();
            return service;
        } 

        public static IServiceCollection AddStandardStructureTypeConverters(this IServiceCollection service)
        {
            service.AddSingleton<IObjectConverter<StructureType, StructureTypeObj>, StructureTypeServerToClientConverter>();
            service.AddSingleton<IObjectConverter<StructureTypeObj, StructureType>, StructureTypeClientToServerConverter>();
            service.AddTransient<IObjectUpdater<StructureTypeObj, StructureType>, StructureTypeServerToClientUpdater>();
            return service;
        }

        public static IServiceCollection AddStandardStructureConverters(this IServiceCollection service)
        {
            service.AddSingleton<IObjectConverter<Structure, StructureObj>, StructureServerToClientConverter>();
            service.AddSingleton<IObjectConverter<StructureObj, Structure>, StructureClientToServerConverter>();
            service.AddTransient<IObjectUpdater<StructureObj, Structure>, StructureServerToClientUpdater>();
            return service;
        }

        public static IServiceCollection AddStandardStructureLinkConverters(this IServiceCollection service)
        {
            service.AddSingleton<IObjectConverter<StructureLink, StructureLinkObj>, StructureLinkServerToClientConverter>();
            service.AddSingleton<IObjectConverter<StructureLinkObj, StructureLink>, StructureLinkClientToServerConverter>();
            service.AddTransient<IObjectUpdater<StructureLinkObj, StructureLink>, StructureLinkServerToClientUpdater>();
            return service;
        }

        public static IServiceCollection AddStandardPermittedStructureLinkConverters(this IServiceCollection service)
        {
            service.AddSingleton<IObjectConverter<PermittedStructureLink, PermittedStructureLinkObj>, PermittedStructureLinkServerToClientConverter>();
            service.AddSingleton<IObjectConverter<PermittedStructureLinkObj, PermittedStructureLink>, PermittedStructureLinkClientToServerConverter>();
            service.AddTransient<IObjectUpdater<PermittedStructureLinkObj, PermittedStructureLink>, PermittedStructureLinkServerToClientUpdater>();
            return service;
        }
         

        public static IServiceCollection ConfigureAnnotationModel(this IServiceCollection services,
            Action<GrpcRepositorySettings> configureOptions, Action<GrpcChannelOptions> configureChannelOptions)
        {
            services.Configure(configureOptions); 
            
            services.AddStandardStructureLinkConverters()
                .AddStandardLocationConverters()
                .AddStandardLocationLinkConverters()
                .AddStandardStructureConverters()
                .AddStandardStructureTypeConverters()
                .AddStandardPermittedStructureLinkConverters()
                .AddStandardQueryLogger()
                .AddStandardQueryConverters()
                .AddSingleton<IGrpcChannelManager, GrpcChannelManager>()
                .AddSingleton<ILocationStore, LocationStore>()
                .AddSingleton<IStructureStore, StructureStore>()
                .AddSingleton<IStructureTypeStore, StructureTypeStore>()
                .AddGrpcLocationRepository(configureChannelOptions)
                //.AddStructureServer(endpoint)
                //.AddStructureTypeServer(endpoint)
                .AddDefaultStructureLinkToStructureUpdater()
                .AddDefaultLocationLinkToLocationUpdater()
                .AddDefaultPermittedStructureLinkToStructureTypeUpdater();

            return services;
        }
    }
}