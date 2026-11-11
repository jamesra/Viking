using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Viking.Identity.Data;
using Viking.Identity.Models;

namespace Viking.Identity.Server.Extensions.Services
{
    public class PermissionService : IPermissionService
    {
        private readonly ApplicationDbContext _context;
        private readonly ILogger<PermissionService> _logger;
        private readonly IDebugLoggingService _debugLoggingService;

        public PermissionService(ApplicationDbContext context, ILogger<PermissionService> logger, IDebugLoggingService debugLoggingService)
        {
            _context = context;
            _logger = logger;
            _debugLoggingService = debugLoggingService;
        }

        public async Task<Dictionary<long, UserResourcePermissions>> GetUserPermissionsByTypeAsync(
            string userId, string resourceTypeId, IReadOnlyCollection<string> requiredPermissions = null)
        {
            var result = await UserPermissionsByTypeAsync(userId, resourceTypeId);
            if (requiredPermissions == null || requiredPermissions.Count == 0)
                return result;

            // Only resources on which the user holds every requested permission (Read already implied where it is).
            return result
                .Where(kvp => requiredPermissions.All(rp =>
                    kvp.Value.Permissions.Contains(rp, StringComparer.OrdinalIgnoreCase)))
                .ToDictionary(kvp => kvp.Key, kvp => kvp.Value);
        }

        private async Task<Dictionary<long, UserResourcePermissions>> UserPermissionsByTypeAsync(string userId, string resourceTypeId)
        {
            string[] resourceTypes = Array.Empty<string>();
            if (resourceTypeId != null)
                resourceTypes = new string[] { resourceTypeId };

            // Contexts are listed only through Access, whatever the caller's spelling of the type name.
            if (string.Equals(resourceTypeId, nameof(AnnotationContext), StringComparison.OrdinalIgnoreCase))
                return await VolumesWithPermissionsAsync(await _context.UserAnnotationContextPermissionsAsync(userId));

            var userPermittedResources = await _context.UserResourcePermissionsByType(userId, resourceTypes);
            if (string.Equals(resourceTypeId, nameof(AnnotationServer), StringComparison.OrdinalIgnoreCase))
            {
                foreach (var id in userPermittedResources.Keys.ToList())
                    userPermittedResources[id] = ApplicationDBContextExtensions.WithImpliedRead(userPermittedResources[id]);
            }

            var resourceMap = from r in await _context.Resource.ToListAsync()
                              join upr in userPermittedResources.Keys on r.Id equals upr
                              select new UserResourcePermissions
                              {
                                  Id = r.Id,
                                  Name = r.Name,
                                  ResourceType = r.ResourceTypeId,
                                  Permissions = userPermittedResources[upr],
                                  ParentId = r.ParentID,
                                  // An annotation database is listed with its database name (the stored value, else the
                                  // server name) so a client can match volumes without a second call. No URLs here.
                                  Metadata = r is AnnotationServer server
                                      ? new Dictionary<string, object> { [VolumeMetadata.AnnotationDatabaseName] = VolumeMetadata.CorrectionDatabaseName(server) }
                                      : new Dictionary<string, object>()
                              };
            return resourceMap.ToDictionary(r => r.Id, r => r);
        }

        public async Task<List<ResourceTypeInfo>> GetResourceTypeCatalogAsync(string resourceTypeId = null)
        {
            var types = await _context.ResourceTypes
                .Where(t => resourceTypeId == null || t.Id == resourceTypeId)
                .OrderBy(t => t.Id)
                .Select(t => new { t.Id, t.Description })
                .ToListAsync();

            // The permissions come from the enums, the same source the ResourceTypePermission rows are seeded from.
            return types.Select(t => new ResourceTypeInfo
            {
                ResourceTypeId = t.Id,
                Description = t.Description,
                Permissions = PermissionRegistry.Entries(t.Id)
                    .Select(p => new PermissionInfo { PermissionId = p.Id, Description = p.Description })
                    .ToList()
            }).ToList();
        }

        public async Task<Dictionary<long, Dictionary<string, object>>> GetReadableAnnotationServerDetailsAsync(
            string userId, IReadOnlyCollection<long> ids)
        {
            var readable = await GetUserPermissionsByTypeAsync(userId, nameof(AnnotationServer), new[] { AnnotationServerPermission.Read.Id() });
            var wanted = ids.Where(readable.ContainsKey).Distinct().ToList();
            if (wanted.Count == 0)
                return new Dictionary<long, Dictionary<string, object>>();

            var servers = await _context.Set<AnnotationServer>().Where(s => wanted.Contains(s.Id)).ToListAsync();
            return servers.ToDictionary(s => s.Id, ServerMetadata);
        }

