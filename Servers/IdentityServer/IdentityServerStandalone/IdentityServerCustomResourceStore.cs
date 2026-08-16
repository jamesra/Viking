using IdentityModel;
using Duende.IdentityServer.Models;
using Duende.IdentityServer.Stores;
using Microsoft.EntityFrameworkCore;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Viking.Identity.Data;
using Viking.Identity.Models;
using Viking.Identity.Server;
using Resource = Viking.Identity.Models.Resource;

namespace Viking.Identity
{
    public class IdentityServerCustomResourceStore : IResourceStore
    {
        private static readonly string[] ApiFacingResourceTypeIds =
        {
            nameof(Volume),
            nameof(SegmentationService)
        };

        private readonly ApplicationDbContext _context;
        private readonly Secret _Secret;
        private readonly ILogger<IdentityServerCustomResourceStore> _logger;

        internal static ApiScope[] StandardScopes = new ApiScope[]
        {  
            new ApiScope("Viking.Annotation")
        };

        internal readonly ApiResource[] StandardResources;

        internal IdentityResource[] StandardIdentityResources = new IdentityResource[]
        {
            new IdentityResources.OpenId(),
            new IdentityResources.Address(),
            new IdentityResources.Email(),
            new IdentityResources.Phone(),
            new IdentityResources.Profile()
        };


        public IdentityServerCustomResourceStore(
            ApplicationDbContext context,
            IOptions<VikingIdentityServerOptions> serverOptions,
            ILogger<IdentityServerCustomResourceStore> logger)
        {
            var options = serverOptions.Value;
            _Secret = new Secret(options.Secret.Sha256());
            _context = context;
            _logger = logger;

            StandardResources = new ApiResource[]
            {
                new ApiResource("Viking.Annotation", "Viking Annotation API")
                {
                    UserClaims = { JwtClaimTypes.Role, JwtClaimTypes.Id, JwtClaimTypes.Name},
                    ApiSecrets = { _Secret },
                    Scopes = options.ApiScopes.Select(s => s.Name).Append("Viking.Annotation").ToArray()
                },
            };
        }

        private static bool IsApiFacingResource(Resource r) =>
            ApiFacingResourceTypeIds.Contains(r.ResourceTypeId);

        /// <summary>
        /// Keeps only Volume/SegmentationService rows and collapses duplicate names so Duende discovery stays valid.
        /// </summary>
        private List<Resource> SelectUniqueApiFacingResources(IEnumerable<Resource> resources)
        {
            var apiFacing = resources.Where(IsApiFacingResource).ToList();
            var duplicates = apiFacing
                .GroupBy(r => ResourceScopeNames.ToScopePrefix(r.Name), StringComparer.OrdinalIgnoreCase)
                .Where(g => g.Count() > 1)
                .ToList();

            foreach (var group in duplicates)
            {
                _logger.LogWarning(
                    "Duplicate API-facing resource name {ResourceName} found for ids {ResourceIds}; using first entry only",
                    group.Key,
                    string.Join(", ", group.Select(r => r.Id)));
            }

            return apiFacing
                .GroupBy(r => ResourceScopeNames.ToScopePrefix(r.Name), StringComparer.OrdinalIgnoreCase)
                .Select(g => g.OrderBy(r => r.Id).First())
                .ToList();
        }

        /// <summary>
        /// Converts a Resource to an ApiResource
        /// </summary>
        private ApiResource ResourceToResourceApi(Resource r)
        {
            return new ApiResource()
            {
                Name = ResourceScopeNames.ToScopePrefix(r.Name),
                UserClaims = { JwtClaimTypes.Role, JwtClaimTypes.Id, JwtClaimTypes.Name },
                Description = r.Description,
                Scopes = r.AvailablePermissions.Select(permission => ResourceScopeNames.ToScope(r.Name, permission.PermissionId)).ToList(),
                ApiSecrets = { _Secret }
            };
        }

        /// <summary>
        /// Converts an IEnumerable of Resources to ApiResources
        /// </summary>
        private IEnumerable<ApiResource> ResourceToResourceApi(IEnumerable<Resource> resources)
        {
            return SelectUniqueApiFacingResources(resources).Select(ResourceToResourceApi);
        }

        /// <summary>
        /// This is a bit ambiguous, it expects only the resource name as it appears in the database column "Name".
        /// It does not work for apiscope names such as RC1.Annotate
        /// </summary>
        private async Task<IEnumerable<ApiResource>> FindApiResourcesByNameOnlyAsync(IEnumerable<string> apiResourceNames)
        {
            var resources = await _context.ApiFacingResourcesNamed(apiResourceNames)
                .Include(r => r.ResourceType).ThenInclude(rt => rt.Permissions)
                .ToListAsync();

            return ResourceToResourceApi(resources);
        }

