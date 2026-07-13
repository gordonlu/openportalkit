using System.Data.Common;
using Npgsql;
using OpenPortalKit.Kernel.Configuration;
using OpenPortalKit.Kernel.Audit;
using OpenPortalKit.Kernel.Events;
using OpenPortalKit.Kernel.Persistence;
using OpenPortalKit.Infrastructure.Production;
using OpenPortalKit.AdminHost;
using OpenPortalKit.AdminHost.AgentAccess;
using OpenPortalKit.AdminHost.IndustryPacks;
using OpenPortalKit.AdminHost.Security;
using OpenPortalKit.Modules.AgentAccess;
using OpenPortalKit.Modules.AgentAccess.AgentOutputs;
using OpenPortalKit.Modules.Assets;
using OpenPortalKit.Modules.Audit;
using OpenPortalKit.Modules.Content;
using OpenPortalKit.Modules.Content.BlockTemplates;
using OpenPortalKit.Modules.Content.ContentItems;
using OpenPortalKit.Modules.Dashboard;
using OpenPortalKit.Modules.Dashboard.Analytics;
using OpenPortalKit.Modules.Dashboard.Observability;
using OpenPortalKit.Modules.Dashboard.Sources;
using OpenPortalKit.Modules.Dashboard.Storage;
using OpenPortalKit.Modules.Dashboard.Summaries;
using OpenPortalKit.Modules.Data;
using OpenPortalKit.Modules.Data.Datasets;
using OpenPortalKit.Modules.Identity;
using OpenPortalKit.Modules.Identity.Authentication;
using OpenPortalKit.Modules.IndustryPacks;
using OpenPortalKit.Modules.Jobs;
using OpenPortalKit.Modules.Search;
using OpenPortalKit.Modules.Seo;
using OpenPortalKit.Modules.Seo.Revalidation;
using OpenPortalKit.Modules.Workflow;
using Microsoft.Extensions.Options;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.AspNetCore.Authentication.OpenIdConnect;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.DataProtection;
using System.Security.Cryptography.X509Certificates;

var builder = WebApplication.CreateBuilder(args);
DbProviderFactories.RegisterFactory("Npgsql", NpgsqlFactory.Instance);

var adminAuthentication = builder.Configuration
    .GetSection(AdminAuthenticationOptions.SectionName)
    .Get<AdminAuthenticationOptions>() ?? new AdminAuthenticationOptions();
if (adminAuthentication.RequireAuthentication && adminAuthentication.Mode == AdminAuthenticationMode.Local &&
    !adminAuthentication.PasswordHash.StartsWith("pbkdf2-sha512$", StringComparison.Ordinal))
{
    throw new InvalidOperationException(
        "Admin authentication requires OpenPortalKit:AdminAuthentication:PasswordHash in PBKDF2-SHA512 format.");
}
if (adminAuthentication.RequireAuthentication && adminAuthentication.Mode == AdminAuthenticationMode.OpenIdConnect &&
    (!Uri.TryCreate(adminAuthentication.Authority, UriKind.Absolute, out var authority) || authority.Scheme != Uri.UriSchemeHttps ||
     string.IsNullOrWhiteSpace(adminAuthentication.ClientId) || string.IsNullOrWhiteSpace(adminAuthentication.ClientSecret)))
{
    throw new InvalidOperationException(
        "OpenID Connect admin authentication requires an HTTPS Authority, ClientId, and ClientSecret.");
}

var industryPackRoot = Path.GetFullPath(Path.Combine(
    builder.Environment.ContentRootPath,
    builder.Configuration["OpenPortalKit:IndustryPacks:RootPath"] ?? "../../industry-packs"));
var industryPackCatalog = new IndustryPackCatalog(new IndustryPackLoader(
    builder.Configuration["OpenPortalKit:IndustryPacks:CoreVersion"] ?? "0.1.0"));
