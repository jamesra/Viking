using System;
using System.Collections.Generic;
using System.Diagnostics.CodeAnalysis;
using System.Linq;
using System.Net.Mime;
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

        [HttpGet("type/{resourceTypeId}")]
        public async Task<ActionResult<Dictionary<long, object>>> UserPermissionsByType(string resourceTypeId = null)
        {
            var caller = await GetAuthenticatedUserAsync();
            if (caller == null)
                return Unauthorized();

            var permissions = await _permissionService.GetUserPermissionsByTypeAsync(caller.Id, resourceTypeId);
            return ToObjectDictionary(permissions);
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

        [HttpGet("AccessibleVolumes")]
        public async Task<ActionResult<Dictionary<long, object>>> UserAccessibleVolumes()
        {
            var caller = await GetAuthenticatedUserAsync();
            if (caller == null)
                return Unauthorized();

            var volumes = await _permissionService.GetUserAccessibleVolumesAsync(caller.Id);
            return ToObjectDictionary(volumes);
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

        [HttpGet("AccessibleSegmentationServices")]
        public async Task<ActionResult<Dictionary<long, object>>> UserAccessibleSegmentationServices()
        {
            var caller = await GetAuthenticatedUserAsync();
            if (caller == null)
                return Unauthorized();

            var services = await _permissionService.GetUserAccessibleSegmentationServicesAsync(caller.Id);
            return ToObjectDictionary(services);
        }

        private async Task<ApplicationUser> GetAuthenticatedUserAsync()
        {
            return await _authService.GetApplicationUserAsync(User, HttpContext);
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
                        permissions = p.Permissions
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
