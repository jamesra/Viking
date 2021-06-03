using Grpc.Core;
using GrpcAnnotationService.Protos;
using Microsoft.Extensions.Logging;
using System.Linq;
using System.Threading.Tasks;
using Viking.DataModel.Annotation;
using Viking.gRPC.AnnotationTypes.V1.Protos;

namespace GrpcAnnotationService
{
    public class PermittedStructureLinksService : Viking.gRPC.AnnotationTypes.V1.Protos.PermittedStructureLinks.PermittedStructureLinksBase
    {
        private readonly AnnotationContext _context;
        private readonly ILogger<LocationService> _logger;
        public PermittedStructureLinksService(AnnotationContext context, ILogger<LocationService> logger)
        {
            _logger = logger;
            _context = context;
        }

        public override async Task<GetPermittedStructureLinksResponse> GetPermittedStructureLinks(GetPermittedStructureLinksRequest request, ServerCallContext context)
        {
            try
            {
                GetPermittedStructureLinksResponse response = new GetPermittedStructureLinksResponse()
                {
                };

                response.PermittedLinks.AddRange(_context.PermittedStructureLinks.Select(p =>
                    new Viking.gRPC.AnnotationTypes.V1.Protos.PermittedStructureLink() {
                        SourceTypeId = p.SourceTypeId,
                        TargetTypeId = p.TargetTypeId,
                        Bidirectional = p.Bidirectional,
                    })
                );

                return response;
            }
            catch (System.Exception e)
            {
                //This means there was no row with that ID; 
                _logger.LogInformation($"{nameof(GetPermittedStructureLinks)}: {e}");
                throw new Grpc.Core.RpcException(new Status(StatusCode.Unknown, nameof(GetPermittedStructureLinks), e));

            }
        }

        public override async Task<CreatePermittedStructureLinkResponse> CreatePermittedStructureLink(CreatePermittedStructureLinkRequest request, ServerCallContext context)
        {
            try
            {
                Viking.DataModel.Annotation.PermittedStructureLink new_obj = request.NewObj.ToPermittedStructureLink();

                var ef_result = await _context.PermittedStructureLinks.AddAsync(new_obj);

                CreatePermittedStructureLinkResponse response = new CreatePermittedStructureLinkResponse()
                {
                    Result = ef_result.Entity.ToProtobufMessage()
                };
                  
                return response;
            }
            catch (System.Exception e)
            {
                //This means there was no row with that ID; 
                _logger.LogInformation($"{nameof(GetPermittedStructureLinks)}: {e}");
                throw new Grpc.Core.RpcException(new Status(StatusCode.Unknown, nameof(GetPermittedStructureLinks), e));

            }
        }

        public override async Task<UpdatePermittedStructureLinksResponse> UpdatePermittedStructureLinks(UpdatePermittedStructureLinksRequest request, ServerCallContext context)
        {
            try
            {
                UpdatePermittedStructureLinksResponse response = new UpdatePermittedStructureLinksResponse()
                { 
                };

                foreach (var r in request.Changes)
                {
                    var ef_obj = r.Result.ToPermittedStructureLink();

                    PermittedStructureLinkChangeResponse row_response = new PermittedStructureLinkChangeResponse() { Action = r.Action };

                    switch (r.Action)
                    {
                        case DBAction.None:
                            row_response.Sucess = true;
                            break;
                        case DBAction.Insert:
                            await _context.PermittedStructureLinks.AddAsync(r.Result.ToPermittedStructureLink());
                            break;
                        case DBAction.Update:
                            var psl = _context.PermittedStructureLinks.FirstOrDefault(psl => psl.SourceTypeId == r.Result.SourceTypeId && psl.TargetTypeId == r.Result.TargetTypeId);
                            psl.Bidirectional = r.Result.Bidirectional;
                            var EF_Result = _context.PermittedStructureLinks.Update(psl);
                            row_response.Sucess = true;
                            row_response.Result = EF_Result.Entity.ToProtobufMessage();
                            break;
                        case DBAction.Delete:
                            var EF_remove_row = _context.PermittedStructureLinks.FirstOrDefault(psl => psl.SourceTypeId == r.Result.SourceTypeId && psl.TargetTypeId == r.Result.TargetTypeId);
                            _context.PermittedStructureLinks.Remove(EF_remove_row);
                            row_response.Sucess = true;
                            break;
                    }

                    response.Changes.Add(row_response);
                }

                await _context.SaveChangesAsync();

                return response;
            }
            catch (System.Exception e)
            {
                //This means there was no row with that ID; 
                _logger.LogInformation($"{nameof(GetPermittedStructureLinks)}: {e}");
                throw new Grpc.Core.RpcException(new Status(StatusCode.Unknown, nameof(GetPermittedStructureLinks), e));

            } 
        }
    }
}