var industryPackCatalogResult = industryPackCatalog.DiscoverAsync(industryPackRoot).GetAwaiter().GetResult();
if (!industryPackCatalogResult.Succeeded)
{
    throw new InvalidOperationException("Industry pack validation failed: " + string.Join("; ",
        industryPackCatalogResult.Errors.Select(error => $"{error.Code}: {error.Message}")));
}

// Add services to the container.
builder.Services.AddRazorPages();
builder.Services.AddSingleton(adminAuthentication);
var dataProtection = builder.Services.AddDataProtection()
    .SetApplicationName("OpenPortalKit.AdminHost");
if (!string.IsNullOrWhiteSpace(adminAuthentication.DataProtectionKeyPath))
{
    if (string.IsNullOrWhiteSpace(adminAuthentication.KeyEncryptionCertificatePath))
    {
        throw new InvalidOperationException(
            "A custom Data Protection key path requires an X.509 key-encryption certificate.");
    }
    var keyDirectory = Directory.CreateDirectory(Path.GetFullPath(adminAuthentication.DataProtectionKeyPath));
    var certificate = X509CertificateLoader.LoadPkcs12FromFile(
        adminAuthentication.KeyEncryptionCertificatePath,
        adminAuthentication.KeyEncryptionCertificatePassword);
    dataProtection.PersistKeysToFileSystem(keyDirectory).ProtectKeysWithCertificate(certificate);
}
builder.Services.AddSingleton<PasswordCredentialHasher>();
builder.Services.AddSingleton<AdminCredentialValidator>();
builder.Services.AddSingleton<AdminLoginAttemptGuard>();
var authenticationBuilder = builder.Services
    .AddAuthentication(CookieAuthenticationDefaults.AuthenticationScheme)
    .AddCookie(CookieAuthenticationDefaults.AuthenticationScheme, options =>
    {
        options.Cookie.Name = "__Host-OpenPortalKit.Admin";
        options.Cookie.HttpOnly = true;
        options.Cookie.SameSite = SameSiteMode.Strict;
        options.Cookie.SecurePolicy = CookieSecurePolicy.Always;
        options.LoginPath = "/Account/Login";
        options.AccessDeniedPath = "/Account/Login";
        options.ExpireTimeSpan = TimeSpan.FromMinutes(Math.Max(5, adminAuthentication.IdleTimeoutMinutes));
        options.SlidingExpiration = true;
        options.Events.OnValidatePrincipal = async context =>
        {
            var startedValue = context.Principal?.FindFirst("opk:session_started")?.Value;
            var sessionVersion = context.Principal?.FindFirst("opk:session_version")?.Value;
            if (!long.TryParse(startedValue, out var startedSeconds) ||
                !string.Equals(sessionVersion, adminAuthentication.SessionVersion, StringComparison.Ordinal) ||
                DateTimeOffset.UtcNow - DateTimeOffset.FromUnixTimeSeconds(startedSeconds) >
                TimeSpan.FromHours(Math.Max(1, adminAuthentication.AbsoluteTimeoutHours)))
            {
                context.RejectPrincipal();
                await context.HttpContext.SignOutAsync(CookieAuthenticationDefaults.AuthenticationScheme);
            }
        };
    });
