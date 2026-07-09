using System.Diagnostics.Metrics;
using OpenPortalKit.Kernel.Events;
using OpenPortalKit.Modules.Content.ContentItems;
using OpenPortalKit.Modules.Dashboard.Analytics;
using OpenPortalKit.Modules.Dashboard.Observability;
using OpenPortalKit.Modules.Dashboard.Sources;
using OpenPortalKit.Modules.Dashboard.Storage;
using OpenPortalKit.Modules.Dashboard.Summaries;
using OpenPortalKit.Modules.Data.Datasets;

var tests = new (string Name, Func<Task> Run)[]
{
    ("dashboard aggregates source signals into stable cards", DashboardAggregatesSourceSignalsIntoStableCards),
    ("dashboard preserves actionable alerts", DashboardPreservesActionableAlerts),
    ("analytics factory hashes session and anonymizes IP", AnalyticsFactoryHashesSessionAndAnonymizesIp),
    ("analytics store prunes events by retention cutoff", AnalyticsStorePrunesEventsByRetentionCutoff),
    ("analytics privacy defaults avoid cross-site tracking", AnalyticsPrivacyDefaultsAvoidCrossSiteTracking),
    ("analytics factory classifies bot traffic", AnalyticsFactoryClassifiesBotTraffic),
    ("content dashboard source aggregates publishing state", ContentDashboardSourceAggregatesPublishingState),
    ("data dashboard source reports freshness and traceability", DataDashboardSourceReportsFreshnessAndTraceability),
    ("outbox dashboard source reports backlog age", OutboxDashboardSourceReportsBacklogAge),
    ("site operations dashboard source summarizes privacy events", SiteOperationsDashboardSourceSummarizesPrivacyEvents),
    ("content agent readiness provider derives public page signals", ContentAgentReadinessProviderDerivesPublicPageSignals),
    ("agent readiness dashboard source reports readiness and agent traffic", AgentReadinessDashboardSourceReportsReadinessAndAgentTraffic),
    ("system runtime dashboard source reports operational events", SystemRuntimeDashboardSourceReportsOperationalEvents),
    ("system health dashboard source reports dependency status", SystemHealthDashboardSourceReportsDependencyStatus),
    ("dashboard summary service reuses fresh snapshot", DashboardSummaryServiceReusesFreshSnapshot),
    ("prometheus exporter emits dashboard metrics", PrometheusExporterEmitsDashboardMetrics),
    ("dashboard telemetry publisher emits meter measurements", DashboardTelemetryPublisherEmitsMeterMeasurements),
    ("postgres migration preserves dashboard privacy and indexes", PostgresMigrationPreservesDashboardPrivacyAndIndexes),
    ("postgres store SQL preserves dashboard privacy", PostgresStoreSqlPreservesDashboardPrivacy),
    ("admin dashboard postgres storage is explicitly configurable", AdminDashboardPostgresStorageIsExplicitlyConfigurable),
    ("admin dashboard registers R7 signal sources", AdminDashboardRegistersR7SignalSources),
    ("api host captures public analytics runtime events", ApiHostCapturesPublicAnalyticsRuntimeEvents)
};

var failed = 0;

foreach (var test in tests)
{
    try
    {
        await test.Run();
        Console.WriteLine($"PASS {test.Name}");
    }
    catch (Exception exception)
    {
        failed++;
        Console.Error.WriteLine($"FAIL {test.Name}: {exception.Message}");
    }
}

return failed == 0 ? 0 : 1;

static async Task DashboardAggregatesSourceSignalsIntoStableCards()
{
    var observedAt = new DateTimeOffset(2026, 7, 8, 12, 0, 0, TimeSpan.Zero);
    var aggregator = new DashboardAggregator(new IDashboardSignalSource[]
    {
        new StaticDashboardSignalSource(
            "Search",
            new[]
            {
                Metric("system.failedJobs", "Failed jobs", DashboardArea.SystemHealth, "system.background", "System health", 0, "jobs", observedAt, "Jobs", 20),
                Metric("system.outboxBacklog", "Outbox backlog", DashboardArea.SystemHealth, "system.background", "System health", 3, "messages", observedAt, "Jobs", 10)
            }),
        new StaticDashboardSignalSource(
            "Content",
            new[]
            {
                Metric("content.reviewQueue", "Review queue", DashboardArea.Content, "content.publishing", "Content publishing", 6, "items", observedAt, "Content", 20),
                Metric("content.draftCount", "Drafts", DashboardArea.Content, "content.publishing", "Content publishing", 12, "items", observedAt, "Content", 10)
            })
    });

    var summary = await aggregator.BuildSummaryAsync(observedAt);

    Assert.Equal(2, summary.Cards.Count);
    Assert.Equal("content.publishing", summary.Cards[0].Code);
    Assert.Equal(DashboardArea.Content, summary.Cards[0].Area);
    Assert.Equal("content.draftCount", summary.Cards[0].Metrics[0].Code);
    Assert.Equal("system.background", summary.Cards[1].Code);
}

static async Task DashboardPreservesActionableAlerts()
{
    var observedAt = new DateTimeOffset(2026, 7, 8, 12, 0, 0, TimeSpan.Zero);
    var aggregator = new DashboardAggregator(new IDashboardSignalSource[]
    {
        new StaticDashboardSignalSource(
            "Content",
            new[]
            {
                Metric("content.missingMetadata", "Missing metadata", DashboardArea.Content, "content.readiness", "Content readiness", 2, "items", observedAt, "Content", 10)
            },
            new[]
            {
                Alert("content.metadata", "Some public content is missing metadata.", DashboardArea.Content, "content.readiness", "Content readiness", DashboardAlertLevel.Warning, "Content", observedAt, "/Content"),
                Alert("content.cover", "Some public content is missing cover media.", DashboardArea.Content, "content.readiness", "Content readiness", DashboardAlertLevel.Info, "Content", observedAt, null)
            })
    });

    var summary = await aggregator.BuildSummaryAsync(observedAt);

    Assert.Equal(1, summary.Cards.Count);
    Assert.Equal(2, summary.Cards[0].Alerts.Count);
    Assert.Equal("content.metadata", summary.Cards[0].Alerts[0].Code);
    Assert.True(summary.Cards[0].Alerts[0].IsActionable, "Expected alert to be actionable.");
    Assert.Equal(1, summary.ActionableAlertCount);
}

