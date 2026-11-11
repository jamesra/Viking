using System;
using System.Collections.Generic;
using System.Diagnostics.CodeAnalysis;
using System.Linq;
using System.Net.Mime;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Viking.Identity.Data;
using Viking.Identity.Models;
using Viking.Identity.Server.Extensions.Services;
using Viking.Identity.Server.WebApi.Models;

namespace Viking.Identity.Server.WebApi.ApiControllers
{
    [Produces(MediaTypeNames.Application.Json)]
    [ApiController]
    [Route("[controller]")]
    public partial class PermissionsController : ControllerBase
    {
        private readonly ApplicationDbContext _context;
        private readonly IAuthenticationService _authService;
        private readonly IPermissionService _permissionService;

        public PermissionsController(
            ApplicationDbContext context,
            IAuthenticationService authService,
            IPermissionService permissionService)
        {
            _context = context;
            _authService = authService;
            _permissionService = permissionService;
        }

        [HttpGet("CurrentUser")]
        public async Task<ActionResult<string>> GetUsername()
        {
            var user = await GetAuthenticatedUserAsync();
            if (user == null)
                return Unauthorized();
            return user.UserName;
        }

        [HttpGet("CurrentUserId")]
        public async Task<ActionResult<string>> GetUserId()
        {
            var user = await GetAuthenticatedUserAsync();
            if (user == null)
                return Unauthorized();
            return user.Id;
        }

        /// <summary>
        /// The permission catalog: every resource type with the permissions it defines (<c>permissionId</c>,
        /// <c>description</c>). Clients read permission names here instead of hardcoding them. Not user specific.
        /// </summary>
        [HttpGet("ResourceTypes")]
        public async Task<ActionResult<List<ResourceTypeDto>>> ResourceTypes()
        {
            var caller = await GetAuthenticatedUserAsync();
            if (caller == null)
                return Unauthorized();

            return (await _permissionService.GetResourceTypeCatalogAsync()).Select(ToDto).ToList();
        }

        /// <summary>One entry of <see cref="ResourceTypes"/>; 404 when the type does not exist.</summary>
        [HttpGet("ResourceTypes/{resourceTypeId}")]
        public async Task<ActionResult<ResourceTypeDto>> ResourceTypeById(string resourceTypeId)
        {
            var caller = await GetAuthenticatedUserAsync();
            if (caller == null)
                return Unauthorized();

            var type = (await _permissionService.GetResourceTypeCatalogAsync(resourceTypeId)).FirstOrDefault();
            if (type == null)
                return NotFound();
            return ToDto(type);
        }

        private static ResourceTypeDto ToDto(ResourceTypeInfo type) => new ResourceTypeDto
        {
            ResourceTypeId = type.ResourceTypeId,
            Description = type.Description,
            Permissions = type.Permissions
                .Select(p => new PermissionDto { PermissionId = p.PermissionId, Description = p.Description })
                .ToList()
        };

        /// <summary>
        /// Resources of one type the caller holds grants on, keyed by resource id, each with <c>id</c>, <c>name</c>,
        /// <c>resourceType</c> and <c>permissions</c>. Repeat <c>?permission=Read</c> to keep only resources on which the
        /// caller holds all the named permissions, e.g. <c>type/AnnotationServer?permission=Read</c> lists the annotation
        /// databases the caller may read, whatever annotation contexts they have. Details (URLs) are read with
        /// <see cref="AnnotationServerDetails"/>.
        /// </summary>
        [HttpGet("type/{resourceTypeId}")]
        public async Task<ActionResult<Dictionary<long, object>>> UserPermissionsByType(
            string resourceTypeId = null, [FromQuery(Name = "permission")] string[] permission = null)
        {
            var caller = await GetAuthenticatedUserAsync();
            if (caller == null)
                return Unauthorized();

            if (resourceTypeId != null && permission != null && permission.Length > 0)
            {
                var error = PermissionRegistry.FilterError(resourceTypeId, permission);
                if (error != null)
                    return BadRequest(error);
            }

            var permissions = await _permissionService.GetUserPermissionsByTypeAsync(caller.Id, resourceTypeId, permission);
            return ToTypeListing(permissions);
        }

        [HttpGet]
        public async Task<ActionResult<List<object>>> UserPermissions()
        {
            var caller = await GetAuthenticatedUserAsync();
            if (caller == null)
                return Unauthorized();

            var permissions = await _permissionService.GetUserPermissionsAsync(caller.Id);
            return permissions
                .Select(p => (object)new
                {
                    p.Id,
                    p.Name,
                    Description = p.Metadata.TryGetValue("Description", out var desc) ? desc : null,
                    p.ResourceType,
                    permissions = p.Permissions
                })
                .ToList();
        }

