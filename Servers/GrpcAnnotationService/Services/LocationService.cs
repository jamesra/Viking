using Grpc.Core;
using GrpcAnnotationService.Protos;
using Microsoft.Extensions.Logging;
using System.Threading.Tasks;
using Viking.DataModel.Annotation;
using Viking.gRPC.AnnotationTypes.V1.Protos;

namespace GrpcAnnotationService
{
    public class LocationService : Viking.gRPC.AnnotationTypes.V1.Protos.AnnotateLocations.AnnotateLocationsBase
    {
        private readonly AnnotationContext _context;
        private readonly ILogger<LocationService> _logger;
        public LocationService(AnnotationContext context, ILogger<LocationService> logger)
        {
            _logger = logger;
            _context = context;
        }

        public override async Task<GetLocationByIDResponse> GetLocationByID(GetLocationByIDRequest request, ServerCallContext context)
        {
            try
            { 
                var obj = await _context.Locations.FindAsync(request.Id);
                if (obj == null)
                    throw new Grpc.Core.RpcException(new Status(StatusCode.NotFound, $"Location ID {request.Id} not found"));

                GetLocationByIDResponse response = new GetLocationByIDResponse()
                {
                    Value = obj.ToProtobufMessage()
                };
                return response;
            }
            catch (System.ArgumentNullException e)
            {
                //This means there was no row with that ID; 
                _logger.LogInformation("Could not find requested location ID: " + request.Id.ToString());
                throw new Grpc.Core.RpcException(new Status(StatusCode.InvalidArgument, $"Location ID {request.Id}", e));
            }
            catch (System.InvalidOperationException e)
            {
                //This means there was no row with that ID; 
                _logger.LogInformation("Could not find requested location ID: " + request.Id.ToString());
                throw new Grpc.Core.RpcException(new Status(StatusCode.InvalidArgument, $"Location ID {request.Id}", e));
            }
        }
    }
}