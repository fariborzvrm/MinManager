using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using ObjectStorage.Application.Configuration;
using ObjectStorage.Domain.Entities;
using ObjectStorage.Domain.Interfaces;
using ObjectStorage.Infrastructure.Configuration;

namespace ObjectStorage.Infrastructure.Middleware;

public sealed class ApiKeyMiddleware
{
    private readonly RequestDelegate _next;
    private readonly AuthOptions _authOptions;
    private readonly ILogger<ApiKeyMiddleware> _logger;

    public const string ServiceIdentityKey = "ServiceIdentity";
    public const string CorrelationIdKey = "CorrelationId";

    public ApiKeyMiddleware(
        RequestDelegate next,
        IOptions<AuthOptions> authOptions,
        ILogger<ApiKeyMiddleware> logger)
    {
        _next = next;
        _authOptions = authOptions.Value;
        _logger = logger;
    }

    public async Task InvokeAsync(HttpContext context)
    {
        if (IsExcludedEndpoint(context.Request.Path))
        {
            await _next(context);
            return;
        }

        // Correlation ID: extract from header or generate new
        if (!context.Request.Headers.TryGetValue("X-Correlation-Id", out var correlationIdValues) ||
            string.IsNullOrWhiteSpace(correlationIdValues.FirstOrDefault()))
        {
            context.Items[CorrelationIdKey] = Guid.NewGuid().ToString("D");
        }
        else
        {
            context.Items[CorrelationIdKey] = correlationIdValues.First()!;
        }

        if (!context.Request.Headers.TryGetValue(_authOptions.HeaderName, out var apiKeyValues) ||
            string.IsNullOrWhiteSpace(apiKeyValues.FirstOrDefault()))
        {
            context.Response.StatusCode = StatusCodes.Status401Unauthorized;
            await context.Response.WriteAsJsonAsync(new
            {
                status = 401,
                title = "Unauthorized",
                detail = $"Missing or empty {_authOptions.HeaderName} header.",
                type = "https://httpstatuses.com/401"
            });
            return;
        }

        var apiKey = apiKeyValues.First()!;

        if (_authOptions.SkipInDevelopment)
        {
            var env = context.RequestServices.GetRequiredService<IWebHostEnvironment>();
            if (string.Equals(env.EnvironmentName, "Development", StringComparison.OrdinalIgnoreCase) && apiKey == "dev-bypass-key")
            {
                var fallbackServiceName = context.RequestServices
                    .GetRequiredService<IOptions<StorageOptions>>()
                    .Value.DefaultService;

                var fallbackIdentity = new ServiceIdentity(
                    fallbackServiceName,
                    [$"{fallbackServiceName}/"]);

                context.Items[ServiceIdentityKey] = fallbackIdentity;
                await _next(context);
                return;
            }
        }

        var resolver = context.RequestServices.GetRequiredService<IServiceResolver>();
        var identity = await resolver.ResolveAsync(apiKey, context.RequestAborted);

        if (identity is null)
        {
            _logger.LogWarning("Invalid API key attempt from {RemoteIp}", context.Connection.RemoteIpAddress);
            context.Response.StatusCode = StatusCodes.Status401Unauthorized;
            await context.Response.WriteAsJsonAsync(new
            {
                status = 401,
                title = "Unauthorized",
                detail = "Invalid or unknown API key.",
                type = "https://httpstatuses.com/401"
            });
            return;
        }

        context.Items[ServiceIdentityKey] = identity;
        await _next(context);
    }

    private static bool IsExcludedEndpoint(PathString path) =>
        path.StartsWithSegments("/health");
}
