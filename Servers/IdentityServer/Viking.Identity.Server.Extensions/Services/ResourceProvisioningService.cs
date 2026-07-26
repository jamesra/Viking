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
    /// Shared create/grant helpers for organizational units and volumes.
    /// Controllers keep authorization; this service owns persistence.
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

        public async Task<Volume> CreateVolumeAsync(string name, string description, long? parentId, Uri endpointUrl)
        {
            var volume = new Volume
            {
                Name = name,
                Description = description,
                ParentID = parentId == 0 ? null : parentId,
                Endpoint = endpointUrl,
                ResourceTypeId = nameof(Volume)
            };

            _context.Volume.Add(volume);
            await _context.SaveChangesAsync();
            return volume;
        }

        public async Task GrantUserOrgUnitAdminAsync(string userId, long orgId)
        {
            await GrantUserPermissionIfMissingAsync(userId, orgId, Special.Permissions.OrgUnit.Admin);
            await _context.SaveChangesAsync();
        }

        public async Task GrantUserVolumeFullAccessAsync(string userId, long volumeId)
        {
            await GrantUserPermissionIfMissingAsync(userId, volumeId, Special.Permissions.Volume.Read);
            await GrantUserPermissionIfMissingAsync(userId, volumeId, Special.Permissions.Volume.Annotate);
            await GrantUserPermissionIfMissingAsync(userId, volumeId, Special.Permissions.Volume.Review);
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
