using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Identity.EntityFrameworkCore;
using Microsoft.AspNetCore.Identity;
using System.ComponentModel.DataAnnotations;
using Microsoft.EntityFrameworkCore;
using IdentityServer.Models;
using IdentityServer.Models.UserViewModels;
using Microsoft.Extensions.Logging;

namespace IdentityServer.Data
{
    public class ApplicationDbContext : IdentityDbContext<ApplicationUser>
    {
        ILogger Log;

        public ApplicationDbContext(DbContextOptions<ApplicationDbContext> options, ILogger log=null)
            : base(options)
        {
            Log = log;
        }
        
        protected override void OnModelCreating(ModelBuilder builder)
        {
            
            if (System.Diagnostics.Debugger.IsAttached == false)
            {
                System.Diagnostics.Debugger.Launch();
            }

            base.OnModelCreating(builder);
            // Customize the ASP.NET Identity model and override the defaults if needed.
            // For example, you can rename the ASP.NET Identity table names and more.
            // Add your customizations after calling base.OnModelCreating(builder);
             
            builder.Entity<ResourceTypePermission>().HasOne(rtp => rtp.ResourceType).WithMany(nameof(Models.ResourceType.Permissions)).HasForeignKey(rtp => rtp.ResourceTypeId);
             
            builder.Entity<UserToGroupAssignment>().HasKey(oa => new { oa.GroupId, oa.UserId});
            builder.Entity<UserToGroupAssignment>().HasOne(oa => oa.Group).WithMany(nameof(Models.Group.MemberUsers)).HasForeignKey(oa => oa.GroupId);
            builder.Entity<UserToGroupAssignment>().HasOne(oa => oa.User).WithMany(nameof(Models.ApplicationUser.GroupAssignments)).HasForeignKey(oa => oa.UserId);

            //builder.Entity<Group>().HasMany(typeof(UserToGroupAssignment), "MemberUsers").WithMany(nameof(UserToGroupAssignment));

            builder.Entity<GroupToGroupAssignment>().HasKey(gga => new { gga.ContainerGroupId, gga.MemberGroupId });
            builder.Entity<GroupToGroupAssignment>().HasOne(gga => gga.Container).WithMany(nameof(Models.Group.MemberGroups)).HasForeignKey(gga => gga.ContainerGroupId);
            builder.Entity<GroupToGroupAssignment>().HasOne(gga => gga.Member).WithMany(nameof(Models.Group.MemberOfGroups)).HasForeignKey(gga => gga.MemberGroupId);

            builder.Entity<ResourceTypePermission>().HasKey(c => new { c.ResourceTypeId, c.PermissionId });

            builder.Entity<GrantedUserPermission>().Property(gup => gup.ResourceId).HasColumnName(nameof(GrantedPermissionBase.ResourceId));
            builder.Entity<GrantedGroupPermission>().Property(gup => gup.ResourceId).HasColumnName(nameof(GrantedPermissionBase.ResourceId));
            builder.Entity<GrantedUserPermission>().Property(gup => gup.PermissionId).HasColumnName(nameof(GrantedPermissionBase.PermissionId));
            builder.Entity<GrantedGroupPermission>().Property(gup => gup.PermissionId).HasColumnName(nameof(GrantedPermissionBase.PermissionId));

            builder.Entity<GrantedUserPermission>()
                .HasDiscriminator<string>("GranteeType") 
                .HasValue<GrantedUserPermission>("User");

            builder.Entity<GrantedGroupPermission>()
                .HasDiscriminator<string>("GranteeType")
                .HasValue<GrantedGroupPermission>("Group");
            
            builder.Entity<GrantedUserPermission>().HasKey(c => new { c.ResourceId, c.PermissionId, c.UserId }); 
            builder.Entity<GrantedGroupPermission>().HasKey(c => new { c.ResourceId, c.PermissionId, c.GroupId});

           // builder.Entity<GrantedPermissionBase>().HasOne(oa => oa.Resource).WithMany(nameof(Models.Group.PermissionsHeld)).HasForeignKey(oa => oa.GroupId).OnDelete(DeleteBehavior.ClientCascade);

            builder.Entity<GrantedGroupPermission>().HasOne(oa => oa.PermittedGroup).WithMany(nameof(Models.Group.PermissionsHeld)).HasForeignKey(oa => oa.GroupId).OnDelete(DeleteBehavior.ClientCascade);
            builder.Entity<GrantedGroupPermission>().HasOne(oa => oa.Resource).WithMany(nameof(Models.Resource.GroupsWithPermissions)).HasForeignKey(oa => oa.ResourceId);

            builder.Entity<GrantedUserPermission>().HasOne(oa => oa.PermittedUser).WithMany(nameof(Models.ApplicationUser.PermissionsHeld)).HasForeignKey(oa => oa.UserId).OnDelete(DeleteBehavior.ClientCascade);
            builder.Entity<GrantedUserPermission>().HasOne(oa => oa.Resource).WithMany(nameof(Models.Resource.UsersWithPermissions)).HasForeignKey(oa => oa.ResourceId);

            builder.Entity<Resource>().HasOne(oa => oa.Parent).WithMany(nameof(Models.Group.Children)).HasForeignKey(oa => oa.ParentID);

            builder.Entity<Resource>()
                .HasDiscriminator<string>(nameof(Models.Resource.TypeId))
                .HasValue<Resource>(nameof(Models.Resource))
                .HasValue<Group>(nameof(Models.Group));

            builder.Entity<Resource>().ToTable(nameof(Resource));
            builder.Entity<Group>().ToTable(nameof(Group)).HasBaseType(nameof(Resource));

            builder.Entity<ApplicationRole>().HasData(
                new ApplicationRole() { Name = Config.AdminRoleName, NormalizedName = Config.AdminRoleName });

            builder.Entity<ResourceType>().HasData(new ResourceType() { Id = Config.GroupResourceType });
            builder.Entity<ResourceTypePermission>().HasData(new ResourceTypePermission() {  ResourceTypeId= Config.GroupResourceType, PermissionId=Config.GroupAccessManagerPermission });

            builder.Entity<ResourceType>().HasData(new ResourceType() { Id = Config.VolumeResourceType });
            builder.Entity<ResourceTypePermission>().HasData(new ResourceTypePermission() { ResourceTypeId = Config.VolumeResourceType, PermissionId = "Read" });
            builder.Entity<ResourceTypePermission>().HasData(new ResourceTypePermission() { ResourceTypeId = Config.VolumeResourceType, PermissionId = "Annotate" });
            builder.Entity<ResourceTypePermission>().HasData(new ResourceTypePermission() { ResourceTypeId = Config.VolumeResourceType, PermissionId = "Review" });
            builder.Entity<ResourceTypePermission>().HasData(
                new ResourceTypePermission() { ResourceTypeId = Config.GroupResourceType, PermissionId = Config.GroupAccessManagerPermission, Description = "Add/Remove group members" });

            builder.Entity<Group>().HasData( new Group[] {
                new Group { Id = Config.EveryoneGroupId, Name = Config.EveryoneGroupName}
            }
            );

        }

