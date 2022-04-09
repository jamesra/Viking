using System;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace Viking.DataModel.Annotation.Tests
{
    /// <summary>
    /// This interface represents a class that provides an empty database for tests. 
    /// </summary>
    /// <typeparam name="T"></typeparam>
    public interface IContextBuilder<out T> where T : DbContext
    {
        /// <summary>
        /// Returns the context for the database that was created
        /// </summary>
        T DataContext { get; }

        /// <summary>
        /// The random name generated for the database
        /// </summary>
        string DatabaseName { get; }
    }

    public class ContextBuilderOptions<T> where T : DbContext
    {
        public string ConnectionStringName
        {
            get;
            set;
        } = nameof(T);
    }

    /// <summary>
    /// ContextBuilder<T> creates a new database with a random name using the provided context type T.
    /// This can then be fed into fixtures with dependency injection.  Fixtures then populate the database with any starting data necessary
    /// for specific tests or test collections.  Configure the ContextBuilder in the Startup.cs file.
    ///
    /// Fixtures should be sure to dispose of the context builder to remove the database after the test.
    /// </summary>
    /// <typeparam name="T"></typeparam>
    public class ContextBuilder<T> : IContextBuilder<T>, IDisposable where T : DbContext
    { 
        private bool disposedValue;
        public T DataContext { get; }

        private readonly ContextBuilderOptions<AnnotationContext> _options;
        public string DatabaseName { get; }

        public ContextBuilder(IOptions<ContextBuilderOptions<AnnotationContext>> options,
            IConfiguration configuration, IServiceProvider provider, ILogger log = null)
        {
            _options = options.Value;
            var connStringName = _options.ConnectionStringName;
            var connStringTemplate = configuration.GetConnectionString(connStringName);

            DatabaseName = Shared.RandomLetters(6);
            var connString = string.Format(connStringTemplate, DatabaseName);

            DbContextOptionsBuilder<AnnotationContext> builder = new DbContextOptionsBuilder<AnnotationContext>();
            builder = builder.UseSqlServer(connString, config => config.UseNetTopologySuite()).EnableDetailedErrors().EnableSensitiveDataLogging();
            var contextOptions = builder.Options;
            DataContext = ActivatorUtilities.CreateInstance<T>(provider, contextOptions);
            DataContext.Database.Migrate();
            //DataContext.Database.EnsureCreated();
            
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

        void IDisposable.Dispose()
        {
            // Do not change this code. Put cleanup code in 'Dispose(bool disposing)' method
            Dispose(disposing: true);
            GC.SuppressFinalize(this);
        }

    }
}