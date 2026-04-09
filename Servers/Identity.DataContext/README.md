# Identity.DataContext

This project contains the Entity Framework Core data context for the Identity Server, including user management, role-based access control, and resource permissions.

## Database Setup

### Prerequisites

- .NET 9.0 SDK
- SQL Server (local or remote)
- Entity Framework Core Tools

### Connection String

**Important**: Connection strings with credentials should **never** be stored in `appsettings.json` files that are committed to version control. Use one of the following secure methods:

#### Option 1: User Secrets (Recommended for Development)

For local development, use .NET User Secrets to store connection strings with credentials:

```bash
# Navigate to the Identity.DataContext project directory
cd C:\src\git\Viking\Servers\Identity.DataContext

# Set the connection string in user secrets
dotnet user-secrets set "ConnectionStrings:IdentityConnection" "Server=YourServer;Database=IdentityViking;Trusted_Connection=False;User ID=YourUser;Password=YourPassword;MultipleActiveResultSets=true;TrustServerCertificate=True"
```

**User Secrets ID**: `aspnet-Identity.DataContext-9A8B7C6D-5E4F-3A2B-1C0D-9E8F7A6B5C4D`

#### Option 2: Environment Variables

You can also use environment variables for connection strings:

```bash
# Windows PowerShell
$env:ConnectionStrings__IdentityConnection = "Server=YourServer;Database=IdentityViking;Trusted_Connection=False;User ID=YourUser;Password=YourPassword;MultipleActiveResultSets=true;TrustServerCertificate=True"

# Linux/Mac
export ConnectionStrings__IdentityConnection="Server=YourServer;Database=IdentityViking;Trusted_Connection=False;User ID=YourUser;Password=YourPassword;MultipleActiveResultSets=true;TrustServerCertificate=True"
```

#### Option 3: appsettings.json with Environment Variable Substitution

The `appsettings.json` file uses environment variable substitution for deployment scenarios:

```json
{
  "ConnectionStrings": {
    "IdentityConnection": "Server=${SQL_SERVER_HOST},${SQL_SERVER_PORT};Database=IdentityViking;Trusted_Connection=False;User ID=${SQL_SERVER_USER};Password=${SQL_SERVER_PASSWORD};MultipleActiveResultSets=true;TrustServerCertificate=True"
  }
}
```

Set the following environment variables:
- `SQL_SERVER_HOST` - SQL Server hostname or IP
- `SQL_SERVER_PORT` - SQL Server port (default: 1433)
- `SQL_SERVER_USER` - SQL Server username
- `SQL_SERVER_PASSWORD` - SQL Server password

#### Generic Connection String Examples

**SQL Server with Integrated Security (Windows Authentication)**:
```
Server=YourServer;Database=IdentityViking;Trusted_Connection=True;Integrated Security=True;MultipleActiveResultSets=true;TrustServerCertificate=True
```

**SQL Server with SQL Authentication**:
```
Server=YourServer;Database=IdentityViking;Trusted_Connection=False;User ID=YourUser;Password=YourPassword;MultipleActiveResultSets=true;TrustServerCertificate=True
```

**SQL Server with Custom Port**:
```
Server=YourServer,1433;Database=IdentityViking;Trusted_Connection=False;User ID=YourUser;Password=YourPassword;MultipleActiveResultSets=true;TrustServerCertificate=True
```

## Entity Framework Migrations

### Creating Migrations

To create a new migration after making changes to your models:

```bash
# Navigate to the Identity.DataContext project directory
cd C:\src\git\Viking\Servers\Identity.DataContext

# Create a new migration
dotnet ef migrations add YourMigrationName

# Example:
dotnet ef migrations add AddNewUserProperty
```

### Applying Migrations

To apply pending migrations to the database:

```bash
# Apply all pending migrations
dotnet ef database update

# Apply to a specific migration
dotnet ef database update YourMigrationName
```

### Removing Migrations

