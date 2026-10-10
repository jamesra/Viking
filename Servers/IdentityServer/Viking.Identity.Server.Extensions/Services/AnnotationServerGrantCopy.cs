using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Viking.Identity.Data;
using Viking.Identity.Models;

namespace Viking.Identity.Server.Extensions.Services
{
    /// <summary>
    /// A principal that reaches more volumes after the copy than it was granted directly.
    /// </summary>
    public class WidenedAccessEntry
    {
        /// <summary>"User" or "Group".</summary>
        public string PrincipalType { get; set; }
        public string PrincipalId { get; set; }
        public string PrincipalName { get; set; }
        public string Permission { get; set; }
        public long AnnotationServerId { get; set; }
        public string AnnotationServerName { get; set; }
        public List<string> SourceVolumes { get; set; } = new List<string>();
        public List<string> NewlyReachableVolumes { get; set; } = new List<string>();
    }

    public class GrantCopyReport
    {
        public int UserGrantsAdded { get; set; }
        public int GroupGrantsAdded { get; set; }
        public int VolumesWithoutAnnotationServer { get; set; }
        public List<WidenedAccessEntry> WidenedAccess { get; set; } = new List<WidenedAccessEntry>();
    }

    /// <summary>
    /// Copies Read / Annotate / Review grants from each volume onto the annotation server it points at.
    /// Additive and idempotent. Volume grant rows are left in place so the copy can be undone by removing
    /// the annotation server grants; effective permissions are the union of both.
    /// </summary>
    public class AnnotationServerGrantCopy
    {
        private static readonly string[] CopiedPermissions = Special.Permissions.AnnotationServer.All;

        private readonly ApplicationDbContext _context;
        private readonly ILogger<AnnotationServerGrantCopy> _logger;

        public AnnotationServerGrantCopy(ApplicationDbContext context, ILogger<AnnotationServerGrantCopy> logger = null)
        {
            _context = context;
            _logger = logger;
        }

