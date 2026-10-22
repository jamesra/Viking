using System;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.EntityFrameworkCore;
using Viking.Identity.Data;
using Viking.Identity.Models;
using Viking.Identity.Server.Extensions.Services;
using Xunit;

namespace TestIdentityModel
{
    /// <summary>
    /// Covers groups holding resources, and the annotation contexts a segmentation service reaches from its local
    /// resources (contexts, groups, organizational units).
    /// </summary>
    public class SegmentationServiceTests
    {
        private static SegmentationService AddService(ApplicationDbContext db, string name)
        {
            var service = new SegmentationService
            {
                Name = name,
                ResourceTypeId = nameof(SegmentationService),
                Endpoint = new Uri($"https://{name}.example/")
            };
            db.SegmentationServices.Add(service);
            db.SaveChanges();
            return service;
        }

        private static Group AddGroup(ApplicationDbContext db, string name, long? parentId = null)
        {
            var group = new Group { Name = name, ResourceTypeId = nameof(Group), ParentID = parentId };
            db.Group.Add(group);
            db.SaveChanges();
            return group;
        }

        private static OrganizationalUnit AddOrgUnit(ApplicationDbContext db, string name, long? parentId = null)
        {
            var ou = new OrganizationalUnit { Name = name, ResourceTypeId = nameof(OrganizationalUnit), ParentID = parentId };
            db.OrgUnit.Add(ou);
            db.SaveChanges();
            return ou;
        }

        private static AnnotationContext AddContext(ApplicationDbContext db, string name, long? parentId = null)
        {
            var context = CatalogDb.AddVolume(db, name, $"http://storage/{name}/{name}.vikingxml");
            context.ParentID = parentId;
            db.SaveChanges();
            return context;
        }

        private static void Local(ApplicationDbContext db, SegmentationService service, Resource resource)
        {
            db.SegmentationServiceLocalResources.Add(new SegmentationServiceLocalResource { SegmentationServiceId = service.Id, ResourceId = resource.Id });
            db.SaveChanges();
        }

        private static async Task Member(ApplicationDbContext db, Group group, Resource resource)
        {
            Assert.True(await db.AddResourceToGroupAsync(group.Id, resource.Id));
            await db.SaveChangesAsync();
        }

        private static void Nest(ApplicationDbContext db, Group container, Group member)
        {
            db.GroupToGroupAssignments.Add(new GroupToGroupAssignment { ContainerGroupId = container.Id, MemberGroupId = member.Id });
            db.SaveChanges();
        }

        private static async Task<string[]> LocalNames(ApplicationDbContext db, SegmentationService service) =>
            (await db.LocalAnnotationContextsAsync(service.Id)).Select(l => l.Context.Name).ToArray();

        [Fact]
        public async Task AddResourceToGroup_RefusesGroupsAndRepeats()
        {
            await using var db = CatalogDb.Create();
            var utah = AddGroup(db, "Utah");
            var other = AddGroup(db, "Other");
            var rc1 = AddContext(db, "RC1");

            Assert.True(await db.AddResourceToGroupAsync(utah.Id, rc1.Id));
            await db.SaveChangesAsync();
            Assert.False(await db.AddResourceToGroupAsync(utah.Id, rc1.Id));
            Assert.False(await db.AddResourceToGroupAsync(utah.Id, other.Id));
            Assert.Single(db.ResourceToGroupAssignments);
        }

        [Fact]
        public async Task DeletingAGroupOrResource_RemovesItsMembershipAndLocalRows()
        {
            await using var db = CatalogDb.Create();
            var utah = AddGroup(db, "Utah");
            var rc1 = AddContext(db, "RC1");
            var rc2 = AddContext(db, "RC2");
            var service = AddService(db, "seg-utah");
            await Member(db, utah, rc1);
            await Member(db, utah, rc2);
            Local(db, service, utah);
            Local(db, service, rc2);

            db.AnnotationContexts.Remove(rc2);
            await db.SaveChangesAsync();
            Assert.Equal(new[] { rc1.Id }, db.ResourceToGroupAssignments.Select(a => a.ResourceId).ToArray());
            Assert.Equal(new[] { utah.Id }, db.SegmentationServiceLocalResources.Select(l => l.ResourceId).ToArray());

            db.Group.Remove(utah);
            await db.SaveChangesAsync();
            Assert.Empty(db.ResourceToGroupAssignments);
            Assert.Empty(db.SegmentationServiceLocalResources);
        }

