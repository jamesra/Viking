using Microsoft.AspNetCore.Mvc;
using System;
using System.Collections.Generic;
using System.Diagnostics.CodeAnalysis;
using System.Linq;
using System.Net.Mime;
using System.Threading.Tasks;
using Viking.Identity.Extensions;
using Microsoft.AspNetCore.Authorization;
using Microsoft.EntityFrameworkCore;
using Viking.Identity.Data;
using Viking.Identity.Models;

namespace Viking.Identity.Controllers
{
    /// <summary>
    /// I got stumped getting an interactive authentication working with Identity server
    /// (viewing the JSON output from a browser that had authenticated on the site.
    /// As a workaround and to keep non-interactive public functions in one place I
    /// created this api controller, it may be moved to a separate project in the future
    /// 
    /// </summary>
    [Authorize(AuthenticationSchemes = Config.AuthenticationSchemes)]
    [Produces(MediaTypeNames.Application.Json)]
    [ApiController]
    [Route("api/[controller]")]
    public partial class PermissionsController : ControllerBase
    {
        private readonly ApplicationDbContext _context; 

        public PermissionsController(ApplicationDbContext context)
        {
            _context = context; 
        }

        [HttpGet("CurrentUser")]
        public string GetUsername() => User.Identity.GetUsername();

        [HttpGet("CurrentUserId")]
        public async Task<string> GetUserId() => (await GetApplicationUser()).Id;

         
        private async Task<ApplicationUser> GetApplicationUser()
        { 
            var username = User.Identity.GetUsername();
            if (username == null)
                throw new UnexpectedResultException(Unauthorized());
            
            var appUser = await _context.Users.FirstOrDefaultAsync(u => u.UserName == username);
            if (appUser == null)
                throw new UnexpectedResultException(Unauthorized());
            
            return appUser;
        }

        /// <summary>
        /// Return the permissions the specified user has on the resource
        /// </summary>
        /// <returns></returns>
        /// <param name="id"></param>
        // GET: permissions/{resourceTypeId}
        [HttpGet("type/{resourceTypeId}")]
        public async Task<Dictionary<long, object>> UserPermissionsByType(string resourceTypeId = null)
        {
            ApplicationUser appUser;
            try
            {
                appUser = await GetApplicationUser();
            }
            catch (UnexpectedResultException e)
            {
                throw;
//                return Erro e.Result;
            }

            string[] resourceTypes = Array.Empty<string>();
            if (resourceTypeId != null)
                resourceTypes = new string[] { resourceTypeId };

            var userPermittedResources = await _context.UserResourcePermissionsByType(appUser.Id, resourceTypes);

            var resourceMap = from r in await _context.Resource.ToListAsync()
                              join upr in userPermittedResources.Keys on r.Id equals upr
                              select new { r.Id, r.Name, permissions = userPermittedResources[upr] };

            //return Json(new {Resources = resourceMap.ToDictionary(r => r.Id, r => r.Name), Permissions = userPermittedResources });

            //return Json(resourceMap.ToDictionary(r => r.Id, r => r));
            return resourceMap.ToDictionary(r => r.Id, r => (object)r);
        }

        /// <summary>
        /// Return the permissions the current user has on the resource
        /// </summary>
        /// <returns></returns>
        /// <param name="id">ResourceID</param>
        // GET: Resources/UserPermissions/5/jamesan  
        [HttpGet("resource/{resourceId}")]
        public async Task<ActionResult<List<string>>> UserPermissions([NotNull] string resourceId)
        {
            if (resourceId is null)
            {
                throw new ArgumentNullException(nameof(resourceId));
            } 

            ApplicationUser appUser;
            try
            {
                appUser = await GetApplicationUser();
            }
            catch (UnexpectedResultException e)
            {
                throw;
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
            catch (FormatException e)
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
            catch (FormatException e)
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
        [HttpGet("AccessibleVolumes")]
        public Task<Dictionary<long, object>> UserAccessibleVolumes()
        {
            return UserPermissionsByType(resourceTypeId: nameof(Volume));
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
