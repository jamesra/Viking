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

            var volumeResourceType = _dbContext.ResourceTypes.FirstOrDefault(t => t.Id == nameof(AnnotationContext));

            var allowedVolume = new AnnotationContext()
            {
                Name = "Allowed Volume",
                ParentID = null,
            };

            _dbContext.AnnotationContexts.Add(allowedVolume);

            _dbContext.GrantedUserPermissions.Add(new GrantedUserPermission()
            {
                PermissionId = Special.Permissions.AnnotationContext.Review,
                Resource = allowedVolume,
                UserId = testUserId
            });

            await _dbContext.SaveChangesAsync();

            Assert.True(await _dbContext.IsUserPermitted(allowedVolume.Id, testUserId, Special.Permissions.AnnotationContext.Review));
            Assert.False(await _dbContext.IsUserPermitted(allowedVolume.Id, testUserId, Special.Permissions.AnnotationContext.Read));

            ////////////////////////////////////////////////
            //Add a second volume and ensure the user does not have permissions there.
            var deniedVolume = new AnnotationContext()
            {
                Name = "Denied Volume",
                ParentID = null,
            };
            _dbContext.AnnotationContexts.Add(deniedVolume);
            await _dbContext.SaveChangesAsync();

            Assert.True(await _dbContext.IsUserPermitted(allowedVolume.Id, testUserId, Special.Permissions.AnnotationContext.Review));
            Assert.False(await _dbContext.IsUserPermitted(deniedVolume.Id, testUserId, Special.Permissions.AnnotationContext.Review));
            Assert.False(await _dbContext.IsUserPermitted(deniedVolume.Id, testUserId, Special.Permissions.AnnotationContext.Read));
            
            ////////////////////////////////////////////////
            //Add a second user, give it read on both volumes, and make sure it does not have extra permissions
            var testUserBId = _dbContext.CreateUser("TestB", "None");
            await _dbContext.SaveChangesAsync();

            _dbContext.GrantedUserPermissions.Add(new GrantedUserPermission()
            {
                PermissionId = Special.Permissions.AnnotationContext.Read,
                Resource = allowedVolume,
                UserId = testUserBId
            });

            _dbContext.GrantedUserPermissions.Add(new GrantedUserPermission()
            {
                PermissionId = Special.Permissions.AnnotationContext.Read,
                Resource = deniedVolume,
                UserId = testUserBId
            });
             
            await _dbContext.SaveChangesAsync();

            Assert.True(await _dbContext.IsUserPermitted(allowedVolume.Id, testUserId, Special.Permissions.AnnotationContext.Review));
            Assert.False(await _dbContext.IsUserPermitted(deniedVolume.Id, testUserId, Special.Permissions.AnnotationContext.Read));
            Assert.False(await _dbContext.IsUserPermitted(deniedVolume.Id, testUserId, Special.Permissions.AnnotationContext.Read));

            Assert.False(await _dbContext.IsUserPermitted(allowedVolume.Id, testUserBId, Special.Permissions.AnnotationContext.Review));
            Assert.True(await _dbContext.IsUserPermitted(deniedVolume.Id, testUserBId, Special.Permissions.AnnotationContext.Read));
            Assert.True(await _dbContext.IsUserPermitted(deniedVolume.Id, testUserBId, Special.Permissions.AnnotationContext.Read));

            //List the volumes each user has access to 
            var permittedReview = _dbContext.GetPermittedUsers(allowedVolume.Id, Special.Permissions.AnnotationContext.Review);

            Assert.Equal(1, permittedReview.Count());
            Assert.True(permittedReview.Any(p => p.Id == testUserId));

            //List the volumes each user has access to 
            var permittedAnnotate = _dbContext.GetPermittedUsers(allowedVolume.Id, Special.Permissions.AnnotationContext.Annotate);

            Assert.False(permittedAnnotate.Any()); 

            //List the volumes each user has access to 
            var permittedRead = _dbContext.GetPermittedUsers(allowedVolume.Id, Special.Permissions.AnnotationContext.Read);

            Assert.Equal(1, permittedReview.Count());
            Assert.True(permittedRead.Any(p => p.Id == testUserBId));

            //List the volumes UserB can read, which should be both
            var userBPermits = await _dbContext.UserResourcePermissionsByType(testUserBId, new string[] { nameof(AnnotationContext) });

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
              
            var volumeResourceType = _dbContext.ResourceTypes.FirstOrDefault(t => t.Id == nameof(AnnotationContext));

            var allowedVolume = new AnnotationContext()
            {
                Name = "Allowed Volume",
                Parent = orgUnit,
            };

            _dbContext.AnnotationContexts.Add(allowedVolume);

            _dbContext.GrantedUserPermissions.Add(new GrantedUserPermission()
            {
                PermissionId = Special.Permissions.AnnotationContext.Review,
                Resource = allowedVolume,
                UserId = testUserId
            });

            await _dbContext.SaveChangesAsync();

            Assert.True(await _dbContext.IsUserPermitted(allowedVolume.Id, testUserId, Special.Permissions.AnnotationContext.Review));
            Assert.False(await _dbContext.IsUserPermitted(allowedVolume.Id, testUserId, Special.Permissions.AnnotationContext.Read));

            //Add a second volume and ensure the user does not have permissions there.
            var deniedVolume = new AnnotationContext()
            {
                Name = "Denied Volume",
                Parent = orgUnit
            };
            _dbContext.AnnotationContexts.Add(deniedVolume);
            await _dbContext.SaveChangesAsync();

            Assert.True(await _dbContext.IsUserPermitted(allowedVolume.Id, testUserId, Special.Permissions.AnnotationContext.Review));
            Assert.False(await _dbContext.IsUserPermitted(deniedVolume.Id, testUserId, Special.Permissions.AnnotationContext.Review));
            Assert.False(await _dbContext.IsUserPermitted(deniedVolume.Id, testUserId, Special.Permissions.AnnotationContext.Read));
        } 
    }

    public class GroupPermissionsUnitTests : IClassFixture<CreateDropDatabaseFixture>
    {
        private readonly ApplicationDbContext _dbContext;
        private readonly IConfiguration _config;
        private readonly ILogger<GroupPermissionsUnitTests> Log;

        public GroupPermissionsUnitTests(CreateDropDatabaseFixture dbFixture, IConfiguration config, ILogger<GroupPermissionsUnitTests> log = null)
        {
            _dbContext = dbFixture.DataContext;
            _config = config;
            Log = log;
        }
         
        [Fact]
        public async Task UserHasPermissionViaGroup()
        {
            var testUserId = _dbContext.CreateUser("Test", "None");
            await _dbContext.SaveChangesAsync();

            var orgUnitResourceType = _dbContext.ResourceTypes.FirstOrDefault(t => t.Id == nameof(OrganizationalUnit));

            var group = new Group()
            {
                Name = "A",
                Parent = null,
            };

            _dbContext.Group.Add(group);
              
            var volumeResourceType = _dbContext.ResourceTypes.FirstOrDefault(t => t.Id == nameof(AnnotationContext));

            var allowedVolume = new AnnotationContext()
            {
                Name = "Allowed Volume",
                ParentID = null,
            };

            _dbContext.AnnotationContexts.Add(allowedVolume);
            await _dbContext.SaveChangesAsync();

            var groupPermission = _dbContext.GrantedGroupPermissions.Add(new GrantedGroupPermission()
            {
                PermissionId = Special.Permissions.AnnotationContext.Review,
                Resource = allowedVolume,
                GroupId = group.Id
            });

            await _dbContext.SaveChangesAsync();

            Assert.False(await _dbContext.IsUserPermitted(allowedVolume.Id, testUserId, Special.Permissions.AnnotationContext.Review));
            Assert.False(await _dbContext.IsUserPermitted(allowedVolume.Id, testUserId, Special.Permissions.AnnotationContext.Read));

            var groupAssignment = new UserToGroupAssignment()
            {
                GroupId = group.Id,
                UserId = testUserId
            };
            
            _dbContext.UserToGroupAssignments.Add(groupAssignment);

            await _dbContext.SaveChangesAsync(); 

            Assert.True(await _dbContext.IsUserPermitted(allowedVolume.Id, testUserId, Special.Permissions.AnnotationContext.Review));
            Assert.False(await _dbContext.IsUserPermitted(allowedVolume.Id, testUserId, Special.Permissions.AnnotationContext.Read));

            _dbContext.UserToGroupAssignments.Remove(groupAssignment);
            await _dbContext.SaveChangesAsync(); 
             
            Assert.False(await _dbContext.IsUserPermitted(allowedVolume.Id, testUserId, Special.Permissions.AnnotationContext.Review));
            Assert.False(await _dbContext.IsUserPermitted(allowedVolume.Id, testUserId, Special.Permissions.AnnotationContext.Read));
             
            _dbContext.Add(groupAssignment);

            var childGroup = new Group()
            {
                Name = "Child",
                ParentID = group.Id
            };
            
            _dbContext.Group.Add(childGroup);
             
            var groupAssignmentToChild = new UserToGroupAssignment()
            {
                Group = childGroup,
                UserId = testUserId
            };
            
            _dbContext.UserToGroupAssignments.Add(groupAssignmentToChild);

            await _dbContext.SaveChangesAsync();

            //See if we have access to the volume because we are members of a parent group that has access
            Assert.True(await _dbContext.IsUserPermitted(allowedVolume.Id, testUserId, Special.Permissions.AnnotationContext.Review));
            Assert.False(await _dbContext.IsUserPermitted(allowedVolume.Id, testUserId, Special.Permissions.AnnotationContext.Read));

            //Add a second volume and ensure the user does not have permissions there.
            var deniedVolume = new AnnotationContext()
            {
                Name = "Denied Volume",
                ParentID = null
            };
            _dbContext.AnnotationContexts.Add(deniedVolume);
            await _dbContext.SaveChangesAsync();

            Assert.True(await _dbContext.IsUserPermitted(allowedVolume.Id, testUserId, Special.Permissions.AnnotationContext.Review));
            Assert.False(await _dbContext.IsUserPermitted(deniedVolume.Id, testUserId, Special.Permissions.AnnotationContext.Review));
            Assert.False(await _dbContext.IsUserPermitted(deniedVolume.Id, testUserId, Special.Permissions.AnnotationContext.Read));
        } 
    }
}
