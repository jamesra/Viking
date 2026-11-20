using System;
using System.Collections.Generic;
using System.Linq;

namespace Viking.Identity.Models
{
    /// <summary>
    /// The order every list on the identity management site uses, so the same things always appear in the same place:
    /// <list type="bullet">
    /// <item>Names sort naturally: ignoring case, with runs of digits compared as numbers (RC2 before RC10).</item>
    /// <item>Organizations sort as a tree: each one directly after its parent, siblings by name.</item>
    /// <item>Things owned by an organization sort by that organization, then by name; unowned ones come last.</item>
    /// <item>People sort by family name, then given name, then user name.</item>
    /// <item>Permissions keep the order their resource type declares them in (<see cref="PermissionRegistry"/>), which
    /// runs from the least to the most powerful; unknown ids follow alphabetically.</item>
    /// </list>
    /// </summary>
    public static class ListOrder
    {
        /// <summary>Label of the bucket for resources without an organization.</summary>
        public const string NoOrganization = "No Organization";

        /// <summary>Separator between the names of an organization path.</summary>
        public const string PathSeparator = " / ";

        /// <summary>Compares names naturally: case-insensitive, digit runs by numeric value, nulls first. Names that differ
        /// only in case are equal.</summary>
        public static readonly IComparer<string> Names = new NaturalComparer();

        /// <summary><paramref name="items"/> by <paramref name="name"/>, naturally.</summary>
        public static IOrderedEnumerable<T> ByName<T>(this IEnumerable<T> items, Func<T, string> name) =>
            items.OrderBy(name, Names);

        /// <summary>Resources by name, then id (so equal names keep a fixed order).</summary>
        public static IOrderedEnumerable<T> ByName<T>(this IEnumerable<T> resources) where T : Resource =>
            resources.OrderBy(r => r.Name, Names).ThenBy(r => r.Id);

        /// <summary>
        /// Resources by the name of the organization that owns them (unowned last), then by their own name. Needs
        /// <see cref="Resource.Parent"/> loaded.
        /// </summary>
        public static IOrderedEnumerable<T> ByOrganization<T>(this IEnumerable<T> resources) where T : Resource =>
            resources
                .OrderBy(r => r.Parent == null)
                .ThenBy(r => r.Parent?.Name, Names)
                .ThenBy(r => r.Name, Names)
                .ThenBy(r => r.Id);

        /// <summary>
        /// Resources grouped by the organization that owns them: groups by organization name with unowned resources
        /// (keyed <see cref="NoOrganization"/>) last, each group by name. Needs <see cref="Resource.Parent"/> loaded.
        /// </summary>
        public static List<IGrouping<string, T>> GroupedByOrganization<T>(this IEnumerable<T> resources) where T : Resource =>
            resources
                .ByOrganization()
                .GroupBy(r => r.Parent?.Name ?? NoOrganization)
                .ToList();

        /// <summary>People by family name, then given name, then user name.</summary>
        public static IOrderedEnumerable<ApplicationUser> ByPerson(this IEnumerable<ApplicationUser> users) =>
            users
                .OrderBy(u => u.FamilyName, Names)
                .ThenBy(u => u.GivenName, Names)
                .ThenBy(u => u.UserName, Names);

        /// <summary>
        /// Permission ids of <paramref name="resourceTypeId"/> in the order the type declares them; ids the type does
        /// not declare follow by name.
        /// </summary>
        public static List<string> Permissions(string resourceTypeId, IEnumerable<string> permissionIds)
        {
            var declared = PermissionRegistry.Entries(resourceTypeId).Select(e => e.Id).ToList();
            return permissionIds
                .OrderBy(id => Rank(declared, id))
                .ThenBy(id => id, Names)
                .ToList();
        }

        /// <summary>
        /// Position of <paramref name="permissionId"/> among all declared permissions: by resource type, then
        /// declaration order. For lists that mix resource types; unknown ids sort last.
        /// </summary>
        public static int PermissionRank(string resourceTypeId, string permissionId) =>
            Rank(PermissionRegistry.Entries(resourceTypeId).Select(e => e.Id).ToList(), permissionId);