static Task AnalyticsFactoryHashesSessionAndAnonymizesIp()
{
    var factory = new AnalyticsEventFactory(new AnalyticsPrivacyOptions
    {
        SessionHashSalt = "test-salt"
    });

    var first = factory.Create(
        "site-1",
        "page_view",
        "/articles/launch",
        "session-123",
        new DateTimeOffset(2026, 7, 8, 12, 0, 0, TimeSpan.Zero),
        ipAddress: "203.0.113.42");
    var second = factory.Create(
        "site-1",
        "page_view",
        "/articles/launch",
        "session-123",
        new DateTimeOffset(2026, 7, 8, 12, 1, 0, TimeSpan.Zero),
        ipAddress: "203.0.113.42");

    Assert.NotEqual("session-123", first.HashedSessionId);
    Assert.Equal(first.HashedSessionId, second.HashedSessionId);
    Assert.Equal("203.0.113.0", first.AnonymizedIpAddress);

    return Task.CompletedTask;
}

static async Task AnalyticsStorePrunesEventsByRetentionCutoff()
{
    var store = new InMemoryAnalyticsEventStore();
    var factory = new AnalyticsEventFactory(new AnalyticsPrivacyOptions());
    var cutoff = new DateTimeOffset(2026, 7, 1, 0, 0, 0, TimeSpan.Zero);

    await store.AddAsync(factory.Create(
        "site-1",
        "page_view",
        "/old",
        "session-old",
        cutoff.AddTicks(-1)));
    await store.AddAsync(factory.Create(
        "site-1",
        "page_view",
        "/new",
        "session-new",
        cutoff));

    var removed = await store.DeleteOlderThanAsync(cutoff);
    var remaining = await store.ListAsync(new AnalyticsEventQuery(SiteId: "site-1"));

    Assert.Equal(1, removed);
    Assert.Equal(1, remaining.Count);
    Assert.Equal("/new", remaining[0].Path);
}

static Task AnalyticsPrivacyDefaultsAvoidCrossSiteTracking()
{
    var options = new AnalyticsPrivacyOptions();

    Assert.False(options.AllowCrossSiteTracking, "Cross-site tracking should be disabled by default.");
    Assert.False(options.AllowThirdPartyCookies, "Third-party cookies should be disabled by default.");
    Assert.True(options.AnonymizeIpAddresses, "IP anonymization should be enabled by default.");
    Assert.True(options.RetentionDays > 0, "Retention period should be explicit.");

    return Task.CompletedTask;
}

static Task AnalyticsFactoryClassifiesBotTraffic()
{
    var factory = new AnalyticsEventFactory(new AnalyticsPrivacyOptions());
    var botEvent = factory.Create(
        "site-1",
        "page_view",
        "/",
        "session-1",
        new DateTimeOffset(2026, 7, 8, 12, 0, 0, TimeSpan.Zero),
        userAgent: "ExampleBot/1.0");

    Assert.True(botEvent.IsBot, "Expected bot user agent to be classified.");

    return Task.CompletedTask;
}

static async Task ContentDashboardSourceAggregatesPublishingState()
{
    var siteId = Guid.NewGuid();
    var store = new InMemoryContentItemStore();
    var observedAt = new DateTimeOffset(2026, 7, 8, 12, 0, 0, TimeSpan.Zero);
    var contentTypeId = Guid.NewGuid();
    var authorId = Guid.NewGuid();
    var categoryId = Guid.NewGuid();

    await store.AddAsync(Content(
        siteId,
        "published-current",
        ContentPublicationStatus.Published,
        observedAt.AddHours(-2),
        publishedAt: observedAt.AddHours(-1),
        summary: "Ready",
        coverAssetId: Guid.NewGuid(),
        source: "editorial",
        contentTypeId: contentTypeId,
        authorId: authorId,
        categoryId: categoryId));
    await store.AddAsync(Content(
        siteId,
        "published-stale",
        ContentPublicationStatus.Published,
        observedAt.AddDays(-120),
        publishedAt: observedAt.AddDays(-120),
        summary: "",
        coverAssetId: null,
        contentTypeId: contentTypeId,
        authorId: authorId,
        categoryId: categoryId));
    await store.AddAsync(Content(siteId, "draft", ContentPublicationStatus.Draft, observedAt.AddMinutes(-10)));
    await store.AddAsync(Content(siteId, "review", ContentPublicationStatus.Review, observedAt.AddMinutes(-5)));
    await store.AddAsync(Content(siteId, "archived", ContentPublicationStatus.Archived, observedAt.AddDays(-3)));

    var source = new ContentDashboardSignalSource(
        store,
        siteId,
        staleAfterDays: 90,
        clock: () => observedAt);

    var signals = await source.CollectAsync();

    Assert.Equal(14, signals.Metrics.Count);
    Assert.Equal(1, Value(signals, "content.reviewQueue"));
    Assert.Equal(1, Value(signals, "content.publishedToday"));
    Assert.Equal(1, Value(signals, "content.staleCount"));
    Assert.Equal(1, Value(signals, "content.missingSeoMetadata"));
    Assert.Equal(1, Value(signals, "content.missingSummary"));
    Assert.Equal(1, Value(signals, "content.missingCover"));
    Assert.Equal(1, Value(signals, "content.missingAgentSnapshots"));
    Assert.Equal(2, Value(signals, "content.topContentTypeCount"));
    Assert.Equal(2, Value(signals, "content.topAuthorCount"));
    Assert.Equal(2, Value(signals, "content.topCategoryCount"));
    Assert.True(signals.Alerts.Any(alert => alert.Code == "content.stale"), "Expected stale content alert.");
    Assert.True(signals.Alerts.Any(alert => alert.Code == "content.seoMetadata"), "Expected SEO metadata alert.");
    Assert.True(signals.Alerts.Any(alert => alert.Code == "content.agentSnapshots"), "Expected AgentSEO snapshot alert.");
}

