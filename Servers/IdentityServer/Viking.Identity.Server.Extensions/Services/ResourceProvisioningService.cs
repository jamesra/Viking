using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.EntityFrameworkCore;
using Viking.Identity.Data;
using Viking.Identity.Models;

namespace Viking.Identity.Server.Extensions.Services
{
    /// <summary>
    /// Shared create/grant helpers for organizational units and annotation contexts.
    /// Controllers keep authorization; this service owns persistence.
    /// Linking a new context to its annotation server is <see cref="AnnotationServerCatalogSync"/>'s job.
    /// </summary>
    public class ResourceProvisioningService
    {
        private readonly ApplicationDbContext _context;

        public ResourceProvisioningService(ApplicationDbContext context)
        {
            _context = context;
        }

        public async Task<OrganizationalUnit> CreateOrganizationalUnitAsync(string name, string description, long? parentId)
        {
            var ou = new OrganizationalUnit
            {
                Name = name,
                Description = description,
                ResourceTypeId = nameof(OrganizationalUnit),
                ParentID = parentId == 0 ? null : parentId
            };

            _context.OrgUnit.Add(ou);
            await _context.SaveChangesAsync();
            return ou;
        }

        public async Task GrantSiteAdminsOrgUnitAdminAsync(long orgId)
        {
            var adminUsers = await _context.GetUsersInAdminRole().ToListAsync();
            foreach (var adminUser in adminUsers)
            {
                await GrantUserPermissionIfMissingAsync(adminUser.Id, orgId, Special.Permissions.OrgUnit.Admin);
            }

            await _context.SaveChangesAsync();
        }

        /// <summary>
        /// Creates a Connectome and its default AnnotationContext (same name). Catalog sync attaches
        /// Volume and AnnotationServer and sets ConnectomeId on them.
        /// </summary>
        public async Task<AnnotationContext> CreateAnnotationContextAsync(string name, string description, long? parentId, Uri endpointUrl)
        {
            var parent = parentId == 0 ? null : parentId;

            var connectome = new Connectome
            {
                Name = name,
                Description = description,
                ParentID = parent,
                ResourceTypeId = nameof(Connectome)
            };
            _context.Connectomes.Add(connectome);
            await _context.SaveChangesAsync();

            var context = new AnnotationContext
            {
                Name = name,
                Description = description,
                ParentID = parent,
                Endpoint = endpointUrl,
                ResourceTypeId = nameof(AnnotationContext),
                ConnectomeId = connectome.Id
            };
            _context.AnnotationContexts.Add(context);
            await _context.SaveChangesAsync();

            connectome.DefaultAnnotationContextId = context.Id;
            await _context.SaveChangesAsync();
            return context;
        }

        public async Task GrantUserOrgUnitAdminAsync(string userId, long orgId)
        {
            await GrantUserPermissionIfMissingAsync(userId, orgId, Special.Permissions.OrgUnit.Admin);
            await _context.SaveChangesAsync();
        }

        /// <summary>
        /// Grants Read, Annotate, and Review. The grant goes on the context's annotation server so it covers
        /// every copy of the images; a context without one (images only) gets the grant directly.
        /// </summary>
        public async Task GrantUserAnnotationContextFullAccessAsync(string userId, long annotationContextId)
        {
            var annotationServerId = await _context.AnnotationContexts
                .Where(v => v.Id == annotationContextId)
                .Select(v => v.AnnotationServerId)
                .FirstOrDefaultAsync();
            var resourceId = annotationServerId ?? annotationContextId;

            foreach (var permissionId in Special.Permissions.AnnotationServer.All)
                await GrantUserPermissionIfMissingAsync(userId, resourceId, permissionId);

            await _context.SaveChangesAsync();
        }

        public async Task GrantUserPermissionsAsync(string userId, long resourceId, IEnumerable<string> permissionIds)
        {
            foreach (var permissionId in permissionIds)
            {
                await GrantUserPermissionIfMissingAsync(userId, resourceId, permissionId);
            }

            await _context.SaveChangesAsync();
        }

        private async Task GrantUserPermissionIfMissingAsync(string userId, long resourceId, string permissionId)
        {
            var exists = await _context.GrantedUserPermissions.AnyAsync(p =>
                p.UserId == userId && p.ResourceId == resourceId && p.PermissionId == permissionId);

            if (exists)
                return;

            _context.GrantedUserPermissions.Add(new GrantedUserPermission
            {
                UserId = userId,
                ResourceId = resourceId,
                PermissionId = permissionId
            });
        }
    }
}