        [HttpGet("resource/{resourceId}")]
        public async Task<ActionResult<List<string>>> UserPermissions([NotNull] string resourceId)
        {
            if (resourceId is null)
                throw new ArgumentNullException(nameof(resourceId));

            var caller = await GetAuthenticatedUserAsync();
            if (caller == null)
                return Unauthorized();

            if (await _context.FindApiFacingResourceAsync(resourceId) == null)
                return NotFound();

            var permissions = await _permissionService.GetUserResourcePermissionsAsync(caller.Id, resourceId);
            return permissions;
        }

        [HttpGet("{userId}/resource/{resourceId}")]
        public async Task<ActionResult<List<string>>> UserPermissionsByUserId([NotNull] string resourceId, [NotNull] string userId)
        {
            if (resourceId is null)
                throw new ArgumentNullException(nameof(resourceId));
            if (userId is null)
                throw new ArgumentNullException(nameof(userId));

            var caller = await GetAuthenticatedUserAsync();
            if (caller == null)
                return Unauthorized();

            if (caller.Id != userId && !User.IsInRole(Special.Roles.Admin))
                return Forbid();

            if (await _context.FindApiFacingResourceAsync(resourceId) == null)
                return NotFound();

            var permissions = await _permissionService.GetUserResourcePermissionsAsync(userId, resourceId);
            return permissions;
        }

        /// <summary>
        /// Annotation contexts the caller can open, with the annotation server each one uses. Also served as
        /// <c>AccessibleVolumes</c>, the route shipped clients call to resolve <c>AnnotationServerName</c>; without it
        /// they scope the token by context name (e.g. <c>RC1-Pitt.Read</c>) instead of by annotation server
        /// (<c>RC1.Read</c>) and the annotation service denies access.
        /// </summary>
        [HttpGet("AccessibleAnnotationContexts")]
        [HttpGet("AccessibleVolumes")]
        public async Task<ActionResult<Dictionary<long, object>>> UserAccessibleAnnotationContexts()
        {
            var caller = await GetAuthenticatedUserAsync();
            if (caller == null)
                return Unauthorized();

            var volumes = await _permissionService.GetUserAccessibleAnnotationContextsAsync(caller.Id);
            return ToVolumeObjectDictionary(volumes);
        }

        [AllowAnonymous]
        [HttpGet("UserAccessibleVolumeTree")]
        public async Task<ActionResult<List<VolumeTreeNodeDto>>> UserAccessibleVolumeTree()
        {
            var caller = await _authService.GetApplicationUserAsync(User, HttpContext);
            var tree = caller != null
                ? await _permissionService.GetUserAccessibleVolumeTreeAsync(caller.Id)
                : await _permissionService.GetUserAccessibleVolumeTreeForAnonymousAsync();
            return MapToDto(tree);
        }

        /// <summary>
        /// Database name and service URLs of annotation servers (annotation databases), several ids per call:
        /// <c>AnnotationServers?id=3&amp;id=7</c>. Keyed by id. Each entry is authorized on its own: ids that do not
        /// exist or on which the caller lacks Read are omitted from the result.
        /// </summary>
        [HttpGet("AnnotationServers")]
        public async Task<ActionResult<Dictionary<long, Dictionary<string, object>>>> AnnotationServerDetails(
            [FromQuery(Name = "id")] long[] id)
        {
            var caller = await GetAuthenticatedUserAsync();
            if (caller == null)
                return Unauthorized();
            if (id == null || id.Length == 0)
                return BadRequest("at least one id is required");

            return await _permissionService.GetReadableAnnotationServerDetailsAsync(caller.Id, id);
        }

        [HttpGet("AccessibleSegmentationServices")]
        public async Task<ActionResult<Dictionary<long, object>>> UserAccessibleSegmentationServices()
        {
            var caller = await GetAuthenticatedUserAsync();
            if (caller == null)
                return Unauthorized();

            var services = await _permissionService.GetUserAccessibleSegmentationServicesAsync(caller.Id);
            return ToObjectDictionary(services);
        }

        /// <summary>
        /// Lists the annotation contexts a correction or segmentation service serves (its assigned resources). Called by
        /// the container at startup and on refresh with a client-credentials token, so there is no user: access is limited
        /// to the "correction" and "segmentation" OAuth clients, and the client id picks which kind of service
        /// <paramref name="service"/> names.
        /// </summary>
        /// <param name="service">Name of the CorrectionService or SegmentationService resource the container is configured with.</param>
        /// <param name="cancellationToken">Request abort.</param>
        [HttpGet("AssignedAnnotationContexts")]
        public async Task<ActionResult<List<AssignedAnnotationContext>>> AssignedAnnotationContexts(
            [FromQuery] string service, CancellationToken cancellationToken)
        {
            string clientId = User.FindFirst("client_id")?.Value;
            if (clientId != CorrectionClientId && clientId != SegmentationClientId)
                return Forbid();
            if (string.IsNullOrWhiteSpace(service))
                return BadRequest("service is required");

            var assigned = clientId == CorrectionClientId
                ? await ContextServiceAssignments.ListAsync<CorrectionService>(_context, service, cancellationToken)
                : await ContextServiceAssignments.ListAsync<SegmentationService>(_context, service, cancellationToken);
            if (assigned == null)
                return NotFound();
            return assigned;
        }