static async Task DataDashboardSourceReportsFreshnessAndTraceability()
{
    var siteId = Guid.NewGuid();
    var dataSetStore = new InMemoryDataSetStore();
    var recordStore = new InMemoryDataRecordStore();
    var eventStore = new InMemoryAnalyticsEventStore();
    var factory = new AnalyticsEventFactory(new AnalyticsPrivacyOptions
    {
        SessionHashSalt = "data-test"
    });
    var observedAt = new DateTimeOffset(2026, 7, 8, 12, 0, 0, TimeSpan.Zero);
    var currentSet = DataSet(siteId, "current", true, observedAt);
    var staleSet = DataSet(siteId, "stale", true, observedAt);
    var privateSet = DataSet(siteId, "private", false, observedAt);
    var otherSiteSet = DataSet(Guid.NewGuid(), "other", true, observedAt);

    await dataSetStore.AddDataSetAsync(currentSet);
    await dataSetStore.AddDataSetAsync(staleSet);
    await dataSetStore.AddDataSetAsync(privateSet);
    await dataSetStore.AddDataSetAsync(otherSiteSet);
    await recordStore.UpsertAsync(Record(currentSet.Id, "a", new DateOnly(2026, 7, 8), "import"));
    await recordStore.UpsertAsync(Record(staleSet.Id, "b", new DateOnly(2026, 5, 1), ""));
    await recordStore.UpsertAsync(Record(privateSet.Id, "c", new DateOnly(2026, 5, 1), "private"));
    await recordStore.UpsertAsync(Record(otherSiteSet.Id, "d", new DateOnly(2026, 7, 8), "other"));
    await eventStore.AddAsync(factory.Create(
        "site-1",
        "data_import",
        "/admin/data/imports/current",
        "import-a",
        observedAt.AddMinutes(-20),
        metadata: new Dictionary<string, string>
        {
            ["status"] = "failed",
            ["quality_status"] = "failed"
        }));
    await eventStore.AddAsync(factory.Create(
        "site-1",
        "api_request",
        "/api/public/datasets/current",
        "session-a",
        observedAt.AddMinutes(-10),
        metadata: new Dictionary<string, string>
        {
            ["status_code"] = "200",
            ["dataset_code"] = "current"
        }));
    await eventStore.AddAsync(factory.Create(
        "site-1",
        "api_request",
        "/api/public/datasets/current/export.csv",
        "session-b",
        observedAt.AddMinutes(-8),
        metadata: new Dictionary<string, string>
        {
            ["status_code"] = "200",
            ["dataset_code"] = "current"
        }));
    await eventStore.AddAsync(factory.Create(
        "site-1",
        "dataset_export",
        "/api/public/datasets/stale/export.csv",
        "session-c",
        observedAt.AddMinutes(-7),
        metadata: new Dictionary<string, string>
        {
            ["dataset_code"] = "stale"
        }));

    var source = new DataPublishingDashboardSignalSource(
        dataSetStore,
        recordStore,
        siteId,
        staleAfterDays: 30,
        clock: () => observedAt,
        eventStore: eventStore);

    var signals = await source.CollectAsync();

    Assert.Equal(3, Value(signals, "data.datasetCount"));
    Assert.Equal(2, Value(signals, "data.publicDatasetCount"));
    Assert.Equal(3, Value(signals, "data.recordCount"));
    Assert.Equal(3, Value(signals, "data.importBatchCount"));
    Assert.Equal(3, Value(signals, "data.importSuccessCount"));
    Assert.Equal(1, Value(signals, "data.importFailureCount"));
    Assert.Equal(1, Value(signals, "data.qualityFailureCount"));
    Assert.Equal(1, Value(signals, "data.staleDatasetCount"));
    Assert.Equal(1, Value(signals, "data.missingSourceCount"));
    Assert.Equal(0, Value(signals, "data.missingAsOfDateCount"));
    Assert.Equal(1, Value(signals, "data.latestSnapshotStatus"));
    Assert.Equal(2, Value(signals, "data.datasetApiRequestCount"));
    Assert.Equal(2, Value(signals, "data.datasetExportCount"));
    Assert.Equal(1, Value(signals, "data.topDatasetRecordCount"));
    Assert.Equal(2, Value(signals, "data.topDatasetRequestCount"));
    Assert.True(signals.Alerts.Any(alert => alert.Code == "data.source"), "Expected missing source alert.");
    Assert.True(signals.Alerts.Any(alert => alert.Code == "data.importFailures"), "Expected import failure alert.");
}

static async Task OutboxDashboardSourceReportsBacklogAge()
{
    var outbox = new InMemoryOutboxMessageStore();
    var observedAt = new DateTimeOffset(2026, 7, 8, 12, 0, 0, TimeSpan.Zero);
    await outbox.AddAsync(new OutboxMessage(
        Guid.NewGuid(),
        "ContentPublished",
        "{}",
        "content:1",
        observedAt.AddMinutes(-42),
        ProcessedAt: null,
        AttemptCount: 0,
        LastError: null));
    await outbox.AddAsync(new OutboxMessage(
        Guid.NewGuid(),
        "ContentPublished",
        "{}",
        "content:2",
        observedAt.AddMinutes(-4),
        ProcessedAt: observedAt.AddMinutes(-1),
        AttemptCount: 0,
        LastError: null));

    var source = new OutboxDashboardSignalSource(
        outbox,
        warningThreshold: 1,
        clock: () => observedAt);

    var signals = await source.CollectAsync();

    Assert.Equal(1, Value(signals, "system.outboxBacklog"));
    Assert.Equal(42, Value(signals, "system.outboxOldestPendingMinutes"));
    Assert.True(signals.Alerts.Any(alert => alert.Code == "system.outboxBacklog"), "Expected backlog alert.");
}

