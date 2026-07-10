namespace OpenPortalKit.Modules.Content.BlockTemplates;

public sealed class PredefinedBlockCatalog : IBlockDefinitionCatalog
{
    private readonly IReadOnlyList<BlockDefinition> _definitions;

    public PredefinedBlockCatalog(IEnumerable<BlockDefinition>? definitions = null)
    {
        _definitions = (definitions ?? CreateDefaults())
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
                })
        };
    }
}
