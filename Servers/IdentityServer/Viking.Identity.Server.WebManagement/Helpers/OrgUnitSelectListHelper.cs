using System.Collections.Generic;
using System.Linq;
using Microsoft.AspNetCore.Mvc.Rendering;
using Viking.Identity.Data;
using Viking.Identity.Models;

namespace Viking.Identity.Server.WebManagement.Helpers
{
    public static class OrgUnitSelectListHelper
    {
        /// <summary>Every organization (ids &gt;= 0) as a parent choice; see <see cref="Options"/>.</summary>
        public static SelectList AvailableParents(ApplicationDbContext context, long? selectedParentId = null) =>
            Options(context.OrgUnit.Where(ou => ou.Id >= 0).ToList(), selectedParentId);

        /// <summary>
        /// <paramref name="orgs"/> as dropdown options in tree order (<see cref="ListOrder.Tree"/>), each labelled with
        /// its full path so organizations with the same name can be told apart.
        /// </summary>
        public static SelectList Options(IEnumerable<OrganizationalUnit> orgs, long? selectedId = null)
        {
            var list = orgs.ToList();
            var paths = ListOrder.Paths(list);
            var items = ListOrder.Tree(list).Select(node => new { node.Org.Id, Path = paths[node.Org.Id] });
            return new SelectList(items, "Id", "Path", selectedId);
        }
    }
}