        private void LogShadowProperties()
        {
            DbContextOptionsBuilder builderShadow = new DbContextOptionsBuilder<ApplicationDbContext>()
                .UseSqlServer();
            var optionsShadow = (DbContextOptions<ApplicationDbContext>)builderShadow.Options;
            using ApplicationDbContext contextShadow = new ApplicationDbContext(optionsShadow);
            EFModelAnalysis efModelShadow = new EFModelAnalysis(contextShadow);
            var shadowPropertiesShadow = efModelShadow.ShadowProperties();
            foreach (var shadow in shadowPropertiesShadow)
                Log?.LogInformation(shadow); //List the shadow properties
            var alternateKeysShadow = efModelShadow.AlternateKeyRelationships();
            foreach (var ak in alternateKeysShadow)
                Log?.LogInformation(ak); //List relationships using alternate key`
        }

        public DbSet<IdentityServer.Models.ApplicationRole> ApplicationRole { get; set; }

        public DbSet<IdentityServer.Models.ApplicationUser> ApplicationUser { get; set; }

        public DbSet<Resource> Resource { get; set; }

        public DbSet<Group> Group { get; set; }

        public DbSet<UserToGroupAssignment> UserToGroupAssignments { get; set; }

        public DbSet<GroupToGroupAssignment> GroupToGroupAssignments { get; set; }
         
        public DbSet<ResourceTypePermission> Permissions { get; set; }

        public DbSet<ResourceType> ResourceTypes { get; set; }

        public DbSet<GrantedGroupPermission> GrantedGroupPermissions { get; set; }

        public DbSet<GrantedUserPermission> GrantedUserPermissions { get; set; }
    }
}
