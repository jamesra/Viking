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

            builder.Entity<OrganizationAssignment>().HasKey(oa => new { oa.OrganizationId, oa.UserId});
            builder.Entity<OrganizationAssignment>().HasOne(oa => oa.Organization).WithMany("OrganizationAssignments").HasForeignKey(oa => oa.OrganizationId);
            builder.Entity<OrganizationAssignment>().HasOne(oa => oa.User).WithMany("OrganizationAssignments").HasForeignKey(oa => oa.UserId);
        }

        public DbSet<IdentityServer.Models.ApplicationRole> ApplicationRole { get; set; }

        public DbSet<IdentityServer.Models.ApplicationUser> ApplicationUser { get; set; }

        public DbSet<Organization> Organization { get; set; }

        public DbSet<OrganizationAssignment> OrganizationAssignments { get; set; }

        public DbSet<IdentityServer.Models.UserViewModels.UserSelectedViewModel> UserSelectedViewModel { get; set; }
    }
}
