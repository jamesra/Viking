using System.Collections.Generic;
using System.Text;
using Microsoft.Extensions.DependencyInjection;
using Viking.AnnotationServiceTypes.gRPC.V1.Protos;
using WebAnnotationModel;

namespace WebAnnotationModel.gRPC.Converters
{
    public static class AnnotationSetConverterExtensions
    {
        public static IServiceCollection AddStandardQueryConverters(this IServiceCollection service)
        {
            service.AddTransient<IServerQuerySingleAddOrUpdateHandler<AnnotationSet>, ProcessorForServerAnnotationSets>();
            return service;
        }
    }
}
