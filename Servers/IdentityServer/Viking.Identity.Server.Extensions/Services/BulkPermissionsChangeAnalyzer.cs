using System;
using System.Collections.Generic;
using System.Linq;

namespace Viking.Identity.Server.Extensions.Services
{
    /// <summary>
    /// A single permission grant on a resource (user or group).
    /// </summary>
    public readonly struct PermissionGrant : IEquatable<PermissionGrant>
    {
        public PermissionGrant(long resourceId, string granteeKey, string permissionId, bool isUserGrant)
        {
            ResourceId = resourceId;
            GranteeKey = granteeKey ?? throw new ArgumentNullException(nameof(granteeKey));
            PermissionId = permissionId ?? throw new ArgumentNullException(nameof(permissionId));
            IsUserGrant = isUserGrant;
        }

        public long ResourceId { get; }
        public string GranteeKey { get; }
        public string PermissionId { get; }
        public bool IsUserGrant { get; }

        public bool Equals(PermissionGrant other) =>
            ResourceId == other.ResourceId
            && IsUserGrant == other.IsUserGrant
            && string.Equals(GranteeKey, other.GranteeKey, StringComparison.Ordinal)
            && string.Equals(PermissionId, other.PermissionId, StringComparison.Ordinal);

        public override bool Equals(object obj) => obj is PermissionGrant other && Equals(other);

        public override int GetHashCode() =>
            HashCode.Combine(ResourceId, GranteeKey, PermissionId, IsUserGrant);
    }

    /// <summary>
    /// One grantee's permission checkbox row as submitted from the bulk edit form.
    /// </summary>
    public class SubmittedGranteePermissions
    {
        public string GranteeKey { get; set; }
        public IList<(string PermissionId, bool Selected)> Permissions { get; set; }
            = new List<(string PermissionId, bool Selected)>();
    }

    public class BulkPermissionsChangeResult
    {
        public IReadOnlyList<PermissionGrant> GrantsToAdd { get; init; } = Array.Empty<PermissionGrant>();
        public IReadOnlyList<PermissionGrant> GrantsToRemove { get; init; } = Array.Empty<PermissionGrant>();
        public int TotalExistingGrants { get; init; }
        public double RemovalPercent { get; init; }
        public bool RemovesAllUserGrants { get; init; }
        public bool RequiresConfirmation { get; init; }
    }

    /// <summary>
    /// Computes bulk permission checkbox union state and add/remove deltas for replace-on-save.
    /// </summary>
    public static class BulkPermissionsChangeAnalyzer
    {
        public const double RemovalConfirmationThreshold = 0.10;

        /// <summary>
        /// True when any of the selected resources already has this grant (union display).
        /// </summary>
        public static bool IsGrantedOnAnyResource(
            IEnumerable<PermissionGrant> existingGrants,
            IEnumerable<long> resourceIds,
            string granteeKey,
            string permissionId,
            bool isUserGrant)
        {
            if (existingGrants == null || resourceIds == null)
                return false;

            var resourceIdSet = resourceIds as ISet<long> ?? resourceIds.ToHashSet();
            return existingGrants.Any(g =>
                g.IsUserGrant == isUserGrant
                && resourceIdSet.Contains(g.ResourceId)
                && string.Equals(g.GranteeKey, granteeKey, StringComparison.Ordinal)
                && string.Equals(g.PermissionId, permissionId, StringComparison.Ordinal));
        }

        /// <summary>
        /// Builds the desired grant set when the submitted checkbox matrix is applied to every resource.
        /// </summary>
        public static HashSet<PermissionGrant> BuildDesiredGrants(
            IEnumerable<long> resourceIds,
            IEnumerable<SubmittedGranteePermissions> userPermissions,
            IEnumerable<SubmittedGranteePermissions> groupPermissions)
        {
            var desired = new HashSet<PermissionGrant>();
            if (resourceIds == null)
                return desired;

            var ids = resourceIds.ToList();
            if (userPermissions != null)
            {
                foreach (var row in userPermissions)
                {
                    if (row?.Permissions == null || string.IsNullOrEmpty(row.GranteeKey))
                        continue;

                    foreach (var (permissionId, selected) in row.Permissions)
                    {
                        if (!selected || string.IsNullOrEmpty(permissionId))
                            continue;

                        foreach (var resourceId in ids)
                            desired.Add(new PermissionGrant(resourceId, row.GranteeKey, permissionId, isUserGrant: true));
                    }
                }
            }

            if (groupPermissions != null)
            {
                foreach (var row in groupPermissions)
                {
                    if (row?.Permissions == null || string.IsNullOrEmpty(row.GranteeKey))
                        continue;

                    foreach (var (permissionId, selected) in row.Permissions)
                    {
                        if (!selected || string.IsNullOrEmpty(permissionId))
                            continue;

                        foreach (var resourceId in ids)
                            desired.Add(new PermissionGrant(resourceId, row.GranteeKey, permissionId, isUserGrant: false));
                    }
                }
            }

            return desired;
        }

        /// <summary>
        /// Compares existing grants on the selected resources to the desired replace matrix.
        /// Requires confirmation when all user grants would be removed, or when removal exceeds 10%.
        /// </summary>
        public static BulkPermissionsChangeResult Analyze(
            IEnumerable<PermissionGrant> existingGrants,
            IEnumerable<long> resourceIds,
            IEnumerable<SubmittedGranteePermissions> userPermissions,
            IEnumerable<SubmittedGranteePermissions> groupPermissions)
        {
            var resourceIdSet = (resourceIds ?? Enumerable.Empty<long>()).ToHashSet();
            var existing = (existingGrants ?? Enumerable.Empty<PermissionGrant>())
                .Where(g => resourceIdSet.Contains(g.ResourceId))
                .ToHashSet();

            var desired = BuildDesiredGrants(resourceIdSet, userPermissions, groupPermissions);

            var toAdd = desired.Except(existing).ToList();
            var toRemove = existing.Except(desired).ToList();

            var totalExisting = existing.Count;
            var removalPercent = totalExisting == 0 ? 0.0 : (double)toRemove.Count / totalExisting;

            var existingUserGrants = existing.Where(g => g.IsUserGrant).ToList();
            var removesAllUserGrants = existingUserGrants.Count > 0
                && existingUserGrants.All(g => toRemove.Contains(g));

            var requiresConfirmation =
                (totalExisting > 0 && removalPercent > RemovalConfirmationThreshold)
                || removesAllUserGrants;

            return new BulkPermissionsChangeResult
            {
                GrantsToAdd = toAdd,
                GrantsToRemove = toRemove,
                TotalExistingGrants = totalExisting,
                RemovalPercent = removalPercent,
                RemovesAllUserGrants = removesAllUserGrants,
                RequiresConfirmation = requiresConfirmation
            };
        }
    }
}
