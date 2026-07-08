using OpenPortalKit.Kernel.Events;
using OpenPortalKit.Kernel.Publishing;
using OpenPortalKit.Modules.Search.Indexing;

var tests = new (string Name, Func<Task> Run)[]
{
    ("public search finds published content and hides drafts", PublicSearchFindsPublishedContentAndHidesDrafts),
    ("admin search can include non public documents", AdminSearchCanIncludeNonPublicDocuments),
    ("archived documents are hidden unless requested", ArchivedDocumentsAreHiddenUnlessRequested),
    ("search filters by target type and tags", SearchFiltersByTargetTypeAndTags),
    ("reindexing is repeatable and idempotent", ReindexingIsRepeatableAndIdempotent),
    ("outbox handler indexes resolved document", OutboxHandlerIndexesResolvedDocument)
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

static async Task PublicSearchFindsPublishedContentAndHidesDrafts()
{
    var index = new InMemorySearchIndex();

    await index.UpsertAsync(CreateDocument("content:published", "ContentItem", "Launch Notes", SearchVisibility.Public));
    await index.UpsertAsync(CreateDocument("content:draft", "ContentItem", "Launch Draft", SearchVisibility.NonPublic));

    var results = await index.SearchAsync(new SearchQuery("launch"));

    Assert.Equal(1, results.Count);
    Assert.Equal("content:published", results[0].Document.Id);
}

static async Task AdminSearchCanIncludeNonPublicDocuments()
{
    var index = new InMemorySearchIndex();

    await index.UpsertAsync(CreateDocument("content:draft", "ContentItem", "Launch Draft", SearchVisibility.NonPublic));

    var publicResults = await index.SearchAsync(new SearchQuery("draft"));
    var adminResults = await index.SearchAsync(new SearchQuery("draft", IncludeNonPublic: true));

    Assert.Equal(0, publicResults.Count);
    Assert.Equal(1, adminResults.Count);
}

static async Task ArchivedDocumentsAreHiddenUnlessRequested()
{
    var index = new InMemorySearchIndex();

    await index.UpsertAsync(CreateDocument("content:archived", "ContentItem", "Old Launch", SearchVisibility.Archived));

    var publicResults = await index.SearchAsync(new SearchQuery("launch"));
    var archivedResults = await index.SearchAsync(new SearchQuery("launch", IncludeArchived: true));

    Assert.Equal(0, publicResults.Count);
    Assert.Equal(1, archivedResults.Count);
}

static async Task SearchFiltersByTargetTypeAndTags()
{
    var index = new InMemorySearchIndex();

    await index.UpsertAsync(CreateDocument("content:launch", "ContentItem", "Launch Notes", SearchVisibility.Public, tags: new[] { "release" }));
    await index.UpsertAsync(CreateDocument("dataset:launch", "DataSet", "Launch Metrics", SearchVisibility.Public, tags: new[] { "data" }));

    var dataResults = await index.SearchAsync(new SearchQuery(
        "launch",
        TargetTypes: new[] { "DataSet" },
        Tags: new[] { "data" }));

    Assert.Equal(1, dataResults.Count);
    Assert.Equal("dataset:launch", dataResults[0].Document.Id);
}

static async Task ReindexingIsRepeatableAndIdempotent()
{
    var index = new InMemorySearchIndex();
    var source = new FixedSearchDocumentSource(new[]
    {
        CreateDocument("content:launch", "ContentItem", "Launch Notes", SearchVisibility.Public)
    });
    var reindexer = new SearchReindexer(index, new[] { source });

    var first = await reindexer.ReindexAsync();
    var second = await reindexer.ReindexAsync();
    var results = await index.SearchAsync(new SearchQuery("launch"));

    Assert.Equal(1, first.IndexedDocuments);
    Assert.Equal(1, second.IndexedDocuments);
    Assert.Equal(1, results.Count);
}

static async Task OutboxHandlerIndexesResolvedDocument()
{
    var index = new InMemorySearchIndex();
    var resolver = new FixedOutboxSearchDocumentResolver(CreateDocument(
        "content:launch",
        "ContentItem",
        "Launch Notes",
        SearchVisibility.Public));
    var handler = new SearchIndexingOutboxHandler(index, resolver);

    await handler.HandleAsync(new OutboxMessage(
        Guid.NewGuid(),
        PublishingEventNames.ContentPublished,
        """{"Slug":"launch-notes"}""",
        "content:launch-notes:published",
        new DateTimeOffset(2026, 7, 8, 9, 0, 0, TimeSpan.Zero),
        ProcessedAt: null,
        AttemptCount: 0,
        LastError: null));

    var results = await index.SearchAsync(new SearchQuery("launch"));

    Assert.Equal(1, results.Count);
    Assert.Equal("content:launch", results[0].Document.Id);
}

static SearchDocument CreateDocument(
    string id,
    string targetType,
    string title,
    SearchVisibility visibility,
    IReadOnlyList<string>? tags = null)
{
    return new SearchDocument(
        id,
        targetType,
        id,
        title,
        $"Summary for {title}.",
        $"Body for {title}.",
        "/" + id.Replace(":", "/", StringComparison.Ordinal),
        targetType,
        tags ?? Array.Empty<string>(),
        Category: null,
        PublishedAt: new DateTimeOffset(2026, 1, 1, 9, 0, 0, TimeSpan.Zero),
        UpdatedAt: new DateTimeOffset(2026, 1, 1, 10, 0, 0, TimeSpan.Zero),
        visibility,
        "en-US",
        MetadataJson: null);
}

internal sealed class FixedSearchDocumentSource : ISearchDocumentSource
{
    private readonly IReadOnlyList<SearchDocument> _documents;

    public FixedSearchDocumentSource(IReadOnlyList<SearchDocument> documents)
    {
        _documents = documents;
    }

    public Task<IReadOnlyList<SearchDocument>> GetDocumentsAsync(CancellationToken cancellationToken = default)
    {
        return Task.FromResult(_documents);
    }
}

internal sealed class FixedOutboxSearchDocumentResolver : IOutboxSearchDocumentResolver
{
    private readonly SearchDocument _document;

    public FixedOutboxSearchDocumentResolver(SearchDocument document)
    {
        _document = document;
    }

    public Task<SearchDocument?> ResolveAsync(OutboxMessage message, CancellationToken cancellationToken = default)
    {
        return Task.FromResult<SearchDocument?>(_document);
    }
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
}