static async Task SiteOperationsDashboardSourceSummarizesPrivacyEvents()
{
    var observedAt = new DateTimeOffset(2026, 7, 8, 12, 0, 0, TimeSpan.Zero);
    var store = new InMemoryAnalyticsEventStore();
    var factory = new AnalyticsEventFactory(new AnalyticsPrivacyOptions
    {
        SessionHashSalt = "ops-test"
    });

    await store.AddAsync(factory.Create(
        "site-1",
        "page_view",
        "/",
        "session-a",
        observedAt.AddMinutes(-10),
        referrer: "https://search.example/results",
        ipAddress: "203.0.113.8",
        metadata: new Dictionary<string, string>
        {
            ["traffic_source"] = "search",
            ["search_keyword"] = "portal",
            ["is_entry"] = "true",
            ["latency_ms"] = "50"
        }));
    await store.AddAsync(factory.Create(
        "site-1",
        "page_view",
        "/",
        "session-d",
        observedAt.AddMinutes(-9),
        metadata: new Dictionary<string, string>
        {
            ["traffic_source"] = "direct",
            ["is_exit"] = "true",
            ["latency_ms"] = "2500"
        }));
    await store.AddAsync(factory.Create(
        "site-1",
        "page_view",
        "/missing",
        "session-a",
        observedAt.AddMinutes(-8),
        ipAddress: "203.0.113.8",
        metadata: new Dictionary<string, string>
        {
            ["status_code"] = "404",
            ["is_exit"] = "true"
        }));
    await store.AddAsync(factory.Create(
        "site-1",
        "page_view",
        "/agent/readiness",
        "session-b",
        observedAt.AddMinutes(-7),
        userAgent: "ExampleBot/1.0",
        metadata: new Dictionary<string, string>
        {
            ["traffic_source"] = "agent",
            ["latency_ms"] = "100"
        }));
    await store.AddAsync(factory.Create(
        "site-1",
        "download",
        "/files/report.pdf",
        "session-b",
        observedAt.AddMinutes(-6)));
    await store.AddAsync(factory.Create(
        "site-1",
        "form_submission",
        "/contact",
        "session-c",
        observedAt.AddMinutes(-5)));
    await store.AddAsync(factory.Create(
        "site-1",
        "activity_registration",
        "/activities/intro",
        "session-c",
        observedAt.AddMinutes(-4)));
    await store.AddAsync(factory.Create(
        "other-site",
        "page_view",
        "/",
        "session-x",
        observedAt.AddMinutes(-5)));

    var source = new SiteOperationsDashboardSignalSource(
        store,
        "site-1",
        clock: () => observedAt);

    var signals = await source.CollectAsync();

    Assert.Equal(4, Value(signals, "site.pageViews"));
    Assert.Equal(3, Value(signals, "site.uniqueVisitors"));
    Assert.Equal(1, Value(signals, "site.botPageViews"));
    Assert.Equal(1, Value(signals, "site.notFoundPages"));
    Assert.Equal(1, Value(signals, "site.downloads"));
    Assert.Equal(1, Value(signals, "site.formSubmissions"));
    Assert.Equal(1, Value(signals, "site.activityRegistrations"));
    Assert.Equal(2, Value(signals, "site.topPageViews"));
    Assert.Equal(1, Value(signals, "site.topSectionViews"));
    Assert.Equal(3, Value(signals, "site.trafficSources"));
    Assert.Equal(1, Value(signals, "site.searchKeywords"));
    Assert.Equal(1, Value(signals, "site.topEntryPageViews"));
    Assert.Equal(1, Value(signals, "site.topExitPageViews"));
    Assert.Equal(1, Value(signals, "site.slowPages"));
    Assert.Equal("/", Description(signals, "site.topPageViews"));
    Assert.Equal("agent", Description(signals, "site.topSectionViews"));
    Assert.Equal("agent", Description(signals, "site.trafficSources"));
    Assert.Equal("portal", Description(signals, "site.searchKeywords"));
    Assert.True(signals.Alerts.Any(alert => alert.Code == "site.notFoundPages"), "Expected 404 alert.");
    Assert.True(signals.Alerts.Any(alert => alert.Code == "site.slowPages"), "Expected slow page alert.");
}

static async Task ContentAgentReadinessProviderDerivesPublicPageSignals()
{
    var siteId = Guid.NewGuid();
    var store = new InMemoryContentItemStore();
    var observedAt = new DateTimeOffset(2026, 7, 8, 12, 0, 0, TimeSpan.Zero);

    await store.AddAsync(Content(
        siteId,
        "ready",
        ContentPublicationStatus.Published,
        observedAt.AddHours(-2),
        publishedAt: observedAt.AddHours(-2),
        summary: "Ready summary",
        body: "Ready body",
        source: "editorial"));
    await store.AddAsync(Content(
        siteId,
        "thin",
        ContentPublicationStatus.Published,
        observedAt.AddHours(-1),
        publishedAt: observedAt.AddHours(-1),
        summary: "",
        body: "",
        source: null));
    await store.AddAsync(Content(
        siteId,
        "draft",
        ContentPublicationStatus.Draft,
        observedAt.AddMinutes(-30),
        summary: "Draft",
        body: "Draft",
        source: "editorial"));
    await store.AddAsync(Content(
        Guid.NewGuid(),
        "other-site",
        ContentPublicationStatus.Published,
        observedAt.AddMinutes(-20),
        publishedAt: observedAt.AddMinutes(-20),
        summary: "Other",
        body: "Other",
        source: "editorial"));

    var provider = new ContentAgentReadinessSignalProvider(
        store,
        siteId,
        clock: () => observedAt);

    var signals = await provider.ListAsync();

    Assert.Equal(2, signals.Count);
    var ready = signals.First(signal => signal.Url == "/content/ready");
    var thin = signals.First(signal => signal.Url == "/content/thin");

    Assert.Equal(100, ready.ReadinessScore);
    Assert.True(ready.HasMarkdownSnapshot, "Expected complete content to have Markdown snapshot readiness.");
    Assert.True(ready.HasJsonSnapshot, "Expected complete content to have JSON snapshot readiness.");
    Assert.True(ready.IncludedInSitemap, "Expected published content to be sitemap-ready.");
    Assert.True(ready.IncludedInLlmsTxt, "Expected published content to be llms.txt-ready.");
    Assert.True(ready.HasStructuredData, "Expected complete content to have structured data readiness.");
    Assert.True(ready.PublicOpenApiAvailable, "Expected public OpenAPI to be available by default.");

    Assert.Equal(40, thin.ReadinessScore);
    Assert.False(thin.HasMarkdownSnapshot, "Expected thin content to miss Markdown snapshot readiness.");
    Assert.False(thin.HasJsonSnapshot, "Expected thin content to miss JSON snapshot readiness.");
    Assert.False(thin.HasStructuredData, "Expected thin content to miss structured data readiness.");
}

