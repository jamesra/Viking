using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Routing;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;

var builder = WebApplication.CreateBuilder(args);

// Add services to the container.
builder.Services.AddControllers();

// Configure routing options
builder.Services.Configure<RouteOptions>(options =>
{
    options.LowercaseUrls = true;           // Generate lowercase URLs
    options.LowercaseQueryStrings = false;   // Keep query strings as-is
});

var app = builder.Build();

// Ensure required directories exist at startup
var contentRoot = app.Environment.ContentRootPath;
var requiredDirs = new[] { "logs", "Output" };
foreach (var dir in requiredDirs)
{
    var dirPath = Path.Combine(contentRoot, dir);
    if (!Directory.Exists(dirPath))
    {
        try
        {
            Directory.CreateDirectory(dirPath);
            app.Logger.LogInformation("Created directory: {Directory}", dirPath);
        }
        catch (Exception ex)
        {
            app.Logger.LogWarning(ex, "Could not create directory {Directory}. Ensure app pool has write permissions.", dirPath);
        }
    }
}

// Configure the HTTP request pipeline.
app.UseStaticFiles();

// Add URL rewrite middleware to handle case-insensitive URLs
app.Use(async (context, next) =>
{
    var path = context.Request.Path.Value;
    if (path != null)
    {
        // Normalize controller and action names to proper casing
        var normalizedPath = System.Text.RegularExpressions.Regex.Replace(
            path,
            @"/network/",
            "/Network/",
            System.Text.RegularExpressions.RegexOptions.IgnoreCase);
        
        normalizedPath = System.Text.RegularExpressions.Regex.Replace(
            normalizedPath,
            @"/morphology/",
            "/Morphology/",
            System.Text.RegularExpressions.RegexOptions.IgnoreCase);
        
        normalizedPath = System.Text.RegularExpressions.Regex.Replace(
            normalizedPath,
            @"/motif/",
            "/Motif/",
            System.Text.RegularExpressions.RegexOptions.IgnoreCase);
        
        normalizedPath = System.Text.RegularExpressions.Regex.Replace(
            normalizedPath,
            @"/export/",
            "/Export/",
            System.Text.RegularExpressions.RegexOptions.IgnoreCase);
        
        context.Request.Path = normalizedPath;
    }
    await next();
});

app.UseRouting();

app.MapControllers();

// Add static file fallback for index.html if needed
app.MapFallbackToFile("index.html");

app.Run(); 