using System;
using System.Collections.Generic;
using System.Diagnostics.CodeAnalysis;
using System.Linq;
using System.Net.Mime;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Viking.Identity.Data;
using Viking.Identity.Models;
using Viking.Identity.Server.WebApi.Models;
using Viking.Identity.Server.WebManagement.ApiControllers;
using Viking.Identity.Server.WebManagement.Extensions;

namespace Viking.Identity.Server.WebApi.ApiControllers
{
    /// <summary>
    /// I got stumped getting an interactive authentication working with Identity server
    /// (viewing the JSON output from a browser that had authenticated on the site.
    /// As a workaround and to keep non-interactive public functions in one place I
    /// created this api controller, it may be moved to a separate project in the future
    /// 
    /// </summary>
    [Produces(MediaTypeNames.Application.Json)]
    [ApiController]
    [Route("[controller]")]
    public partial class PermissionsController : ControllerBase
    {
        private readonly ApplicationDbContext _context; 

        public PermissionsController(ApplicationDbContext context)
        {
            _context = context;
            ValidateConnectionString();
        }

        private void ValidateConnectionString()
        {
            try
            {
                // Attempt to parse the connection string to ensure it is valid
                var connectionString = _context.Database.GetDbConnection().ConnectionString;
                var builder = new Microsoft.Data.SqlClient.SqlConnectionStringBuilder(connectionString);
            }
            catch (ArgumentException ex)
            {
                // Log the error and rethrow with additional context
                throw new InvalidOperationException("The database connection string is invalid. Please check your configuration.", ex);
            }
        }

        [AllowAnonymous]
        [HttpGet("CurrentUser")] 
        public string GetUsername() => User.Identity?.GetUsername() ?? "Anonymous";

        [AllowAnonymous]
        [HttpGet("CurrentUserId")] 
        public async Task<string> GetUserId()
        {
            var user = await GetApplicationUser();
            return user?.Id ?? "Anonymous";
        }

         
        private async Task<ApplicationUser> GetApplicationUser()
        { 
            // Manually trigger authentication if not already authenticated
            if (User.Identity == null || !User.Identity.IsAuthenticated)
            {
                // Debug: Check if there's an Authorization header
                var authHeader = HttpContext.Request.Headers["Authorization"].FirstOrDefault();
                Console.WriteLine($"[DEBUG] Authorization header: {authHeader}");
                
                // Debug: List available authentication schemes
                Console.WriteLine($"[DEBUG] Trying to authenticate with available schemes...");
                
                // Try to authenticate using the default scheme
                var authResult = await HttpContext.AuthenticateAsync();
                Console.WriteLine($"[DEBUG] Default authentication result: Succeeded={authResult.Succeeded}, Principal={authResult.Principal != null}");
                
                // If default scheme fails, try Bearer scheme (which is what's actually registered)
                if (!authResult.Succeeded)
                {
                    Console.WriteLine($"[DEBUG] Default authentication failed, trying Bearer scheme");
                    authResult = await HttpContext.AuthenticateAsync("Bearer");
                    Console.WriteLine($"[DEBUG] Bearer result: Succeeded={authResult.Succeeded}, Principal={authResult.Principal != null}");
                }
                
                if (authResult.Succeeded && authResult.Principal != null)
                {
                    // Set the authenticated user
                    HttpContext.User = authResult.Principal;
                    Console.WriteLine($"[DEBUG] Set authenticated user: {HttpContext.User.Identity.Name}");
                }
                else
                {
                    Console.WriteLine($"[DEBUG] Authentication failed - returning null");
                    return null; // Return null for unauthenticated users
                }
            }
            
            var username = User.Identity.GetUsername();
            Console.WriteLine($"[DEBUG] Username from identity: {username}");
            if (username == null)
                return null; // Return null if username cannot be determined
            
            var appUser = await _context.Users.FirstOrDefaultAsync(u => u.UserName == username);
            Console.WriteLine($"[DEBUG] Found user in database: {appUser != null}");
            return appUser; // Return null if user not found
        }