        public async Task<IEnumerable<ApiResource>> FindApiResourcesByNameAsync(IEnumerable<string> apiResourceNames)
        {
            if (apiResourceNames == null) throw new ArgumentNullException(nameof(apiResourceNames));

            var standard_resources = StandardResources.Where(r => apiResourceNames.Contains(r.Name)).ToList(); 
            
            var resource_scopes = ParseScopeNames(apiResourceNames).Where(r => r.ResourceName != null);

            var resource_names = resource_scopes.Select(r => r.ResourceName).ToList();

            var resources = await _context.ApiFacingResourcesNamed(resource_names)
                .Include(r => r.ResourceType).ThenInclude(rt => rt.Permissions)
                .ToListAsync();

            standard_resources.AddRange(ResourceToResourceApi(resources));
            return standard_resources;
        }

        public async Task<IEnumerable<ApiResource>> FindApiResourcesByScopeNameAsync(IEnumerable<string> scopeNames)
        {
            if (scopeNames == null) throw new ArgumentNullException(nameof(scopeNames));

            var standard_resources = StandardResources.Where(r => r.Scopes.Any(s => scopeNames.Contains(s))).ToList();

            var remaining_scopes = scopeNames.Where(scope_name => standard_resources.Any(stand => stand.Scopes.Any(s => s == scope_name) == false)).ToList();

            var resourceNames = ParseScopeNames(remaining_scopes).Where(r => r.ResourceName != null).Select(r => r.ResourceName);

            var resources = await FindApiResourcesByNameOnlyAsync(resourceNames);

            standard_resources.AddRange(resources);
            return standard_resources;
        }
         

        public async Task<IEnumerable<ApiScope>> FindApiScopesByNameAsync(IEnumerable<string> scopeNames)
        {
            if (scopeNames == null) throw new ArgumentNullException(nameof(scopeNames));

            var scopeNameList = scopeNames.ToList();
            var standard_scopes = StandardScopes.Where(s => scopeNameList.Contains(s.Name)).ToList();

            var resource_scope = ParseScopeNames(scopeNameList)
                .Where(r => r.ResourceName != null && !string.Equals(r.ResourceName, "Viking", StringComparison.OrdinalIgnoreCase))
                .ToList();

            if (resource_scope.Count == 0)
                return standard_scopes;

            var resourceNames = resource_scope.Select(r => r.ResourceName).Distinct().ToList();
            var api_resources = await FindApiResourcesByNameOnlyAsync(resourceNames);

            var resource_api_scopes = api_resources
                .SelectMany(r => r.Scopes.Where(s => scopeNameList.Contains(s)).Select(s => new ApiScope(s)));

            standard_scopes.AddRange(resource_api_scopes);
            return standard_scopes;
        }

        public Task<IEnumerable<IdentityResource>> FindIdentityResourcesByScopeNameAsync(IEnumerable<string> scopeNames)
        {
            return Task<IEnumerable<IdentityResource>>.Run(() => { return StandardIdentityResources.Where(sr => scopeNames.Contains(sr.Name)); });
        }

        private struct ResourceScope
        {
            public string ResourceName;
            public string ScopeName;
        }

        private IEnumerable<ResourceScope> ParseScopeNames(IEnumerable<string> scopeNames)
        {
            return scopeNames.Select(scope =>
            {
                if (ResourceScopeNames.TryParse(scope, out var prefix, out var permission))
                {
                    return new ResourceScope() { ResourceName = prefix, ScopeName = permission };
                }

                return new ResourceScope() { ScopeName = scope, ResourceName = null };
            });
        }

        public async Task<Resources> GetAllResourcesAsync()
        {
            var resources = await _context.Resource
                                .Include(r => r.ResourceType).ThenInclude(rt => rt.Permissions)
                                .Where(r => ApiFacingResourceTypeIds.Contains(r.ResourceTypeId))
                                .ToListAsync();

            var uniqueResources = SelectUniqueApiFacingResources(resources);

            var results = uniqueResources.Select(r => new
            {
                ApiResource = ResourceToResourceApi(r),
                ApiScopes = r.AvailablePermissions.Select(p => new ApiScope()
                {
                    Name = ResourceScopeNames.ToScope(r.Name, p.PermissionId),
                    Description = p.Description,
                    UserClaims = { JwtClaimTypes.Role, JwtClaimTypes.Id, JwtClaimTypes.Name }
                }).ToArray()
            }).ToList();

            var ApiScopes = results.SelectMany(r => r.ApiScopes).ToList();
            ApiScopes.AddRange(StandardScopes);
              
            return new Resources(new IdentityResource[] { },
                results.Select(r => r.ApiResource),
                ApiScopes.ToList()
                );
        }
    }
}
