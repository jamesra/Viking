using ConnectomeDataModel;
using Microsoft.Extensions.Logging;
using System;
using System.Data.Entity;
using System.Web.Http;

namespace ConnectomeODataV4.Controllers
{
    /// <summary>
    /// Health check controller for monitoring application and database connectivity
    /// </summary>
    /// <remarks>
    /// Constructor with dependency injection
    /// </remarks>
    [RoutePrefix("health")]
    public class HealthController(ConnectomeEntities db, ILogger<HealthController> logger) : ApiController
    {
        private readonly ConnectomeEntities _db = db ?? throw new ArgumentNullException(nameof(db));
        private readonly ILogger<HealthController> _logger = logger ?? throw new ArgumentNullException(nameof(logger));

        /// <summary>
        /// Basic health check endpoint
        /// </summary>
        /// <returns>Health status</returns>
        [HttpGet]
        [Route("")]
        public IHttpActionResult Get()
        {
            try
            {
                _logger.LogInformation("Health check requested");

                return Ok(new
                {
                    status = "healthy",
                    timestamp = DateTime.UtcNow,
                    application = "ConnectomeODataV4",
                    version = System.Reflection.Assembly.GetExecutingAssembly().GetName().Version?.ToString()
                });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Health check failed");
                return InternalServerError(ex);
            }
        }

        /// <summary>
        /// Database connectivity health check
        /// </summary>
        /// <returns>Database health status</returns>
        [HttpGet]
        [Route("database")]
        public IHttpActionResult GetDatabaseHealth()
        {
            try
            {
                _logger.LogInformation("Database health check requested");

                // Open connection to test database connectivity
                var connection = _db.Database.Connection;
                connection.Open();
                var serverVersion = connection.ServerVersion;
                connection.Close();

                _logger.LogInformation("Database connection successful");

                return Ok(new
                {
                    status = "healthy",
                    timestamp = DateTime.UtcNow,
                    database = new
                    {
                        connected = true,
                        serverVersion = serverVersion,
                        connectionString = MaskConnectionString(connection.ConnectionString)
                    }
                });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Database health check failed");

                return Ok(new
                {
                    status = "unhealthy",
                    timestamp = DateTime.UtcNow,
                    database = new
                    {
                        connected = false,
                        error = ex.Message
                    }
                });
            }
        }

        /// <summary>
        /// Detailed health check with database query test
        /// </summary>
        /// <returns>Detailed health status</returns>
        [HttpGet]
        [Route("detailed")]
        public IHttpActionResult GetDetailedHealth()
        {
            try
            {
                _logger.LogInformation("Detailed health check requested");

                var startTime = DateTime.UtcNow;

                // Test database connection
                var connection = _db.Database.Connection;
                connection.Open();
                var serverVersion = connection.ServerVersion;

                // Test a simple query
                _db.ConfigureAsReadOnly();
                var structureCount = _db.Structures.CountAsync().Result;
                var locationCount = _db.Locations.CountAsync().Result;

                connection.Close();

                var elapsed = (DateTime.UtcNow - startTime).TotalMilliseconds;

                _logger.LogInformation("Detailed health check completed successfully in {ElapsedMs}ms", elapsed);

                return Ok(new
                {
                    status = "healthy",
                    timestamp = DateTime.UtcNow,
                    application = new
                    {
                        name = "ConnectomeODataV4",
                        version = System.Reflection.Assembly.GetExecutingAssembly().GetName().Version?.ToString()
                    },
                    database = new
                    {
                        connected = true,
                        serverVersion = serverVersion,
                        responseTimeMs = elapsed,
                        statistics = new
                        {
                            structureCount = structureCount,
                            locationCount = locationCount
                        }
                    }
                });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Detailed health check failed");

                return Ok(new
                {
                    status = "unhealthy",
                    timestamp = DateTime.UtcNow,
                    error = ex.Message
                });
            }
        }

        /// <summary>
        /// Mask sensitive information in connection string
        /// </summary>
        private string MaskConnectionString(string connectionString)
        {
            if (string.IsNullOrWhiteSpace(connectionString))
                return "N/A";

            // Simple masking - replace password if present
            var masked = connectionString;
            var passwordIndex = masked.IndexOf("password=", StringComparison.OrdinalIgnoreCase);
            if (passwordIndex >= 0)
            {
                var endIndex = masked.IndexOf(";", passwordIndex);
                if (endIndex > passwordIndex)
                {
                    masked = masked.Substring(0, passwordIndex) + "password=***" + masked.Substring(endIndex);
                }
            }

            return masked;
        }
    }
}




