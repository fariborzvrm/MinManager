using System.Net;
using System.Text.Json;
using Microsoft.AspNetCore.Diagnostics;
using Microsoft.AspNetCore.Diagnostics.HealthChecks;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Diagnostics.HealthChecks;
using ObjectStorage.Api.Configuration;
using ObjectStorage.Application.Interfaces;
using ObjectStorage.Domain.Exceptions;
using ObjectStorage.Infrastructure;
using ObjectStorage.Infrastructure.MinIO;
using ObjectStorage.Infrastructure.Middleware;
using ObjectStorage.Infrastructure.Persistence;
using ObjectStorage.Infrastructure.Telemetry;
using OpenTelemetry.Metrics;
using OpenTelemetry.Resources;
using OpenTelemetry.Trace;

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddProblemDetails();
builder.Services.AddControllers();
builder.Services.AddHealthChecks()
    .AddCheck<MinioHealthCheck>("minio", failureStatus: HealthStatus.Unhealthy, tags: ["ready"])
    .AddCheck<PostgresHealthCheck>("postgres", failureStatus: HealthStatus.Unhealthy, tags: ["ready"]);

builder.Services.AddObjectStorage(builder.Configuration);

// OpenTelemetry
var otelOptions = new OpenTelemetryOptions();
builder.Configuration.GetSection(OpenTelemetryOptions.SectionName).Bind(otelOptions);

if (otelOptions.Enabled)
{
    builder.Services.AddOpenTelemetry()
        .ConfigureResource(resource => resource
            .AddService(otelOptions.ServiceName))
        .WithTracing(tracing =>
        {
            tracing
                .AddAspNetCoreInstrumentation()
                .AddHttpClientInstrumentation()
                .AddSource(ObjectStorageTracing.ActivitySourceName);

            if (!string.IsNullOrEmpty(otelOptions.OtlpEndpoint))
            {
                tracing.AddOtlpExporter(opt =>
                {
                    opt.Endpoint = new Uri(otelOptions.OtlpEndpoint);
                });
            }

            if (otelOptions.ConsoleExporter)
            {
                tracing.AddConsoleExporter();
            }
        })
        .WithMetrics(metrics =>
        {
            metrics
                .AddAspNetCoreInstrumentation()
                .AddHttpClientInstrumentation()
                .AddMeter(ObjectStorageMetrics.MeterName);

            if (!string.IsNullOrEmpty(otelOptions.OtlpEndpoint))
            {
                metrics.AddOtlpExporter(opt =>
                {
                    opt.Endpoint = new Uri(otelOptions.OtlpEndpoint);
                });
            }

            if (otelOptions.ConsoleExporter)
            {
                metrics.AddConsoleExporter();
            }
        });
}

var app = builder.Build();

using (var scope = app.Services.CreateScope())
{
    var dbContext = scope.ServiceProvider.GetRequiredService<ObjectStorageDbContext>();
    dbContext.Database.Migrate();
}

app.UseExceptionHandler(errorApp =>
{
    errorApp.Run(async context =>
    {
        var exception = context.Features.Get<IExceptionHandlerFeature>()?.Error;
        var (status, title, detail) = MapExceptionToProblem(exception);

        context.Response.StatusCode = (int)status;
        context.Response.ContentType = "application/problem+json";

        var problemDetails = new Microsoft.AspNetCore.Mvc.ProblemDetails
        {
            Status = (int)status,
            Title = title,
            Detail = detail,
            Type = $"https://httpstatuses.com/{(int)status}"
        };

        var json = JsonSerializer.Serialize(problemDetails, new JsonSerializerOptions
        {
            PropertyNamingPolicy = JsonNamingPolicy.CamelCase
        });

        await context.Response.WriteAsync(json);

        // Audit log auth failures
        if (exception is ObjectStorage.Domain.Exceptions.UnauthorizedAccessException or ForbiddenAccessException)
        {
            ObjectStorageMetrics.AuthFailuresTotal.Add(1,
                new KeyValuePair<string, object?>("exception_type", exception.GetType().Name));

            try
            {
                var scopeFactory = context.RequestServices.GetRequiredService<IServiceScopeFactory>();
                using var scope = scopeFactory.CreateScope();
                var auditLogger = scope.ServiceProvider.GetRequiredService<IAuditLogger>();

                var correlationId = context.Items[ApiKeyMiddleware.CorrelationIdKey] as string ?? string.Empty;
                var serviceName = context.Request.Headers.TryGetValue("X-Api-Key", out var keyValues)
                    ? keyValues.First()!
                    : "unknown";

                await auditLogger.LogAsync(
                    serviceName,
                    "AuthFailure",
                    null,
                    "Failure",
                    correlationId,
                    ipAddress: context.Connection.RemoteIpAddress?.ToString());
            }
            catch
            {
                // Audit logging failure must not break the response
            }
        }
    });
});

app.UseMiddleware<SecurityHeadersMiddleware>();
app.UseMiddleware<ApiKeyMiddleware>();

app.UseStatusCodePages();

app.MapControllers();

var livenessOptions = new HealthCheckOptions { Predicate = _ => false };
app.MapHealthChecks("/health/live", livenessOptions);

var readinessOptions = new HealthCheckOptions { Predicate = check => check.Tags.Contains("ready") };
app.MapHealthChecks("/health/ready", readinessOptions);

app.MapHealthChecks("/health");

app.Run();

static (HttpStatusCode status, string title, string detail) MapExceptionToProblem(Exception? exception)
{
    return exception switch
    {
        ObjectNotFoundException ex =>
            (HttpStatusCode.NotFound, "Object Not Found", ex.Message),
        InvalidRequestException ex =>
            (HttpStatusCode.BadRequest, "Invalid Request", ex.Message),
        CategoryNotAllowedException ex =>
            (HttpStatusCode.BadRequest, "Category Not Allowed", ex.Message),
        FileTooLargeException ex =>
            (HttpStatusCode.RequestEntityTooLarge, "File Too Large", ex.Message),
        UnsupportedContentTypeException ex =>
            (HttpStatusCode.UnsupportedMediaType, "Unsupported Content Type", ex.Message),
        StorageUnavailableException ex =>
            (HttpStatusCode.ServiceUnavailable, "Storage Unavailable", ex.Message),
        ObjectStorage.Domain.Exceptions.UnauthorizedAccessException ex =>
            (HttpStatusCode.Unauthorized, "Unauthorized", ex.Message),
        ForbiddenAccessException ex =>
            (HttpStatusCode.Forbidden, "Forbidden", ex.Message),
        _ =>
            (HttpStatusCode.InternalServerError, "Internal Server Error",
                "An unexpected error occurred. Please try again later.")
    };
}

public partial class Program;
