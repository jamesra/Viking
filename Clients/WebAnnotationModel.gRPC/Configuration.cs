using System;
using Grpc.Net.Client;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Hosting;
using WebAnnotationModel.gRPC.Converters;
using WebAnnotationModel.Objects;
using WebAnnotationModel.ServerInterface;
using Viking.AnnotationServiceTypes;
using Viking.AnnotationServiceTypes.Interfaces;
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
            service.AddTransient<IObjectConverter<ILocation, LocationObj>, LocationServerToClientConverter>();
            service.AddTransient<IObjectConverter<LocationObj, Location>, LocationClientToServerConverter>();
            service.AddTransient<IObjectConverter<LocationObj, ILocation>, LocationClientToServerConverter>();
            service.AddTransient<IObjectConverter<ILocation, Location>, LocationToLocationServerConverter>();
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
            service.AddSingleton<IObjectConverter<IStructureType, StructureTypeObj>, StructureTypeServerToClientConverter>();
            service.AddSingleton<IObjectConverter<StructureTypeObj, StructureType>, StructureTypeClientToServerConverter>();
            service.AddSingleton<IObjectConverter<StructureTypeObj, IStructureType>, StructureTypeClientToServerConverter>();
            service.AddTransient<IObjectUpdater<StructureTypeObj, StructureType>, StructureTypeServerToClientUpdater>();
            return service;
        }

        public static IServiceCollection AddStandardStructureConverters(this IServiceCollection service)
        {
            service.AddSingleton<IObjectConverter<Structure, StructureObj>, StructureServerToClientConverter>();
            service.AddSingleton<IObjectConverter<IStructure, StructureObj>, StructureServerToClientConverter>();
            service.AddSingleton<IObjectConverter<StructureObj, Structure>, StructureClientToServerConverter>();
            service.AddSingleton<IObjectConverter<StructureObj, IStructure>, StructureClientToServerConverter>();
            service.AddTransient<StructureServerToClientUpdater>();
            service.AddTransient<IObjectUpdater<StructureObj, Structure>>(sp =>
                sp.GetRequiredService<StructureServerToClientUpdater>());
            service.AddTransient<IObjectUpdater<StructureObj, IStructure>>(sp =>
                sp.GetRequiredService<StructureServerToClientUpdater>());
            return service;
        }

        public static IServiceCollection AddStandardStructureLinkConverters(this IServiceCollection service)
        {
            service.AddSingleton<IObjectConverter<StructureLink, StructureLinkObj>, StructureLinkServerToClientConverter>();
            service.AddSingleton<IObjectConverter<IStructureLink, StructureLinkObj>, StructureLinkServerToClientConverter>();
            service.AddSingleton<IObjectConverter<StructureLinkObj, StructureLink>, StructureLinkClientToServerConverter>();
            service.AddSingleton<IObjectConverter<StructureLinkObj, IStructureLink>, StructureLinkClientToServerConverter>();
            service.AddTransient<IObjectUpdater<StructureLinkObj, StructureLink>, StructureLinkServerToClientUpdater>();
            return service;
        }

        public static IServiceCollection AddStandardPermittedStructureLinkConverters(this IServiceCollection service)
        {
            service.AddSingleton<IObjectConverter<PermittedStructureLink, PermittedStructureLinkObj>, PermittedStructureLinkServerToClientConverter>();
            service.AddSingleton<IObjectConverter<IPermittedStructureLink, PermittedStructureLinkObj>, PermittedStructureLinkServerToClientConverter>();
            service.AddSingleton<IObjectConverter<PermittedStructureLinkObj, PermittedStructureLink>, PermittedStructureLinkClientToServerConverter>();
            service.AddSingleton<IObjectConverter<PermittedStructureLinkObj, IPermittedStructureLink>, PermittedStructureLinkClientToServerConverter>();
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
                .AddSingleton<IStructureLinkStore, StructureLinkStore>()
                .AddSingleton<ILocationLinkStore, LocationLinkStore>()
                .AddSingleton<IPermittedStructureLinkStore, PermittedStructureLinkStore>()
                .AddSingleton<IAnnotationStores, AnnotationStores>()
                .AddGrpcLocationRepository(configureChannelOptions)
                .AddGrpcStructureLinkRepository()
                .AddGrpcPermittedStructureLinkRepository()
                .AddStructureServer()
                .AddStructureTypeServer()
                .AddDefaultStructureLinkToStructureUpdater()
                .AddDefaultLocationLinkToLocationUpdater()
                .AddDefaultPermittedStructureLinkToStructureTypeUpdater();

            return services;
        }
    }
}