using System.Globalization;
using System.Text;
using System.Text.Encodings.Web;
using System.Text.Json;
using OpenPortalKit.Modules.Content.ContentItems;

namespace OpenPortalKit.Modules.Content.BlockTemplates;

public sealed class ServerRenderedBlockPageRenderer
{
    private readonly HtmlEncoder _htmlEncoder;
    private readonly IContentItemStore? _contentStore;
    private readonly IPageBlockDataResolver? _dataResolver;

    public ServerRenderedBlockPageRenderer(
        IContentItemStore? contentStore = null,
        IPageBlockDataResolver? dataResolver = null,
        HtmlEncoder? htmlEncoder = null)
    {
        _htmlEncoder = htmlEncoder ?? HtmlEncoder.Default;
        _contentStore = contentStore;
        _dataResolver = dataResolver;
    }

    public string RenderBody(PortalPage page)
    {
        ArgumentNullException.ThrowIfNull(page);

        var output = new StringBuilder();
        foreach (var block in page.Blocks.OrderBy(block => block.SortOrder))
        {
            RenderStaticBlock(output, block);
        }

        return output.ToString();
    }

    public async Task<string> RenderBodyAsync(
        PortalPage page,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(page);

        var output = new StringBuilder();
        foreach (var block in page.Blocks.OrderBy(block => block.SortOrder))
        {
            switch (block.DefinitionCode)
            {
                case "content-list":
                case "announcement-list":
                case "activity-list":
                case "report-list":
                    await RenderContentListAsync(output, page.SiteId, block, cancellationToken);
                    break;
                case "data-table":
                    await RenderDataTableAsync(output, page.SiteId, block, cancellationToken);
                    break;
                default:
                    RenderStaticBlock(output, block);
                    break;
            }
        }

        return output.ToString();
    }

    private void RenderStaticBlock(StringBuilder output, BlockInstance block)
    {
        switch (block.DefinitionCode)
        {
            case "hero":
                RenderHero(output, block);
                break;
            case "rich-text":
                RenderRichText(output, block);
                break;
            case "chart":
                RenderChart(output, block);
                break;
            case "link-list":
                RenderLinkList(output, block);
                break;
            case "download-list":
                RenderDownloadList(output, block);
                break;
            case "faq":
                RenderFaq(output, block);
                break;
            case "contact":
                RenderContact(output, block);
                break;
            case "embed":
                RenderEmbed(output, block);
                break;
            default:
                throw new InvalidOperationException(
                    $"Block '{block.DefinitionCode}' cannot be rendered until its server renderer is registered.");
        }
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
        foreach (var paragraph in body.Replace("\r\n", "\n", StringComparison.Ordinal)
                     .Split('\n', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries))
        {
            output.Append("<p>").Append(_htmlEncoder.Encode(paragraph)).Append("</p>");
        }

        output.Append("</section>");
    }

    private async Task RenderContentListAsync(
        StringBuilder output,
        Guid siteId,
        BlockInstance block,
        CancellationToken cancellationToken)
    {
        if (_contentStore is null)
        {
            throw new InvalidOperationException("A published-content store is required to render content list blocks.");
        }

        using var config = JsonDocument.Parse(block.ConfigurationJson);
        var query = GetRequiredString(config.RootElement, "query");
        var take = GetBoundedTake(config.RootElement);
        var tag = string.Equals(query, "*", StringComparison.Ordinal) ? null : query;
        var items = await new PublicContentQueryService(_contentStore).ListPublishedAsync(
            new ContentListQuery(SiteId: siteId, Tag: tag, Take: take),
            cancellationToken: cancellationToken);

        output.Append("<section class=\"opk-page-list opk-page-")
            .Append(_htmlEncoder.Encode(block.DefinitionCode)).Append("\">");
        AppendHeading(output, config.RootElement);
        output.Append("<ul>");
        foreach (var item in items.Take(take))
        {
            output.Append("<li><a href=\"/content/").Append(_htmlEncoder.Encode(item.Slug)).Append("\">")
                .Append(_htmlEncoder.Encode(item.Title)).Append("</a>");
            if (!string.IsNullOrWhiteSpace(item.Summary))
            {
                output.Append("<p>").Append(_htmlEncoder.Encode(item.Summary)).Append("</p>");
            }

            output.Append("<time datetime=\"").Append(item.PublishedAt.ToString("O", CultureInfo.InvariantCulture))
                .Append("\">").Append(item.PublishedAt.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture)).Append("</time></li>");
        }

