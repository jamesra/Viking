using System.Text.Json;
using DataExport.Controllers;
using Microsoft.Extensions.Logging.Abstractions;

namespace DataExport.Tests;

/// <summary>
/// Pins how <see cref="IdRequestExceptionMiddleware"/> turns rejected export inputs into ProblemDetails
/// responses, and that it leaves other exceptions and already-started responses alone.
/// </summary>
[TestClass]
public class IdRequestExceptionMiddlewareTest
{
    private static IdRequestExceptionMiddleware Create(RequestDelegate next) =>
        new(next, NullLogger<IdRequestExceptionMiddleware>.Instance);

    private static DefaultHttpContext NewContext(string path)
    {
        DefaultHttpContext context = new();
        context.Request.Path = path;
        context.Response.Body = new MemoryStream();
        return context;
    }

    /// <summary>A rejected input yields its status code and a problem+json body naming the reason and path.</summary>
    [TestMethod]
    public async Task IdRequestException_BecomesProblemDetails()
    {
        DefaultHttpContext context = NewContext("/Morphology");
        var middleware = Create(_ => throw new IdRequestException(StatusCodes.Status413PayloadTooLarge, "too many ids"));

        await middleware.InvokeAsync(context);

        Assert.AreEqual(StatusCodes.Status413PayloadTooLarge, context.Response.StatusCode);
        StringAssert.StartsWith(context.Response.ContentType, "application/problem+json");
        context.Response.Body.Position = 0;
        using JsonDocument doc = await JsonDocument.ParseAsync(context.Response.Body);
        Assert.AreEqual(413, doc.RootElement.GetProperty("status").GetInt32());
        Assert.AreEqual("too many ids", doc.RootElement.GetProperty("detail").GetString());
        Assert.AreEqual("/Morphology", doc.RootElement.GetProperty("instance").GetString());
    }

    /// <summary>A request that does not throw passes through untouched.</summary>
    [TestMethod]
    public async Task NoException_PassesThrough()
    {
        DefaultHttpContext context = NewContext("/ok");
        var middleware = Create(c =>
        {
            c.Response.StatusCode = StatusCodes.Status204NoContent;
            return Task.CompletedTask;
        });

        await middleware.InvokeAsync(context);

        Assert.AreEqual(StatusCodes.Status204NoContent, context.Response.StatusCode);
        Assert.AreEqual(0, context.Response.Body.Length);
    }

    /// <summary>Exceptions other than <see cref="IdRequestException"/> are not swallowed.</summary>
    [TestMethod]
    public async Task OtherExceptions_Propagate()
    {
        var middleware = Create(_ => throw new InvalidOperationException("boom"));

        await Assert.ThrowsExceptionAsync<InvalidOperationException>(() => middleware.InvokeAsync(NewContext("/x")));
    }
}
