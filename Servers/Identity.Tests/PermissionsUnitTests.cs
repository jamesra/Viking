using System;
using System.Linq;
using System.Threading.Tasks;
using Viking.Identity.Data;
using Viking.Identity.Models;
using Microsoft.AspNetCore.Identity;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using Xunit;

namespace TestIdentityModel
{
    public class DirectVolumePermissionsUnitTests : IClassFixture<CreateDropDatabaseFixture>
    {
        private readonly ApplicationDbContext _dbContext;
        private readonly IConfiguration _config;
        private readonly ILogger<DirectVolumePermissionsUnitTests> Log;

        public DirectVolumePermissionsUnitTests(CreateDropDatabaseFixture dbFixture, IConfiguration config, ILogger<DirectVolumePermissionsUnitTests> log = null)
        {
            _dbContext = dbFixture.DataContext;
            _config = config;
            Log = log;
        }

        [Fact]
        public void DatabaseComesPopulatedWithDefaults()
        {
            var resourceTypes = _dbContext.ResourceTypes.ToArray();
            foreach (var rt in resourceTypes)
            {
                Console.WriteLine(rt.ToString());
                Log?.LogInformation(rt.ToString());
            }

            var admins = _dbContext.GetUsersInAdminRole();
            Assert.Equal(1, admins.Count());
            Assert.True(admins.First().UserName == "admin");
        }

        [Fact]
        public async Task UserHasPermission()
        {
            ////////////////////////////////////////////////
            /// Create a user, give it access to a volume and check that it is reported as having access
            var testUserId = _dbContext.CreateUser("Test", "None");
            await _dbContext.SaveChangesAsync();

            var volumeResourceType = _dbContext.ResourceTypes.FirstOrDefault(t => t.Id == nameof(Volume));

            var allowedVolume = new Volume()
            {
                Name = "Allowed Volume",
                ParentID = null,
            };

            _dbContext.Volume.Add(allowedVolume);

            _dbContext.GrantedUserPermissions.Add(new GrantedUserPermission()
            {
                PermissionId = Special.Permissions.Volume.Review,
                Resource = allowedVolume,
                UserId = testUserId
            });

            await _dbContext.SaveChangesAsync();

            Assert.True(await _dbContext.IsUserPermitted(allowedVolume.Id, testUserId, Special.Permissions.Volume.Review));
            Assert.False(await _dbContext.IsUserPermitted(allowedVolume.Id, testUserId, Special.Permissions.Volume.Read));

            ////////////////////////////////////////////////
            //Add a second volume and ensure the user does not have permissions there.
            var deniedVolume = new Volume()
            {
                Name = "Denied Volume",
                ParentID = null,
            };
            _dbContext.Volume.Add(deniedVolume);
            await _dbContext.SaveChangesAsync();

            Assert.True(await _dbContext.IsUserPermitted(allowedVolume.Id, testUserId, Special.Permissions.Volume.Review));
            Assert.False(await _dbContext.IsUserPermitted(deniedVolume.Id, testUserId, Special.Permissions.Volume.Review));
            Assert.False(await _dbContext.IsUserPermitted(deniedVolume.Id, testUserId, Special.Permissions.Volume.Read));
            
            ////////////////////////////////////////////////
            //Add a second user, give it read on both volumes, and make sure it does not have extra permissions
            var testUserBId = _dbContext.CreateUser("TestB", "None");
            await _dbContext.SaveChangesAsync();

            _dbContext.GrantedUserPermissions.Add(new GrantedUserPermission()
            {
                PermissionId = Special.Permissions.Volume.Read,
                Resource = allowedVolume,
                UserId = testUserBId
            });

            _dbContext.GrantedUserPermissions.Add(new GrantedUserPermission()
            {
                PermissionId = Special.Permissions.Volume.Read,
                Resource = deniedVolume,
                UserId = testUserBId
            });
             
            await _dbContext.SaveChangesAsync();

            Assert.True(await _dbContext.IsUserPermitted(allowedVolume.Id, testUserId, Special.Permissions.Volume.Review));
            Assert.False(await _dbContext.IsUserPermitted(deniedVolume.Id, testUserId, Special.Permissions.Volume.Read));
            Assert.False(await _dbContext.IsUserPermitted(deniedVolume.Id, testUserId, Special.Permissions.Volume.Read));

            Assert.False(await _dbContext.IsUserPermitted(allowedVolume.Id, testUserBId, Special.Permissions.Volume.Review));
            Assert.True(await _dbContext.IsUserPermitted(deniedVolume.Id, testUserBId, Special.Permissions.Volume.Read));
            Assert.True(await _dbContext.IsUserPermitted(deniedVolume.Id, testUserBId, Special.Permissions.Volume.Read));

            //List the volumes each user has access to 
            var permittedReview = _dbContext.GetPermittedUsers(allowedVolume.Id, Special.Permissions.Volume.Review);

            Assert.Equal(1, permittedReview.Count());
            Assert.True(permittedReview.Any(p => p.Id == testUserId));

            //List the volumes each user has access to 
            var permittedAnnotate = _dbContext.GetPermittedUsers(allowedVolume.Id, Special.Permissions.Volume.Annotate);

            Assert.False(permittedAnnotate.Any()); 

            //List the volumes each user has access to 
            var permittedRead = _dbContext.GetPermittedUsers(allowedVolume.Id, Special.Permissions.Volume.Read);

            Assert.Equal(1, permittedReview.Count());
            Assert.True(permittedRead.Any(p => p.Id == testUserBId));

            //List the volumes UserB can read, which should be both
            var userBPermits = await _dbContext.UserResourcePermissionsByType(testUserBId, new string[] { nameof(Volume) });

            Assert.Equal(2,userBPermits.Count);
            Assert.True(userBPermits.ContainsKey(allowedVolume.Id));
            Assert.True(userBPermits.ContainsKey(deniedVolume.Id));
        }
    }

