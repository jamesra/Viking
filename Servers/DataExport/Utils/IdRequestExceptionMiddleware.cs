using Microsoft.AspNetCore.Mvc;

namespace DataExport.Controllers;

/// <summary>
/// Converts <see cref="IdRequestException"/> into a ProblemDetails response.
/// </summary>
public sealed class IdRequestExceptionMiddleware(RequestDelegate next, ILogger<IdRequestExceptionMiddleware> logger)
{
    /// <summary>
    /// Invokes the middleware for a request.
    /// </summary>
    /// <param name="context">The request context.</param>
    public async Task InvokeAsync(HttpContext context)
    {
        try
        {
            await next(context).ConfigureAwait(false);
        }
        catch (IdRequestException ex)
        {
            if (context.Response.HasStarted)
            {
                throw;
            }

            logger.LogInformation("Rejected export request inputs: {Reason}", ex.Message);

            context.Response.Clear();
            context.Response.StatusCode = ex.StatusCode;

            ProblemDetails problem = new()
            {
                Status = ex.StatusCode,
                Title = "The export request inputs were rejected.",
                Detail = ex.Message,
                Instance = context.Request.Path
            };

            await context.Response
                .WriteAsJsonAsync<ProblemDetails>(problem, options: null, contentType: "application/problem+json")
                .ConfigureAwait(false);
        }
    }
}

/// <summary>
/// Registration helpers for <see cref="IdRequestExceptionMiddleware"/>.
/// </summary>
public static class IdRequestExceptionMiddlewareExtensions
{
    /// <summary>
    /// Adds the middleware that turns rejected export inputs into ProblemDetails responses.
    /// </summary>
    /// <param name="app">The application builder.</param>
    /// <returns>The application builder, for chaining.</returns>
    public static IApplicationBuilder UseIdRequestExceptionHandler(this IApplicationBuilder app) =>
        app.UseMiddleware<IdRequestExceptionMiddleware>();
}
