using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.EntityFrameworkCore;
using Viking.Identity.Data;
using Viking.Identity.Models;

namespace Viking.Identity.Server.WebManagement.Models.UserViewModels
{
    /// <summary>A resource in a checklist, with its type so the list can be grouped and filtered by type.</summary>
    public class ResourceSelectedViewModel : NamedItemSelectedViewModel<long>
    {
        public string ResourceTypeId { get; set; }

        /// <summary>The management controller that shows a resource of this type.</summary>
        public static string ControllerFor(string resourceTypeId) => resourceTypeId + "s";

        /// <summary>The resources of <paramref name="resourceTypeIds"/>, by type then name, selected where <paramref name="selected"/> holds the id.</summary>
        public static async Task<List<ResourceSelectedViewModel>> ListAsync(ApplicationDbContext context, IEnumerable<string> resourceTypeIds, ISet<long> selected)
        {
            var types = resourceTypeIds.ToList();
            return (await context.Resource.AsNoTracking()
                    .Where(r => types.Contains(r.ResourceTypeId))
                    .Select(r => new { r.Id, r.Name, r.ResourceTypeId })
                    .ToListAsync())
                .OrderBy(r => types.IndexOf(r.ResourceTypeId))
                .ThenBy(r => r.Name)
                .Select(r => new ResourceSelectedViewModel { Id = r.Id, Name = r.Name, ResourceTypeId = r.ResourceTypeId, Selected = selected.Contains(r.Id) })
                .ToList();
        }
    }

    public static class ResourceSelectedViewModelExtensions
    {
        /// <summary>
        /// Resource types a group can hold as resource members. Groups are left out: they nest through
        /// <see cref="GroupToGroupAssignment"/>.
        /// </summary>
        public static readonly string[] GroupMemberResourceTypes =
        {
            nameof(AnnotationContext), nameof(OrganizationalUnit), nameof(SegmentationService),
            nameof(CorrectionService), nameof(AnnotationServer), nameof(Connectome),
        };

        /// <summary>Resource types worth listing as a segmentation service's local resources: contexts and the containers that reach them.</summary>
        public static readonly string[] SegmentationLocalResourceTypes =
        {
            nameof(OrganizationalUnit), nameof(Group), nameof(AnnotationContext),
        };

        /// <summary>Add or remove the group's resource members to match the checklist. Group rows in the list are ignored.</summary>
        public static void UpdateResourceMembership(this Group group, IEnumerable<ResourceSelectedViewModel> resources)
        {
            if (resources == null)
                return;

            foreach (var resource in resources)
            {
                if (resource.ResourceTypeId == nameof(Group) || resource.Id == group.Id)
                    continue;

                var existing = group.MemberResources.FirstOrDefault(m => m.ResourceId == resource.Id);
                if (resource.Selected && existing == null)
                    group.MemberResources.Add(new ResourceToGroupAssignment { GroupId = group.Id, ResourceId = resource.Id });
                else if (!resource.Selected && existing != null)
                    group.MemberResources.Remove(existing);
            }
        }

        /// <summary>Add or remove the service's local resources to match the checklist.</summary>
        public static void UpdateLocalResources(this SegmentationService service, IEnumerable<ResourceSelectedViewModel> resources)
        {
            if (resources == null)
                return;

            foreach (var resource in resources)
            {
                var existing = service.LocalResources.FirstOrDefault(l => l.ResourceId == resource.Id);
                if (resource.Selected && existing == null)
                    service.LocalResources.Add(new SegmentationServiceLocalResource { SegmentationServiceId = service.Id, ResourceId = resource.Id });
                else if (!resource.Selected && existing != null)
                    service.LocalResources.Remove(existing);
            }
        }
    }
}
