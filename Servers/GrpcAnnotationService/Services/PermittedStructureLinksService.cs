using Grpc.Core;
using gRPCAnnotationService.Protos;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using System.Linq;
using System.Threading.Tasks;
using Viking.DataModel.Annotation;
using Viking.AnnotationServiceTypes.gRPC.V1.Protos;

namespace gRPCAnnotationService
{
    public class PermittedStructureLinksService : Viking.AnnotationServiceTypes.gRPC.V1.Protos.PermittedStructureLinks.PermittedStructureLinksBase
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
                var rows = await _context.PermittedStructureLinks.AsNoTracking().ToListAsync();
                var response = new GetPermittedStructureLinksResponse();
                response.PermittedLinks.AddRange(rows.Select(p => p.ToProtobufMessage()));
                return response;
            }
            catch (System.Exception e)
            {
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
                await _context.SaveChangesAsync();

                return new CreatePermittedStructureLinkResponse
                {
                    Result = ef_result.Entity.ToProtobufMessage()
                };
            }
            catch (System.Exception e)
            {
                _logger.LogInformation($"{nameof(CreatePermittedStructureLink)}: {e}");
                throw new Grpc.Core.RpcException(new Status(StatusCode.Unknown, nameof(CreatePermittedStructureLink), e));
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
                            var inserted = await _context.PermittedStructureLinks.AddAsync(r.Result.ToPermittedStructureLink());
                            row_response.Sucess = true;
                            row_response.Result = inserted.Entity.ToProtobufMessage();
                            break;
                        case DBAction.Update:
                            var psl = _context.PermittedStructureLinks.FirstOrDefault(psl => psl.SourceTypeId == r.Result.SourceTypeId && psl.TargetTypeId == r.Result.TargetTypeId);
                            if (psl == null)
                            {
                                row_response.Sucess = false;
                                break;
                            }
                            psl.Bidirectional = r.Result.Bidirectional;
                            var EF_Result = _context.PermittedStructureLinks.Update(psl);
                            row_response.Sucess = true;
                            row_response.Result = EF_Result.Entity.ToProtobufMessage();
                            break;
                        case DBAction.Delete:
                            var EF_remove_row = _context.PermittedStructureLinks.FirstOrDefault(psl => psl.SourceTypeId == r.Result.SourceTypeId && psl.TargetTypeId == r.Result.TargetTypeId);
                            if (EF_remove_row == null)
                            {
                                row_response.Sucess = false;
                                break;
                            }
                            _context.PermittedStructureLinks.Remove(EF_remove_row);
                            row_response.Sucess = true;
                            // Echo the request key so clients can reconcile DeletedIDs without a row read.
                            row_response.Result = r.Result;
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