        private static int Rank(List<string> declared, string id)
        {
            int index = declared.FindIndex(d => string.Equals(d, id, StringComparison.OrdinalIgnoreCase));
            return index < 0 ? int.MaxValue : index;
        }

        /// <summary>
        /// <paramref name="orgs"/> in tree order: roots (no parent, or a parent not in the list) by name, each followed
        /// by its children, depth first. <c>Depth</c> is 0 for a root. Uses <see cref="Resource.ParentID"/> only, so
        /// the navigation properties need not be loaded.
        /// </summary>
        public static List<(OrganizationalUnit Org, int Depth)> Tree(IEnumerable<OrganizationalUnit> orgs)
        {
            var list = orgs.Where(o => o != null).DistinctBy(o => o.Id).ToList();
            var ids = list.Select(o => o.Id).ToHashSet();
            var children = list
                .Where(o => o.ParentID.HasValue && ids.Contains(o.ParentID.Value))
                .ToLookup(o => o.ParentID.Value);
            var result = new List<(OrganizationalUnit, int)>(list.Count);
            var placed = new HashSet<long>();

            void Visit(OrganizationalUnit org, int depth)
            {
                if (!placed.Add(org.Id))
                    return;
                result.Add((org, depth));
                foreach (var child in children[org.Id].ByName())
                    Visit(child, depth + 1);
            }

            foreach (var root in list.Where(o => !o.ParentID.HasValue || !ids.Contains(o.ParentID.Value)).ByName())
                Visit(root, 0);

            // A parent cycle has no root; list what is left so nothing disappears.
            foreach (var org in list.ByName())
                Visit(org, 0);

            return result;
        }

        /// <summary>
        /// Path of every organization in <paramref name="orgs"/>, from its root down ("Lab / Project"). Organizations
        /// whose parent is not in the list start their own path.
        /// </summary>
        public static Dictionary<long, string> Paths(IEnumerable<OrganizationalUnit> orgs)
        {
            var byId = orgs.Where(o => o != null).DistinctBy(o => o.Id).ToDictionary(o => o.Id);
            var paths = new Dictionary<long, string>();
            foreach (var org in byId.Values)
            {
                var names = new List<string>();
                var seen = new HashSet<long>();
                for (var current = org; current != null && seen.Add(current.Id);
                     current = current.ParentID.HasValue && byId.TryGetValue(current.ParentID.Value, out var parent) ? parent : null)
                {
                    names.Add(current.Name);
                }

                names.Reverse();
                paths[org.Id] = string.Join(PathSeparator, names);
            }

            return paths;
        }

        private sealed class NaturalComparer : IComparer<string>
        {
            public int Compare(string x, string y)
            {
                if (ReferenceEquals(x, y))
                    return 0;
                if (x == null)
                    return -1;
                if (y == null)
                    return 1;

                int i = 0, j = 0;
                while (i < x.Length && j < y.Length)
                {
                    if (char.IsDigit(x[i]) && char.IsDigit(y[j]))
                    {
                        int startX = i, startY = j;
                        while (i < x.Length && char.IsDigit(x[i])) i++;
                        while (j < y.Length && char.IsDigit(y[j])) j++;
                        var digitsX = x.AsSpan(startX, i - startX).TrimStart('0');
                        var digitsY = y.AsSpan(startY, j - startY).TrimStart('0');
                        if (digitsX.Length != digitsY.Length)
                            return digitsX.Length.CompareTo(digitsY.Length);
                        int digits = digitsX.SequenceCompareTo(digitsY);
                        if (digits != 0)
                            return digits;
                        continue;
                    }

                    int chars = char.ToUpperInvariant(x[i]).CompareTo(char.ToUpperInvariant(y[j]));
                    if (chars != 0)
                        return chars;
                    i++;
                    j++;
                }

                // Names that differ only in case (or leading zeros) are equal, so the next key decides.
                return (x.Length - i).CompareTo(y.Length - j);
            }
        }
    }
}
