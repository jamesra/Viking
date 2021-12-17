using Microsoft.AspNetCore.Mvc;
using System;
using System.Collections.Generic;
using System.Diagnostics.CodeAnalysis;
using System.Linq;
using System.Threading.Tasks;
using IdentityServer.Data;
using IdentityServer.Extensions;
using IdentityServer.Models;
using Microsoft.AspNetCore.Authorization;
using Microsoft.EntityFrameworkCore;
using System.Runtime.Serialization;

namespace IdentityServer.Controllers
{
    /// <summary>
    /// I got stumped getting an interactive authentication working with Identity server
    /// (viewing the JSON output from a browser that had authenticated on the site.
    /// As a workaround and to keep non-interactive public functions in one place I
    /// created this api controller, it may be moved to a separate project in the future
    /// 
    /// </summary>
    [Authorize(AuthenticationSchemes = Config.AuthenticationSchemes)]
    public class Api : Controller
    {
        /// <summary>
        /// Thrown from my code to prompt an IActionResult that is not the expected result for the operation
        /// </summary>
        public class UnexpectedResultException : Exception
        {
            public readonly IActionResult Result;

            public UnexpectedResultException([NotNull] IActionResult result)
            {
                Result = result;
            }

            public UnexpectedResultException([NotNull] IActionResult result, string message) : base(message)
            {
                Result = result;
            }

            public UnexpectedResultException([NotNull] IActionResult result, string message, Exception innerException) : base(message, innerException)
            {
                Result = result;
            }

            protected UnexpectedResultException([NotNull] IActionResult result, SerializationInfo info, StreamingContext context) : base(info, context)
            {
                Result = result;
            }
        } 

        private readonly ApplicationDbContext _context;
        private readonly IAuthorizationService _authorization;

        public Api(ApplicationDbContext context, IAuthorizationService authorization)
        {
            _context = context;
            _authorization = authorization;
        }

        private async Task<ApplicationUser> GetApplicationUser( )
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
        // GET: Resources/UserPermissions/5/jamesan 
        private async Task<IActionResult> UserPermissions(string resourceTypeId = null)
        {
            ApplicationUser appUser;
            try
            {
                appUser = await GetApplicationUser();
            }
            catch (UnexpectedResultException e)
            {
                return e.Result;
            }

            string[] resourceTypes = Array.Empty<string>();
            if (resourceTypeId != null)
                resourceTypes = new string[] { resourceTypeId };

            var userPermittedResources = await _context.UserResourcePermissions(appUser.Id, resourceTypes);

            var resourceMap = from r in await _context.Resource.Include(nameof(Volume)).ToListAsync()
                              join upr in userPermittedResources.Keys on r.Id equals upr
                              select new { r.Id, r.Name, permissions = userPermittedResources[upr] };

            //return Json(new {Resources = resourceMap.ToDictionary(r => r.Id, r => r.Name), Permissions = userPermittedResources });

            return Json(resourceMap.ToDictionary(r => r.Id, r => r));
        }

        /// <summary>
        /// Return the permissions the specified user has on the resource
        /// </summary>
        /// <returns></returns>
        /// <param name="id"></param>
        // GET: Resources/UserPermissions/5/jamesan 
        public async Task<IActionResult> UserPermissions(string id, string resourceTypeId = null)
        {
            if (id == null)
                return await UserPermissions(resourceTypeId);

            ApplicationUser appUser;
            try
            {
                appUser = await GetApplicationUser();
            }
            catch (UnexpectedResultException e)
            {
                return e.Result;
            }

            Resource resourceObj = null;
            try
            {
                long ResourceId = System.Convert.ToInt64(id);
                resourceObj = await _context.Resource.FirstOrDefaultAsync(r => r.Id == ResourceId);
            }
            catch (FormatException e)
            {
                resourceObj = await _context.Resource.FirstOrDefaultAsync(r => r.Name == id);
            }

            if (resourceObj == null)
            {
                return NotFound();
            }
             
            string[] resourceTypes = Array.Empty<string>();
            if (resourceTypeId != null)
                resourceTypes = new string[] { resourceTypeId };

            var result = await _context.UserResourcePermissions(resourceObj.Id, appUser.Id, resourceTypes);

            return Json(result);
        }


        /// <summary>
        /// Return the permissions the specified user has on the resource
        /// </summary>
        /// <returns></returns>
        /// <param name="id"></param>
        // GET: Resources/UserAccessibleVolumes/5/jamesan 
        public async Task<IActionResult> UserAccessibleVolumes()
        {
            ApplicationUser appUser;
            try
            {
                appUser = await GetApplicationUser();
            }
            catch (UnexpectedResultException e)
            {
                return e.Result;
            }

            var userPermittedResources = await _context.UserResourcePermissions(appUser.Id, new string[]
                {nameof(Volume)});

            var resourceMap = from r in await _context.Volume.ToListAsync()
                join upr in userPermittedResources.Keys on r.Id equals upr
                select new { r.Id, r.Name, r.Description, r.Endpoint, permissions = userPermittedResources[upr] };

            //return Json(new { Resources = resourceMap.ToDictionary(r => r.Id, r => new{r.Name, r.Description, r.Endpoint}), Permissions = userPermittedResources });

            return Json(resourceMap.ToDictionary(r => r.Id, r => r));
        }
    }
}