static async Task AgentReadinessDashboardSourceReportsReadinessAndAgentTraffic()
{
    var observedAt = new DateTimeOffset(2026, 7, 8, 12, 0, 0, TimeSpan.Zero);
    var readiness = new InMemoryAgentReadinessSignalProvider();
    await readiness.ReplaceAsync(new[]
    {
        new AgentReadinessPageSignal(
            "home",
            "/",
            95,
            HasMarkdownSnapshot: true,
            HasJsonSnapshot: true,
            IncludedInSitemap: true,
            IncludedInLlmsTxt: true,
            HasStructuredData: true,
            PublicOpenApiAvailable: true),
        new AgentReadinessPageSignal(
            "missing",
            "/missing",
            52,
            HasMarkdownSnapshot: false,
            HasJsonSnapshot: false,
            IncludedInSitemap: true,
            IncludedInLlmsTxt: false,
            HasStructuredData: false,
            PublicOpenApiAvailable: false,
            AgentFacingErrorCount: 1)
    });

    var store = new InMemoryAnalyticsEventStore();
    var factory = new AnalyticsEventFactory(new AnalyticsPrivacyOptions
    {
        SessionHashSalt = "agent-test"
    });
    await store.AddAsync(factory.Create(
        "site-1",
        "page_view",
        "/missing",
        "agent-session",
        observedAt.AddMinutes(-5),
        userAgent: "ExampleBot/1.0",
        metadata: new Dictionary<string, string>
        {
            ["status_code"] = "404",
            ["bot_policy"] = "blocked_training"
        }));
    await store.AddAsync(factory.Create(
        "site-1",
        "page_view",
        "/missing",
        "agent-session-2",
        observedAt.AddMinutes(-4),
        userAgent: "ExampleBot/1.0"));
    await store.AddAsync(factory.Create(
        "site-1",
        "page_view",
        "/human",
        "human-session",
        observedAt.AddMinutes(-4)));

    var source = new AgentReadinessDashboardSignalSource(
        readiness,
        store,
        "site-1",
        clock: () => observedAt);

    var signals = await source.CollectAsync();

    Assert.Equal(74, Value(signals, "agent.averageReadinessScore"));
    Assert.Equal(1, Value(signals, "agent.lowScorePages"));
    Assert.Equal(1, Value(signals, "agent.missingMarkdownSnapshots"));
    Assert.Equal(1, Value(signals, "agent.missingJsonSnapshots"));
    Assert.Equal(100, Value(signals, "agent.sitemapCoverage"));
    Assert.Equal(50, Value(signals, "agent.llmsTxtCoverage"));
    Assert.Equal(50, Value(signals, "agent.structuredDataCoverage"));
    Assert.Equal(1, Value(signals, "agent.publicOpenApiStatus"));
    Assert.Equal(2, Value(signals, "agent.aiBotTraffic"));
    Assert.Equal(1, Value(signals, "agent.blockedTrainingBotRequests"));
    Assert.Equal(2, Value(signals, "agent.agentFacingErrors"));
    Assert.Equal(2, Value(signals, "agent.topAgentPageViews"));
    Assert.Equal("/missing", Description(signals, "agent.topAgentPageViews"));
    Assert.True(signals.Alerts.Any(alert => alert.Code == "agent.snapshots"), "Expected missing snapshot alert.");
    Assert.True(signals.Alerts.Any(alert => alert.Code == "agent.errors"), "Expected agent-facing error alert.");
    Assert.True(
        signals.Alerts.First(alert => alert.Code == "agent.errors").IsActionable,
        "Expected agent-facing error alert to link to analytics.");
}

static async Task SystemRuntimeDashboardSourceReportsOperationalEvents()
{
    var observedAt = new DateTimeOffset(2026, 7, 8, 12, 0, 0, TimeSpan.Zero);
    var store = new InMemoryAnalyticsEventStore();
    var factory = new AnalyticsEventFactory(new AnalyticsPrivacyOptions
    {
        SessionHashSalt = "runtime-test"
    });

    await store.AddAsync(factory.Create(
        "site-1",
        "api_request",
        "/api/public/search",
        "session-a",
        observedAt.AddMinutes(-10),
        metadata: new Dictionary<string, string>
        {
            ["latency_ms"] = "1200",
            ["status_code"] = "500"
        }));
    await store.AddAsync(factory.Create(
        "site-1",
        "api_request",
        "/api/public/datasets",
        "session-b",
        observedAt.AddMinutes(-9),
        metadata: new Dictionary<string, string>
        {
            ["latency_ms"] = "100",
            ["status_code"] = "200"
        }));
    await store.AddAsync(factory.Create(
        "site-1",
        "background_job",
        "search-index",
        "job-a",
        observedAt.AddMinutes(-8),
        metadata: new Dictionary<string, string> { ["status"] = "failed" }));
    await store.AddAsync(factory.Create(
        "site-1",
        "background_job",
        "snapshot-generator",
        "job-b",
        observedAt.AddMinutes(-7),
        metadata: new Dictionary<string, string> { ["status"] = "succeeded" }));
    await store.AddAsync(factory.Create(
        "site-1",
        "search_indexing",
        "content",
        "job-c",
        observedAt.AddMinutes(-6),
        metadata: new Dictionary<string, string> { ["lag_seconds"] = "45" }));
    await store.AddAsync(factory.Create(
        "site-1",
        "snapshot_generation",
        "/content/a",
        "job-d",
        observedAt.AddMinutes(-5),
        metadata: new Dictionary<string, string> { ["status"] = "failed" }));
    await store.AddAsync(factory.Create(
        "site-1",
        "public_output_revalidation",
        "/sitemap.xml",
        "job-e",
        observedAt.AddMinutes(-4),
        metadata: new Dictionary<string, string> { ["status"] = "failed" }));

    var source = new SystemRuntimeDashboardSignalSource(
        store,
        "site-1",
        apiErrorRateWarningPercent: 10,
        apiLatencyWarningMilliseconds: 500,
        clock: () => observedAt);

    var signals = await source.CollectAsync();

    Assert.Equal(650, Value(signals, "system.apiAverageLatencyMs"));
    Assert.Equal(50.0m, Value(signals, "system.apiErrorRate"));
    Assert.Equal(50.0m, Value(signals, "system.backgroundJobSuccessRate"));
    Assert.Equal(1, Value(signals, "system.failedJobCount"));
    Assert.Equal(45, Value(signals, "system.searchIndexingLagSeconds"));
    Assert.Equal(1, Value(signals, "system.snapshotGenerationFailures"));
    Assert.Equal(1, Value(signals, "system.revalidationFailures"));
    Assert.True(signals.Alerts.Any(alert => alert.Code == "system.apiErrorRate"), "Expected API error alert.");
    Assert.True(signals.Alerts.Any(alert => alert.Code == "system.failedJobs"), "Expected failed job alert.");
    Assert.True(
        signals.Alerts.All(alert => alert.IsActionable),
        "Expected runtime health alerts to be actionable.");
}

