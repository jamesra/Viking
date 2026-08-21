using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Viking.Identity.Data;
using Viking.Identity.Models;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Xunit;

namespace TestIdentityModel
{
    /// <summary>
    /// The database fixture ensures that each test collection has a unique fresh instance of the database to
    /// run against.
    /// </summary>
    public class CreateDropDatabaseFixture : IDisposable
    {
        private bool disposedValue;
        public readonly ApplicationDbContext DataContext;
        //private readonly IConfiguration Config;
        public readonly string DatabaseName;
        private static readonly Random rd = new Random();

        internal static string RandomLetters(int stringLength)
        {
            const string allowedChars = "ABCDEFGHJKLMNOPQRSTUVWXYZabcdefghijkmnopqrstuvwxyz0123456789";
            char[] chars = new char[stringLength];

            for (int i = 0; i < stringLength; i++)
            {
                chars[i] = allowedChars[rd.Next(0, allowedChars.Length)];
            }

            return new string(chars);
        }

        /// <summary>
        /// Builds a Database={0} SQL template. Prefer SQL_SERVER_* env vars (or user secrets mapped
        /// to DataContext:ConnectionStrings:IdentityConnection) so passwords stay out of git.
        /// SQL_SERVER_PASSWORD selects SQL auth; otherwise the json Integrated Security template is used.
        /// </summary>
        private static string BuildSqlConnectionTemplate(IConfiguration configuration)
        {
            var fromConfig = configuration.GetRequiredSection("DataContext").GetConnectionString("IdentityConnection");
            var password = Environment.GetEnvironmentVariable("SQL_SERVER_PASSWORD");
            if (string.IsNullOrEmpty(password))
            {
                return fromConfig;
            }

            var host = Environment.GetEnvironmentVariable("SQL_SERVER_HOST") ?? "localhost";
            var port = Environment.GetEnvironmentVariable("SQL_SERVER_PORT") ?? "1433";
            var user = Environment.GetEnvironmentVariable("SQL_SERVER_USER") ?? "sa";
            return $"Server={host},{port};Database={{0}};Trusted_Connection=False;User ID={user};Password={password};MultipleActiveResultSets=true;TrustServerCertificate=True";
        }

        public CreateDropDatabaseFixture(IConfiguration configuration, IPasswordHasher<ApplicationUser> passwordHasher, ILogger<CreateDropDatabaseFixture> log = null)
        {
            var connStringTemplate = BuildSqlConnectionTemplate(configuration);

            DatabaseName = "IdentityTest" + RandomLetters(8);
            var connString = string.Format(connStringTemplate, DatabaseName);

            DbContextOptionsBuilder<ApplicationDbContext> builder = new DbContextOptionsBuilder<ApplicationDbContext>();
            builder = builder.UseSqlServer(connString).EnableDetailedErrors().EnableSensitiveDataLogging();
            DataContext = new ApplicationDbContext(builder.Options, passwordHasher, log);
             
            DataContext.Database.EnsureCreated();
        }

        protected virtual void Dispose(bool disposing)
        {
            if (!disposedValue)
            {
                if (disposing)
                {
                    // TODO: dispose managed state (managed objects)
                }

                DataContext?.Database?.EnsureDeleted();
                disposedValue = true;
            }
        }

        // // TODO: override finalizer only if 'Dispose(bool disposing)' has code to free unmanaged resources
        // ~CreateDropDatabaseFixture()
        // {
        //     // Do not change this code. Put cleanup code in 'Dispose(bool disposing)' method
        //     Dispose(disposing: false);
        // }

        void IDisposable.Dispose()
        {
            // Do not change this code. Put cleanup code in 'Dispose(bool disposing)' method
            Dispose(disposing: true);
            GC.SuppressFinalize(this);
        }
    }
}
