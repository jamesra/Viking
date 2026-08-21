using System;
using System.Collections.Generic;
using System.Linq;
using System.Security.Claims;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Authorization;
using Viking.Identity.Data;
using Viking.Identity.Models;

namespace Viking.Identity.Server.Authorization
{
    /// <summary>
    /// Index/Details visibility: grants (user, recursive groups, site admin via
    /// <see cref="ApplicationDBContextExtensions.UserResourcePermissionsByType"/>)
    /// or parent-org administration. Mutate actions must keep using
    /// <see cref="AuthorizationServiceExtensions.IsParentOrgUnitAdminAsync"/>.
    /// </summary>
    public static class ResourceVisibilityExtensions
    {
        /// <summary>
        /// True when the caller may open the resource in management UI (list/details).
        /// Does not grant create/edit/delete.
        /// </summary>
        public static async Task<bool> CanViewResourceAsync(
            this IAuthorizationService authorization,
            ApplicationDbContext context,
            ClaimsPrincipal user,
            Resource resource)
        {
            ArgumentNullException.ThrowIfNull(authorization);
            ArgumentNullException.ThrowIfNull(context);
            ArgumentNullException.ThrowIfNull(user);
            if (resource == null)
            {
                return false;
            }

            var visible = await authorization.FilterAccessibleResourcesAsync(
                context,
                user,
                new[] { resource },
                resource.ResourceTypeId);
            return visible.Count > 0;
        }

        /// <summary>
        /// Returns the subset of <paramref name="resources"/> the caller may list or open.
        /// Loads grant ids once, then parent-org admin per row for resources without a grant.
        /// </summary>
        public static async Task<List<T>> FilterAccessibleResourcesAsync<T>(
            this IAuthorizationService authorization,
            ApplicationDbContext context,
            ClaimsPrincipal user,
            IEnumerable<T> resources,
            string resourceTypeId)
            where T : Resource
        {
            ArgumentNullException.ThrowIfNull(authorization);
            ArgumentNullException.ThrowIfNull(context);
            ArgumentNullException.ThrowIfNull(user);
            ArgumentNullException.ThrowIfNull(resources);
            ArgumentException.ThrowIfNullOrEmpty(resourceTypeId);

            var list = resources as IList<T> ?? resources.ToList();
            if (list.Count == 0)
            {
                return new List<T>();
            }

            var userId = user.FindFirst(ClaimTypes.NameIdentifier)?.Value;
            var grantedIds = string.IsNullOrEmpty(userId)
                ? new HashSet<long>()
                : (await context.UserResourcePermissionsByType(userId, new[] { resourceTypeId })).Keys.ToHashSet();

            var accessible = new List<T>();
            foreach (var resource in list)
            {
                if (grantedIds.Contains(resource.Id)
                    || await authorization.IsParentOrgUnitAdminAsync(user, resource))
                {
                    accessible.Add(resource);
                }
            }

            return accessible;
        }
    }
}
