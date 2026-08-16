using System;
using System.Linq;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Viking.Identity.Data;
using Viking.Identity.Models;

namespace TestIdentityModel
{
    /// <summary>
    /// In-memory DB fixture so Identity permission/onboarding tests run without SQL Server.
    /// </summary>
    public class InMemoryIdentityFixture : IDisposable
    {
        public ApplicationDbContext DataContext { get; }
        public string AdminUserId { get; }

        private readonly PasswordHasher<ApplicationUser> _hasher = new PasswordHasher<ApplicationUser>();

        public InMemoryIdentityFixture()
        {
            var options = new DbContextOptionsBuilder<ApplicationDbContext>()
                .UseInMemoryDatabase("IdentityTests-" + Guid.NewGuid().ToString("N"))
                .Options;

            DataContext = new ApplicationDbContext(options, _hasher);
            DataContext.Database.EnsureCreated();

            AdminUserId = DataContext.CreateUser("admin", "AdminPass1!", "admin@example.com");
            DataContext.UserRoles.Add(new IdentityUserRole<string>
            {
                UserId = AdminUserId,
                RoleId = Special.Roles.AdminId
            });
            DataContext.SaveChanges();
        }

        /// <summary>
        /// Creates a non-admin user and removes the automatic Everyone assignment
        /// so tests can verify virtual membership independently.
        /// </summary>
        public string CreateUserWithoutEveryoneAssignment(string username, string password = "None", string email = null)
        {
            var userId = DataContext.CreateUser(username, password, email ?? $"{username}@example.com");
            DataContext.SaveChanges();

            var everyoneRows = DataContext.UserToGroupAssignments
                .Where(a => a.UserId == userId && a.GroupId == Special.Groups.Everyone.Id)
                .ToList();
            DataContext.UserToGroupAssignments.RemoveRange(everyoneRows);
            DataContext.SaveChanges();
            return userId;
        }

        public void Dispose()
        {
            DataContext?.Dispose();
        }
    }
}
