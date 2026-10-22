using Microsoft.EntityFrameworkCore;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Viking.Identity.Models;

namespace Viking.Identity.Data
{
    /// <summary>One annotation context a segmentation service serves, and the local resource it was reached through.</summary>
    public sealed record LocalAnnotationContext(AnnotationContext Context, Resource Via);

    /// <summary>
    /// Resource membership of groups, and the annotation contexts a segmentation service reaches from its local resources.
    /// </summary>
    public static class ResourceMembership
    {
        /// <summary>
        /// Adds <paramref name="resourceId"/> to <paramref name="groupId"/>. Groups nest through
        /// <see cref="GroupToGroupAssignment"/>, so a group is refused here. Does not save.
        /// </summary>
        /// <returns>False when the resource is a group, either id is unknown, or it is already a member.</returns>
        public static async Task<bool> AddResourceToGroupAsync(this ApplicationDbContext context, long groupId, long resourceId)
        {
            var resource = await context.Resource.FirstOrDefaultAsync(r => r.Id == resourceId);
            if (resource == null || resource is Group)
                return false;
            if (!await context.Group.AnyAsync(g => g.Id == groupId))
                return false;
            if (await context.ResourceToGroupAssignments.AnyAsync(a => a.GroupId == groupId && a.ResourceId == resourceId))
                return false;

            context.ResourceToGroupAssignments.Add(new ResourceToGroupAssignment { GroupId = groupId, ResourceId = resourceId });
            return true;
        }

        /// <summary>
        /// The annotation contexts reached from a segmentation service's local resources: a context itself, a group's
        /// member resources and member groups, an organizational unit's children (sub-units and groups included).
        /// Each context appears once, with the first local resource that reached it. Cycles end the walk.
        /// </summary>
        public static async Task<List<LocalAnnotationContext>> LocalAnnotationContextsAsync(this ApplicationDbContext context, long segmentationServiceId)
        {
            var roots = await context.SegmentationServiceLocalResources
                .Where(l => l.SegmentationServiceId == segmentationServiceId)
                .Select(l => l.ResourceId)
                .ToListAsync();

            // via[id] is the local resource the walk reached id from.
            var via = new Dictionary<long, long>();
            foreach (long root in roots)
                via.TryAdd(root, root);

            var contextIds = new List<long>();
            var frontier = roots.Distinct().ToList();
            while (frontier.Count > 0)
            {
                var level = await context.Resource
                    .Where(r => frontier.Contains(r.Id))
                    .Select(r => new { r.Id, r.ResourceTypeId })
                    .ToListAsync();

                contextIds.AddRange(level.Where(r => r.ResourceTypeId == nameof(AnnotationContext)).Select(r => r.Id));
                var groups = level.Where(r => r.ResourceTypeId == nameof(Group)).Select(r => r.Id).ToList();
                var orgUnits = level.Where(r => r.ResourceTypeId == nameof(OrganizationalUnit)).Select(r => r.Id).ToList();

                var next = new List<(long Child, long Parent)>();
                if (groups.Count > 0)
                {
                    next.AddRange((await context.ResourceToGroupAssignments
                            .Where(a => groups.Contains(a.GroupId))
                            .Select(a => new { a.ResourceId, a.GroupId })
                            .ToListAsync())
                        .Select(a => (a.ResourceId, a.GroupId)));
                    next.AddRange((await context.GroupToGroupAssignments
                            .Where(a => groups.Contains(a.ContainerGroupId))
                            .Select(a => new { a.MemberGroupId, a.ContainerGroupId })
                            .ToListAsync())
                        .Select(a => (a.MemberGroupId, a.ContainerGroupId)));
                }
                if (orgUnits.Count > 0)
                {
                    next.AddRange((await context.Resource
                            .Where(r => r.ParentID.HasValue && orgUnits.Contains(r.ParentID.Value))
                            .Select(r => new { r.Id, Parent = r.ParentID.Value })
                            .ToListAsync())
                        .Select(r => (r.Id, r.Parent)));
                }

                frontier = new List<long>();
                foreach (var (child, parent) in next)
                {
                    if (via.TryAdd(child, via[parent]))
                        frontier.Add(child);
                }
            }

            if (contextIds.Count == 0)
                return new List<LocalAnnotationContext>();

            var rootIds = contextIds.Select(id => via[id]).Distinct().ToList();
            var rootResources = await context.Resource.Where(r => rootIds.Contains(r.Id)).ToDictionaryAsync(r => r.Id);
            var contexts = await context.AnnotationContexts.Where(c => contextIds.Contains(c.Id)).ToListAsync();
            return contexts
                .OrderBy(c => c.Name, StringComparer.OrdinalIgnoreCase)
                .Select(c => new LocalAnnotationContext(c, rootResources[via[c.Id]]))
                .ToList();
        }
    }
}
