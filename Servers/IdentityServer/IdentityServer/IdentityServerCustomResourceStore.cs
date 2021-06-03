using IdentityModel;
using IdentityServer.Data;
using IdentityServer4.Models;
using IdentityServer4.Stores;
using Microsoft.EntityFrameworkCore;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

namespace IdentityServer
{
    public class IdentityServerCustomResourceStore : IResourceStore
    {
        ApplicationDbContext _context;
        internal static ApiScope[] StandardScopes = new ApiScope[]
        {  
            new ApiScope("Viking.Annotation")
        };

        internal static ApiResource[] StandardResources = new ApiResource[]
        {
            new ApiResource("Viking.Annotation", "Viking Annotation API")
            {
                UserClaims = { JwtClaimTypes.Role, JwtClaimTypes.Id, JwtClaimTypes.Name},
                ApiSecrets = { new Secret(Config.Secret.Sha256())},
                Scopes = {"Viking.Annotation"}
            },
        };

        internal static IdentityResource[] StandardIdentityResources = new IdentityResource[]
        {
            new IdentityResources.OpenId(),
            new IdentityResources.Address(),
            new IdentityResources.Email(),
            new IdentityResources.Phone(),
            new IdentityResources.Profile()
        };


        public IdentityServerCustomResourceStore(ApplicationDbContext context)
        {
            _context = context;
        }

        private static ApiResource ResourceToResourceApi(IdentityServer.Models.Resource r)
        {
            return new ApiResource()
            {
                Name = r.Name,
                UserClaims = { JwtClaimTypes.Role, JwtClaimTypes.Id, JwtClaimTypes.Name },
                Description = r.Description,
                Scopes = r.AvailablePermissions.Select(permission => $"{r.Name}.{permission.PermissionId}").ToList(),
                ApiSecrets = { new Secret(Config.Secret.Sha256()) }
            };
        }

        private static IEnumerable<ApiResource> ResourceToResourceApi(IEnumerable<IdentityServer.Models.Resource> resources)
        {
            return resources.Select(r => ResourceToResourceApi(r));
        }

        /// <summary>
        /// This is a bit ambiguous, it expects only the resource name as it appears in the database column "Name".
        /// It does not work for apiscope names such as RC1.Annotate
        /// </summary>
        /// <param name="apiResourceNames"></param>
        /// <returns></returns>
        private async Task<IEnumerable<ApiResource>> FindApiResourcesByNameOnlyAsync(IEnumerable<string> apiResourceNames)
        {
            var resources = await _context.Resource.Include(r => r.ResourceType).ThenInclude(rt => rt.Permissions)
                .Where(r => apiResourceNames.Contains(r.Name)).ToListAsync();

            var results = ResourceToResourceApi(resources);

            return results;
        }

        public async Task<IEnumerable<ApiResource>> FindApiResourcesByNameAsync(IEnumerable<string> apiResourceNames)
        {
            if (apiResourceNames == null) throw new ArgumentNullException(nameof(apiResourceNames));

            var standard_resources = StandardResources.Where(r => apiResourceNames.Contains(r.Name)).ToList(); 
            
            var remaining_resource_names = apiResourceNames.Where(s => standard_resources.Any(stand => stand.Name == s) == false).ToList();

            var resource_scopes = ParseScopeNames(apiResourceNames).Where(r => r.ResourceName != null);

            var resource_names = resource_scopes.Select(r => r.ResourceName).ToList();

            var resources = await _context.Resource.Include(r => r.ResourceType).ThenInclude(rt => rt.Permissions)
                .Where(r => resource_names.Contains(r.Name)).ToListAsync();

            var results = ResourceToResourceApi(resources);

            standard_resources.AddRange(results);
            return standard_resources;
        }

        public async Task<IEnumerable<ApiResource>> FindApiResourcesByScopeNameAsync(IEnumerable<string> scopeNames)
        {
            if (scopeNames == null) throw new ArgumentNullException(nameof(scopeNames));

            var standard_resources = StandardResources.Where(r => r.Scopes.Any(s => scopeNames.Contains(s))).ToList();

            var remaining_scopes = scopeNames.Where(scope_name => standard_resources.Any(stand => stand.Scopes.Any(s => s == scope_name) == false)).ToList();

            var resourceNames = ParseScopeNames(remaining_scopes).Where(r => r.ResourceName != null).Select(r => r.ResourceName);
             
            /*
            var resourceTypes = scopeNames.SelectMany(scope => _context.Permissions.Where(perm => perm.PermissionId == scope).Select(p => p.ResourceTypeId).Distinct());

            var resources = _context.Resource.Where(r => resourceTypes.Contains(r.ResourceTypeId)).Select(r => r.Name);

            return FindApiResourcesByNameAsync(resources);
            */

            var resources = await FindApiResourcesByNameOnlyAsync(resourceNames);

            standard_resources.AddRange(resources);
            return standard_resources;
        }
         

        public async Task<IEnumerable<ApiScope>> FindApiScopesByNameAsync(IEnumerable<string> scopeNames)
        {
            if (scopeNames == null) throw new ArgumentNullException(nameof(scopeNames));

            var standard_scopes = StandardScopes.Where(s => scopeNames.Contains(s.Name)).ToList();

            var resource_scope = ParseScopeNames(scopeNames).Where(r => r.ResourceName != null);

            var resourceTypes = resource_scope.SelectMany(scope => _context.Permissions.Where(perm => perm.PermissionId == scope.ScopeName).Select(p => p.ResourceTypeId).Distinct());

            var resources = _context.Resource.Where(r => resourceTypes.Contains(r.ResourceTypeId)).Select(r => r.Name);

            var api_resources = await FindApiResourcesByNameOnlyAsync(resources);

            var resource_api_scopes = api_resources.SelectMany(r => r.Scopes.Where(s => scopeNames.Contains(s)).Select(s => new ApiScope(s)));

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
                var parts = scope.Split('.');
                if (parts.Length == 2)
                {
                    return new ResourceScope() { ResourceName = parts[0], ScopeName = parts[1] };
                }
                else if (parts.Length == 1)
                {
                    return new ResourceScope() { ScopeName = scope, ResourceName = null };
                }
                else
                {
                    return new ResourceScope() { ScopeName = null, ResourceName = null };
                }
            });
        }

        public async Task<Resources> GetAllResourcesAsync()
        {
            var resources = await _context.Resource
                                .Include(r => r.ResourceType).ThenInclude(rt => rt.Permissions)
                                .ToListAsync();

            var results = resources.Select(r => new
            {
                ApiResource = ResourceToResourceApi(r),
                ApiScopes = r.AvailablePermissions.Select(p => new ApiScope()
                {
                    Name = $"{r.Name}.{p.PermissionId}",
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