static async Task SystemHealthDashboardSourceReportsDependencyStatus()
{
    var observedAt = new DateTimeOffset(2026, 7, 8, 12, 0, 0, TimeSpan.Zero);
    var source = new SystemHealthDashboardSignalSource(
        new IDashboardHealthProbe[]
        {
            new StaticDashboardHealthProbe(
                "database",
                "Database",
                DashboardHealthProbeStatus.Healthy,
                TimeSpan.FromMilliseconds(4),
                clock: () => observedAt),
            new StaticDashboardHealthProbe(
                "redis",
                "Redis",
                DashboardHealthProbeStatus.Degraded,
                TimeSpan.FromMilliseconds(10),
                "Redis cache is not configured.",
                "/admin/system/storage",
                () => observedAt),
            new StaticDashboardHealthProbe(
                "storage",
                "Object storage",
                DashboardHealthProbeStatus.Unhealthy,
                TimeSpan.FromMilliseconds(1),
                "Object storage is not configured.",
                "/admin/system/storage",
                () => observedAt)
        },
        () => observedAt);

    var signals = await source.CollectAsync();

    Assert.Equal(3, Value(signals, "system.dependencyCount"));
    Assert.Equal(1, Value(signals, "system.unhealthyDependencies"));
    Assert.Equal(1, Value(signals, "system.degradedDependencies"));
    Assert.Equal(5, Value(signals, "system.averageDependencyLatencyMs"));
    Assert.Equal(1, Value(signals, "system.dependency.database"));
    Assert.Equal(0, Value(signals, "system.dependency.redis"));
    Assert.Equal(0, Value(signals, "system.dependency.storage"));
    Assert.True(signals.Alerts.Any(alert => alert.Code == "system.dependency.redis"), "Expected degraded dependency alert.");
    Assert.True(signals.Alerts.Any(alert => alert.Code == "system.dependency.storage"), "Expected unhealthy dependency alert.");
}

static async Task DashboardSummaryServiceReusesFreshSnapshot()
{
    var observedAt = new DateTimeOffset(2026, 7, 8, 12, 0, 0, TimeSpan.Zero);
    var source = new CountingDashboardSignalSource(observedAt);
    var service = new DashboardSummaryService(
        new DashboardAggregator(new[] { source }),
        new InMemoryDashboardSnapshotStore(),
        new DashboardSummaryOptions
        {
            SnapshotTtlSeconds = 60,
            MaxSnapshotTtlSeconds = 60
        },
        () => observedAt);

    var first = await service.GetSnapshotAsync(new DashboardSummaryRequest(RequestedAt: observedAt));
    var second = await service.GetSnapshotAsync(new DashboardSummaryRequest(RequestedAt: observedAt.AddSeconds(10)));
    var forced = await service.GetSnapshotAsync(new DashboardSummaryRequest(
        ForceRefresh: true,
        RequestedAt: observedAt.AddSeconds(20)));

    Assert.Equal(first.Id, second.Id);
    Assert.NotEqual(first.Id, forced.Id);
    Assert.Equal(2, source.CollectCount);
    Assert.True(!string.IsNullOrWhiteSpace(first.SourceChecksum), "Expected snapshot checksum.");
}

static Task PrometheusExporterEmitsDashboardMetrics()
{
    var observedAt = new DateTimeOffset(2026, 7, 8, 12, 0, 0, TimeSpan.Zero);
    var summary = new DashboardSummary(
        observedAt,
        new[]
        {
            new DashboardCard(
                "site.operations",
                "Site operations",
                DashboardArea.SiteOperations,
                new[]
                {
                    Metric("site.pageViews", "Page views", DashboardArea.SiteOperations, "site.operations", "Site operations", 12, "views", observedAt, "Analytics", 10)
                },
                new[]
                {
                    Alert("site.notFoundPages", "Missing page", DashboardArea.SiteOperations, "site.operations", "Site operations", DashboardAlertLevel.Warning, "Analytics", observedAt, null)
                })
        },
        Array.Empty<DashboardAlert>());
    var snapshot = new DashboardSnapshot(
        Guid.NewGuid(),
        summary,
        observedAt,
        observedAt.AddSeconds(60),
        DashboardSummaryChecksum.Compute(summary));

    var text = DashboardPrometheusExporter.Export(snapshot);

    Assert.True(text.Contains("openportalkit_dashboard_metric", StringComparison.Ordinal), "Expected metric gauge.");
    Assert.True(text.Contains("code=\"site.pageViews\"", StringComparison.Ordinal), "Expected page view label.");
    Assert.True(text.Contains("} 12", StringComparison.Ordinal), "Expected page view value.");
    Assert.True(text.Contains("openportalkit_dashboard_alerts{level=\"Warning\"} 1", StringComparison.Ordinal), "Expected warning alert count.");

    return Task.CompletedTask;
}