        /// <summary>OAuth client id issued to correction service containers (see IdentityServerVikingClientStore).</summary>
        private const string CorrectionClientId = "correction";

        /// <summary>OAuth client id issued to segmentation server containers (see IdentityServerVikingClientStore).</summary>
        private const string SegmentationClientId = "segmentation";

        private async Task<ApplicationUser> GetAuthenticatedUserAsync()
        {
            return await _authService.GetApplicationUserAsync(User, HttpContext);
        }

        /// <summary>
        /// Slim per-type listing: identity and permissions only. Annotation servers add <c>AnnotationDatabaseName</c>;
        /// other types carry no extra field.
        /// </summary>
        private static Dictionary<long, object> ToTypeListing(Dictionary<long, UserResourcePermissions> permissions)
        {
            return permissions.ToDictionary(
                kvp => kvp.Key,
                kvp =>
                {
                    var p = kvp.Value;
                    var entry = new Dictionary<string, object>
                    {
                        ["id"] = p.Id,
                        ["name"] = p.Name,
                        ["resourceType"] = p.ResourceType,
                        ["permissions"] = p.Permissions
                    };
                    if (p.Metadata.TryGetValue(VolumeMetadata.AnnotationDatabaseName, out var databaseName))
                        entry["AnnotationDatabaseName"] = databaseName;
                    return (object)entry;
                });
        }

        private static Dictionary<long, object> ToObjectDictionary(Dictionary<long, UserResourcePermissions> permissions)
        {
            return permissions.ToDictionary(
                kvp => kvp.Key,
                kvp =>
                {
                    var p = kvp.Value;
                    p.Metadata.TryGetValue("Description", out var description);
                    p.Metadata.TryGetValue("Endpoint", out var endpoint);
                    return (object)new
                    {
                        p.Id,
                        p.Name,
                        Description = description,
                        Endpoint = endpoint,
                        ResourceType = p.ResourceType,
                        permissions = p.Permissions
                    };
                });
        }

        /// <summary>
        /// Same shape as <see cref="ToObjectDictionary"/> (Endpoint is still the VikingXML URL) plus the
        /// annotation server, image set, mirrors, and registration added with the AnnotationServer split.
        /// </summary>
        private static Dictionary<long, object> ToVolumeObjectDictionary(Dictionary<long, UserResourcePermissions> volumes)
        {
            return volumes.ToDictionary(
                kvp => kvp.Key,
                kvp =>
                {
                    var v = kvp.Value;
                    object Meta(string key) => v.Metadata.TryGetValue(key, out var value) ? value : null;
                    return (object)new
                    {
                        v.Id,
                        v.Name,
                        Description = Meta(VolumeMetadata.Description),
                        Endpoint = Meta(VolumeMetadata.Endpoint),
                        AnnotationServerName = Meta(VolumeMetadata.AnnotationServerName),
                        AnnotationEndpoint = Meta(VolumeMetadata.AnnotationEndpoint),
                        Volume = Meta(VolumeMetadata.Volume),
                        BaseUrl = Meta(VolumeMetadata.BaseUrl),
                        AnnotationDatabaseName = Meta(VolumeMetadata.AnnotationDatabaseName),
                        ODataEndpoint = Meta(VolumeMetadata.ODataEndpoint),
                        ExportEndpoint = Meta(VolumeMetadata.ExportEndpoint),
                        AnnotationServers = Meta(VolumeMetadata.AnnotationServers),
                        Mirrors = Meta(VolumeMetadata.Mirrors),
                        RegistrationName = Meta(VolumeMetadata.RegistrationName),
                        CorrectionServiceUrl = Meta(VolumeMetadata.CorrectionServiceUrl),
                        permissions = v.Permissions
                    };
                });
        }

        private static List<VolumeTreeNodeDto> MapToDto(List<VolumeTreeNode> nodes)
        {
            if (nodes == null || nodes.Count == 0)
                return new List<VolumeTreeNodeDto>();

            return nodes.Select(MapNode).ToList();
        }

        private static VolumeTreeNodeDto MapNode(VolumeTreeNode node)
        {
            return new VolumeTreeNodeDto
            {
                Id = node.Id,
                Name = node.Name,
                ParentId = node.ParentId,
                ResourceType = node.ResourceType,
                Volumes = node.Volumes?.Select(v => new UserResourcePermissionsDto
                {
                    Id = v.Id,
                    Name = v.Name,
                    ResourceType = v.ResourceType,
                    Permissions = v.Permissions,
                    ParentId = v.ParentId,
                    Metadata = v.Metadata ?? new Dictionary<string, object>()
                }).ToList() ?? new List<UserResourcePermissionsDto>(),
                Children = node.Children?.Select(MapNode).ToList() ?? new List<VolumeTreeNodeDto>()
            };
        }
    }
}
