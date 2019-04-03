using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Identity.EntityFrameworkCore; 
using System.ComponentModel.DataAnnotations;
using Microsoft.EntityFrameworkCore;
using IdentityServer.Models;
using IdentityServer.Models.UserViewModels;

namespace IdentityServer.Data
{
    public class ApplicationDbContext : IdentityDbContext<ApplicationUser>
    {
        public ApplicationDbContext(DbContextOptions<ApplicationDbContext> options)
            : base(options)
        {
        }

        protected override void OnModelCreating(ModelBuilder builder)
        {
            base.OnModelCreating(builder);
            // Customize the ASP.NET Identity model and override the defaults if needed.
            // For example, you can rename the ASP.NET Identity table names and more.
            // Add your customizations after calling base.OnModelCreating(builder);

            builder.Entity<GroupAssignment>().HasKey(oa => new { oa.GroupId, oa.UserId});
            builder.Entity<GroupAssignment>().HasOne(oa => oa.Group).WithMany("GroupAssignments").HasForeignKey(oa => oa.GroupId);
            builder.Entity<GroupAssignment>().HasOne(oa => oa.User).WithMany("GroupAssignments").HasForeignKey(oa => oa.UserId);
        }

        public DbSet<IdentityServer.Models.ApplicationRole> ApplicationRole { get; set; }

        public DbSet<IdentityServer.Models.ApplicationUser> ApplicationUser { get; set; }

        public DbSet<Group> Group { get; set; }

        public DbSet<GroupAssignment> GroupAssignments { get; set; }

        public DbSet<IdentityServer.Models.UserViewModels.UserSelectedViewModel> UserSelectedViewModel { get; set; }
    }
}