static Task DashboardTelemetryPublisherEmitsMeterMeasurements()
{
    var observedAt = new DateTimeOffset(2026, 7, 8, 12, 0, 0, TimeSpan.Zero);
    var summary = new DashboardSummary(
        observedAt,
        new[]
        {
            new DashboardCard(
                "site.operations",
                "Site operations",
                DashboardArea.SiteOperations,
                new[]
                {
                    Metric("site.pageViews", "Page views", DashboardArea.SiteOperations, "site.operations", "Site operations", 7, "views", observedAt, "Analytics", 10)
                },
                Array.Empty<DashboardAlert>())
        },
        Array.Empty<DashboardAlert>());
    var snapshot = new DashboardSnapshot(
        Guid.NewGuid(),
        summary,
        observedAt,
        observedAt.AddSeconds(60),
        DashboardSummaryChecksum.Compute(summary));
    var received = false;

    using var listener = new MeterListener();
    listener.InstrumentPublished = (instrument, meterListener) =>
    {
        if (instrument.Meter.Name == DashboardTelemetryPublisher.MeterName)
        {
            meterListener.EnableMeasurementEvents(instrument);
        }
    };
    listener.SetMeasurementEventCallback<double>((instrument, measurement, tags, state) =>
    {
        if (instrument.Name == "openportalkit.dashboard.metric.value" && measurement == 7)
        {
            received = true;
        }
    });
    listener.Start();

    using var publisher = new DashboardTelemetryPublisher();
    publisher.Publish(snapshot);

    Assert.True(received, "Expected dashboard metric measurement.");
    return Task.CompletedTask;
}

static Task PostgresMigrationPreservesDashboardPrivacyAndIndexes()
{
    var path = Path.Combine(
        "db",
        "postgresql",
        "migrations",
        "0007_dashboard_analytics.sql");
    var sql = File.ReadAllText(path);

    Assert.Contains("create table if not exists opk_analytics_events", sql);
    Assert.Contains("hashed_session_id text not null", sql);
    Assert.Contains("anonymized_ip_address inet null", sql);
    Assert.Contains("metadata_json jsonb not null", sql);
    Assert.Contains("ix_opk_analytics_events_site_occurred", sql);
    Assert.Contains("ix_opk_analytics_events_site_type_occurred", sql);
    Assert.Contains("create table if not exists opk_dashboard_snapshots", sql);
    Assert.Contains("source_checksum text not null", sql);
    Assert.Contains("summary_json jsonb not null", sql);
    Assert.Contains("schema_version integer not null", sql);
    Assert.False(sql.Contains("raw_session_id", StringComparison.OrdinalIgnoreCase), "Migration must not store raw sessions.");
    Assert.False(sql.Contains("raw_ip_address", StringComparison.OrdinalIgnoreCase), "Migration must not store raw IP addresses.");

    return Task.CompletedTask;
}

static Task PostgresStoreSqlPreservesDashboardPrivacy()
{
    var sql = string.Join(
        "\n",
        DashboardPostgresSql.InsertAnalyticsEvent,
        DashboardPostgresSql.DeleteAnalyticsEventsOlderThan,
        DashboardPostgresSql.InsertDashboardSnapshot,
        DashboardPostgresSql.SelectLatestDashboardSnapshot);

    Assert.Contains("hashed_session_id", sql);
    Assert.Contains("anonymized_ip_address", sql);
    Assert.Contains("cast(@metadata_json as jsonb)", sql);
    Assert.Contains("cast(@summary_json as jsonb)", sql);
    Assert.Contains("source_checksum", sql);
    Assert.Contains("schema_version", sql);
    Assert.Contains("on conflict (id) do update", sql);
    Assert.False(sql.Contains("raw_session_id", StringComparison.OrdinalIgnoreCase), "Store SQL must not persist raw sessions.");
    Assert.False(sql.Contains("raw_ip_address", StringComparison.OrdinalIgnoreCase), "Store SQL must not persist raw IP addresses.");

    return Task.CompletedTask;
}

static Task AdminDashboardPostgresStorageIsExplicitlyConfigurable()
{
    var production = File.ReadAllText(Path.Combine(
        "src",
        "OpenPortalKit.AdminHost",
        "appsettings.json"));
    var development = File.ReadAllText(Path.Combine(
        "src",
        "OpenPortalKit.AdminHost",
        "appsettings.Development.json"));

    Assert.Contains("\"PostgreSQL\"", production);
    Assert.Contains("\"Enabled\": false", production);
    Assert.Contains("\"ProviderInvariantName\": \"Npgsql\"", production);
    Assert.Contains("\"ConnectionStringName\": \"Default\"", production);
    Assert.Contains("\"PostgreSQL\"", development);
    Assert.Contains("\"Enabled\": false", development);

    return Task.CompletedTask;
}

static Task AdminDashboardRegistersR7SignalSources()
{
    var program = File.ReadAllText(Path.Combine(
        "src",
        "OpenPortalKit.AdminHost",
        "Program.cs"));

    Assert.Contains("AgentReadinessDashboardSignalSource", program);
    Assert.Contains("ContentAgentReadinessSignalProvider", program);
    Assert.Contains("SystemRuntimeDashboardSignalSource", program);
    Assert.Contains("SystemHealthDashboardSignalSource", program);
    Assert.Contains("IAgentReadinessSignalProvider", program);
    Assert.Contains("IDashboardHealthProbe", program);

    return Task.CompletedTask;
}