if (adminAuthentication.Mode == AdminAuthenticationMode.OpenIdConnect)
{
    authenticationBuilder.AddOpenIdConnect("OpenIdConnect", options =>
    {
        options.SignInScheme = CookieAuthenticationDefaults.AuthenticationScheme;
        options.Authority = adminAuthentication.Authority;
        options.ClientId = adminAuthentication.ClientId;
        options.ClientSecret = adminAuthentication.ClientSecret;
        options.CallbackPath = adminAuthentication.CallbackPath;
        options.ResponseType = "code";
        options.UsePkce = true;
        options.SaveTokens = false;
        options.GetClaimsFromUserInfoEndpoint = true;
        options.Scope.Clear();
        options.Scope.Add("openid");
        options.Scope.Add("profile");
        options.Scope.Add("email");
        options.Events.OnTokenValidated = async context =>
        {
            var identity = context.Principal?.Identity as System.Security.Claims.ClaimsIdentity;
            var hasRequiredRole = context.Principal?.Claims.Any(claim =>
                (claim.Type == "roles" || claim.Type == System.Security.Claims.ClaimTypes.Role) &&
                string.Equals(claim.Value, adminAuthentication.RequiredRole, StringComparison.Ordinal)) == true;
            if (identity is null || !hasRequiredRole)
            {
                context.Fail("The identity is not assigned the required OpenPortalKit administrator role.");
                await context.HttpContext.RequestServices.GetRequiredService<AuditRecorder>().RecordAsync(
                    new AuditRecordRequest(null, "admin.oidc.denied", "AdminAccount",
                        context.Principal?.Identity?.Name ?? "unknown",
                        MetadataJson: System.Text.Json.JsonSerializer.Serialize(new
                        {
                            TraceId = context.HttpContext.TraceIdentifier,
                            Reason = "required_role_missing"
                        })), context.HttpContext.RequestAborted);
                return;
            }
            identity.AddClaim(new System.Security.Claims.Claim(
                System.Security.Claims.ClaimTypes.Role, "Administrator"));
            identity.AddClaim(new System.Security.Claims.Claim(
                "opk:session_started", DateTimeOffset.UtcNow.ToUnixTimeSeconds().ToString(System.Globalization.CultureInfo.InvariantCulture)));
            identity.AddClaim(new System.Security.Claims.Claim("opk:session_version", adminAuthentication.SessionVersion));
            await context.HttpContext.RequestServices.GetRequiredService<AuditRecorder>().RecordAsync(
                new AuditRecordRequest(null, "admin.oidc.succeeded", "AdminAccount",
                    context.Principal?.Identity?.Name ?? "unknown",
                    MetadataJson: System.Text.Json.JsonSerializer.Serialize(new
                    {
                        TraceId = context.HttpContext.TraceIdentifier,
                        Authority = adminAuthentication.Authority
                    })), context.HttpContext.RequestAborted);
        };
        options.Events.OnRemoteFailure = async context =>
        {
            context.HttpContext.RequestServices.GetRequiredService<ILoggerFactory>()
                .CreateLogger("OpenPortalKit.AdminAuthentication")
                .LogWarning("OpenID Connect authentication failed. TraceId: {TraceId}; Error: {Error}",
                    context.HttpContext.TraceIdentifier,
                    context.Failure?.GetType().Name ?? "RemoteFailure");
            await context.HttpContext.RequestServices.GetRequiredService<AuditRecorder>().RecordAsync(
                new AuditRecordRequest(null, "admin.oidc.failed", "AdminAccount", "unknown",
                    MetadataJson: System.Text.Json.JsonSerializer.Serialize(new
                    {
                        TraceId = context.HttpContext.TraceIdentifier,
                        ErrorType = context.Failure?.GetType().Name ?? "RemoteFailure"
                    })), context.HttpContext.RequestAborted);
        };
    });
}
builder.Services.AddAuthorization(options =>
{
    if (adminAuthentication.RequireAuthentication)
    {
        options.FallbackPolicy = new AuthorizationPolicyBuilder()
            .RequireAuthenticatedUser()
            .RequireRole("Administrator")
            .Build();
    }
});
builder.Services.AddSingleton(industryPackCatalogResult);
builder.Services.AddOpenPortalKitProductionHardening(builder.Configuration, adminHost: true);
builder.Services.Configure<OpenPortalKitStorageOptions>(
    builder.Configuration.GetSection(OpenPortalKitStorageOptions.SectionName));
builder.Services.Configure<AnalyticsPrivacyOptions>(
    builder.Configuration.GetSection(AnalyticsPrivacyOptions.SectionName));
builder.Services.Configure<DashboardSummaryOptions>(
    builder.Configuration.GetSection(DashboardSummaryOptions.SectionName));