    public class OrgUnitPermissionsUnitTests : IClassFixture<CreateDropDatabaseFixture>
    {
        private readonly ApplicationDbContext _dbContext;
        private readonly IConfiguration _config;
        private readonly ILogger<OrgUnitPermissionsUnitTests> Log;

        public OrgUnitPermissionsUnitTests(CreateDropDatabaseFixture dbFixture, IConfiguration config, ILogger<OrgUnitPermissionsUnitTests> log = null)
        {
            _dbContext = dbFixture.DataContext;
            _config = config;
            Log = log;
        }
         
        [Fact]
        public async Task UserHasPermissionViaOrgUnit()
        {
            var testUserId = _dbContext.CreateUser("Test", "None");
            await _dbContext.SaveChangesAsync();

            var orgUnitResourceType = _dbContext.ResourceTypes.FirstOrDefault(t => t.Id == nameof(OrganizationalUnit));

            var orgUnit = new OrganizationalUnit()
            {
                Name = "Root OrgUnit",
                Parent = null,
            };

            _dbContext.OrgUnit.Add(orgUnit);
              
            var volumeResourceType = _dbContext.ResourceTypes.FirstOrDefault(t => t.Id == nameof(Volume));

            var allowedVolume = new Volume()
            {
                Name = "Allowed Volume",
                Parent = orgUnit,
            };

            _dbContext.Volume.Add(allowedVolume);

            _dbContext.GrantedUserPermissions.Add(new GrantedUserPermission()
            {
                PermissionId = Special.Permissions.Volume.Review,
                Resource = allowedVolume,
                UserId = testUserId
            });

            await _dbContext.SaveChangesAsync();

            Assert.True(await _dbContext.IsUserPermitted(allowedVolume.Id, testUserId, Special.Permissions.Volume.Review));
            Assert.False(await _dbContext.IsUserPermitted(allowedVolume.Id, testUserId, Special.Permissions.Volume.Read));

            //Add a second volume and ensure the user does not have permissions there.
            var deniedVolume = new Volume()
            {
                Name = "Denied Volume",
                Parent = orgUnit
            };
            _dbContext.Volume.Add(deniedVolume);
            await _dbContext.SaveChangesAsync();

            Assert.True(await _dbContext.IsUserPermitted(allowedVolume.Id, testUserId, Special.Permissions.Volume.Review));
            Assert.False(await _dbContext.IsUserPermitted(deniedVolume.Id, testUserId, Special.Permissions.Volume.Review));
            Assert.False(await _dbContext.IsUserPermitted(deniedVolume.Id, testUserId, Special.Permissions.Volume.Read));
        } 
    }

