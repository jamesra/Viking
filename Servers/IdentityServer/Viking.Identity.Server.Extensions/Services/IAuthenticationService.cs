using System.Security.Claims;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Http;
using Viking.Identity.Models;

namespace Viking.Identity.Server.Extensions.Services
{
    public interface IAuthenticationService
    {
        Task<ApplicationUser> GetApplicationUserAsync(ClaimsPrincipal user, HttpContext httpContext);
        Task<string> GetCurrentUserIdAsync(ClaimsPrincipal user);
    }
}


