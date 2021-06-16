using Grpc.Core;
using gRPCAnnotationService.Protos;
using Microsoft.Extensions.Logging;
using System.Threading.Tasks;
using Viking.DataModel.Annotation;
using Viking.gRPC.AnnotationTypes.V1.Protos;

namespace gRPCAnnotationService
{
    public class StructureService : Viking.gRPC.AnnotationTypes.V1.Protos.AnnotateStructures.AnnotateStructuresBase
    {
        private readonly AnnotationContext _context;
        private readonly ILogger<StructureService> _logger;
        public StructureService(AnnotationContext context, ILogger<StructureService> logger)
        {
            _logger = logger;
            _context = context;
        }

        public override async Task<GetStructureByIDResponse> GetStructureByID(GetStructureByIDRequest request, ServerCallContext context)
        {
            try
            { 
                var obj = await _context.Structures.FindAsync(request.Id);
                if (obj == null)
                    throw new Grpc.Core.RpcException(new Status(StatusCode.NotFound, $"Structure ID {request.Id} not found"));
                 
                GetStructureByIDResponse response = new GetStructureByIDResponse()
                {
                    Result = obj.ToProtobufMessage()
                };
                return response;
            }
            catch (System.ArgumentNullException e)
            {
                //This means there was no row with that ID; 
                _logger.LogInformation("Could not find requested location ID: " + request.Id.ToString());
                throw new Grpc.Core.RpcException(new Status(StatusCode.InvalidArgument, $"Structure ID {request.Id}", e));
            }
            catch (System.InvalidOperationException e)
            {
                //This means there was no row with that ID; 
                _logger.LogInformation("Could not find requested location ID: " + request.Id.ToString());
                throw new Grpc.Core.RpcException(new Status(StatusCode.InvalidArgument, $"Structure Type ID {request.Id}", e));
            } 
        }
        /*
        public override Task<HelloReply> SayHello(HelloRequest request, ServerCallContext context)
        {
            return Task.FromResult(new HelloReply
            {
                Message = "Hello " + request.Name
            });
        }
        */
    }
}