builder.Services.AddSingleton<IContentItemStore, InMemoryContentItemStore>();
builder.Services.AddSingleton<IDataSetStore, InMemoryDataSetStore>();
builder.Services.AddSingleton<IDataRecordStore, InMemoryDataRecordStore>();
builder.Services.AddSingleton<IAgentReadinessSignalProvider, ContentAgentReadinessSignalProvider>();
builder.Services.Configure<AgentBotPolicyOptions>(
    builder.Configuration.GetSection(AgentBotPolicyOptions.SectionName));
builder.Services.Configure<AgentOutputGenerationOptions>(
    builder.Configuration.GetSection(AgentOutputGenerationOptions.SectionName));
var persistencePostgres = builder.Configuration
    .GetSection(PostgresPersistenceOptions.SectionName)
    .Get<PostgresPersistenceOptions>() ?? new PostgresPersistenceOptions();
if (string.IsNullOrWhiteSpace(persistencePostgres.ConnectionString))
{
    persistencePostgres.ConnectionString = builder.Configuration.GetConnectionString(
        persistencePostgres.ConnectionStringName);
}

builder.Services.AddSingleton(persistencePostgres);
var dashboardPostgres = builder.Configuration
    .GetSection(DashboardPostgresStorageOptions.SectionName)
    .Get<DashboardPostgresStorageOptions>() ?? new DashboardPostgresStorageOptions();
if (string.IsNullOrWhiteSpace(dashboardPostgres.ConnectionString))
{
    dashboardPostgres.ConnectionString = builder.Configuration.GetConnectionString(
        dashboardPostgres.ConnectionStringName);
}

builder.Services.AddSingleton(dashboardPostgres);
var agentOutputPostgres = builder.Configuration
    .GetSection(AgentOutputPostgresStorageOptions.SectionName)
    .Get<AgentOutputPostgresStorageOptions>() ?? new AgentOutputPostgresStorageOptions();
if (string.IsNullOrWhiteSpace(agentOutputPostgres.ConnectionString))
{
    agentOutputPostgres.ConnectionString = builder.Configuration.GetConnectionString(
        agentOutputPostgres.ConnectionStringName);
}

builder.Services.AddSingleton(agentOutputPostgres);
builder.Services.AddSingleton(
    builder.Configuration
        .GetSection(AgentOutputGenerationOptions.SectionName)
        .Get<AgentOutputGenerationOptions>() ?? new AgentOutputGenerationOptions());
if (dashboardPostgres.Enabled)
{
    builder.Services.AddSingleton<IDashboardDbConnectionFactory, DashboardPostgresConnectionFactory>();
    builder.Services.AddSingleton<IAnalyticsEventStore, PostgresAnalyticsEventStore>();
    builder.Services.AddSingleton<IDashboardSnapshotStore, PostgresDashboardSnapshotStore>();
}
else
{
    builder.Services.AddSingleton<IAnalyticsEventStore, InMemoryAnalyticsEventStore>();
    builder.Services.AddSingleton<IDashboardSnapshotStore, InMemoryDashboardSnapshotStore>();
}
if (persistencePostgres.Enabled)
{
    builder.Services.AddSingleton<IOpenPortalKitDbConnectionFactory, PostgresOpenPortalKitDbConnectionFactory>();
    builder.Services.AddSingleton<IOutboxMessageStore, PostgresOutboxMessageStore>();
    builder.Services.AddSingleton<IIdempotencyStore, PostgresIdempotencyStore>();
    builder.Services.AddSingleton<IAuditLogStore, PostgresAuditLogStore>();
    builder.Services.AddSingleton<IPublicOutputRevalidationStore, PostgresPublicOutputRevalidationStore>();
    builder.Services.AddSingleton<IIndustryPackInstallationStore, PostgresIndustryPackInstallationStore>();
}
else
{
    builder.Services.AddSingleton<IOutboxMessageStore, InMemoryOutboxMessageStore>();
    builder.Services.AddSingleton<IIdempotencyStore, InMemoryIdempotencyStore>();
    builder.Services.AddSingleton<IAuditLogStore, InMemoryAuditLogStore>();
    builder.Services.AddSingleton<IPublicOutputRevalidationStore, InMemoryPublicOutputRevalidationStore>();
    builder.Services.AddSingleton<IIndustryPackInstallationStore, InMemoryIndustryPackInstallationStore>();
}

