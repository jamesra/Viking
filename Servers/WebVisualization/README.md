# WebVisualization

Web application for visualizing connectome data.

## Configuration

This service requires SQL Server database credentials for connecting to the UserAccounts and Rabbit databases. Credentials should be provided via environment variables to avoid hardcoding sensitive information.

### Required Environment Variables

The following environment variables must be set before running the service:

```
VIKING_DB_USER=your-sql-username
VIKING_DB_PASSWORD=your-sql-password
RABBIT_DB_PASSWORD=your-rabbit-db-password
```

### Connection Strings

The service uses two connection strings:

1. **VikingApplicationServices**: Connects to the UserAccounts database for membership and authentication
   - Uses `VIKING_DB_USER` for the username
   - Uses `VIKING_DB_PASSWORD` for the password
   - Server and database are configured in `web.config`

2. **RabbitConnectionString**: Connects to the Rabbit database for role management
   - Username is hardcoded as "Website"
   - Uses `RABBIT_DB_PASSWORD` for the password
   - Server and database are configured in `web.config`

### Local Development Setup

For local development, set environment variables in your development environment:

**Windows (PowerShell):**
```powershell
$env:VIKING_DB_USER="your-username"
$env:VIKING_DB_PASSWORD="your-password"
$env:RABBIT_DB_PASSWORD="your-rabbit-password"
```

**Windows (Command Prompt):**
```cmd
set VIKING_DB_USER=your-username
set VIKING_DB_PASSWORD=your-password
set RABBIT_DB_PASSWORD=your-rabbit-password
```

**Linux/macOS:**
```bash
export VIKING_DB_USER="your-username"
export VIKING_DB_PASSWORD="your-password"
export RABBIT_DB_PASSWORD="your-rabbit-password"
```

### IIS Deployment

For IIS deployment, configure environment variables at the application pool level:

1. Open IIS Manager
2. Navigate to Application Pools
3. Select the application pool for WebVisualization
4. Right-click and select "Advanced Settings"
5. Under "Environment Variables", add the three required variables:
   - `VIKING_DB_USER`
   - `VIKING_DB_PASSWORD`
   - `RABBIT_DB_PASSWORD`

Alternatively, you can set them at the system level, but application pool level is preferred for security isolation.

**Note:** After setting environment variables in IIS, you must restart the application pool for the changes to take effect.

### Environment Variable Substitution

The service automatically substitutes environment variables in connection strings using the pattern `%VARIABLE_NAME%`. This allows you to keep server and database names in `web.config` while loading credentials from environment variables.

Example connection string in `web.config`:
```
Server=YourServer,1433;Database=UserAccounts;User ID=%VIKING_DB_USER%;Password=%VIKING_DB_PASSWORD%
```

### Fallback Configuration

If environment variables are not set, the service will use the values directly from `web.config`. However, these values should be treated as placeholders only and should not contain real credentials. The environment variable approach is strongly recommended for all deployments.

### Security Notes

- Never commit real credentials to source control
- Use strong, unique passwords for production environments
- Rotate credentials regularly
- Ensure the application pool identity has minimal required permissions on the databases
- Consider using Windows Authentication (Integrated Security) where possible instead of SQL authentication