    public class GroupPermissionsUnitTests : IClassFixture<InMemoryIdentityFixture>
    {
        private readonly ApplicationDbContext _dbContext;

        public GroupPermissionsUnitTests(InMemoryIdentityFixture fixture)
        {
            _dbContext = fixture.DataContext;
        }

        [Fact]
        public async Task UserHasPermissionViaDirectGroupMembership()
        {
            var unique = Guid.NewGuid().ToString("N").Substring(0, 8);
            var testUserId = _dbContext.CreateUser($"grp-direct-{unique}", "None");
            await _dbContext.SaveChangesAsync();

            var group = new Group { Name = $"GroupA-{unique}", Parent = null };
            _dbContext.Group.Add(group);

            var allowedVolume = new Volume { Name = $"Allowed-{unique}", ParentID = null };
            _dbContext.Volume.Add(allowedVolume);
            await _dbContext.SaveChangesAsync();

            _dbContext.GrantedGroupPermissions.Add(new GrantedGroupPermission
            {
                PermissionId = Special.Permissions.Volume.Review,
                Resource = allowedVolume,
                GroupId = group.Id
            });
            await _dbContext.SaveChangesAsync();

            Assert.False(await _dbContext.IsUserPermitted(allowedVolume.Id, testUserId, Special.Permissions.Volume.Review));

            var groupAssignment = new UserToGroupAssignment
            {
                GroupId = group.Id,
                UserId = testUserId
            };
            _dbContext.UserToGroupAssignments.Add(groupAssignment);
            await _dbContext.SaveChangesAsync();

            Assert.True(await _dbContext.IsUserPermitted(allowedVolume.Id, testUserId, Special.Permissions.Volume.Review));
            Assert.False(await _dbContext.IsUserPermitted(allowedVolume.Id, testUserId, Special.Permissions.Volume.Read));

            _dbContext.UserToGroupAssignments.Remove(groupAssignment);
            await _dbContext.SaveChangesAsync();

            Assert.False(await _dbContext.IsUserPermitted(allowedVolume.Id, testUserId, Special.Permissions.Volume.Review));
        }