        output.Append("</ul>");
        if (items.Count == 0)
        {
            output.Append("<p class=\"opk-page-empty\">No published items are available.</p>");
        }

        output.Append("</section>");
    }

    private async Task RenderDataTableAsync(
        StringBuilder output,
        Guid siteId,
        BlockInstance block,
        CancellationToken cancellationToken)
    {
        if (_dataResolver is null)
        {
            throw new InvalidOperationException("A public data resolver is required to render data table blocks.");
        }

        using var config = JsonDocument.Parse(block.ConfigurationJson);
        var data = await _dataResolver.ResolveDataTableAsync(
            siteId,
            GetRequiredString(config.RootElement, "dataSet"),
            GetBoundedTake(config.RootElement),
            cancellationToken);
        if (data is null)
        {
            throw new InvalidOperationException("The configured public dataset is not available.");
        }

        output.Append("<section class=\"opk-page-data-table\">");
        if (!string.IsNullOrWhiteSpace(GetOptionalString(config.RootElement, "heading")))
        {
            AppendHeading(output, config.RootElement);
        }
        else
        {
            output.Append("<h2>").Append(_htmlEncoder.Encode(data.Name)).Append("</h2>");
        }

        if (!string.IsNullOrWhiteSpace(data.Description))
        {
            output.Append("<p>").Append(_htmlEncoder.Encode(data.Description)).Append("</p>");
        }

        output.Append("<div class=\"opk-page-table-wrap\"><table><thead><tr>");
        foreach (var column in data.Columns)
        {
            output.Append("<th scope=\"col\">").Append(_htmlEncoder.Encode(column)).Append("</th>");
        }

        output.Append("</tr></thead><tbody>");
        foreach (var row in data.Rows)
        {
            output.Append("<tr>");
            foreach (var cell in row)
            {
                output.Append("<td>").Append(_htmlEncoder.Encode(cell)).Append("</td>");
            }

            output.Append("</tr>");
        }

        output.Append("</tbody></table></div><a class=\"opk-page-data-source\" href=\"")
            .Append(_htmlEncoder.Encode(data.SourceUrl)).Append("\">View source data</a></section>");
    }

    private void RenderChart(StringBuilder output, BlockInstance block)
    {
        using var config = JsonDocument.Parse(block.ConfigurationJson);
        var items = GetStructuredItems(config.RootElement, "series");
        var values = items.Select(item => GetRequiredDecimal(item, "value")).ToArray();
        var maximum = Math.Max(1m, values.Max());

        output.Append("<section class=\"opk-page-chart\">");
        AppendHeading(output, config.RootElement);
        output.Append("<ul>");
        foreach (var item in items)
        {
            var label = GetRequiredString(item, "label");
            var value = GetRequiredDecimal(item, "value");
            var width = Math.Round(value / maximum * 100m, 2);
            output.Append("<li><span>").Append(_htmlEncoder.Encode(label)).Append("</span><meter min=\"0\" max=\"")
                .Append(maximum.ToString(CultureInfo.InvariantCulture)).Append("\" value=\"")
                .Append(value.ToString(CultureInfo.InvariantCulture)).Append("\" style=\"--opk-value:")
                .Append(width.ToString(CultureInfo.InvariantCulture)).Append("%\"></meter><strong>")
                .Append(_htmlEncoder.Encode(value.ToString(CultureInfo.InvariantCulture))).Append("</strong></li>");
        }

        output.Append("</ul></section>");
    }

    private void RenderLinkList(StringBuilder output, BlockInstance block)
    {
        using var config = JsonDocument.Parse(block.ConfigurationJson);
        output.Append("<section class=\"opk-page-link-list\">");
        AppendHeading(output, config.RootElement);
        output.Append("<ul>");
        foreach (var item in GetStructuredItems(config.RootElement, "links"))
        {
            output.Append("<li><a href=\"").Append(_htmlEncoder.Encode(GetRequiredString(item, "url"))).Append("\">")
                .Append(_htmlEncoder.Encode(GetRequiredString(item, "label"))).Append("</a></li>");
        }

        output.Append("</ul></section>");
    }

    private void RenderDownloadList(StringBuilder output, BlockInstance block)
    {
        using var config = JsonDocument.Parse(block.ConfigurationJson);
        output.Append("<section class=\"opk-page-download-list\">");
        AppendHeading(output, config.RootElement);
        output.Append("<ul>");
        foreach (var item in GetStructuredItems(config.RootElement, "downloads"))
        {
            output.Append("<li><a download href=\"").Append(_htmlEncoder.Encode(GetRequiredString(item, "url"))).Append("\">")
                .Append(_htmlEncoder.Encode(GetRequiredString(item, "label"))).Append("</a>");
            var description = GetOptionalString(item, "description");
            if (!string.IsNullOrWhiteSpace(description))
            {
                output.Append("<p>").Append(_htmlEncoder.Encode(description)).Append("</p>");
            }

            output.Append("</li>");
        }

        output.Append("</ul></section>");
    }

    private void RenderFaq(StringBuilder output, BlockInstance block)
    {
        using var config = JsonDocument.Parse(block.ConfigurationJson);
        output.Append("<section class=\"opk-page-faq\">");
        AppendHeading(output, config.RootElement);
        foreach (var item in GetStructuredItems(config.RootElement, "items"))
        {
            output.Append("<details><summary>").Append(_htmlEncoder.Encode(GetRequiredString(item, "question")))
                .Append("</summary><p>").Append(_htmlEncoder.Encode(GetRequiredString(item, "answer")))
                .Append("</p></details>");
        }

        output.Append("</section>");
    }

    private void RenderContact(StringBuilder output, BlockInstance block)
    {
        using var config = JsonDocument.Parse(block.ConfigurationJson);
        output.Append("<section class=\"opk-page-contact\">");
        AppendHeading(output, config.RootElement);
        output.Append("<address><strong>").Append(_htmlEncoder.Encode(GetRequiredString(config.RootElement, "name"))).Append("</strong>");
        AppendContactLine(output, GetOptionalString(config.RootElement, "email"));
        AppendContactLine(output, GetOptionalString(config.RootElement, "phone"));
        AppendContactLine(output, GetOptionalString(config.RootElement, "address"));
        output.Append("</address></section>");
    }

    private void RenderEmbed(StringBuilder output, BlockInstance block)
    {
        using var config = JsonDocument.Parse(block.ConfigurationJson);
        var url = GetRequiredString(config.RootElement, "url");
        if (!Uri.TryCreate(url, UriKind.Absolute, out var uri) || uri.Scheme != Uri.UriSchemeHttps)
        {
            throw new InvalidOperationException("Embed blocks require an absolute HTTPS URL.");
        }

        output.Append("<section class=\"opk-page-embed\">");
        AppendHeading(output, config.RootElement);
        output.Append("<iframe sandbox=\"allow-scripts allow-forms allow-popups\" referrerpolicy=\"strict-origin-when-cross-origin\" loading=\"lazy\" title=\"")
            .Append(_htmlEncoder.Encode(GetRequiredString(config.RootElement, "title"))).Append("\" src=\"")
            .Append(_htmlEncoder.Encode(uri.AbsoluteUri)).Append("\"></iframe></section>");
    }

    private void AppendHeading(StringBuilder output, JsonElement config)
    {
        var heading = GetOptionalString(config, "heading");
        if (!string.IsNullOrWhiteSpace(heading))
        {
            output.Append("<h2>").Append(_htmlEncoder.Encode(heading)).Append("</h2>");
        }
    }

    private void AppendContactLine(StringBuilder output, string? value)
    {
        if (!string.IsNullOrWhiteSpace(value))
        {
            output.Append("<span>").Append(_htmlEncoder.Encode(value)).Append("</span>");
        }
    }

    private static int GetBoundedTake(JsonElement config)
    {
        return config.TryGetProperty("take", out var value) && value.TryGetInt32(out var take)
            ? take
            : 10;
    }

    private static IReadOnlyList<JsonElement> GetStructuredItems(JsonElement config, string property)
    {
        if (!config.TryGetProperty(property, out var value) || value.ValueKind != JsonValueKind.Array)
        {
            throw new InvalidOperationException($"Block configuration requires list '{property}'.");
        }

        return value.EnumerateArray().ToArray();
    }

    private static decimal GetRequiredDecimal(JsonElement config, string property)
    {
        return config.TryGetProperty(property, out var value) && value.TryGetDecimal(out var result)
            ? result
            : throw new InvalidOperationException($"Block configuration requires numeric '{property}'.");
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