builder.Services.AddSingleton<AuditRecorder>();
builder.Services.AddSingleton<IBlockDefinitionCatalog, PredefinedBlockCatalog>();
if (persistencePostgres.Enabled)
{
    builder.Services.AddSingleton<IPageTemplateStore, PostgresPageTemplateStore>();
}
else
{
    builder.Services.AddSingleton<IPageTemplateStore, InMemoryPageTemplateStore>();
}

builder.Services.AddSingleton<PageTemplateService>();
if (persistencePostgres.Enabled)
{
    builder.Services.AddSingleton<IPageStore, PostgresPageStore>();
}
else
{
    builder.Services.AddSingleton<IPageStore, InMemoryPageStore>();
}

builder.Services.AddSingleton<PortalPageService>();
builder.Services.AddSingleton<IndustryPackRuntimeRegistry>();
foreach (var resourceKind in Enum.GetValues<IndustryPackResourceKind>())
{
    builder.Services.AddSingleton<IIndustryPackResourceRegistrationTarget>(provider =>
        new AdminIndustryPackRegistrationTarget(
            resourceKind,
            provider.GetRequiredService<IndustryPackRuntimeRegistry>(),
            provider.GetRequiredService<IBlockDefinitionCatalog>(),
            provider.GetRequiredService<PageTemplateService>(),
            provider.GetRequiredService<IDataSetStore>()));
}
builder.Services.AddSingleton<IndustryPackInstallationService>();
builder.Services.AddHostedService<IndustryPackRehydrationHostedService>();
builder.Services.AddSingleton<IPageBlockDataResolver, AdminPageBlockDataResolver>();
builder.Services.AddSingleton<ServerRenderedBlockPageRenderer>();
if (agentOutputPostgres.Enabled)
{
    builder.Services.AddSingleton<IAgentOutputDbConnectionFactory, AgentOutputPostgresConnectionFactory>();
    builder.Services.AddSingleton<IAgentOutputArtifactStore, PostgresAgentOutputArtifactStore>();
}
else
{
    builder.Services.AddSingleton<IAgentOutputArtifactStore, InMemoryAgentOutputArtifactStore>();
}

builder.Services.AddSingleton<PublishingEventAgentContentDocumentResolver>();
builder.Services.AddSingleton<ContentStoreAgentContentDocumentResolver>();
builder.Services.AddSingleton<IAgentContentDocumentResolver>(provider =>
    new FallbackAgentContentDocumentResolver(new IAgentContentDocumentResolver[]
    {
        provider.GetRequiredService<PublishingEventAgentContentDocumentResolver>(),
        provider.GetRequiredService<ContentStoreAgentContentDocumentResolver>()
    }));
builder.Services.AddSingleton<PublishingAgentOutputArtifactFactory>();
builder.Services.AddSingleton<IPublicOutputRegenerator>(provider =>
    new AgentOutputArtifactRegenerator(
        provider.GetRequiredService<IAgentOutputArtifactStore>(),
        provider.GetRequiredService<PublishingAgentOutputArtifactFactory>().CreateArtifactsAsync));
builder.Services.AddSingleton<IPublicOutputRevalidationExecutor>(provider =>
    new RecordingPublicOutputRevalidationExecutor(
        provider.GetRequiredService<IPublicOutputRevalidationStore>(),
        auditRecorder: provider.GetRequiredService<AuditRecorder>(),
        regenerator: provider.GetRequiredService<IPublicOutputRegenerator>()));
builder.Services.AddSingleton<PublishingRevalidationPlanner>();
builder.Services.AddSingleton<IOutboxMessageHandler, PublishingRevalidationOutboxHandler>();
builder.Services.AddSingleton<AnalyticsEventFactory>(provider =>
    new AnalyticsEventFactory(provider.GetRequiredService<IOptions<AnalyticsPrivacyOptions>>().Value));
