using System.Linq;
using System.Security.Claims;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Identity;
using Viking.Identity.Data;
using Viking.Identity.Models;

namespace Viking.Identity.Server.Extensions.Services
{
    /// <summary>
    /// Decides who may see the administrator help page: site administrators, and anyone holding the Administrator
    /// permission on at least one organizational unit, directly or through group membership.
    /// </summary>
    /// <remarks>
    /// Scoped, so the layout (nav link) and <c>HelpController</c> share one database lookup per request.
    /// </remarks>
    public class HelpAccess
    {
        readonly ApplicationDbContext _context;
        readonly UserManager<ApplicationUser> _userManager;
        bool? _cached;

        public HelpAccess(ApplicationDbContext context, UserManager<ApplicationUser> userManager)
        {
            _context = context;
            _userManager = userManager;
        }

        /// <summary>True when <paramref name="user"/> may open the help page. Always false for anonymous callers.</summary>
        /// <param name="user">The request's principal.</param>
        public async Task<bool> CanViewAsync(ClaimsPrincipal user)
        {
            if (_cached.HasValue)
                return _cached.Value;

            if (user?.Identity?.IsAuthenticated != true)
                return (_cached = false).Value;

            if (user.IsInRole(Special.Roles.Admin))
                return (_cached = true).Value;

            string userId = _userManager.GetUserId(user);
            _cached = !string.IsNullOrEmpty(userId) && await IsOrgUnitAdminAnywhereAsync(_context, userId);
            return _cached.Value;
        }

        /// <summary>
        /// True when the user holds the OrgUnit Administrator permission on any organizational unit.
        /// </summary>
        /// <param name="context">Identity database.</param>
        /// <param name="userId">Identity user id.</param>
        public static async Task<bool> IsOrgUnitAdminAnywhereAsync(ApplicationDbContext context, string userId)
        {
            var grants = await context.UserResourcePermissionsByType(userId, new[] { nameof(OrganizationalUnit) });
            return grants.Values.Any(permissions => permissions.Contains(Special.Permissions.OrgUnit.Admin));
        }
    }
}
