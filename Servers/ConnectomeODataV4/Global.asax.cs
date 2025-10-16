using System;
using System.Web.Http;
using Unity;
using Unity.AspNet.WebApi;
using Unity.Lifetime;
using ConnectomeDataModel;
using Microsoft.Extensions.Logging;

namespace ConnectomeODataV4
{
    public class WebApiApplication : System.Web.HttpApplication
    {
        private static IUnityContainer _container;
        private static ILoggerFactory _loggerFactory;

        protected void Application_Start()
        {
            //SqlServerTypes.Utilities.LoadNativeAssemblies(Server.MapPath("~/bin"));
            ConnectomeDataModel.Configuration.LoadNativeAssemblies(Server.MapPath("~/bin"));
            
            // Configure Dependency Injection
            _container = new UnityContainer();
            ConfigureDependencyInjection(_container);
            
            // Set up Web API configuration
            GlobalConfiguration.Configure(config =>
            {
                // Set Unity as the dependency resolver
                config.DependencyResolver = new UnityDependencyResolver(_container);
                
                // Register Web API routes and OData model
                WebApiConfig.Register(config);
            });
            
            GlobalConfiguration.DefaultServer.Configuration.EnsureInitialized();
        }

        /// <summary>
        /// Configure dependency injection for the application
        /// </summary>
        private void ConfigureDependencyInjection(IUnityContainer container)
        {
            // Register DbContext with per-request lifetime
            // This ensures proper disposal after each request
            container.RegisterType<ConnectomeEntities>(new HierarchicalLifetimeManager());

            // Register logging
            _loggerFactory = LoggerFactory.Create(builder =>
            {
                builder.AddConsole();
                builder.SetMinimumLevel(LogLevel.Information);
            });
            
            container.RegisterInstance<ILoggerFactory>(_loggerFactory);
            container.RegisterType(typeof(ILogger<>), typeof(Logger<>));
        }

        protected void Application_End()
        {
            // Clean up resources on shutdown
            _loggerFactory?.Dispose();
            _container?.Dispose();
        }
    }
}
