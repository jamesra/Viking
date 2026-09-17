using DataExport.Controllers;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Routing;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using System.ComponentModel;
using System.Text.RegularExpressions;

var builder = WebApplication.CreateBuilder(new WebApplicationOptions
{
    Args = args,
    WebRootPath = "Content"
});

// Add services to the container.
builder.Services.AddControllers();

// Configure routing options
builder.Services.Configure<RouteOptions>(options =>
{
    options.LowercaseUrls = true;           // Generate lowercase URLs
    options.LowercaseQueryStrings = false;   // Keep query strings as-is
});

var app = builder.Build();

app.UseDefaultFiles(new DefaultFilesOptions
{
    DefaultFileNames = new List<string> { "index.html" }
});

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
app.UseIdRequestExceptionHandler();

app.UseStaticFiles();

// Add URL rewrite middleware to handle case-insensitive URLs
app.Use(async (context, next) =>
{
    var path = context.Request.Path.Value;
    if (path != null)
    {
        // Normalize controller and action names to proper casing
        var normalizedPath = MyRegex().Replace(path, "/Network/");

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

partial class Program
{
#if NETFRAMEWORK
    private static readonly System.Text.RegularExpressions.Regex MyRegex = new System.Text.RegularExpressions.Regex(@"/network/", System.Text.RegularExpressions.RegexOptions.IgnoreCase | System.Text.RegularExpressions.RegexOptions.Compiled);
#else
    [GeneratedRegex(@"/network/", RegexOptions.IgnoreCase, "en-US")]
    private static partial System.Text.RegularExpressions.Regex MyRegex();
#endif
}