        /// <summary>
        /// Return the permissions the specified user has on the resource
        /// </summary>
        /// <returns></returns>
        /// <param name="id"></param>
        // GET: permissions/{resourceTypeId}
        [AllowAnonymous]
        [HttpGet("type/{resourceTypeId}")]
        public async Task<Dictionary<long, object>> UserPermissionsByType(string resourceTypeId = null)
        {
            var appUser = await GetApplicationUser();
            
            // If no user is authenticated, return empty dictionary
            if (appUser == null)
            {
                return new Dictionary<long, object>();
            }

            string[] resourceTypes = Array.Empty<string>();
            if (resourceTypeId != null)
                resourceTypes = new string[] { resourceTypeId };

            var userPermittedResources = await _context.UserResourcePermissionsByType(appUser.Id, resourceTypes);

            // For Volume resources, include description and endpoint
            if (resourceTypeId == nameof(Volume))
            {
                var resourceMap = from r in await _context.Volume.ToListAsync()
                                  join upr in userPermittedResources.Keys on r.Id equals upr
                                  select new { r.Id, r.Name, r.Description, Endpoint = r.Endpoint?.ToString(), permissions = userPermittedResources[upr] };
                return resourceMap.ToDictionary(r => r.Id, r => (object)r);
            }
            else
            {
                var resourceMap = from r in await _context.Resource.ToListAsync()
                                  join upr in userPermittedResources.Keys on r.Id equals upr
                                  select new { r.Id, r.Name, permissions = userPermittedResources[upr] };
                return resourceMap.ToDictionary(r => r.Id, r => (object)r);
            }
        }

        [AllowAnonymous]
        [HttpGet]
        public async Task<List<object>> UserPermissions()
        {
            var appUser = await GetApplicationUser();

            if (appUser == null)
            {
                return new List<object>();
            }

            var resourceTypeIds = await _context.ResourceTypes
                .Select(rt => rt.Id)
                .ToArrayAsync();

            if (resourceTypeIds.Length == 0)
            {
                return new List<object>();
            }

            var userPermittedResources = await _context.UserResourcePermissionsByType(appUser.Id, resourceTypeIds);

            if (userPermittedResources.Count == 0)
            {
                return new List<object>();
            }

            var resourceIds = userPermittedResources.Keys.ToArray();

            var resources = await _context.Resource
                .Where(r => resourceIds.Contains(r.Id))
                .Select(r => new { r.Id, r.Name, r.Description, r.ResourceTypeId })
                .ToListAsync();

            var resourceSummaries = resources
                .Select(r =>
                {
                    userPermittedResources.TryGetValue(r.Id, out var grantedPermissions);
                    return new
                    {
                        r.Id,
                        r.Name,
                        r.Description,
                        r.ResourceTypeId,
                        permissions = grantedPermissions ?? Array.Empty<string>()
                    };
                })
                .Cast<object>()
                .ToList();

            return resourceSummaries;
        }

        /// <summary>
        /// Return the permissions the current user has on the resource
        /// </summary>
        /// <returns></returns>
        /// <param name="id">ResourceID</param>
        // GET: Resources/UserPermissions/5/jamesan  
        [AllowAnonymous]
        [HttpGet("resource/{resourceId}")]
        public async Task<ActionResult<List<string>>> UserPermissions([NotNull] string resourceId)
        {
            if (resourceId is null)
            {
                throw new ArgumentNullException(nameof(resourceId));
            } 

            var appUser = await GetApplicationUser();
            
            // If no user is authenticated, return empty list
            if (appUser == null)
            {
                return new List<string>();
            }

            return await UserPermissions(resourceId, appUser.Id); 
        }

