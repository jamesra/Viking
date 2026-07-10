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

        public async Task<Dictionary<long, UserResourcePermissions>> GetUserPermissionsByTypeAsync(string userId, string resourceTypeId)
        {
            string[] resourceTypes = Array.Empty<string>();
            if (resourceTypeId != null)
                resourceTypes = new string[] { resourceTypeId };

            var userPermittedResources = await _context.UserResourcePermissionsByType(userId, resourceTypes);

            // For Volume resources, include description and endpoint
            if (resourceTypeId == nameof(Volume))
            {
                var resourceMap = from r in await _context.Volume.ToListAsync()
                                  join upr in userPermittedResources.Keys on r.Id equals upr
                                  select new UserResourcePermissions
                                  {
                                      Id = r.Id,
                                      Name = r.Name,
                                      ResourceType = resourceTypeId,
                                      Permissions = userPermittedResources[upr],
                                      ParentId = r.ParentID,
                                      Metadata = new Dictionary<string, object>
                                      {
                                          ["Description"] = r.Description,
                                          ["Endpoint"] = r.Endpoint?.ToString()
                                      }
                                  };
                return resourceMap.ToDictionary(r => r.Id, r => r);
            }
            else
            {
                var resourceMap = from r in await _context.Resource.ToListAsync()
                                  join upr in userPermittedResources.Keys on r.Id equals upr
                                  select new UserResourcePermissions
                                  {
                                      Id = r.Id,
                                      Name = r.Name,
                                      ResourceType = r.ResourceTypeId,
                                      Permissions = userPermittedResources[upr],
                                      ParentId = r.ParentID,
                                      Metadata = new Dictionary<string, object>()
                                  };
                return resourceMap.ToDictionary(r => r.Id, r => r);
            }
        }

        public async Task<List<UserResourcePermissions>> GetUserPermissionsAsync(string userId)
        {
            var resourceTypeIds = await _context.ResourceTypes
                .Select(rt => rt.Id)
                .ToArrayAsync();

            if (resourceTypeIds.Length == 0)
            {
                return new List<UserResourcePermissions>();
            }

            var userPermittedResources = await _context.UserResourcePermissionsByType(userId, resourceTypeIds);

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

            var result = await _context.UserResourcePermissions(userId, resourceObj.Id);
            return await result.ToListAsync();
        }

        public async Task<Dictionary<long, UserResourcePermissions>> GetUserAccessibleVolumesAsync(string userId)
        {
            return await GetUserPermissionsByTypeAsync(userId, nameof(Volume));
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

        public async Task<Dictionary<long, UserResourcePermissions>> GetUserAccessibleVolumesByUsernameAsync(string username)
        {
            var appUser = await _context.Users.FirstOrDefaultAsync(u => u.UserName == username);
            if (appUser == null)
            {
                _logger.LogDebugIfEnabled(_debugLoggingService, DebugLogCategory.Permissions, "User '{Username}' not found in database", username);
                return new Dictionary<long, UserResourcePermissions>();
            }

            _logger.LogDebugIfEnabled(_debugLoggingService, DebugLogCategory.Permissions, "Found user '{Username}' with ID: {UserId}", username, appUser.Id);

            var userPermittedResources = await _context.UserResourcePermissionsByType(appUser.Id, new string[] { nameof(Volume) });
            _logger.LogDebugIfEnabled(_debugLoggingService, DebugLogCategory.Permissions, "User {Username} has permissions for {Count} Volume resources", username, userPermittedResources.Count);

            var resourceMap = from r in await _context.Volume.ToListAsync()
                              join upr in userPermittedResources.Keys on r.Id equals upr
                              select new UserResourcePermissions
                              {
                                  Id = r.Id,
                                  Name = r.Name,
                                  ResourceType = nameof(Volume),
                                  Permissions = userPermittedResources[upr],
                                  ParentId = r.ParentID,
                                  Metadata = new Dictionary<string, object>
                                  {
                                      ["Description"] = r.Description,
                                      ["Endpoint"] = r.Endpoint?.ToString()
                                  }
                              };
            
            return resourceMap.ToDictionary(r => r.Id, r => r);
        }

        public async Task<List<VolumeTreeNode>> GetUserAccessibleVolumeTreeAsync(string userId)
        {
            return await BuildVolumeTreeAsync(userId);
        }

        public async Task<List<VolumeTreeNode>> GetUserAccessibleVolumeTreeForAnonymousAsync()
        {
            var userPermittedResources = await _context.UserResourcePermissionsByTypeForAnonymous(new[] { nameof(Volume) });
            if (userPermittedResources.Count == 0)
            {
                return new List<VolumeTreeNode>();
            }

            var resourceMap = from r in await _context.Volume.ToListAsync()
                             join upr in userPermittedResources.Keys on r.Id equals upr
                             select new UserResourcePermissions
                             {
                                 Id = r.Id,
                                 Name = r.Name,
                                 ResourceType = nameof(Volume),
                                 Permissions = userPermittedResources[upr],
                                 ParentId = r.ParentID,
                                 Metadata = new Dictionary<string, object>
                                 {
                                     ["Description"] = r.Description,
                                     ["Endpoint"] = r.Endpoint?.ToString()
                                 }
                             };
            var volumes = resourceMap.ToDictionary(r => r.Id, r => r);
            var tree = await BuildVolumeTreeAsync(null, null, volumes, null);
            return tree ?? new List<VolumeTreeNode>();
        }

        private async Task<List<VolumeTreeNode>> BuildVolumeTreeAsync(string userId, long? parentID = null, Dictionary<long, UserResourcePermissions> volumes = null, List<Resource> organizations = null)
        {
            if (volumes is null)
            {
                volumes = await GetUserPermissionsByTypeAsync(userId, nameof(Volume));
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