        /// <summary>Name and service URLs of an annotation database (annotation server), keyed like <see cref="VolumeMetadata"/>.</summary>
        internal static Dictionary<string, object> ServerMetadata(AnnotationServer server) => new Dictionary<string, object>
        {
            [VolumeMetadata.Description] = server.Description,
            [VolumeMetadata.AnnotationDatabaseName] = VolumeMetadata.CorrectionDatabaseName(server),
            [VolumeMetadata.BaseUrl] = server.BaseUrl,
            [VolumeMetadata.AnnotationEndpoint] = AnnotationServiceUrls.AnnotationUrl(server),
            [VolumeMetadata.ODataEndpoint] = AnnotationServiceUrls.ODataUrl(server),
            [VolumeMetadata.ExportEndpoint] = AnnotationServiceUrls.ExportUrl(server)
        };

        public async Task<List<UserResourcePermissions>> GetUserPermissionsAsync(string userId)
        {
            // Annotation contexts are left out of the grant query on purpose: a context the user holds some grant on
            // but not Access (a leftover data right, say) must not be sent to the client. They are added below, and
            // only through Access.
            var resourceTypeIds = await _context.ResourceTypes
                .Where(rt => rt.Id != nameof(AnnotationContext))
                .Select(rt => rt.Id)
                .ToArrayAsync();

            var userPermittedResources = await _context.UserResourcePermissionsByType(userId, resourceTypeIds);

            // Contexts the user holds Access on are listed with the rights of their annotation server.
            foreach (var volume in await _context.UserAnnotationContextPermissionsAsync(userId))
                userPermittedResources[volume.Key] = volume.Value;

            if (userPermittedResources.Count == 0)
            {
                return new List<UserResourcePermissions>();
            }

            var resourceIds = userPermittedResources.Keys.ToArray();

            var resources = await _context.Resource
                .Where(r => resourceIds.Contains(r.Id))
                .Select(r => new { r.Id, r.Name, r.Description, r.ResourceTypeId, r.ParentID })
                .ToListAsync();

            var resourceSummaries = resources
                .Select(r =>
                {
                    userPermittedResources.TryGetValue(r.Id, out var grantedPermissions);
                    return new UserResourcePermissions
                    {
                        Id = r.Id,
                        Name = r.Name,
                        ResourceType = r.ResourceTypeId,
                        Permissions = grantedPermissions ?? Array.Empty<string>(),
                        ParentId = r.ParentID,
                        Metadata = new Dictionary<string, object>
                        {
                            ["Description"] = r.Description
                        }
                    };
                })
                .ToList();

            return resourceSummaries;
        }

        public async Task<List<string>> GetUserResourcePermissionsAsync(string userId, string resourceId)
        {
            var resourceObj = await _context.FindApiFacingResourceAsync(resourceId);

            if (resourceObj == null)
            {
                return new List<string>();
            }

            return (await _context.UserEffectiveResourcePermissionsAsync(userId, resourceObj)).ToList();
        }

        /// <summary>
        /// Builds the volume rows of the accessible-volume APIs from effective permissions.
        /// </summary>
        private async Task<Dictionary<long, UserResourcePermissions>> VolumesWithPermissionsAsync(Dictionary<long, string[]> volumePermissions)
        {
            if (volumePermissions.Count == 0)
                return new Dictionary<long, UserResourcePermissions>();

            var ids = volumePermissions.Keys.ToList();
            var volumes = await _context.WithCatalog()
                .Where(v => ids.Contains(v.Id))
                .ToListAsync();
            var correctionOwners = await _context.CorrectionOwnersAsync();

            return volumes.ToDictionary(v => v.Id, v => new UserResourcePermissions
            {
                Id = v.Id,
                Name = v.Name,
                ResourceType = nameof(AnnotationContext),
                Permissions = volumePermissions[v.Id],
                ParentId = v.ParentID,
                Metadata = VolumeMetadata.Build(v, correctionOwners.GetValueOrDefault(v.Id))
            });
        }

        public async Task<Dictionary<long, UserResourcePermissions>> GetUserAccessibleAnnotationContextsAsync(string userId)
        {
            return await GetUserPermissionsByTypeAsync(userId, nameof(AnnotationContext));
        }

