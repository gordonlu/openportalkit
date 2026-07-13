namespace OpenPortalKit.Modules.Content.BlockTemplates;

public static class PageTemplateSeedCatalog
{
    public static IReadOnlyList<BlockInstance> CreateDefaultBlocks(IEnumerable<string> definitionCodes)
    {
        ArgumentNullException.ThrowIfNull(definitionCodes);

        return definitionCodes
            .Where(code => !string.IsNullOrWhiteSpace(code))
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .Select((code, index) => CreateDefaultBlock(code, index))
            .ToArray();
    }

    public static IReadOnlyList<PageTemplate> CreateInitialTemplates(Guid actorId, DateTimeOffset now)
    {
        return new[]
        {
            CreateTemplate("corporate-homepage", "Corporate Homepage", "A concise public portal homepage with reusable introduction, links, and contact information.", new[] { "hero", "rich-text", "link-list", "contact" }, actorId, now),
            CreateTemplate("news-portal", "News Portal", "A public stream of published editorial updates.", new[] { "hero", "content-list" }, actorId, now),
            CreateTemplate("announcement-center", "Announcement Center", "A focused public announcement destination.", new[] { "hero", "announcement-list" }, actorId, now),
            CreateTemplate("activity-portal", "Activity Portal", "A public activity and event destination.", new[] { "hero", "activity-list", "contact" }, actorId, now),
            CreateTemplate("research-portal", "Research Portal", "A publishing surface for reports and research updates.", new[] { "hero", "report-list", "download-list" }, actorId, now),
            CreateTemplate("data-portal", "Data Portal", "A public data publication surface with traceable tabular output.", new[] { "hero", "data-table", "chart" }, actorId, now)
        };
    }

    private static PageTemplate CreateTemplate(
        string code,
        string name,
        string description,
        IReadOnlyList<string> blockCodes,
        Guid actorId,
        DateTimeOffset now)
    {
        return new PageTemplate(
            Guid.NewGuid(),
            code,
            name,
            description,
            PageTemplateStatus.Published,
            1,
            CreateDefaultBlocks(blockCodes),
            actorId,
            actorId,
            now,
            now);
    }

    private static BlockInstance CreateDefaultBlock(string definitionCode, int sortOrder)
    {
        var normalizedCode = definitionCode.Trim().ToLowerInvariant();
        var configuration = normalizedCode switch
        {
            "hero" => """{"headline":"Add a page headline","summary":"Use this template as a controlled starting point for a public page."}""",
            "rich-text" => """{"body":"Add the page's editorial content here."}""",
            "content-list" => """{"heading":"Latest updates","query":"*","take":10}""",
            "announcement-list" => """{"heading":"Announcements","query":"announcement","take":10}""",
            "activity-list" => """{"heading":"Activities","query":"activity","take":10}""",
            "report-list" => """{"heading":"Reports","query":"report","take":10}""",
            "data-table" => """{"heading":"Published data","dataSet":"sample-catalog","take":10}""",
            "chart" => """{"heading":"Key indicators","series":[{"label":"Current period","value":75},{"label":"Previous period","value":52}]}""",
            "link-list" => """{"heading":"Explore","links":[{"label":"Public API","url":"/api/public"},{"label":"Sitemap","url":"/sitemap.xml"}]}""",
            "download-list" => """{"heading":"Downloads","downloads":[{"label":"Publication guide","url":"/downloads/publication-guide.pdf","description":"A controlled placeholder for an approved public asset."}]}""",
            "faq" => """{"heading":"Questions","items":[{"question":"How is this page assembled?","answer":"It uses a fixed, versioned template and server-rendered blocks."}]}""",
            "contact" => """{"heading":"Contact","name":"Portal team","email":"portal@example.test","phone":"+1 555 0100"}""",
            "embed" => """{"heading":"Embedded resource","title":"Approved public resource","url":"https://example.test/embed"}""",
            _ => throw new ArgumentException($"'{definitionCode}' is not a predefined block.", nameof(definitionCode))
        };

        return new BlockInstance(
            Guid.NewGuid(),
            normalizedCode,
            "block." + normalizedCode + ".v1",
            sortOrder,
            configuration);
    }
}