static Task ApiHostCapturesPublicAnalyticsRuntimeEvents()
{
    var program = File.ReadAllText(Path.Combine(
        "src",
        "OpenPortalKit.ApiHost",
        "Program.cs"));

    Assert.Contains("IAnalyticsEventStore", program);
    Assert.Contains("InMemoryAnalyticsEventStore", program);
    Assert.Contains("PostgresAnalyticsEventStore", program);
    Assert.Contains("DashboardPostgresStorageOptions", program);
    Assert.Contains("AnalyticsEventFactory", program);
    Assert.Contains("ApiAnalyticsCaptureQueue", program);
    Assert.Contains("AddHostedService", program);
    Assert.Contains("TryEnqueue", program);
    Assert.Contains("Channel.CreateBounded", program);
    Assert.Contains("IsPublicOutputRequest", program);
    Assert.Contains("\"api_request\"", program);
    Assert.Contains("\"latency_ms\"", program);
    Assert.Contains("\"status_code\"", program);
    Assert.Contains("/analytics/client.js", program);
    Assert.Contains("/analytics/events", program);
    Assert.Contains("localStorage", program);
    Assert.False(program.Contains("document.cookie", StringComparison.OrdinalIgnoreCase), "Analytics client must not use cookies.");
    Assert.False(program.Contains("await CaptureAnalyticsEventAsync", StringComparison.Ordinal), "Public analytics capture should not block public responses.");

    var production = File.ReadAllText(Path.Combine(
        "src",
        "OpenPortalKit.ApiHost",
        "appsettings.json"));
    Assert.Contains("\"PostgreSQL\"", production);
    Assert.Contains("\"Enabled\": false", production);

    return Task.CompletedTask;
}

static DashboardMetricSnapshot Metric(
    string code,
    string label,
    DashboardArea area,
    string cardCode,
    string cardTitle,
    decimal value,
    string unit,
    DateTimeOffset observedAt,
    string sourceModule,
    int sortOrder)
{
    return new DashboardMetricSnapshot(
        code,
        label,
        area,
        cardCode,
        cardTitle,
        value,
        unit,
        observedAt,
        sourceModule,
        sortOrder);
}

static DashboardAlert Alert(
    string code,
    string message,
    DashboardArea area,
    string cardCode,
    string cardTitle,
    DashboardAlertLevel level,
    string sourceModule,
    DateTimeOffset observedAt,
    string? actionHref)
{
    return new DashboardAlert(
        code,
        message,
        area,
        cardCode,
        cardTitle,
        level,
        sourceModule,
        observedAt,
        actionHref);
}

static ContentItem Content(
    Guid siteId,
    string slug,
    ContentPublicationStatus status,
    DateTimeOffset updatedAt,
    DateTimeOffset? publishedAt = null,
    string summary = "Summary",
    Guid? coverAssetId = null,
    string body = "Body",
    string? source = null,
    DateTimeOffset? expiresAt = null,
    Guid? contentTypeId = null,
    Guid? authorId = null,
    Guid? categoryId = null)
{
    return new ContentItem(
        Guid.NewGuid(),
        siteId,
        contentTypeId ?? Guid.NewGuid(),
        slug,
        slug,
        summary,
        body,
        coverAssetId,
        status,
        categoryId,
        Array.Empty<string>(),
        authorId,
        source,
        publishedAt,
        ScheduledAt: null,
        expiresAt,
        Guid.NewGuid(),
        Guid.NewGuid(),
        updatedAt.AddDays(-1),
        updatedAt);
}

static DataSet DataSet(Guid siteId, string code, bool isPublic, DateTimeOffset observedAt)
{
    return new DataSet(
        Guid.NewGuid(),
        siteId,
        code,
        code,
        "Description",
        isPublic,
        observedAt.AddDays(-10),
        observedAt.AddDays(-1));
}

static DataRecord Record(Guid dataSetId, string key, DateOnly asOfDate, string source)
{
    return new DataRecord(
        Guid.NewGuid(),
        dataSetId,
        key,
        """{"name":"sample"}""",
        asOfDate,
        Guid.NewGuid(),
        Guid.NewGuid(),
        source,
        "checksum",
        new DateTimeOffset(2026, 7, 8, 12, 0, 0, TimeSpan.Zero),
        new DateTimeOffset(2026, 7, 8, 12, 0, 0, TimeSpan.Zero));
}

static decimal Value(DashboardSignalSet signals, string code)
{
    var metric = signals.Metrics.FirstOrDefault(candidate =>
        string.Equals(candidate.Code, code, StringComparison.Ordinal));

    if (metric is null)
    {
        throw new InvalidOperationException($"Metric '{code}' was not found.");
    }

    return metric.Value;
}

static string? Description(DashboardSignalSet signals, string code)
{
    var metric = signals.Metrics.FirstOrDefault(candidate =>
        string.Equals(candidate.Code, code, StringComparison.Ordinal));

    if (metric is null)
    {
        throw new InvalidOperationException($"Metric '{code}' was not found.");
    }

    return metric.Description;
}

internal static class Assert
{
    public static void Equal<T>(T expected, T actual)
    {
        if (!EqualityComparer<T>.Default.Equals(expected, actual))
        {
            throw new InvalidOperationException($"Expected '{expected}', got '{actual}'.");
        }
    }

    public static void NotEqual<T>(T expectedNotToMatch, T actual)
    {
        if (EqualityComparer<T>.Default.Equals(expectedNotToMatch, actual))
        {
            throw new InvalidOperationException($"Did not expect '{actual}'.");
        }
    }

    public static void True(bool condition, string message)
    {
        if (!condition)
        {
            throw new InvalidOperationException(message);
        }
    }

    public static void False(bool condition, string message)
    {
        if (condition)
        {
            throw new InvalidOperationException(message);
        }
    }

    public static void Contains(string expected, string actual)
    {
        if (!actual.Contains(expected, StringComparison.OrdinalIgnoreCase))
        {
            throw new InvalidOperationException($"Expected text to contain '{expected}'.");
        }
    }
}

internal sealed class CountingDashboardSignalSource : IDashboardSignalSource
{
    private readonly DateTimeOffset _observedAt;

    public CountingDashboardSignalSource(DateTimeOffset observedAt)
    {
        _observedAt = observedAt;
    }

    public int CollectCount { get; private set; }

    public string SourceModule => "Counting";

    public Task<DashboardSignalSet> CollectAsync(CancellationToken cancellationToken = default)
    {
        CollectCount++;

        return Task.FromResult(new DashboardSignalSet(
            SourceModule,
            new[]
            {
                new DashboardMetricSnapshot(
                    "site.pageViews",
                    "Page views",
                    DashboardArea.SiteOperations,
                    "site.operations",
                    "Site operations",
                    CollectCount,
                    "views",
                    _observedAt,
                    SourceModule,
                    10)
            },
            Array.Empty<DashboardAlert>()));
    }
}
