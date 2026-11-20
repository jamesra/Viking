using System.Collections.Generic;
using System.Linq;
using Viking.Identity.Models;
using Xunit;

namespace TestIdentityModel
{
    /// <summary>The list order the identity management site uses (<see cref="ListOrder"/>).</summary>
    public class ListOrderTests
    {
        [Fact]
        public void NamesSortNaturallyIgnoringCase()
        {
            var names = new[] { "RC10", "rc2", "RC1", "Alpha", "alpha 2", "beta", null };
            Assert.Equal(new[] { null, "Alpha", "alpha 2", "beta", "RC1", "rc2", "RC10" }, names.OrderBy(n => n, ListOrder.Names));
        }

        [Fact]
        public void OrganizationsSortAsATreeWithSiblingsByName()
        {
            var orgs = new List<OrganizationalUnit>
            {
                Org(1, "Moran Lab"),
                Org(2, "Retina", 1),
                Org(3, "Anderson Lab"),
                Org(4, "Cochlea", 1),
                Org(5, "RC10", 3),
                Org(6, "RC2", 3),
                Org(7, "Orphan", 99),
            };

            var tree = ListOrder.Tree(orgs);

            Assert.Equal(new[] { "Anderson Lab", "RC2", "RC10", "Moran Lab", "Cochlea", "Retina", "Orphan" }, tree.Select(n => n.Org.Name));
            Assert.Equal(new[] { 0, 1, 1, 0, 1, 1, 0 }, tree.Select(n => n.Depth));
        }

        [Fact]
        public void AParentCycleStillListsEveryOrganization()
        {
            var tree = ListOrder.Tree(new[] { Org(1, "A", 2), Org(2, "B", 1) });
            Assert.Equal(2, tree.Count);
        }

        [Fact]
        public void PathsRunFromTheRoot()
        {
            var paths = ListOrder.Paths(new[] { Org(1, "Lab"), Org(2, "Project", 1), Org(3, "Sub", 2) });
            Assert.Equal("Lab / Project / Sub", paths[3]);
            Assert.Equal("Lab", paths[1]);
        }

        [Fact]
        public void ResourcesGroupByOrganizationWithUnownedLast()
        {
            var lab = Org(1, "Lab");
            var volumes = new[]
            {
                new AnnotationContext { Id = 10, Name = "RC10", Parent = lab },
                new AnnotationContext { Id = 11, Name = "Loose" },
                new AnnotationContext { Id = 12, Name = "RC2", Parent = lab },
            };

            var groups = volumes.GroupedByOrganization();

            Assert.Equal(new[] { "Lab", ListOrder.NoOrganization }, groups.Select(g => g.Key));
            Assert.Equal(new[] { "RC2", "RC10" }, groups[0].Select(v => v.Name));
        }

        [Fact]
        public void PeopleSortByFamilyThenGivenName()
        {
            var users = new[]
            {
                new ApplicationUser { UserName = "z", GivenName = "Bea", FamilyName = "Smith" },
                new ApplicationUser { UserName = "y", GivenName = "Al", FamilyName = "smith" },
                new ApplicationUser { UserName = "x", GivenName = "Cy", FamilyName = "Jones" },
            };
            Assert.Equal(new[] { "x", "y", "z" }, users.ByPerson().Select(u => u.UserName));
        }

        [Fact]
        public void PermissionsKeepDeclarationOrderAndUnknownIdsFollow()
        {
            var ordered = ListOrder.Permissions(nameof(AnnotationServer), new[] { "Review", "Zed", "Read", "Annotate", "Extra" });
            Assert.Equal(new[] { "Read", "Annotate", "Review", "Extra", "Zed" }, ordered);
        }

        private static OrganizationalUnit Org(long id, string name, long? parentId = null) =>
            new OrganizationalUnit { Id = id, Name = name, ParentID = parentId };
    }
}