builder.Services.AddSingleton<IDashboardSignalSource, SiteOperationsDashboardSignalSource>();
builder.Services.AddSingleton<IDashboardSignalSource, ContentDashboardSignalSource>();
builder.Services.AddSingleton<IDashboardSignalSource>(provider =>
    new DataPublishingDashboardSignalSource(
        provider.GetRequiredService<IDataSetStore>(),
        provider.GetRequiredService<IDataRecordStore>(),
        eventStore: provider.GetRequiredService<IAnalyticsEventStore>()));
builder.Services.AddSingleton<IDashboardSignalSource, AgentReadinessDashboardSignalSource>();
builder.Services.AddSingleton<IDashboardSignalSource, IndustryPackDashboardSignalSource>();
builder.Services.AddSingleton<IDashboardSignalSource, SystemRuntimeDashboardSignalSource>();
builder.Services.AddSingleton<IDashboardSignalSource, OutboxDashboardSignalSource>();
foreach (var probe in CreateDefaultHealthProbes(builder.Configuration))
{
    builder.Services.AddSingleton<IDashboardHealthProbe>(probe);
}

builder.Services.AddSingleton<IDashboardSignalSource, SystemHealthDashboardSignalSource>();
builder.Services.AddSingleton<DashboardAggregator>();
builder.Services.AddSingleton(provider =>
    provider.GetRequiredService<IOptions<DashboardSummaryOptions>>().Value);
builder.Services.AddSingleton<DashboardSummaryService>();
builder.Services.AddSingleton<DashboardTelemetryPublisher>();

var app = builder.Build();

// Configure the HTTP request pipeline.
if (!app.Environment.IsDevelopment())
{
    app.UseExceptionHandler("/Error");
}

app.UseRouting();
app.UseOpenPortalKitProductionHardening();

app.UseAuthentication();
app.UseAuthorization();
app.UseAntiforgery();

