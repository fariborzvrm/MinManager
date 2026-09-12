using Microsoft.AspNetCore.Http;
using ObjectStorage.Infrastructure.Middleware;

namespace ObjectStorage.UnitTests;

public sealed class SecurityHeadersMiddlewareTests
{
    private readonly SecurityHeadersMiddleware _middleware;

    public SecurityHeadersMiddlewareTests()
    {
        _middleware = new SecurityHeadersMiddleware(_ => Task.CompletedTask);
    }

    [Fact]
    public async Task InvokeAsync_SetsSecurityHeaders()
    {
        var context = new DefaultHttpContext();
        context.Request.Scheme = "http";

        await _middleware.InvokeAsync(context);

        Assert.Equal("nosniff", context.Response.Headers["X-Content-Type-Options"].ToString());
        Assert.Equal("DENY", context.Response.Headers["X-Frame-Options"].ToString());
        Assert.Equal("1; mode=block", context.Response.Headers["X-XSS-Protection"].ToString());
        Assert.Equal("no-store, no-cache, must-revalidate", context.Response.Headers["Cache-Control"].ToString());
        Assert.Equal("no-cache", context.Response.Headers["Pragma"].ToString());
        Assert.Equal("no-referrer", context.Response.Headers["Referrer-Policy"].ToString());
    }

    [Fact]
    public async Task InvokeAsync_HttpRequest_DoesNotSetHsts()
    {
        var context = new DefaultHttpContext();
        context.Request.Scheme = "http";

        await _middleware.InvokeAsync(context);

        Assert.False(context.Response.Headers.ContainsKey("Strict-Transport-Security"));
    }

    [Fact]
    public async Task InvokeAsync_HttpsRequest_SetsHsts()
    {
        var context = new DefaultHttpContext();
        context.Request.Scheme = "https";

        await _middleware.InvokeAsync(context);

        Assert.Equal("max-age=31536000; includeSubDomains", context.Response.Headers["Strict-Transport-Security"].ToString());
    }

    [Fact]
    public async Task InvokeAsync_CallsNextMiddleware()
    {
        var nextCalled = false;
        var middleware = new SecurityHeadersMiddleware(_ =>
        {
            nextCalled = true;
            return Task.CompletedTask;
        });

        var context = new DefaultHttpContext();
        await middleware.InvokeAsync(context);

        Assert.True(nextCalled);
    }
}