        public async Task<Dictionary<long, UserResourcePermissions>> GetUserAccessibleSegmentationServicesAsync(string userId)
        {
            var userPermittedResources = await _context.UserResourcePermissionsByType(userId, new[] { nameof(SegmentationService) });

            if (userPermittedResources.Count == 0)
            {
                return new Dictionary<long, UserResourcePermissions>();
            }

            var segmentationServiceIds = userPermittedResources.Keys.Distinct().ToArray();

            var segmentationServices = await _context.SegmentationServices
                .Where(s => segmentationServiceIds.Contains(s.Id))
                .Select(s => new
                {
                    s.Id,
                    s.Name,
                    s.Description,
                    Endpoint = s.Endpoint != null ? s.Endpoint.ToString() : null,
                    s.ParentID
                })
                .ToListAsync();

            return segmentationServices
                .Select(r =>
                {
                    userPermittedResources.TryGetValue(r.Id, out var grantedPermissions);
                    return new UserResourcePermissions
                    {
                        Id = r.Id,
                        Name = r.Name,
                        ResourceType = nameof(SegmentationService),
                        Permissions = grantedPermissions ?? Array.Empty<string>(),
                        ParentId = r.ParentID,
                        Metadata = new Dictionary<string, object>
                        {
                            ["Description"] = r.Description,
                            ["Endpoint"] = r.Endpoint
                        }
                    };
                })
                .ToDictionary(r => r.Id, r => r);
        }

        public async Task<Dictionary<long, UserResourcePermissions>> GetUserAccessibleSegmentationServicesByUsernameAsync(string username)
        {
            var appUser = await _context.Users.FirstOrDefaultAsync(u => u.UserName == username);
            if (appUser == null)
            {
                return new Dictionary<long, UserResourcePermissions>();
            }

            return await GetUserAccessibleSegmentationServicesAsync(appUser.Id);
        }

        public async Task<Dictionary<long, UserResourcePermissions>> GetUserAccessibleAnnotationContextsByUsernameAsync(string username)
        {
            var appUser = await _context.Users.FirstOrDefaultAsync(u => u.UserName == username);
            if (appUser == null)
            {
                _logger.LogDebugIfEnabled(_debugLoggingService, DebugLogCategory.Permissions, "User '{Username}' not found in database", username);
                return new Dictionary<long, UserResourcePermissions>();
            }

            _logger.LogDebugIfEnabled(_debugLoggingService, DebugLogCategory.Permissions, "Found user '{Username}' with ID: {UserId}", username, appUser.Id);

            var volumePermissions = await _context.UserAnnotationContextPermissionsAsync(appUser.Id);
            _logger.LogDebugIfEnabled(_debugLoggingService, DebugLogCategory.Permissions, "User {Username} has permissions for {Count} Volume resources", username, volumePermissions.Count);

            return await VolumesWithPermissionsAsync(volumePermissions);
        }

        public async Task<List<VolumeTreeNode>> GetUserAccessibleVolumeTreeAsync(string userId)
        {
            return await BuildVolumeTreeAsync(userId);
        }

        public async Task<List<VolumeTreeNode>> GetUserAccessibleVolumeTreeForAnonymousAsync()
        {
            var volumes = await VolumesWithPermissionsAsync(await _context.AnonymousVolumePermissionsAsync());
            if (volumes.Count == 0)
            {
                return new List<VolumeTreeNode>();
            }

            var tree = await BuildVolumeTreeAsync(null, null, volumes, null);
            return tree ?? new List<VolumeTreeNode>();
        }

        private async Task<List<VolumeTreeNode>> BuildVolumeTreeAsync(string userId, long? parentID = null, Dictionary<long, UserResourcePermissions> volumes = null, List<Resource> organizations = null)
        {
            if (volumes is null)
            {
                volumes = await GetUserPermissionsByTypeAsync(userId, nameof(AnnotationContext));
                if (volumes.Count == 0)
                {
                    return new List<VolumeTreeNode>();
                }
            }

            if (organizations is null)
            {
                organizations = await _context.Resource
                    .Where(o => o.ResourceTypeId == nameof(OrganizationalUnit))
                    .OrderBy(o => o.Name)
                    .ToListAsync();
            }

            var result = new List<VolumeTreeNode>();

            // Root-level volumes (no parent OU)
            if (parentID is null)
            {
                var rootVolumes = volumes.Values.Where(v => v.ParentId == null).ToList();
                if (rootVolumes.Any())
                {
                    result.Add(new VolumeTreeNode
                    {
                        Id = 0,
                        Name = "Volumes",
                        ResourceType = nameof(OrganizationalUnit),
                        Volumes = rootVolumes,
                        Children = new List<VolumeTreeNode>()
                    });
                }
            }

            var parentOrgs = organizations.Where(o => o.ParentID == parentID).ToList();

            foreach (var org in parentOrgs)
            {
                var orgVolumes = volumes.Values.Where(v => v.ParentId == org.Id).ToList();
                var children = await BuildVolumeTreeAsync(userId, org.Id, volumes, organizations);

                if (!orgVolumes.Any() && (children == null || children.Count == 0))
                    continue;

                result.Add(new VolumeTreeNode
                {
                    Id = org.Id,
                    Name = org.Name,
                    ParentId = org.ParentID,
                    ResourceType = nameof(OrganizationalUnit),
                    Volumes = orgVolumes,
                    Children = children ?? new List<VolumeTreeNode>()
                });
            }

            return result;
        }
    }
}