        [Fact]
        public async Task LocalOrgUnit_ReachesSubUnitsAndGroupsItOwns()
        {
            await using var db = CatalogDb.Create();
            var lab = AddOrgUnit(db, "Lab");
            var subLab = AddOrgUnit(db, "SubLab", lab.Id);
            var labGroup = AddGroup(db, "LabGroup", lab.Id);
            var direct = AddContext(db, "Direct", lab.Id);
            var nested = AddContext(db, "Nested", subLab.Id);
            var grouped = AddContext(db, "Grouped");
            AddContext(db, "Elsewhere");
            await Member(db, labGroup, grouped);
            var service = AddService(db, "seg");
            Local(db, service, lab);

            Assert.Equal(new[] { "Direct", "Grouped", "Nested" }, await LocalNames(db, service));

            nested.ParentID = null;
            await db.SaveChangesAsync();
            Assert.Equal(new[] { "Direct", "Grouped" }, await LocalNames(db, service));
        }

        [Fact]
        public async Task LocalGroup_ReachesNestedGroups_AndPicksUpNewMembers()
        {
            await using var db = CatalogDb.Create();
            var usWest = AddGroup(db, "US-West");
            var utah = AddGroup(db, "Utah");
            Nest(db, usWest, utah);
            var rc1 = AddContext(db, "RC1");
            await Member(db, utah, rc1);
            var service = AddService(db, "seg");
            Local(db, service, usWest);

            var only = Assert.Single(await db.LocalAnnotationContextsAsync(service.Id));
            Assert.Equal("RC1", only.Context.Name);
            Assert.Equal(usWest.Id, only.Via.Id);

            await Member(db, utah, AddContext(db, "RC2"));
            Assert.Equal(new[] { "RC1", "RC2" }, await LocalNames(db, service));
        }

        [Fact]
        public async Task LocalGroupInAChild_DoesNotReachTheParentsContexts()
        {
            await using var db = CatalogDb.Create();
            var usWest = AddGroup(db, "US-West");
            var utah = AddGroup(db, "Utah");
            Nest(db, usWest, utah);
            await Member(db, usWest, AddContext(db, "Coastal"));
            await Member(db, utah, AddContext(db, "RC1"));
            var service = AddService(db, "seg");
            Local(db, service, utah);

            Assert.Equal(new[] { "RC1" }, await LocalNames(db, service));
        }

        [Fact]
        public async Task ContextReachedTwice_IsListedOnce_AndCyclesEnd()
        {
            await using var db = CatalogDb.Create();
            var a = AddGroup(db, "A");
            var b = AddGroup(db, "B");
            Nest(db, a, b);
            Nest(db, b, a);
            var rc1 = AddContext(db, "RC1");
            await Member(db, a, rc1);
            await Member(db, b, rc1);
            var service = AddService(db, "seg");
            Local(db, service, a);
            Local(db, service, rc1);

            Assert.Equal(new[] { "RC1" }, await LocalNames(db, service));
        }

        [Fact]
        public async Task OtherResourcesAndTheServicesOwnMembership_ReachNothing()
        {
            await using var db = CatalogDb.Create();
            var utah = AddGroup(db, "Utah");
            await Member(db, utah, AddContext(db, "RC1"));
            var service = AddService(db, "seg");
            var otherService = AddService(db, "seg-other");
            await Member(db, utah, service);
            Local(db, service, otherService);

            Assert.Empty(await LocalNames(db, service));
        }

        [Fact]
        public async Task ContextMembership_GrantsNoPermissions()
        {
            await using var db = CatalogDb.Create();
            var user = new ApplicationUser { Id = Guid.NewGuid().ToString(), UserName = "u", GivenName = "U", FamilyName = "User" };
            db.Users.Add(user);
            var utah = AddGroup(db, "Utah");
            db.UserToGroupAssignments.Add(new UserToGroupAssignment { GroupId = utah.Id, UserId = user.Id });
            var rc1 = AddContext(db, "RC1");
            await Member(db, utah, rc1);

            Assert.False(await db.IsUserPermitted(rc1.Id, user.Id, Special.Permissions.AnnotationContext.Access));
        }

        [Fact]
        public async Task ListAsync_ReturnsLocalContextsWithTheirVikingXml()
        {
            await using var db = CatalogDb.Create();
            var utah = AddGroup(db, "Utah");
            var rc1 = AddContext(db, "RC1");
            await Member(db, utah, rc1);
            AddContext(db, "RC2");
            var service = AddService(db, "seg-utah");
            Local(db, service, utah);

            var assigned = await SegmentationServiceAssignments.ListAsync(db, "seg-utah");

            var only = Assert.Single(assigned);
            Assert.Equal(rc1.Id, only.AnnotationContextId);
            Assert.Equal("http://storage/RC1/RC1.vikingxml", only.Endpoint);
            Assert.Null(await SegmentationServiceAssignments.ListAsync(db, "nope"));
        }
    }
}