app.MapStaticAssets().AllowAnonymous();
app.MapOpenPortalKitHealthEndpoints();
app.MapGet("/admin/system/modules", () => new[]
{
    IdentityModule.Descriptor,
    IndustryPacksModule.Descriptor,
    ContentModule.Descriptor,
    AssetsModule.Descriptor,
    WorkflowModule.Descriptor,
    DataModule.Descriptor,
    SearchModule.Descriptor,
    SeoModule.Descriptor,
    AgentAccessModule.Descriptor,
    DashboardModule.Descriptor,
    AuditModule.Descriptor,
    JobsModule.Descriptor
});
app.MapGet("/admin/system/storage", (IConfiguration configuration) =>
{
    var storage = configuration
        .GetSection(OpenPortalKitStorageOptions.SectionName)
        .Get<OpenPortalKitStorageOptions>() ?? new OpenPortalKitStorageOptions();

    return new
    {
        storage.Provider,
        storage.PrimaryConnectionStringName,
        PrimaryConnectionConfigured = !string.IsNullOrWhiteSpace(
            configuration.GetConnectionString(storage.PrimaryConnectionStringName)),
        storage.CacheConnectionStringName,
        CacheConnectionConfigured = !string.IsNullOrWhiteSpace(
            configuration.GetConnectionString(storage.CacheConnectionStringName))
    };
});
app.MapGet("/admin/dashboard/summary", async (
    bool? refresh,
    DashboardSummaryService summaryService,
    DashboardTelemetryPublisher telemetryPublisher,
    CancellationToken cancellationToken) =>
{
    var snapshot = await summaryService.GetSnapshotAsync(
        new DashboardSummaryRequest(refresh ?? false),
        cancellationToken);
    telemetryPublisher.Publish(snapshot);

    return snapshot.Summary;
});
app.MapGet("/admin/dashboard/snapshot", async (
    bool? refresh,
    DashboardSummaryService summaryService,
    DashboardTelemetryPublisher telemetryPublisher,
    CancellationToken cancellationToken) =>
{
    var snapshot = await summaryService.GetSnapshotAsync(
        new DashboardSummaryRequest(refresh ?? false),
        cancellationToken);
    telemetryPublisher.Publish(snapshot);

    return new
    {
        snapshot.Id,
        snapshot.CreatedAt,
        snapshot.ExpiresAt,
        snapshot.SourceChecksum,
        snapshot.Summary.GeneratedAt,
        CardCount = snapshot.Summary.Cards.Count,
        AlertCount = snapshot.Summary.Cards.Sum(card => card.Alerts.Count) + snapshot.Summary.Alerts.Count,
        snapshot.Summary.ActionableAlertCount
    };
});
app.MapGet("/admin/dashboard/metrics.prometheus", async (
    bool? refresh,
    DashboardSummaryService summaryService,
    DashboardTelemetryPublisher telemetryPublisher,
    CancellationToken cancellationToken) =>
{
    var snapshot = await summaryService.GetSnapshotAsync(
        new DashboardSummaryRequest(refresh ?? false),
        cancellationToken);
    telemetryPublisher.Publish(snapshot);

    return Results.Text(
        DashboardPrometheusExporter.Export(snapshot),
        "text/plain; version=0.0.4; charset=utf-8");
});
app.MapGet("/admin/analytics/privacy", (IOptions<AnalyticsPrivacyOptions> options) => new
{
    options.Value.AnonymizeIpAddresses,
    options.Value.RetentionDays,
    options.Value.AllowCrossSiteTracking,
    options.Value.AllowThirdPartyCookies,
    BotUserAgentKeywords = options.Value.BotUserAgentKeywords,
    SessionHashSaltConfigured = !string.IsNullOrWhiteSpace(options.Value.SessionHashSalt)
});
app.MapGet("/admin/analytics/events", async (
    string? siteId,
    string? eventType,
    DateTimeOffset? from,
    DateTimeOffset? to,
    int? take,
    IAnalyticsEventStore store,
    CancellationToken cancellationToken) =>
{
    var events = await store.ListAsync(
        new AnalyticsEventQuery(siteId, eventType, from, to, Take: Math.Clamp(take ?? 100, 1, 1000)),
        cancellationToken);

    return events.Select(item => new
    {
        item.Id,
        item.SiteId,
        item.EventType,
        item.Path,
        item.HashedSessionId,
        item.OccurredAt,
        item.Referrer,
        item.UserAgent,
        item.AnonymizedIpAddress,
        item.IsBot,
        item.Metadata
    });
});
app.MapPost("/analytics/events", async (
    AnalyticsEventCaptureRequest request,
    HttpContext context,
    AnalyticsEventFactory factory,
    IAnalyticsEventStore store,
    IOptions<AnalyticsPrivacyOptions> options,
    CancellationToken cancellationToken) =>
{
    if (string.IsNullOrWhiteSpace(request.SiteId) ||
        string.IsNullOrWhiteSpace(request.EventType) ||
        string.IsNullOrWhiteSpace(request.Path))
    {
        return Results.BadRequest(new { Error = "siteId, eventType, and path are required." });
    }

    var sessionId = ResolveSessionId(request, context);
    var occurredAt = request.OccurredAt ?? DateTimeOffset.UtcNow;
    var analyticsEvent = factory.Create(
        request.SiteId,
        request.EventType,
        request.Path,
        sessionId,
        occurredAt,
        request.Referrer,
        request.UserAgent ?? context.Request.Headers.UserAgent.ToString(),
        request.IpAddress ?? context.Connection.RemoteIpAddress?.ToString(),
        request.Metadata);

    await store.AddAsync(analyticsEvent, cancellationToken);
    await store.DeleteOlderThanAsync(
        DateTimeOffset.UtcNow.AddDays(-Math.Max(1, options.Value.RetentionDays)),
        cancellationToken);

    return Results.Accepted($"/admin/analytics/events?siteId={Uri.EscapeDataString(request.SiteId)}", new
    {
        analyticsEvent.Id,
        analyticsEvent.SiteId,
        analyticsEvent.EventType,
        analyticsEvent.Path,
        analyticsEvent.OccurredAt,
        analyticsEvent.IsBot
    });
}).AddEndpointFilter(async (invocationContext, next) =>
{
    await invocationContext.HttpContext.RequestServices
        .GetRequiredService<Microsoft.AspNetCore.Antiforgery.IAntiforgery>()
        .ValidateRequestAsync(invocationContext.HttpContext);
    return await next(invocationContext);
});
app.MapRazorPages()
   .WithStaticAssets();

