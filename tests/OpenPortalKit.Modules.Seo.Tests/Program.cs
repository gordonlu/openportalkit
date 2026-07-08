using OpenPortalKit.Kernel.Events;
using OpenPortalKit.Kernel.Publishing;
using OpenPortalKit.Modules.Seo.PublicResources;
using OpenPortalKit.Modules.Seo.Redirects;
using OpenPortalKit.Modules.Seo.Revalidation;

var tests = new (string Name, Func<Task> Run)[]
{
    ("canonical URL strips query and uses site authority", CanonicalUrlStripsQueryAndUsesSiteAuthority),
    ("SEO metadata includes canonical Open Graph and JSON-LD", SeoMetadataIncludesCanonicalOpenGraphAndJsonLd),
    ("sitemap XML orders and escapes public URLs", SitemapXmlOrdersAndEscapesPublicUrls),
    ("RSS XML includes feed and item dates", RssXmlIncludesFeedAndItemDates),
    ("robots txt includes crawler directives and sitemap", RobotsTxtIncludesCrawlerDirectivesAndSitemap),
    ("redirect resolver normalizes legacy paths and status codes", RedirectResolverNormalizesLegacyPathsAndStatusCodes),
    ("redirect resolver ignores disabled and loop rules", RedirectResolverIgnoresDisabledAndLoopRules),
    ("publishing revalidation planner includes public routes and outputs", PublishingRevalidationPlannerIncludesPublicRoutesAndOutputs),
    ("publishing revalidation handler records idempotent result", PublishingRevalidationHandlerRecordsIdempotentResult)
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

static Task CanonicalUrlStripsQueryAndUsesSiteAuthority()
{
    var canonical = CanonicalUrlBuilder.Build(new Uri("https://example.com/base/"), "content//launch?draft=true#preview");

    Assert.Equal("https://example.com/content/launch", canonical.ToString());
    Assert.Equal("/", CanonicalUrlBuilder.NormalizePath("///"));

    return Task.CompletedTask;
}

static Task SeoMetadataIncludesCanonicalOpenGraphAndJsonLd()
{
    var resource = new PublicResourceDescriptor(
        "Launch Notes",
        "A public summary.",
        "/content/launch-notes",
        new DateTimeOffset(2026, 7, 8, 9, 0, 0, TimeSpan.Zero),
        new DateTimeOffset(2026, 7, 8, 10, 0, 0, TimeSpan.Zero),
        "en-US");

    var metadata = SeoPageMetadataBuilder.Build(resource, new Uri("https://portal.example"), "OpenPortalKit");

    Assert.Equal("https://portal.example/content/launch-notes", metadata.CanonicalUrl.ToString());
    Assert.Equal("Launch Notes", metadata.OpenGraph["og:title"]);
    Assert.Equal("en-US", metadata.OpenGraph["og:locale"]);
    Assert.Contains("\"@context\":\"https://schema.org\"", metadata.JsonLd);
    Assert.Contains("\"url\":\"https://portal.example/content/launch-notes\"", metadata.JsonLd);

    return Task.CompletedTask;
}

static Task SitemapXmlOrdersAndEscapesPublicUrls()
{
    var xml = SitemapXmlGenerator.Generate(new[]
    {
        new SitemapEntry(new Uri("https://example.com/content/b"), new DateTimeOffset(2026, 7, 8, 8, 0, 0, TimeSpan.Zero), SitemapChangeFrequency.Daily, 0.8m),
        new SitemapEntry(new Uri("https://example.com/content/a?ref=portal&lang=en"), new DateTimeOffset(2026, 7, 8, 7, 0, 0, TimeSpan.Zero), SitemapChangeFrequency.Weekly, 0.6m)
    });

    Assert.Contains("http://www.sitemaps.org/schemas/sitemap/0.9", xml);
    Assert.True(xml.StartsWith("<?xml version=\"1.0\" encoding=\"utf-8\"?>", StringComparison.Ordinal), "Expected sitemap XML to use a UTF-8 declaration without a BOM.");
    Assert.Contains("https://example.com/content/a?ref=portal&amp;lang=en", xml);
    Assert.True(xml.IndexOf("/content/a", StringComparison.Ordinal) < xml.IndexOf("/content/b", StringComparison.Ordinal), "Expected sitemap entries to be stable-sorted.");
    Assert.Contains("<priority>0.8</priority>", xml);

    return Task.CompletedTask;
}

static Task RssXmlIncludesFeedAndItemDates()
{
    var xml = RssXmlGenerator.Generate(new RssFeed(
        "Portal Feed",
        "Latest public content.",
        new Uri("https://example.com/"),
        new DateTimeOffset(2026, 7, 8, 11, 0, 0, TimeSpan.Zero),
        new[]
        {
            new RssFeedItem(
                "Launch Notes",
                "A public summary.",
                new Uri("https://example.com/content/launch-notes"),
                new DateTimeOffset(2026, 7, 8, 9, 0, 0, TimeSpan.Zero),
                new DateTimeOffset(2026, 7, 8, 10, 0, 0, TimeSpan.Zero),
                "content:launch-notes")
        }));

    Assert.Contains("<rss version=\"2.0\">", xml);
    Assert.True(xml.StartsWith("<?xml version=\"1.0\" encoding=\"utf-8\"?>", StringComparison.Ordinal), "Expected RSS XML to use a UTF-8 declaration without a BOM.");
    Assert.Contains("<title>Portal Feed</title>", xml);
    Assert.Contains("<guid>content:launch-notes</guid>", xml);
    Assert.Contains("<pubDate>Wed, 08 Jul 2026 09:00:00 GMT</pubDate>", xml);

    return Task.CompletedTask;
}

static Task RobotsTxtIncludesCrawlerDirectivesAndSitemap()
{
    var txt = RobotsTxtGenerator.Generate(new RobotsPolicy(
        new Uri("https://example.com/sitemap.xml"),
        new[]
        {
            new RobotsDirective("*", new[] { "/" }, new[] { "/admin" }),
            new RobotsDirective("internal-agent", Array.Empty<string>(), new[] { "/" })
        }));

    Assert.Contains("User-agent: *", txt);
    Assert.Contains("Allow: /", txt);
    Assert.Contains("Disallow: /admin", txt);
    Assert.Contains("Sitemap: https://example.com/sitemap.xml", txt);

    return Task.CompletedTask;
}

static async Task RedirectResolverNormalizesLegacyPathsAndStatusCodes()
{
    var store = new InMemoryRedirectRuleStore();
    var now = new DateTimeOffset(2026, 7, 8, 13, 0, 0, TimeSpan.Zero);

    await store.AddAsync(new RedirectRule(
        Guid.NewGuid(),
        "/old//launch?draft=true",
        "/content/launch-notes",
        RedirectStatus.Permanent,
        IsEnabled: true,
        now,
        now));

    await store.AddAsync(new RedirectRule(
        Guid.NewGuid(),
        "/external",
        "https://example.com/archive",
        RedirectStatus.Temporary,
        IsEnabled: true,
        now,
        now));

    var resolver = new RedirectResolver(store);
    var internalResolution = await resolver.ResolveAsync("/OLD/launch?preview=true");
    var externalResolution = await resolver.ResolveAsync("/external");

    Assert.Equal("/old/launch", internalResolution?.SourcePath);
    Assert.Equal("/content/launch-notes", internalResolution?.Target);
    Assert.Equal(301, internalResolution?.StatusCode);
    Assert.False(internalResolution?.IsExternal ?? true, "Expected internal redirect.");
    Assert.Equal("https://example.com/archive", externalResolution?.Target);
    Assert.Equal(302, externalResolution?.StatusCode);
    Assert.True(externalResolution?.IsExternal ?? false, "Expected external redirect.");
}

static async Task RedirectResolverIgnoresDisabledAndLoopRules()
{
    var store = new InMemoryRedirectRuleStore();
    var now = new DateTimeOffset(2026, 7, 8, 13, 30, 0, TimeSpan.Zero);

    await store.AddAsync(new RedirectRule(
        Guid.NewGuid(),
        "/disabled",
        "/content/disabled",
        RedirectStatus.Permanent,
        IsEnabled: false,
        now,
        now));

    await store.AddAsync(new RedirectRule(
        Guid.NewGuid(),
        "/same",
        "/same?ignored=true",
        RedirectStatus.Permanent,
        IsEnabled: true,
        now,
        now));

    var resolver = new RedirectResolver(store);

    Assert.Null(await resolver.ResolveAsync("/disabled"));
    Assert.Null(await resolver.ResolveAsync("/same"));
}

static Task PublishingRevalidationPlannerIncludesPublicRoutesAndOutputs()
{
    var planner = new PublishingRevalidationPlanner();
    var message = CreateOutboxMessage(PublishingEventNames.ContentPublished, "launch-notes");

    var plan = planner.CreatePlan(message);

    Assert.Equal(PublishingEventNames.ContentPublished, plan.SourceEventName);
    Assert.Contains("/content/launch-notes", plan.Routes);
    Assert.Contains("/content", plan.Routes);
    Assert.Contains("/sitemap.xml", plan.Routes);
    Assert.Contains("/rss.xml", plan.Routes);
    Assert.True(plan.RegenerateSitemap, "Expected sitemap regeneration.");
    Assert.True(plan.RegenerateRss, "Expected RSS regeneration.");
    Assert.True(plan.RegenerateSnapshots, "Expected Markdown/JSON snapshot regeneration.");
    Assert.True(plan.InvalidateRouteCache, "Expected route cache invalidation.");
    Assert.True(plan.WarmImportantPages, "Expected important public pages to be warmed.");

    return Task.CompletedTask;
}

static async Task PublishingRevalidationHandlerRecordsIdempotentResult()
{
    var store = new InMemoryPublicOutputRevalidationStore();
    var executor = new RecordingPublicOutputRevalidationExecutor(
        store,
        () => new DateTimeOffset(2026, 7, 8, 12, 0, 0, TimeSpan.Zero));
    var handler = new PublishingRevalidationOutboxHandler(new PublishingRevalidationPlanner(), executor);
    var message = CreateOutboxMessage(PublishingEventNames.ContentPublished, "launch-notes");

    await handler.HandleAsync(message);
    await handler.HandleAsync(message);

    var results = await store.ListAsync();

    Assert.Equal(1, results.Count);
    Assert.True(results[0].Succeeded, "Expected revalidation to succeed.");
    Assert.Contains("/content/launch-notes", results[0].InvalidatedRoutes);
    Assert.Contains("sitemap.xml", results[0].RegeneratedOutputs);
    Assert.Contains("rss.xml", results[0].RegeneratedOutputs);
    Assert.Contains("markdown-snapshot", results[0].RegeneratedOutputs);
    Assert.Contains("json-snapshot", results[0].RegeneratedOutputs);
}

static OutboxMessage CreateOutboxMessage(string eventName, string slug)
{
    return new OutboxMessage(
        Guid.NewGuid(),
        eventName,
        $$"""
        {
          "EventId": "11111111-1111-1111-1111-111111111111",
          "OccurredAt": "2026-07-08T09:00:00+00:00",
          "EventName": "{{eventName}}",
          "IdempotencyKey": "content:{{slug}}:published",
          "SiteId": "22222222-2222-2222-2222-222222222222",
          "ContentItemId": "33333333-3333-3333-3333-333333333333",
          "Slug": "{{slug}}",
          "PublishedAt": "2026-07-08T09:00:00+00:00"
        }
        """,
        $"content:{slug}:published",
        new DateTimeOffset(2026, 7, 8, 9, 0, 0, TimeSpan.Zero),
        ProcessedAt: null,
        AttemptCount: 0,
        LastError: null);
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

    public static void Null(object? value)
    {
        if (value is not null)
        {
            throw new InvalidOperationException($"Expected null, got '{value}'.");
        }
    }

    public static void Contains(string expected, string actual)
    {
        if (!actual.Contains(expected, StringComparison.Ordinal))
        {
            throw new InvalidOperationException($"Expected output to contain '{expected}'.");
        }
    }

    public static void Contains<T>(T expected, IEnumerable<T> actual)
    {
        if (!actual.Contains(expected))
        {
            throw new InvalidOperationException($"Expected output to contain '{expected}'.");
        }
    }
}
