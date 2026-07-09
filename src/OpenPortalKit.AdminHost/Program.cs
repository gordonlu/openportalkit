using OpenPortalKit.Kernel.Configuration;
using OpenPortalKit.Kernel.Events;
using OpenPortalKit.Modules.AgentAccess;
using OpenPortalKit.Modules.Assets;
using OpenPortalKit.Modules.Audit;
using OpenPortalKit.Modules.Content;
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
using OpenPortalKit.Modules.Jobs;
using OpenPortalKit.Modules.Search;
using OpenPortalKit.Modules.Seo;
using OpenPortalKit.Modules.Workflow;
using Microsoft.Extensions.Options;

var builder = WebApplication.CreateBuilder(args);

// Add services to the container.
builder.Services.AddRazorPages();
builder.Services.AddHealthChecks();
builder.Services.Configure<OpenPortalKitStorageOptions>(
    builder.Configuration.GetSection(OpenPortalKitStorageOptions.SectionName));
builder.Services.Configure<AnalyticsPrivacyOptions>(
    builder.Configuration.GetSection(AnalyticsPrivacyOptions.SectionName));
builder.Services.Configure<DashboardSummaryOptions>(
    builder.Configuration.GetSection(DashboardSummaryOptions.SectionName));
builder.Services.AddSingleton<IContentItemStore, InMemoryContentItemStore>();
builder.Services.AddSingleton<IDataSetStore, InMemoryDataSetStore>();
builder.Services.AddSingleton<IDataRecordStore, InMemoryDataRecordStore>();
builder.Services.AddSingleton<IOutboxMessageStore, InMemoryOutboxMessageStore>();
builder.Services.AddSingleton<IAgentReadinessSignalProvider, ContentAgentReadinessSignalProvider>();
var dashboardPostgres = builder.Configuration
    .GetSection(DashboardPostgresStorageOptions.SectionName)
    .Get<DashboardPostgresStorageOptions>() ?? new DashboardPostgresStorageOptions();
if (string.IsNullOrWhiteSpace(dashboardPostgres.ConnectionString))
{
    dashboardPostgres.ConnectionString = builder.Configuration.GetConnectionString(
        dashboardPostgres.ConnectionStringName);
}

builder.Services.AddSingleton(dashboardPostgres);
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
    // The default HSTS value is 30 days. You may want to change this for production scenarios, see https://aka.ms/aspnetcore-hsts.
    app.UseHsts();
}

app.UseHttpsRedirection();

app.UseRouting();

app.UseAuthorization();

app.MapStaticAssets();
app.MapHealthChecks("/health");
app.MapGet("/admin/system/modules", () => new[]
{
    IdentityModule.Descriptor,
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
