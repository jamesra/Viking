using System;
using System.Linq;
using System.Security.Authentication;
using Grpc.Core;
using gRPCAnnotationService.Protos;
using Microsoft.Extensions.Logging;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.Authorization;
using Microsoft.EntityFrameworkCore;
using Viking.DataModel.Annotation;
using Viking.AnnotationServiceTypes.gRPC.V1.Protos;

namespace gRPCAnnotationService
{
    public class LocationService : global::Viking.AnnotationServiceTypes.gRPC.V1.Protos.AnnotateLocations.AnnotateLocationsBase
    {
        private readonly AnnotationContext _context;
        private readonly ILogger<LocationService> _logger;
        public LocationService(AnnotationContext context, ILogger<LocationService> logger)
        {
            _logger = logger;
            _context = context;
        }

        public override Task<CreateLocationResponse> CreateLocation(CreateLocationRequest request, ServerCallContext context)
        {
            return base.CreateLocation(request, context);
        }

        public override Task<CreateLocationLinkResponse> CreateLocationLink(CreateLocationLinkRequest request, ServerCallContext context)
        {
            return base.CreateLocationLink(request, context);
        }

        public override Task<DeleteLocationLinkResponse> DeleteLocationLink(DeleteLocationLinkRequest request, ServerCallContext context)
        {
            return base.DeleteLocationLink(request, context);
        }

        public override Task<GetAnnotationsInMosaicRegionResponse> GetAnnotationsInMosaicRegion(GetAnnotationsInMosaicRegionRequest request, ServerCallContext context)
        {
            return base.GetAnnotationsInMosaicRegion(request, context);
        }

        //[Authorize(AuthenticationSchemes = JwtBearerDefaults.AuthenticationScheme)]
        //[Authorize(AuthenticationSchemes = "protectedScope")]
        [Authorize(Policy = "protectedScope")]
        //[Authenticate]
        public override async Task<GetLastModifiedLocationResponse> GetLastModifiedLocation(GetLastModifiedLocationRequest request, ServerCallContext context)
        {
            var user = context.GetHttpContext().User;
            if (user.Identity is not null && user.Identity.IsAuthenticated)
            {
                var latest = (from l in _context.Locations
                    orderby l.LastModified descending 
                    where l.Username == user.Identity.Name
                    select l).Take(1);

                var loc_result = await latest.FirstOrDefaultAsync();

                if(loc_result != null)
                    return new GetLastModifiedLocationResponse { Result = loc_result.ToProtobufMessage() };
                else
                {
                    throw new ArgumentException($"No last modified location found for {user.Identity.Name}");
                }
            }

            throw new AuthenticationException("User was not authenticated");

        }

        public override Task<GetLinkedLocationsResponse> GetLinkedLocations(GetLinkedLocationsRequest request, ServerCallContext context)
        {
            return base.GetLinkedLocations(request, context);
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
                    Result = obj.ToProtobufMessage()
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

        public override Task<GetLocationChangeLogResponse> GetLocationChangeLog(GetLocationChangeLogRequest request, ServerCallContext context)
        {
            return base.GetLocationChangeLog(request, context);
        }

        public override Task<GetLocationChangesResponse> GetLocationChanges(GetLocationChangesRequest request, ServerCallContext context)
        {
            return base.GetLocationChanges(request, context);
        }

        public override Task<GetLocationChangesInMosaicRegionResponse> GetLocationChangesInMosaicRegion(GetLocationChangesInMosaicRegionRequest request, ServerCallContext context)
        {
            return base.GetLocationChangesInMosaicRegion(request, context);
        }

        public override Task<GetLocationLinksForSectionResponse> GetLocationLinksForSection(GetLocationLinksForSectionRequest request, ServerCallContext context)
        {
            return base.GetLocationLinksForSection(request, context);
        }

        public override Task<GetLocationLinksForSectionInMosaicRegionResponse> GetLocationLinksForSectionInMosaicRegion(GetLocationLinksForSectionInMosaicRegionRequest request, ServerCallContext context)
        {
            return base.GetLocationLinksForSectionInMosaicRegion(request, context);
        }

        public override Task<GetLocationsByIDResponse> GetLocationsByID(GetLocationsByIDRequest request, ServerCallContext context)
        {
            return base.GetLocationsByID(request, context);
        }

        public override Task<GetLocationsForSectionResponse> GetLocationsForSection(GetLocationsForSectionRequest request, ServerCallContext context)
        {
            return base.GetLocationsForSection(request, context);
        }

        public override Task<GetStructureLocationsResponse> GetStructureLocations(GetStructureLocationsRequest request, ServerCallContext context)
        {
            return base.GetStructureLocations(request, context);
        }

        public override Task<UpdateLocationsResponse> Update(UpdateLocationsRequest request, ServerCallContext context)
        {
            return base.Update(request, context);
        }
    }
}