To remove the last migration (if it hasn't been applied to the database):

```bash
# Remove the last migration
dotnet ef migrations remove
```

**Note:** Only remove migrations that haven't been applied to production databases.

### Deleting Specific Migrations

If you need to delete a specific migration that has been applied:

1. **First, revert the database to the state before that migration:**
   ```bash
   dotnet ef database update PreviousMigrationName
   ```

2. **Then remove the migration files manually:**
   - Delete the migration files from the `Migrations` folder:
     - `YYYYMMDDHHMMSS_MigrationName.cs`
     - `YYYYMMDDHHMMSS_MigrationName.Designer.cs`

3. **Update the model snapshot:**
   ```bash
   dotnet ef migrations add NewMigrationName
   dotnet ef migrations remove
   ```

## Resetting Migration History

To completely reset the migration history and start fresh:

### Method 1: Complete Reset (Recommended for Development)

1. **Delete the entire Migrations folder:**
   ```bash
   # Remove the Migrations directory
   rmdir /s Migrations
   ```

2. **Drop and recreate the database:**
   ```bash
   # Drop the database
   dotnet ef database drop --force
   
   # Create initial migration
   dotnet ef migrations add InitialCreate
   
   # Apply the migration
   dotnet ef database update
   ```

### Method 2: Reset with Existing Database

If you want to keep the database but reset migration history:

1. **Delete the Migrations folder:**
   ```bash
   rmdir /s Migrations
   ```

2. **Create a new initial migration:**
   ```bash
   dotnet ef migrations add InitialCreate
   ```

3. **Mark the database as up-to-date without applying migrations:**
   ```bash
   dotnet ef database update 0
   dotnet ef database update
   ```

## Administrator Role Assignment

**Important:** The first user created in the system automatically becomes the administrator.

### How It Works

1. The system creates an "Admin" role during database initialization
2. When the first user registers through the application, they are automatically assigned the Admin role
3. This ensures there's always at least one administrator in the system
4. Subsequent users will need to be manually assigned roles by an administrator

### Default Data

The following data is seeded during database initialization:

- **Admin Role**: Created with ID `Special.Roles.AdminId`
- **Resource Types**: Resource, OrganizationalUnit, Group, Volume
- **Standard Permissions**: Various permissions for different resource types
- **Everyone Group**: A default group that all users can be assigned to

## Development Notes

### Design-Time Context Factory

The project includes a `DesignTimeDbContextFactory` that allows Entity Framework tools to work properly during development. This factory:

- Reads the connection string from `appsettings.json`
- Creates the DbContext with the proper configuration
- Is used automatically by EF Core tools

### Model Configuration

The `ApplicationDbContext` includes extensive model configuration in the `OnModelCreating` method:

- Custom table relationships
- Composite keys
- Discriminators for inheritance
- Default data seeding

### Password Hashing

The context uses ASP.NET Core Identity's built-in password hashing for secure password storage.

## Troubleshooting

### Common Issues

1. **Connection String Not Found**
   - Ensure `appsettings.json` exists in the project root
   - Verify the connection string name matches `IdentityConnection`

2. **Migration Conflicts**
   - If you have conflicts, consider resetting migration history (see above)
   - Always backup your database before making major changes

3. **Design-Time Context Issues**
   - Ensure the `DesignTimeDbContextFactory` is properly configured
   - Verify the connection string is accessible from the project directory

### Useful Commands

```bash
# Check migration status
dotnet ef migrations list

# Generate SQL script for migrations
dotnet ef migrations script

# Generate SQL script between two migrations
dotnet ef migrations script FromMigration ToMigration

# Check database connection
dotnet ef database update --dry-run
```

## User Secrets Configuration

### Setting Up User Secrets

User secrets are stored outside the repository and are automatically ignored by git. They are stored in:
- **Windows**: `%APPDATA%\Microsoft\UserSecrets\<UserSecretsId>\secrets.json`
- **Linux/Mac**: `~/.microsoft/usersecrets/<UserSecretsId>/secrets.json`

### Required Secrets

The following secrets should be configured for this project:

| Secret Key | Description | Example |
|------------|-------------|---------|
| `ConnectionStrings:IdentityConnection` | Full database connection string with credentials | `Server=YourServer;Database=IdentityViking;Trusted_Connection=False;User ID=YourUser;Password=YourPassword;MultipleActiveResultSets=true;TrustServerCertificate=True` |

### Managing User Secrets

```bash
# List all secrets
dotnet user-secrets list

# Get a specific secret
dotnet user-secrets get "ConnectionStrings:IdentityConnection"

# Remove a secret
dotnet user-secrets remove "ConnectionStrings:IdentityConnection"

# Clear all secrets
dotnet user-secrets clear
```

### Design-Time Operations

The `DesignTimeDbContextFactory` automatically loads user secrets and environment variables, so Entity Framework Core tools (migrations, etc.) will use the connection string from user secrets when available.

## Security Considerations

- **Never commit connection strings with real credentials** to version control
- **Use user secrets for local development** - they are stored outside the repository
- **Use environment variables or secrets managers** for production deployments
- **The `secrets.json` file** (if used) is automatically ignored by `.gitignore`
- **Regularly backup your database** before applying migrations
- **Test migrations on a copy of production data** when possible

