using System.Linq;
using Microsoft.AspNetCore.Mvc.Rendering;
using Viking.Identity.Data;
using Viking.Identity.Models;

namespace Viking.Identity.Server.WebManagement.Helpers
{
    public static class OrgUnitSelectListHelper
    {
        public static SelectList AvailableParents(ApplicationDbContext context, long? selectedParentId = null)
        {
            return new SelectList(
                context.OrgUnit.Where(ou => ou.Id >= 0),
                nameof(OrganizationalUnit.Id),
                nameof(OrganizationalUnit.Name),
                selectedParentId);
        }
    }
}