app.Run();

static string ResolveSessionId(AnalyticsEventCaptureRequest request, HttpContext context)
{
    if (!string.IsNullOrWhiteSpace(request.SessionId))
    {
        return request.SessionId;
    }

    var headerValue = context.Request.Headers["X-OpenPortalKit-Session"].FirstOrDefault();
    if (!string.IsNullOrWhiteSpace(headerValue))
    {
        return headerValue;
    }

    var userAgent = context.Request.Headers.UserAgent.ToString();
    var remoteIp = context.Connection.RemoteIpAddress?.ToString() ?? "unknown";
    return remoteIp + ":" + userAgent;
}

static IReadOnlyList<IDashboardHealthProbe> CreateDefaultHealthProbes(IConfiguration configuration)
{
    var storage = configuration
        .GetSection(OpenPortalKitStorageOptions.SectionName)
        .Get<OpenPortalKitStorageOptions>() ?? new OpenPortalKitStorageOptions();
    var primaryConnectionConfigured = !string.IsNullOrWhiteSpace(
        configuration.GetConnectionString(storage.PrimaryConnectionStringName));
    var cacheConnectionConfigured = !string.IsNullOrWhiteSpace(
        configuration.GetConnectionString(storage.CacheConnectionStringName));
    var usesInMemoryStorage = string.Equals(storage.Provider, "InMemory", StringComparison.OrdinalIgnoreCase);

    return new IDashboardHealthProbe[]
    {
        new StaticDashboardHealthProbe(
            "database",
            "Database",
            usesInMemoryStorage || primaryConnectionConfigured
                ? DashboardHealthProbeStatus.Healthy
                : DashboardHealthProbeStatus.Unhealthy,
            TimeSpan.Zero,
            usesInMemoryStorage || primaryConnectionConfigured
                ? "Primary data store is configured."
                : "Primary database connection is not configured.",
            "/admin/system/storage"),
        new StaticDashboardHealthProbe(
            "redis",
            "Redis",
            cacheConnectionConfigured
                ? DashboardHealthProbeStatus.Healthy
                : DashboardHealthProbeStatus.Degraded,
            TimeSpan.Zero,
            cacheConnectionConfigured
                ? "Cache connection is configured."
                : "Redis cache is not configured; in-memory cache behavior is expected for local development.",
            "/admin/system/storage"),
        new StaticDashboardHealthProbe(
            "search",
            "Search provider",
            DashboardHealthProbeStatus.Healthy,
            TimeSpan.Zero,
            "Search module is registered."),
        new StaticDashboardHealthProbe(
            "storage",
            "Object storage",
            usesInMemoryStorage || primaryConnectionConfigured
                ? DashboardHealthProbeStatus.Healthy
                : DashboardHealthProbeStatus.Degraded,
            TimeSpan.Zero,
            usesInMemoryStorage || primaryConnectionConfigured
                ? "Storage provider is configured."
                : "Persistent storage is not fully configured.",
            "/admin/system/storage")
    };
}

public sealed record AnalyticsEventCaptureRequest(
    string SiteId,
    string EventType,
    string Path,
    string? SessionId = null,
    DateTimeOffset? OccurredAt = null,
    string? Referrer = null,
    string? UserAgent = null,
    string? IpAddress = null,
    IReadOnlyDictionary<string, string>? Metadata = null);
