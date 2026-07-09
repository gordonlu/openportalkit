using System.Text;
using System.Text.Json;

namespace OpenPortalKit.Modules.AgentAccess.AgentOutputs;

public static class AgentSnapshotGenerator
{
    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web)
    {
        WriteIndented = true
    };

    public static string GenerateMarkdown(AgentContentDocument document)
    {
        ArgumentNullException.ThrowIfNull(document);

        var builder = new StringBuilder();
        builder.Append("# ").AppendLine(document.Title.Trim());
        builder.AppendLine();
        builder.Append("> ").AppendLine(document.Summary.Trim());
        builder.AppendLine();
        builder.Append("- Published: ").AppendLine(document.PublishedAt.ToString("O"));
        builder.Append("- Updated: ").AppendLine(document.UpdatedAt.ToString("O"));
        builder.Append("- Canonical URL: ").AppendLine(document.CanonicalUrl.ToString());
        builder.Append("- Source: ").AppendLine(string.IsNullOrWhiteSpace(document.Source) ? "Not provided" : document.Source.Trim());
        builder.Append("- Author: ").AppendLine(string.IsNullOrWhiteSpace(document.Author) ? "Not provided" : document.Author.Trim());
        builder.AppendLine();

        AppendList(builder, "Key Facts", document.KeyFacts);
        AppendList(builder, "Tags", document.Tags);

        builder.AppendLine("## Body");
        builder.AppendLine();
        builder.AppendLine(document.Body.Trim());
        builder.AppendLine();

        AppendLinks(builder, "Related Links", document.RelatedLinks);
        AppendLinks(builder, "Data Sources", document.DataSources);

        builder.AppendLine("## Usage Policy");
        builder.AppendLine();
        builder.AppendLine(document.UsagePolicy.Trim());
        builder.AppendLine();

        builder.AppendLine("## Agent Visibility Policy");
        builder.AppendLine();
        builder.Append("- Include in sitemap: ").AppendLine(document.VisibilityPolicy.IncludeInSitemap ? "yes" : "no");
        builder.Append("- Include in llms.txt: ").AppendLine(document.VisibilityPolicy.IncludeInLlmsText ? "yes" : "no");
        builder.Append("- Search indexing: ").AppendLine(document.VisibilityPolicy.AllowSearchIndexing ? "allowed" : "blocked");
        builder.Append("- AI training: ").AppendLine(document.VisibilityPolicy.AllowAiTraining ? "allowed" : "blocked");
        builder.Append("- RAG extraction: ").AppendLine(document.VisibilityPolicy.AllowRagExtraction ? "allowed" : "blocked");
        builder.Append("- Attribution: ").AppendLine(document.VisibilityPolicy.AttributionRequirement.Trim());

        return builder.ToString().TrimEnd() + Environment.NewLine;
    }

    public static AgentJsonSnapshot CreateJsonSnapshot(AgentContentDocument document)
    {
        ArgumentNullException.ThrowIfNull(document);

        return new AgentJsonSnapshot(
            document.Id,
            document.ContentType,
            document.Title,
            document.Slug,
            document.Summary,
            document.KeyFacts,
            document.Body,
            document.Source,
            document.PublishedAt,
            document.UpdatedAt,
            document.CanonicalUrl,
            document.DataSources,
            document.RelatedLinks,
            document.VisibilityPolicy,
            document.UsagePolicy);
    }

    public static string GenerateJson(AgentContentDocument document)
    {
        return JsonSerializer.Serialize(CreateJsonSnapshot(document), JsonOptions) + Environment.NewLine;
    }

    private static void AppendList(StringBuilder builder, string heading, IReadOnlyList<string> values)
    {
        builder.Append("## ").AppendLine(heading);
        builder.AppendLine();

        if (values.Count == 0)
        {
            builder.AppendLine("- Not provided");
            builder.AppendLine();
            return;
        }

        foreach (var value in values.Where(value => !string.IsNullOrWhiteSpace(value)).Order(StringComparer.OrdinalIgnoreCase))
        {
            builder.Append("- ").AppendLine(value.Trim());
        }

        builder.AppendLine();
    }

    private static void AppendLinks(StringBuilder builder, string heading, IReadOnlyList<AgentLink> links)
    {
        builder.Append("## ").AppendLine(heading);
        builder.AppendLine();

        if (links.Count == 0)
        {
            builder.AppendLine("- Not provided");
            builder.AppendLine();
            return;
        }

        foreach (var link in links.OrderBy(link => link.Title, StringComparer.OrdinalIgnoreCase))
        {
            builder.Append("- [").Append(link.Title.Trim()).Append("](").Append(link.Url).Append(')');
            if (!string.IsNullOrWhiteSpace(link.Description))
            {
                builder.Append(" - ").Append(link.Description.Trim());
            }

            builder.AppendLine();
        }

        builder.AppendLine();
    }
}
