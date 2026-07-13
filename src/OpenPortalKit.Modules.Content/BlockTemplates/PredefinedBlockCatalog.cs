namespace OpenPortalKit.Modules.Content.BlockTemplates;

public sealed class PredefinedBlockCatalog : IBlockDefinitionCatalog
{
    private readonly IReadOnlyList<BlockDefinition> _definitions;

    public PredefinedBlockCatalog()
        : this(CreateDefaults())
    {
    }

    internal PredefinedBlockCatalog(IEnumerable<BlockDefinition> definitions)
    {
        ArgumentNullException.ThrowIfNull(definitions);

        _definitions = definitions
            .OrderBy(definition => definition.DisplayName, StringComparer.OrdinalIgnoreCase)
            .ToArray();

        if (_definitions.GroupBy(definition => definition.Code, StringComparer.OrdinalIgnoreCase)
            .Any(group => group.Count() > 1))
        {
            throw new ArgumentException("Block definition codes must be unique.", nameof(definitions));
        }
    }

    public IReadOnlyList<BlockDefinition> List() => _definitions;

    public BlockDefinition? FindByCode(string code)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(code);

        return _definitions.FirstOrDefault(definition =>
            string.Equals(definition.Code, code, StringComparison.OrdinalIgnoreCase));
    }

    private static IReadOnlyList<BlockDefinition> CreateDefaults()
    {
        return new[]
        {
            new BlockDefinition(
                "hero",
                "Hero",
                "A focused page introduction with a title, supporting copy, and one optional action.",
                "block.hero.v1",
                new[]
                {
                    new BlockSettingDefinition("headline", "Headline", BlockSettingType.Text, true),
                    new BlockSettingDefinition("summary", "Summary", BlockSettingType.Text, false),
                    new BlockSettingDefinition("actionUrl", "Action URL", BlockSettingType.Url, false),
                    new BlockSettingDefinition("actionLabel", "Action label", BlockSettingType.Text, false)
                }),
            new BlockDefinition(
                "rich-text",
                "Rich Text",
                "Structured editorial copy rendered by the server with the standard public content policy.",
                "block.rich-text.v1",
                new[]
                {
                    new BlockSettingDefinition("body", "Body", BlockSettingType.RichText, true)
                }),
            new BlockDefinition(
                "content-list",
                "Content List",
                "A server-rendered query of published content with bounded item count.",
                "block.content-list.v1",
                new[]
                {
                    new BlockSettingDefinition("heading", "Heading", BlockSettingType.Text, false),
                    new BlockSettingDefinition("query", "Content query", BlockSettingType.ContentQuery, true),
                    new BlockSettingDefinition("take", "Items to show", BlockSettingType.Number, false)
                }),
            new BlockDefinition(
                "data-table",
                "Data Table",
                "A server-rendered public dataset view with traceability supplied by the Data module.",
                "block.data-table.v1",
                new[]
                {
                    new BlockSettingDefinition("heading", "Heading", BlockSettingType.Text, false),
                    new BlockSettingDefinition("dataSet", "Dataset", BlockSettingType.DataSetReference, true),
                    new BlockSettingDefinition("take", "Rows to show", BlockSettingType.Number, false)
                }),
            new BlockDefinition(
                "announcement-list",
                "Announcement List",
                "A labelled view of published announcement content selected by a bounded tag query.",
                "block.announcement-list.v1",
                CreateContentListSettings()),
            new BlockDefinition(
                "activity-list",
                "Activity List",
                "A labelled view of published activity content selected by a bounded tag query.",
                "block.activity-list.v1",
                CreateContentListSettings()),
            new BlockDefinition(
                "report-list",
                "Report List",
                "A labelled view of published report content selected by a bounded tag query.",
                "block.report-list.v1",
                CreateContentListSettings()),
            new BlockDefinition(
                "chart",
                "Chart",
                "A server-rendered, accessible comparison chart from a bounded series of labelled values.",
                "block.chart.v1",
                new[]
                {
                    new BlockSettingDefinition("heading", "Heading", BlockSettingType.Text, false),
                    new BlockSettingDefinition("series", "Series", BlockSettingType.StructuredList, true, "Each item provides label and value.")
                }),
            new BlockDefinition(
                "link-list",
                "Link List",
                "A bounded set of labelled public links.",
                "block.link-list.v1",
                new[]
                {
                    new BlockSettingDefinition("heading", "Heading", BlockSettingType.Text, false),
                    new BlockSettingDefinition("links", "Links", BlockSettingType.StructuredList, true, "Each item provides label and URL.")
                }),
            new BlockDefinition(
                "download-list",
                "Download List",
                "A bounded set of labelled downloadable public resources.",
                "block.download-list.v1",
                new[]
                {
                    new BlockSettingDefinition("heading", "Heading", BlockSettingType.Text, false),
                    new BlockSettingDefinition("downloads", "Downloads", BlockSettingType.StructuredList, true, "Each item provides label, URL, and optional description.")
                }),
            new BlockDefinition(
                "faq",
                "FAQ",
                "An accessible disclosure list of frequently asked questions.",
                "block.faq.v1",
                new[]
                {
                    new BlockSettingDefinition("heading", "Heading", BlockSettingType.Text, false),
                    new BlockSettingDefinition("items", "Questions", BlockSettingType.StructuredList, true, "Each item provides question and answer.")
                }),
            new BlockDefinition(
                "contact",
                "Contact",
                "A structured public contact panel.",
                "block.contact.v1",
                new[]
                {
                    new BlockSettingDefinition("heading", "Heading", BlockSettingType.Text, false),
                    new BlockSettingDefinition("name", "Name", BlockSettingType.Text, true),
                    new BlockSettingDefinition("email", "Email", BlockSettingType.Text, false),
                    new BlockSettingDefinition("phone", "Phone", BlockSettingType.Text, false),
                    new BlockSettingDefinition("address", "Address", BlockSettingType.Text, false)
                }),
            new BlockDefinition(
                "embed",
                "Embed",
                "A sandboxed HTTPS embed for approved public resources.",
                "block.embed.v1",
                new[]
                {
                    new BlockSettingDefinition("heading", "Heading", BlockSettingType.Text, false),
                    new BlockSettingDefinition("url", "Embed URL", BlockSettingType.Url, true),
                    new BlockSettingDefinition("title", "Frame title", BlockSettingType.Text, true)
                })
        };
    }

    private static IReadOnlyList<BlockSettingDefinition> CreateContentListSettings()
    {
        return new[]
        {
            new BlockSettingDefinition("heading", "Heading", BlockSettingType.Text, false),
            new BlockSettingDefinition("query", "Tag query", BlockSettingType.ContentQuery, true, "Use * for all published content, or one public tag."),
            new BlockSettingDefinition("take", "Items to show", BlockSettingType.Number, false)
        };
    }
}