        [Fact]
        public async Task UserHasPermissionViaNestedGroupToGroupAssignment()
        {
            var unique = Guid.NewGuid().ToString("N").Substring(0, 8);
            var testUserId = _dbContext.CreateUser($"grp-nested-{unique}", "None");
            await _dbContext.SaveChangesAsync();

            var parentGroup = new Group { Name = $"Parent-{unique}", Parent = null };
            var childGroup = new Group { Name = $"Child-{unique}", Parent = null };
            _dbContext.Group.Add(parentGroup);
            _dbContext.Group.Add(childGroup);

            var allowedVolume = new Volume { Name = $"NestedVol-{unique}", ParentID = null };
            _dbContext.Volume.Add(allowedVolume);
            await _dbContext.SaveChangesAsync();

            // Permission is on the parent container group only.
            _dbContext.GrantedGroupPermissions.Add(new GrantedGroupPermission
            {
                PermissionId = Special.Permissions.Volume.Review,
                Resource = allowedVolume,
                GroupId = parentGroup.Id
            });

            // Child is a member of parent via GroupToGroupAssignment (not ParentID).
            _dbContext.GroupToGroupAssignments.Add(new GroupToGroupAssignment
            {
                ContainerGroupId = parentGroup.Id,
                MemberGroupId = childGroup.Id
            });

            // User is a member of the child only — not the parent.
            _dbContext.UserToGroupAssignments.Add(new UserToGroupAssignment
            {
                GroupId = childGroup.Id,
                UserId = testUserId
            });
            await _dbContext.SaveChangesAsync();

            Assert.True(await _dbContext.IsUserPermitted(allowedVolume.Id, testUserId, Special.Permissions.Volume.Review));
            Assert.False(await _dbContext.IsUserPermitted(allowedVolume.Id, testUserId, Special.Permissions.Volume.Read));

            var deniedVolume = new Volume { Name = $"DeniedNested-{unique}", ParentID = null };
            _dbContext.Volume.Add(deniedVolume);
            await _dbContext.SaveChangesAsync();

            Assert.False(await _dbContext.IsUserPermitted(deniedVolume.Id, testUserId, Special.Permissions.Volume.Review));
        }

        [Fact]
        public async Task UserDoesNotInheritPermissionFromOrgParentId()
        {
            var unique = Guid.NewGuid().ToString("N").Substring(0, 8);
            var testUserId = _dbContext.CreateUser($"grp-parentid-{unique}", "None");
            await _dbContext.SaveChangesAsync();

            var parentGroup = new Group { Name = $"OrgParent-{unique}", Parent = null };
            _dbContext.Group.Add(parentGroup);
            await _dbContext.SaveChangesAsync();

            // Child uses ParentID hierarchy only — no GroupToGroupAssignment.
            var childGroup = new Group { Name = $"OrgChild-{unique}", ParentID = parentGroup.Id };
            _dbContext.Group.Add(childGroup);

            var volume = new Volume { Name = $"ParentIdVol-{unique}", ParentID = null };
            _dbContext.Volume.Add(volume);
            await _dbContext.SaveChangesAsync();

            _dbContext.GrantedGroupPermissions.Add(new GrantedGroupPermission
            {
                PermissionId = Special.Permissions.Volume.Review,
                Resource = volume,
                GroupId = parentGroup.Id
            });

            _dbContext.UserToGroupAssignments.Add(new UserToGroupAssignment
            {
                GroupId = childGroup.Id,
                UserId = testUserId
            });
            await _dbContext.SaveChangesAsync();

            // ParentID alone must NOT confer parent group permissions.
            Assert.False(await _dbContext.IsUserPermitted(volume.Id, testUserId, Special.Permissions.Volume.Review));
        }

        [Fact]
        public async Task UserResourcePermissionsByType_IncludesGroupGrants()
        {
            var unique = Guid.NewGuid().ToString("N").Substring(0, 8);
            var testUserId = _dbContext.CreateUser($"grp-bytype-{unique}", "None");
            await _dbContext.SaveChangesAsync();

            var group = new Group { Name = $"ByType-{unique}" };
            var volume = new Volume { Name = $"ByTypeVol-{unique}" };
            _dbContext.Group.Add(group);
            _dbContext.Volume.Add(volume);
            await _dbContext.SaveChangesAsync();

            _dbContext.GrantedGroupPermissions.Add(new GrantedGroupPermission
            {
                PermissionId = Special.Permissions.Volume.Annotate,
                Resource = volume,
                GroupId = group.Id
            });
            _dbContext.UserToGroupAssignments.Add(new UserToGroupAssignment
            {
                GroupId = group.Id,
                UserId = testUserId
            });
            await _dbContext.SaveChangesAsync();

            var byType = await _dbContext.UserResourcePermissionsByType(testUserId, new[] { nameof(Volume) });
            Assert.True(byType.ContainsKey(volume.Id));
            Assert.Contains(Special.Permissions.Volume.Annotate, byType[volume.Id]);
        }
    }
}
