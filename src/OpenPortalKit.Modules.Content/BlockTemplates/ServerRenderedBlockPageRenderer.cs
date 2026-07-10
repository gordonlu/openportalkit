using System.Text;
using System.Text.Encodings.Web;
using System.Text.Json;

namespace OpenPortalKit.Modules.Content.BlockTemplates;

public sealed class ServerRenderedBlockPageRenderer
{
    private readonly HtmlEncoder _htmlEncoder;

    public ServerRenderedBlockPageRenderer(HtmlEncoder? htmlEncoder = null)
    {
        _htmlEncoder = htmlEncoder ?? HtmlEncoder.Default;
    }

    public string RenderBody(PortalPage page)
    {
        ArgumentNullException.ThrowIfNull(page);

        var output = new StringBuilder();
        foreach (var block in page.Blocks.OrderBy(block => block.SortOrder))
        {
            switch (block.DefinitionCode)
            {
                case "hero":
                    RenderHero(output, block);
                    break;
                case "rich-text":
                    RenderRichText(output, block);
                    break;
                default:
                    throw new InvalidOperationException(
                        $"Block '{block.DefinitionCode}' cannot be rendered until its server renderer is registered.");
            }
        }

        return output.ToString();
    }

    private void RenderHero(StringBuilder output, BlockInstance block)
    {
        using var config = JsonDocument.Parse(block.ConfigurationJson);
        var headline = GetRequiredString(config.RootElement, "headline");
        var summary = GetOptionalString(config.RootElement, "summary");
        var actionUrl = GetOptionalString(config.RootElement, "actionUrl");
        var actionLabel = GetOptionalString(config.RootElement, "actionLabel");

        output.Append("<section class=\"opk-page-hero\"><div>");
        output.Append("<h1>").Append(_htmlEncoder.Encode(headline)).Append("</h1>");
        if (!string.IsNullOrWhiteSpace(summary))
        {
            output.Append("<p>").Append(_htmlEncoder.Encode(summary)).Append("</p>");
        }

        if (!string.IsNullOrWhiteSpace(actionUrl) && !string.IsNullOrWhiteSpace(actionLabel))
        {
            output.Append("<a href=\"").Append(_htmlEncoder.Encode(actionUrl)).Append("\">")
                .Append(_htmlEncoder.Encode(actionLabel)).Append("</a>");
        }

        output.Append("</div></section>");
    }

    private void RenderRichText(StringBuilder output, BlockInstance block)
    {
        using var config = JsonDocument.Parse(block.ConfigurationJson);
        var body = GetRequiredString(config.RootElement, "body");

        output.Append("<section class=\"opk-page-rich-text\">");
        foreach (var paragraph in body.Replace("\r\n", "\n", StringComparison.Ordinal).Split('\n', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries))
        {
            output.Append("<p>").Append(_htmlEncoder.Encode(paragraph)).Append("</p>");
        }

        output.Append("</section>");
    }

    private static string GetRequiredString(JsonElement config, string property)
    {
        return GetOptionalString(config, property) ?? throw new InvalidOperationException(
            $"Block configuration requires '{property}'.");
    }

    private static string? GetOptionalString(JsonElement config, string property)
    {
        return config.TryGetProperty(property, out var value) && value.ValueKind == JsonValueKind.String
            ? value.GetString()
            : null;
    }
}
