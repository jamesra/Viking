# Neo4JService

Neo4J web service for querying graph databases.

## Configuration

This service requires Neo4J database credentials to be configured. Credentials should be provided via environment variables to avoid hardcoding sensitive information.

### Required Environment Variables

The following environment variables must be set before running the service:

```
NEO4J_DATABASE=bolt://your-neo4j-server:7687
NEO4J_USER=your-username
NEO4J_PASSWORD=your-password
```

### Local Development Setup

For local development, set environment variables in your development environment:

**Windows (PowerShell):**
```powershell
$env:NEO4J_DATABASE="bolt://localhost:7687"
$env:NEO4J_USER="neo4j"
$env:NEO4J_PASSWORD="your-password"
```

**Windows (Command Prompt):**
```cmd
set NEO4J_DATABASE=bolt://localhost:7687
set NEO4J_USER=neo4j
set NEO4J_PASSWORD=your-password
```

**Linux/macOS:**
```bash
export NEO4J_DATABASE="bolt://localhost:7687"
export NEO4J_USER="neo4j"
export NEO4J_PASSWORD="your-password"
```

### IIS Deployment

For IIS deployment, configure environment variables at the application pool level:

1. Open IIS Manager
2. Navigate to Application Pools
3. Select the application pool for Neo4JService
4. Right-click and select "Advanced Settings"
5. Under "Environment Variables", add the three required variables

Alternatively, you can set them at the system level, but application pool level is preferred for security isolation.

### Fallback Configuration

If environment variables are not set, the service will attempt to read values from `web.config` under `<appSettings>`. However, these values should be treated as placeholders only and should not contain real credentials. The environment variable approach is strongly recommended for all deployments.

### Security Notes

- Never commit real credentials to source control
- Use strong, unique passwords for production environments
- Rotate credentials regularly
- Consider using Windows Authentication or certificate-based authentication where possible