        /// <summary>
        /// Return the permissions the specified user has on the resource
        /// </summary>
        /// <returns></returns>
        /// <param name="id">ResourceID</param>
        // GET: Resources/UserPermissions/5/jamesan  
        [HttpGet("{userId}/resource/{resourceId}")] 
        public async Task<ActionResult<List<string>>> UserPermissions([NotNull] string resourceId, [NotNull] string userId)
        {
            if (resourceId is null)
            {
                throw new ArgumentNullException(nameof(resourceId));
            }

            if (userId is null)
            {
                throw new ArgumentNullException(nameof(userId));
            } 

            Resource resourceObj = null;
            try
            {
                long rId = System.Convert.ToInt64(resourceId);
                resourceObj = await _context.Resource.FirstOrDefaultAsync(r => r.Id == rId);
            }
            catch (FormatException)
            {
                resourceObj = await _context.Resource.FirstOrDefaultAsync(r => r.Name == resourceId);
            }

            if (resourceObj == null)
            {
                return NotFound();
            }

            var result = await _context.UserResourcePermissions(userId, resourceObj.Id);

            var resultList = await result.ToListAsync();
            return resultList;

            /*string[] resourceTypes = Array.Empty<string>();
            if (resourceTypeId != null)
                resourceTypes = new string[] { resourceTypeId };

            //var result = await _context.UserResourcePermissions(resourceObj.Id, appUser.Id, resourceTypes);
            //return result;

            var userPermittedResources = await _context.UserResourcePermissions(appUser.Id, resourceTypes);

            var resourceMap = from r in await _context.Resource.Include(nameof(Volume)).ToListAsync()
                join upr in userPermittedResources.Keys on r.Id equals upr
                select new { r.Id, r.Name, permissions = userPermittedResources[upr] };

            //return Json(new {Resources = resourceMap.ToDictionary(r => r.Id, r => r.Name), Permissions = userPermittedResources });

            //return Json(resourceMap.ToDictionary(r => r.Id, r => r));
            return resourceMap.ToDictionary(r => r.Id, r => (object)r);
            */
        }

        /// <summary>
        /// Return the permissions the specified user has on the resource
        /// </summary>
        /// <returns></returns>
        /// <param name="id">ResourceID</param>
        // GET: Resources/UserPermissions/5/jamesan  
        private async Task<ActionResult<Dictionary<long, object>>> UserPermissions([NotNull] string resourceId, [NotNull] ApplicationUser user)
        {
            if (resourceId is null)
            {
                throw new ArgumentNullException(nameof(resourceId));
            }

            if (user is null)
            {
                throw new ArgumentNullException(nameof(user));
            } 

            Resource resourceObj = null;
            try
            {
                long rId = System.Convert.ToInt64(resourceId);
                resourceObj = await _context.Resource.FirstOrDefaultAsync(r => r.Id == rId);
            }
            catch (FormatException)
            {
                resourceObj = await _context.Resource.FirstOrDefaultAsync(r => r.Name == resourceId);
            }

            if (resourceObj == null)
            {
                return NotFound();
            }

            throw new NotImplementedException();
            /*string[] resourceTypes = Array.Empty<string>();
            if (resourceTypeId != null)
                resourceTypes = new string[] { resourceTypeId };

            //var result = await _context.UserResourcePermissions(resourceObj.Id, appUser.Id, resourceTypes);
            //return result;

            var userPermittedResources = await _context.UserResourcePermissions(appUser.Id, resourceTypes);

            var resourceMap = from r in await _context.Resource.Include(nameof(Volume)).ToListAsync()
                join upr in userPermittedResources.Keys on r.Id equals upr
                select new { r.Id, r.Name, permissions = userPermittedResources[upr] };

            //return Json(new {Resources = resourceMap.ToDictionary(r => r.Id, r => r.Name), Permissions = userPermittedResources });

            //return Json(resourceMap.ToDictionary(r => r.Id, r => r));
            return resourceMap.ToDictionary(r => r.Id, r => (object)r);
            */
        }

        /// <summary>
        /// Return the permissions the specified user has on the resource
        /// </summary>
        /// <returns></returns>
        /// <param name="id"></param>
        // GET: Resources/UserAccessibleVolumes/5/jamesan 
        [AllowAnonymous]
        [HttpGet("AccessibleVolumes")]
        public Task<Dictionary<long, object>> UserAccessibleVolumes()
        {
            return UserPermissionsByType(resourceTypeId: nameof(Volume));
        }

