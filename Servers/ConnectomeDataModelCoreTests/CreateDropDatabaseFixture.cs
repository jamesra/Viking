using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks; 
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Viking.DataModel.Annotation;
using Xunit;

namespace Viking.DataModel.Annotation.Tests
{
    /// <summary>
    /// The database fixture ensures that each test collection has a unique fresh instance of the database to
    /// run against.
    /// </summary>
    public class CreateDropDatabaseFixture : IDisposable
    {
        private bool disposedValue;
        public readonly AnnotationContext DataContext;
        private readonly IConfiguration Config;
        public readonly string DatabaseName;
        private static readonly Random rd = new Random();

        internal static string RandomLetters(int stringLength)
        {
            const string allowedChars = "ABCDEFGHJKLMNOPQRSTUVWXYZabcdefghijkmnopqrstuvwxyz0123456789!@$?_-";
            char[] chars = new char[stringLength];

            for (int i = 0; i < stringLength; i++)
            {
                chars[i] = allowedChars[rd.Next(0, allowedChars.Length)];
            }

            return new string(chars);
        }

        public CreateDropDatabaseFixture(IConfiguration configuration, ILogger log = null)
        {
            var connStringTemplate = configuration.GetConnectionString("AnnotationConnection");

            DatabaseName = RandomLetters(6);
            var connString = string.Format(connStringTemplate, DatabaseName);

            DbContextOptionsBuilder<AnnotationContext> builder = new DbContextOptionsBuilder<AnnotationContext>();
            builder = builder.UseSqlServer(connString).EnableDetailedErrors().EnableSensitiveDataLogging();
            DataContext = new AnnotationContext(builder.Options, log);
             
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
