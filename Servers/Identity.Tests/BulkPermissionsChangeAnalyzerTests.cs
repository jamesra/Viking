using System.Collections.Generic;
using System.Linq;
using Viking.Identity.Models;
using Viking.Identity.Server.Extensions.Services;
using Xunit;

namespace TestIdentityModel
{
    public class BulkPermissionsChangeAnalyzerTests
    {
        [Fact]
        public void Union_IsGrantedOnAnyResource()
        {
            var grants = new[]
            {
                new PermissionGrant(1, "10", Special.Permissions.Volume.Review, isUserGrant: false),
                new PermissionGrant(2, "10", Special.Permissions.Volume.Read, isUserGrant: false)
            };

            Assert.True(BulkPermissionsChangeAnalyzer.IsGrantedOnAnyResource(
                grants, new[] { 1L, 2L }, "10", Special.Permissions.Volume.Review, isUserGrant: false));
            Assert.True(BulkPermissionsChangeAnalyzer.IsGrantedOnAnyResource(
                grants, new[] { 1L, 2L }, "10", Special.Permissions.Volume.Read, isUserGrant: false));
            Assert.False(BulkPermissionsChangeAnalyzer.IsGrantedOnAnyResource(
                grants, new[] { 1L, 2L }, "10", Special.Permissions.Volume.Annotate, isUserGrant: false));
        }

        [Fact]
        public void Analyze_UnchangedUnion_HasZeroRemovals()
        {
            var existing = new[]
            {
                new PermissionGrant(1, "10", Special.Permissions.Volume.Review, isUserGrant: false),
                new PermissionGrant(2, "10", Special.Permissions.Volume.Read, isUserGrant: false)
            };

            var groups = new[]
            {
                new SubmittedGranteePermissions
                {
                    GranteeKey = "10",
                    Permissions = new List<(string, bool)>
                    {
                        (Special.Permissions.Volume.Review, true),
                        (Special.Permissions.Volume.Read, true),
                        (Special.Permissions.Volume.Annotate, false)
                    }
                }
            };

            var result = BulkPermissionsChangeAnalyzer.Analyze(existing, new[] { 1L, 2L }, null, groups);

            // Replace applies union to both resources: Review+Read on both => adds missing cross grants, no removals of selected.
            Assert.Empty(result.GrantsToRemove);
            Assert.False(result.RequiresConfirmation);
            Assert.Contains(result.GrantsToAdd, g =>
                g.ResourceId == 2 && g.PermissionId == Special.Permissions.Volume.Review);
            Assert.Contains(result.GrantsToAdd, g =>
                g.ResourceId == 1 && g.PermissionId == Special.Permissions.Volume.Read);
        }

        [Fact]
        public void Analyze_UncheckReview_RemovesAcrossResources_TriggersConfirmationWhenOverThreshold()
        {
            var existing = new[]
            {
                new PermissionGrant(1, "10", Special.Permissions.Volume.Review, isUserGrant: false),
                new PermissionGrant(2, "10", Special.Permissions.Volume.Read, isUserGrant: false)
            };

            var groups = new[]
            {
                new SubmittedGranteePermissions
                {
                    GranteeKey = "10",
                    Permissions = new List<(string, bool)>
                    {
                        (Special.Permissions.Volume.Review, false),
                        (Special.Permissions.Volume.Read, true)
                    }
                }
            };

            var result = BulkPermissionsChangeAnalyzer.Analyze(existing, new[] { 1L, 2L }, null, groups);

            Assert.Contains(result.GrantsToRemove, g =>
                g.ResourceId == 1 && g.PermissionId == Special.Permissions.Volume.Review);
            // 1 of 2 existing removed = 50% > 10%
            Assert.True(result.RequiresConfirmation);
            Assert.True(result.RemovalPercent > BulkPermissionsChangeAnalyzer.RemovalConfirmationThreshold);
        }

        [Fact]
        public void Analyze_RemoveAllUserGrants_RequiresConfirmation()
        {
            var existing = new[]
            {
                new PermissionGrant(1, "user-a", Special.Permissions.Volume.Read, isUserGrant: true),
                new PermissionGrant(2, "user-a", Special.Permissions.Volume.Read, isUserGrant: true),
                new PermissionGrant(3, "user-b", Special.Permissions.Volume.Annotate, isUserGrant: true)
            };

            var users = new[]
            {
                new SubmittedGranteePermissions
                {
                    GranteeKey = "user-a",
                    Permissions = new List<(string, bool)>
                    {
                        (Special.Permissions.Volume.Read, false),
                        (Special.Permissions.Volume.Annotate, false)
                    }
                },
                new SubmittedGranteePermissions
                {
                    GranteeKey = "user-b",
                    Permissions = new List<(string, bool)>
                    {
                        (Special.Permissions.Volume.Read, false),
                        (Special.Permissions.Volume.Annotate, false)
                    }
                }
            };

            var result = BulkPermissionsChangeAnalyzer.Analyze(existing, new[] { 1L, 2L, 3L }, users, null);

            Assert.True(result.RemovesAllUserGrants);
            Assert.True(result.RequiresConfirmation);
            Assert.Equal(3, result.GrantsToRemove.Count);
        }

        [Fact]
        public void Analyze_RemoveOneOfTwenty_DoesNotRequireConfirmation()
        {
            var existing = Enumerable.Range(1, 20)
                .Select(i => new PermissionGrant(i, "10", Special.Permissions.Volume.Read, isUserGrant: false))
                .ToList();

            // Keep Read on all except resource 1 (unchecked means remove from all — so check Read for none? 
            // To remove only 1 of 20 with replace semantics: leave Read checked → desired has Read on all 20 → 0 removals.
            // To remove exactly 1 grant under replace: we need the submitted matrix to not include that grant.
            // With replace-to-all, unchecking removes from ALL resources. So "1 of 20" means:
            // existing has 20 grants of different kinds, submit keeps 19.
            // Build: 19 Read grants + 1 Annotate; uncheck Annotate only.
            existing[19] = new PermissionGrant(20, "10", Special.Permissions.Volume.Annotate, isUserGrant: false);

            var groups = new[]
            {
                new SubmittedGranteePermissions
                {
                    GranteeKey = "10",
                    Permissions = new List<(string, bool)>
                    {
                        (Special.Permissions.Volume.Read, true),
                        (Special.Permissions.Volume.Annotate, false)
                    }
                }
            };

            var resourceIds = Enumerable.Range(1, 20).Select(i => (long)i).ToList();
            var result = BulkPermissionsChangeAnalyzer.Analyze(existing, resourceIds, null, groups);

            // Annotate removed from resource 20 only among existing; but replace also adds Read to 20 and Annotate removal is 1/20 = 5%.
            Assert.True(result.RemovalPercent <= BulkPermissionsChangeAnalyzer.RemovalConfirmationThreshold);
            Assert.False(result.RemovesAllUserGrants);
            Assert.False(result.RequiresConfirmation);
            Assert.Single(result.GrantsToRemove);
            Assert.Equal(Special.Permissions.Volume.Annotate, result.GrantsToRemove[0].PermissionId);
        }
    }
}