        public async Task<GrantCopyReport> CopyVolumeGrantsAsync(CancellationToken cancellationToken = default)
        {
            var report = new GrantCopyReport();

            var volumes = await _context.AnnotationContexts
                .Select(v => new { v.Id, v.Name, v.AnnotationServerId })
                .ToListAsync(cancellationToken);
            report.VolumesWithoutAnnotationServer = volumes.Count(v => v.AnnotationServerId == null);

            var linked = volumes.Where(v => v.AnnotationServerId.HasValue).ToList();
            if (linked.Count == 0)
                return report;

            var volumeIds = linked.Select(v => v.Id).ToList();
            var serverIds = linked.Select(v => v.AnnotationServerId.Value).Distinct().ToList();
            var serverOfVolume = linked.ToDictionary(v => v.Id, v => v.AnnotationServerId.Value);
            var volumeName = linked.ToDictionary(v => v.Id, v => v.Name);
            var volumesOfServer = linked.GroupBy(v => v.AnnotationServerId.Value).ToDictionary(g => g.Key, g => g.Select(v => v.Id).ToList());
            var serverNames = await _context.AnnotationServers
                .Where(s => serverIds.Contains(s.Id))
                .ToDictionaryAsync(s => s.Id, s => s.Name, cancellationToken);

            var userGrants = await _context.GrantedUserPermissions
                .Where(g => volumeIds.Contains(g.ResourceId) && CopiedPermissions.Contains(g.PermissionId))
                .Select(g => new { g.ResourceId, g.PermissionId, g.UserId })
                .ToListAsync(cancellationToken);
            var groupGrants = await _context.GrantedGroupPermissions
                .Where(g => volumeIds.Contains(g.ResourceId) && CopiedPermissions.Contains(g.PermissionId))
                .Select(g => new { g.ResourceId, g.PermissionId, g.GroupId })
                .ToListAsync(cancellationToken);

            var existingUserServerGrants = (await _context.GrantedUserPermissions
                    .Where(g => serverIds.Contains(g.ResourceId))
                    .Select(g => new { g.ResourceId, g.PermissionId, g.UserId })
                    .ToListAsync(cancellationToken))
                .Select(g => (g.ResourceId, g.PermissionId, Principal: g.UserId))
                .ToHashSet();
            var existingGroupServerGrants = (await _context.GrantedGroupPermissions
                    .Where(g => serverIds.Contains(g.ResourceId))
                    .Select(g => new { g.ResourceId, g.PermissionId, g.GroupId })
                    .ToListAsync(cancellationToken))
                .Select(g => (g.ResourceId, g.PermissionId, Principal: g.GroupId.ToString()))
                .ToHashSet();

            var userNames = await LookupUserNamesAsync(userGrants.Select(g => g.UserId), cancellationToken);
            var groupNames = await LookupGroupNamesAsync(groupGrants.Select(g => g.GroupId), cancellationToken);

            var userSources = userGrants.Select(g => new GrantSource("User", g.UserId, g.PermissionId, g.ResourceId));
            var groupSources = groupGrants.Select(g => new GrantSource("Group", g.GroupId.ToString(), g.PermissionId, g.ResourceId));

            foreach (var principalGrants in userSources.Concat(groupSources)
                         .GroupBy(g => (g.PrincipalType, g.PrincipalId, g.Permission, Server: serverOfVolume[g.VolumeId])))
            {
                var (principalType, principalId, permission, serverId) = principalGrants.Key;
                var isUser = principalType == "User";
                var existing = isUser ? existingUserServerGrants : existingGroupServerGrants;

                if (existing.Contains((serverId, permission, principalId)))
                    continue;

                if (isUser)
                {
                    _context.GrantedUserPermissions.Add(new GrantedUserPermission
                    {
                        ResourceId = serverId,
                        PermissionId = permission,
                        UserId = principalId
                    });
                    report.UserGrantsAdded++;
                }
                else
                {
                    _context.GrantedGroupPermissions.Add(new GrantedGroupPermission
                    {
                        ResourceId = serverId,
                        PermissionId = permission,
                        GroupId = long.Parse(principalId)
                    });
                    report.GroupGrantsAdded++;
                }

                existing.Add((serverId, permission, principalId));

                var sourceVolumeIds = principalGrants.Select(g => g.VolumeId).Distinct().ToHashSet();
                var newlyReachable = volumesOfServer[serverId].Where(id => !sourceVolumeIds.Contains(id)).ToList();
                if (newlyReachable.Count == 0)
                    continue;

                report.WidenedAccess.Add(new WidenedAccessEntry
                {
                    PrincipalType = principalType,
                    PrincipalId = principalId,
                    PrincipalName = isUser
                        ? userNames.GetValueOrDefault(principalId, principalId)
                        : groupNames.GetValueOrDefault(long.Parse(principalId), principalId),
                    Permission = permission,
                    AnnotationServerId = serverId,
                    AnnotationServerName = serverNames.GetValueOrDefault(serverId, serverId.ToString()),
                    SourceVolumes = sourceVolumeIds.Select(id => volumeName[id]).OrderBy(n => n).ToList(),
                    NewlyReachableVolumes = newlyReachable.Select(id => volumeName[id]).OrderBy(n => n).ToList()
                });
            }

            await _context.SaveChangesAsync(cancellationToken);

            report.WidenedAccess = report.WidenedAccess
                .OrderBy(w => w.AnnotationServerName)
                .ThenBy(w => w.PrincipalType)
                .ThenBy(w => w.PrincipalName)
                .ThenBy(w => w.Permission)
                .ToList();

            _logger?.LogInformation(
                "Copied volume grants to annotation servers: {UserGrants} user and {GroupGrants} group grants added, {Widened} principals reach new volumes",
                report.UserGrantsAdded, report.GroupGrantsAdded, report.WidenedAccess.Count);

            return report;
        }

        private async Task<Dictionary<string, string>> LookupUserNamesAsync(IEnumerable<string> ids, CancellationToken cancellationToken)
        {
            var idList = ids.Distinct().ToList();
            return await _context.Users
                .Where(u => idList.Contains(u.Id))
                .ToDictionaryAsync(u => u.Id, u => u.UserName, cancellationToken);
        }

        private async Task<Dictionary<long, string>> LookupGroupNamesAsync(IEnumerable<long> ids, CancellationToken cancellationToken)
        {
            var idList = ids.Distinct().ToList();
            return await _context.Group
                .Where(g => idList.Contains(g.Id))
                .ToDictionaryAsync(g => g.Id, g => g.Name, cancellationToken);
        }

        private readonly record struct GrantSource(string PrincipalType, string PrincipalId, string Permission, long VolumeId);
    }
}