        /// <summary>
        /// Returns a hierarchical tree of organizational units (branches) and user-accessible volumes (leaves).
        /// Used by the login/volume selection UI to populate the volume tree.
        /// </summary>
        [AllowAnonymous]
        [HttpGet("UserAccessibleVolumeTree")]
        public async Task<ActionResult<List<VolumeTreeNodeDto>>> UserAccessibleVolumeTree()
        {
            var appUser = await GetApplicationUser();
            if (appUser == null)
            {
                return new List<VolumeTreeNodeDto>();
            }

            var userPermittedResources = await _context.UserResourcePermissionsByType(appUser.Id, new[] { nameof(Volume) });
            if (userPermittedResources.Count == 0)
            {
                return new List<VolumeTreeNodeDto>();
            }

            var permittedVolumeIds = userPermittedResources.Keys.ToList();
            var volumes = await _context.Volume
                .Where(v => permittedVolumeIds.Contains(v.Id))
                .Select(v => new VolumeRow { Id = v.Id, Name = v.Name, Description = v.Description, Endpoint = v.Endpoint, ParentID = v.ParentID })
                .ToListAsync();

            var ouList = await _context.OrgUnit
                .Select(o => new OuRow { Id = o.Id, Name = o.Name, ParentID = o.ParentID })
                .ToListAsync();
            var ouById = ouList.ToDictionary(o => o.Id);

            var ouIdsInTree = new HashSet<long>();
            foreach (var vol in volumes)
            {
                var ouId = vol.ParentID;
                while (ouId.HasValue)
                {
                    ouIdsInTree.Add(ouId.Value);
                    if (!ouById.TryGetValue(ouId.Value, out var ou))
                        break;
                    ouId = ou.ParentID;
                }
            }

            var rootOuIds = ouList.Where(o => o.ParentID == null && ouIdsInTree.Contains(o.Id)).Select(o => o.Id).ToList();
            var result = new List<VolumeTreeNodeDto>();
            foreach (var rootId in rootOuIds)
            {
                var node = BuildVolumeTreeNode(
                    rootId,
                    ouList,
                    ouById,
                    ouIdsInTree,
                    volumes,
                    userPermittedResources);
                if (node != null)
                    result.Add(node);
            }

            return result;
        }

        private static VolumeTreeNodeDto BuildVolumeTreeNode(
            long ouId,
            List<OuRow> ouList,
            Dictionary<long, OuRow> ouById,
            HashSet<long> ouIdsInTree,
            List<VolumeRow> volumes,
            Dictionary<long, string[]> userPermittedResources)
        {
            if (!ouById.TryGetValue(ouId, out var ou))
                return null;

            var volumesHere = volumes.Where(v => v.ParentID == ouId).ToList();
            var volumeDtos = volumesHere.Select(v => new UserResourcePermissionsDto
            {
                Id = v.Id,
                Name = v.Name ?? $"Volume {v.Id}",
                ResourceType = nameof(Volume),
                Permissions = userPermittedResources.TryGetValue(v.Id, out var perms) ? perms : Array.Empty<string>(),
                ParentId = v.ParentID,
                Metadata = new Dictionary<string, object>
                {
                    ["Endpoint"] = v.Endpoint?.ToString() ?? (object)string.Empty,
                    ["Description"] = v.Description ?? (object)string.Empty
                }
            }).ToList();

            var childOuIds = ouList.Where(o => o.ParentID == ouId && ouIdsInTree.Contains(o.Id)).Select(o => o.Id).ToList();
            var childNodes = childOuIds
                .Select(childId => BuildVolumeTreeNode(childId, ouList, ouById, ouIdsInTree, volumes, userPermittedResources))
                .Where(n => n != null)
                .ToList();

            return new VolumeTreeNodeDto
            {
                Id = ou.Id,
                Name = ou.Name ?? "Unnamed",
                ParentId = ou.ParentID,
                ResourceType = nameof(OrganizationalUnit),
                Volumes = volumeDtos,
                Children = childNodes
            };
        }

        [AllowAnonymous]
        [HttpGet("AccessibleSegmentationServices")]
        public async Task<Dictionary<long, object>> UserAccessibleSegmentationServices()
        {
            var appUser = await GetApplicationUser();

            if (appUser == null)
            {
                return new Dictionary<long, object>();
            }

            var userPermittedResources = await _context.UserResourcePermissionsByType(appUser.Id, new[] { nameof(SegmentationService) });

            if (userPermittedResources.Count == 0)
            {
                return new Dictionary<long, object>();
            }

            var segmentationServiceIds = userPermittedResources.Keys.Distinct().ToArray();

            var segmentationServices = await _context.SegmentationServices
                .Where(s => segmentationServiceIds.Contains(s.Id))
                .Select(s => new
                {
                    s.Id,
                    s.Name,
                    s.Description,
                    Endpoint = s.Endpoint != null ? s.Endpoint.ToString() : null
                })
                .ToListAsync();

            return segmentationServices
                .Select(r =>
                {
                    userPermittedResources.TryGetValue(r.Id, out var grantedPermissions);
                    return new
                    {
                        r.Id,
                        r.Name,
                        r.Description,
                        r.Endpoint,
                        permissions = grantedPermissions ?? Array.Empty<string>()
                    };
                    return new
                    {
                        r.Id,
                        r.Name,
                        r.Description,
                        r.Endpoint,
                        permissions = grantedPermissions ?? Array.Empty<string>()
                    };
                })
                .ToDictionary(r => r.Id, r => (object)r);
        }

