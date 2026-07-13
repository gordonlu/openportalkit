using System.Net;
using System.Threading.RateLimiting;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Diagnostics.HealthChecks;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.HttpOverrides;
using Microsoft.AspNetCore.RateLimiting;
using Microsoft.AspNetCore.Routing;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Diagnostics.HealthChecks;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Options;
using System.Text.Json;

namespace OpenPortalKit.Infrastructure.Production;

public static class ProductionHardeningExtensions
{
    public static IServiceCollection AddDatabaseReadinessCheck(
        this IServiceCollection services,
        string providerInvariantName,
        string connectionString)
    {
        services.AddHealthChecks().Add(new HealthCheckRegistration(
            "postgres",
            _ => new DatabaseReadinessHealthCheck(providerInvariantName, connectionString),
            HealthStatus.Unhealthy,
            ["ready"]));
        return services;
    }

    public static IServiceCollection AddRedisReadinessCheck(
        this IServiceCollection services,
        string connectionString)
    {
        if (!RedisReadinessHealthCheck.TryParseEndpoint(connectionString, out var host, out var port))
        {
            throw new InvalidOperationException("Redis connection string must begin with a host and optional port.");
        }
        services.AddHealthChecks().Add(new HealthCheckRegistration(
            "redis",
            _ => new RedisReadinessHealthCheck(host, port),
            HealthStatus.Unhealthy,
            ["ready"]));
        return services;
    }

    public static IServiceCollection AddOpenPortalKitProductionHardening(
        this IServiceCollection services,
        IConfiguration configuration,
        bool adminHost)
    {
        services.AddOptions<ProductionHardeningOptions>()
            .Bind(configuration.GetSection(ProductionHardeningOptions.SectionName))
            .Validate(options => options.PublicRequestsPerMinute > 0, "Public rate limit must be positive.")
            .Validate(options => options.AdminRequestsPerMinute > 0, "Admin rate limit must be positive.")
            .Validate(options => options.QueueLimit >= 0, "Rate-limit queue cannot be negative.")
            .Validate(options => options.LoginAttemptsPerFiveMinutes > 0, "Login rate limit must be positive.")
            .ValidateOnStart();
        services.AddHealthChecks()
            .AddCheck("self", () => HealthCheckResult.Healthy(), ["live", "ready"]);

        services.Configure<ForwardedHeadersOptions>(options =>
        {
            options.ForwardedHeaders = ForwardedHeaders.XForwardedFor | ForwardedHeaders.XForwardedProto;
        });
        services.AddHsts(options =>
        {
            var hardening = configuration.GetSection(ProductionHardeningOptions.SectionName)
                .Get<ProductionHardeningOptions>() ?? new ProductionHardeningOptions();
            options.MaxAge = TimeSpan.FromDays(Math.Max(1, hardening.HstsMaxAgeDays));
            options.IncludeSubDomains = true;
        });

        services.AddRateLimiter(options =>
        {
            options.RejectionStatusCode = StatusCodes.Status429TooManyRequests;
            options.OnRejected = async (context, cancellationToken) =>
            {
                context.HttpContext.Response.ContentType = "application/problem+json";
                await context.HttpContext.Response.WriteAsJsonAsync(new
                {
                    type = "https://httpstatuses.com/429",
                    title = "Request rate limit exceeded",
                    status = StatusCodes.Status429TooManyRequests,
                    traceId = context.HttpContext.TraceIdentifier
                }, cancellationToken);
            };
            options.GlobalLimiter = PartitionedRateLimiter.Create<HttpContext, string>(context =>
            {
                var hardening = context.RequestServices.GetRequiredService<IOptions<ProductionHardeningOptions>>().Value;
                var limit = adminHost ? hardening.AdminRequestsPerMinute : hardening.PublicRequestsPerMinute;
                var key = context.Connection.RemoteIpAddress?.ToString() ?? IPAddress.None.ToString();
                return RateLimitPartition.GetFixedWindowLimiter(key, _ => new FixedWindowRateLimiterOptions
                {
                    PermitLimit = limit,
                    QueueLimit = hardening.QueueLimit,
                    QueueProcessingOrder = QueueProcessingOrder.OldestFirst,
                    Window = TimeSpan.FromMinutes(1),
                    AutoReplenishment = true
                });
            });
            options.AddPolicy("admin-login", context =>
            {
                var hardening = context.RequestServices.GetRequiredService<IOptions<ProductionHardeningOptions>>().Value;
                var key = context.Connection.RemoteIpAddress?.ToString() ?? IPAddress.None.ToString();
                return RateLimitPartition.GetFixedWindowLimiter(key, _ => new FixedWindowRateLimiterOptions
                {
                    PermitLimit = hardening.LoginAttemptsPerFiveMinutes,
                    QueueLimit = 0,
                    Window = TimeSpan.FromMinutes(5),
                    AutoReplenishment = true
                });
            });
        });

        return services;
    }

    public static WebApplication UseOpenPortalKitProductionHardening(this WebApplication app)
    {
        var options = app.Services.GetRequiredService<IOptions<ProductionHardeningOptions>>().Value;
        app.UseForwardedHeaders();
        app.UseMiddleware<TraceIdentifierMiddleware>();
        app.Use(async (context, next) =>
        {
            var nonce = CspNonceAccessor.Create(context);
            foreach (var header in SecurityHeaderPolicy.Headers)
            {
                context.Response.Headers[header.Key] = header.Value.Replace("{nonce}", nonce, StringComparison.Ordinal);
            }
            await next();
        });

        if (!app.Environment.IsDevelopment() && options.EnableHsts)
        {
            app.UseHsts();
        }
        if (options.EnableHttpsRedirection)
        {
            app.UseHttpsRedirection();
        }
        if (options.EnableRateLimiting)
        {
            app.UseRateLimiter();
        }
        return app;
    }

    public static IEndpointRouteBuilder MapOpenPortalKitHealthEndpoints(this IEndpointRouteBuilder endpoints)
    {
        endpoints.MapHealthChecks("/health/live", new HealthCheckOptions
        {
            Predicate = check => check.Tags.Contains("live"),
            ResponseWriter = WriteHealthResponseAsync
        }).AllowAnonymous();
        endpoints.MapHealthChecks("/health/ready", new HealthCheckOptions
        {
            Predicate = check => check.Tags.Contains("ready"),
            ResponseWriter = WriteHealthResponseAsync
        }).AllowAnonymous();
        endpoints.MapGet("/health", () => Results.Redirect("/health/ready", permanent: false))
            .AllowAnonymous();
        return endpoints;
    }

    private static Task WriteHealthResponseAsync(HttpContext context, HealthReport report)
    {
        context.Response.ContentType = "application/json; charset=utf-8";
        return context.Response.WriteAsync(JsonSerializer.Serialize(new
        {
            status = report.Status.ToString(),
            durationMilliseconds = Math.Round(report.TotalDuration.TotalMilliseconds, 2),
            checks = report.Entries.OrderBy(entry => entry.Key).Select(entry => new
            {
                name = entry.Key,
                status = entry.Value.Status.ToString(),
                durationMilliseconds = Math.Round(entry.Value.Duration.TotalMilliseconds, 2),
                description = entry.Value.Description
            })
        }));
    }
}
