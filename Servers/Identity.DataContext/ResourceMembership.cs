using Microsoft.EntityFrameworkCore;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Viking.Identity.Models;

namespace Viking.Identity.Data
{
    /// <summary>One annotation context a context service serves, and the assigned resource it was reached through.</summary>
    public sealed record ServedAnnotationContext(AnnotationContext Context, Resource Via);

    /// <summary>A context a correction service would reach that another correction service already has.</summary>
    public sealed record CorrectionConflict(AnnotationContext Context, CorrectionService Owner);

    /// <summary>
    /// Resource membership of groups, and the annotation contexts a <see cref="ContextService"/> reaches from its
    /// assigned resources.
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

        /// <summary>The resources assigned to a context service, by type then name.</summary>
        public static Task<List<Resource>> AssignedResourcesAsync(this ApplicationDbContext context, long serviceId) =>
            context.ContextServiceAssignments
                .Where(a => a.ServiceId == serviceId)
                .Select(a => a.Resource)
                .OrderBy(r => r.ResourceTypeId).ThenBy(r => r.Name)
                .ToListAsync();

        /// <summary>
        /// The annotation contexts reached from a context service's assigned resources: a context itself, a group's
        /// member resources and member groups, an organizational unit's children (sub-units and groups included).
        /// Each context appears once, with the first assigned resource that reached it. Cycles end the walk.
        /// For a correction service this includes contexts another correction service owns; see
        /// <see cref="CorrectionOwnersAsync"/>.
        /// </summary>
        public static async Task<List<ServedAnnotationContext>> ServedAnnotationContextsAsync(this ApplicationDbContext context, long serviceId)
        {
            var roots = await context.ContextServiceAssignments
                .Where(a => a.ServiceId == serviceId)
                .Select(a => a.ResourceId)
                .ToListAsync();

            var via = await context.ContextsReachedFromAsync(roots);
            if (via.Count == 0)
                return new List<ServedAnnotationContext>();

            var rootIds = via.Values.Distinct().ToList();
            var rootResources = await context.Resource.Where(r => rootIds.Contains(r.Id)).ToDictionaryAsync(r => r.Id);
            var contextIds = via.Keys.ToList();
            var contexts = await context.AnnotationContexts.Where(c => contextIds.Contains(c.Id)).ToListAsync();
            return contexts
                .OrderBy(c => c.Name, StringComparer.OrdinalIgnoreCase)
                .Select(c => new ServedAnnotationContext(c, rootResources[via[c.Id]]))
                .ToList();
        }

        /// <summary>
        /// The correction service that owns each annotation context reached by any correction service. A context's
        /// corrections live in one place, so it has one owner: the management site refuses assignments that overlap,
        /// and where later group or unit changes make two services reach a context, the lowest service id keeps it.
        /// </summary>
        /// <param name="exceptServiceId">Leave this service out, to check its proposed assignments against the rest.</param>
        public static async Task<Dictionary<long, CorrectionService>> CorrectionOwnersAsync(this ApplicationDbContext context, long? exceptServiceId = null)
        {
            var services = await context.CorrectionServices
                .Where(s => s.Id != exceptServiceId)
                .OrderBy(s => s.Id)
                .ToListAsync();
            var serviceIds = services.Select(s => s.Id).ToList();
            var assignments = (await context.ContextServiceAssignments
                    .Where(a => serviceIds.Contains(a.ServiceId))
                    .Select(a => new { a.ServiceId, a.ResourceId })
                    .ToListAsync())
                .ToLookup(a => a.ServiceId, a => a.ResourceId);

            var owners = new Dictionary<long, CorrectionService>();
            foreach (var service in services)
            {
                foreach (long contextId in (await context.ContextsReachedFromAsync(assignments[service.Id])).Keys)
                    owners.TryAdd(contextId, service);
            }
            return owners;
        }

        /// <summary>
        /// The annotation contexts a correction service owns: those it reaches that no lower-id correction service
        /// reaches too.
        /// </summary>
        public static async Task<List<ServedAnnotationContext>> CorrectedAnnotationContextsAsync(this ApplicationDbContext context, long correctionServiceId)
        {
            var owners = await context.CorrectionOwnersAsync();
            return (await context.ServedAnnotationContextsAsync(correctionServiceId))
                .Where(s => owners.TryGetValue(s.Context.Id, out var owner) && owner.Id == correctionServiceId)
                .ToList();
        }

        /// <summary>
        /// The contexts that assigning <paramref name="resourceIds"/> to a correction service would reach, but that
        /// another correction service already has. Empty when the assignment can be saved.
        /// </summary>
        public static async Task<List<CorrectionConflict>> CorrectionConflictsAsync(this ApplicationDbContext context, long correctionServiceId, IEnumerable<long> resourceIds)
        {
            var reached = await context.ContextsReachedFromAsync(resourceIds);
            if (reached.Count == 0)
                return new List<CorrectionConflict>();

            var owners = await context.CorrectionOwnersAsync(correctionServiceId);
            var conflictIds = reached.Keys.Where(owners.ContainsKey).ToList();
            if (conflictIds.Count == 0)
                return new List<CorrectionConflict>();

            var contexts = await context.AnnotationContexts.Where(c => conflictIds.Contains(c.Id)).ToListAsync();
            return contexts
                .OrderBy(c => c.Name, StringComparer.OrdinalIgnoreCase)
                .Select(c => new CorrectionConflict(c, owners[c.Id]))
                .ToList();
        }

        /// <summary>The context services that serve <paramref name="annotationContextId"/>, by kind then name.</summary>
        public static async Task<List<ContextService>> ServicesOfContextAsync(this ApplicationDbContext context, long annotationContextId)
        {
            var services = await context.Resource.OfType<ContextService>().ToListAsync();
            var assignments = (await context.ContextServiceAssignments
                    .Select(a => new { a.ServiceId, a.ResourceId })
                    .ToListAsync())
                .ToLookup(a => a.ServiceId, a => a.ResourceId);

            var serving = new List<ContextService>();
            foreach (var service in services)
            {
                if ((await context.ContextsReachedFromAsync(assignments[service.Id])).ContainsKey(annotationContextId))
                    serving.Add(service);
            }
            return serving
                .OrderBy(s => s.ResourceTypeId)
                .ThenBy(s => s.Name, StringComparer.OrdinalIgnoreCase)
                .ToList();
        }

        /// <summary>
        /// Walks from <paramref name="roots"/> to the annotation contexts they reach: a context itself, a group's member
        /// resources and member groups, an organizational unit's children. Cycles end the walk.
        /// </summary>
        /// <returns>Each reached context id, mapped to the first root that reached it.</returns>
        private static async Task<Dictionary<long, long>> ContextsReachedFromAsync(this ApplicationDbContext context, IEnumerable<long> roots)
        {
            // via[id] is the root the walk reached id from.
            var via = new Dictionary<long, long>();
            foreach (long root in roots)
                via.TryAdd(root, root);

            var contextIds = new List<long>();
            var frontier = via.Keys.ToList();
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

            return contextIds.ToDictionary(id => id, id => via[id]);
        }
    }
}