        [AllowAnonymous]
        [HttpGet("AccessibleSegmentationServices/{username}")]
        public async Task<Dictionary<long, object>> UserAccessibleSegmentationServicesByUsername(string username)
        {
            var appUser = await _context.Users.FirstOrDefaultAsync(u => u.UserName == username);
            if (appUser == null)
            {
                return new Dictionary<long, object>();
            }

            var userPermittedResources = await _context.UserResourcePermissionsByType(appUser.Id, new[] { nameof(SegmentationService) });
            if (userPermittedResources.Count == 0)
            {
                return new Dictionary<long, object>();
            }

            var segmentationServiceIds = userPermittedResources.Keys.Distinct().ToArray();

            var segmentationServices = await _context.SegmentationServices
                .Where(s => segmentationServiceIds.Contains(s.Id))
                .Select(s => new
                {
                    s.Id,
                    s.Name,
                    s.Description,
                    Endpoint = s.Endpoint != null ? s.Endpoint.ToString() : null
                })
                .ToListAsync();

            return segmentationServices
                .Select(r =>
                {
                    userPermittedResources.TryGetValue(r.Id, out var grantedPermissions);
                    return new
                    {
                        r.Id,
                        r.Name,
                        r.Description,
                        r.Endpoint,
                        permissions = grantedPermissions ?? Array.Empty<string>()
                    };
                })
                .ToDictionary(r => r.Id, r => (object)r);
        }

        [AllowAnonymous]
        [HttpGet("AccessibleVolumes/{username}")]
        public async Task<Dictionary<long, object>> UserAccessibleVolumesByUsername(string username)
        {
            // Find the user by username
            var appUser = await _context.Users.FirstOrDefaultAsync(u => u.UserName == username);
            if (appUser == null)
            {
                Console.WriteLine($"[DEBUG] User '{username}' not found in database");
                return new Dictionary<long, object>();
            }

            Console.WriteLine($"[DEBUG] Found user '{username}' with ID: {appUser.Id}");

            // Get user permissions for Volume resources
            var userPermittedResources = await _context.UserResourcePermissionsByType(appUser.Id, new string[] { nameof(Volume) });
            Console.WriteLine($"[DEBUG] User {username} has permissions for {userPermittedResources.Count} Volume resources");

            // Get all volumes for debugging
            var allVolumes = await _context.Volume.ToListAsync();
            Console.WriteLine($"[DEBUG] Total volumes in database: {allVolumes.Count}");
            foreach (var vol in allVolumes)
            {
                Console.WriteLine($"[DEBUG] Volume: ID={vol.Id}, Name={vol.Name}, Description={vol.Description}");
            }

            // For Volume resources, include description and endpoint
            var resourceMap = from r in await _context.Volume.ToListAsync()
                              join upr in userPermittedResources.Keys on r.Id equals upr
                              select new { r.Id, r.Name, r.Description, Endpoint = r.Endpoint?.ToString(), permissions = userPermittedResources[upr] };
            
            var result = resourceMap.ToDictionary(r => r.Id, r => (object)r);
            Console.WriteLine($"[DEBUG] Returning {result.Count} accessible volumes for user {username}");
            
            return result;
            /*
            ApplicationUser appUser;
            try
            {
                appUser = await GetApplicationUser();
            }
            catch (UnexpectedResultException e)
            {
                throw;
            }

            var userPermittedResources = await _context.UserResourcePermissionsByType(appUser.Id, new string[]
                {nameof(Volume)});

            var resourceMap = from r in await _context.Volume.ToListAsync()
                join upr in userPermittedResources on r.Id equals upr
                select new { r.Id, r.Name, r.Description, r.Endpoint, permissions = userPermittedResources[upr] };

            //return Json(new { Resources = resourceMap.ToDictionary(r => r.Id, r => new{r.Name, r.Description, r.Endpoint}), Permissions = userPermittedResources });

            return resourceMap.ToDictionary(r => r.Id, r => (object)r);
            */
        }
    }
}
