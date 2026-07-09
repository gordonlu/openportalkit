using System.Text;

namespace OpenPortalKit.Modules.AgentAccess.AgentOutputs;

public static class LlmsTextGenerator
{
    public static string Generate(AgentSiteProfile profile, IReadOnlyList<AgentContentDocument> documents, bool includeFullContent)
    {
        ArgumentNullException.ThrowIfNull(profile);
        ArgumentNullException.ThrowIfNull(documents);

        var builder = new StringBuilder();
        builder.Append("# ").AppendLine(profile.SiteName.Trim());
        builder.AppendLine();
        builder.AppendLine(profile.Description.Trim());
        builder.AppendLine();

        AppendSections(builder, profile);
        AppendResources(builder, profile);
        AppendImportantContent(builder, documents, includeFullContent);

        builder.AppendLine("## Usage Policy");
        builder.AppendLine();
        builder.AppendLine(profile.UsagePolicy.Trim());
        builder.AppendLine();
        builder.AppendLine("## Attribution Policy");
        builder.AppendLine();
        builder.AppendLine(profile.AttributionPolicy.Trim());

        return builder.ToString().TrimEnd() + Environment.NewLine;
    }

    private static void AppendSections(StringBuilder builder, AgentSiteProfile profile)
    {
        builder.AppendLine("## Main Sections");
        builder.AppendLine();

        foreach (var section in profile.Sections.OrderBy(section => section.Name, StringComparer.OrdinalIgnoreCase))
        {
            builder.Append("- [").Append(section.Name.Trim()).Append("](").Append(section.Url).Append(") - ")
                .AppendLine(section.Description.Trim());
        }

        builder.AppendLine();
    }

    private static void AppendResources(StringBuilder builder, AgentSiteProfile profile)
    {
        builder.AppendLine("## Machine-Readable Resources");
        builder.AppendLine();
        builder.Append("- Sitemap: ").AppendLine(profile.SitemapUrl.ToString());
        builder.Append("- RSS: ").AppendLine(profile.RssUrl.ToString());
        builder.Append("- Public API: ").AppendLine(profile.PublicApiUrl.ToString());
        builder.Append("- OpenAPI: ").AppendLine(profile.OpenApiUrl.ToString());
        builder.Append("- llms.txt: ").AppendLine(profile.LlmsTextUrl.ToString());
        builder.Append("- llms-full.txt: ").AppendLine(profile.LlmsFullTextUrl.ToString());
        builder.Append("- Agent manifest: ").AppendLine(profile.AgentManifestUrl.ToString());
        builder.AppendLine();

        builder.AppendLine("## Important Public URLs");
        builder.AppendLine();
        foreach (var link in profile.ImportantUrls.OrderBy(link => link.Title, StringComparer.OrdinalIgnoreCase))
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

    private static void AppendImportantContent(StringBuilder builder, IReadOnlyList<AgentContentDocument> documents, bool includeFullContent)
    {
        builder.AppendLine("## Important Content");
        builder.AppendLine();

        foreach (var document in documents
            .Where(document => document.VisibilityPolicy.IncludeInLlmsText)
            .OrderBy(document => document.Title, StringComparer.OrdinalIgnoreCase))
        {
            builder.Append("- [").Append(document.Title.Trim()).Append("](").Append(document.CanonicalUrl).Append(") - ")
                .AppendLine(document.Summary.Trim());

            if (includeFullContent)
            {
                builder.AppendLine();
                builder.AppendLine("### " + document.Title.Trim());
                builder.AppendLine();
                builder.AppendLine(document.Body.Trim());
                builder.AppendLine();
            }
        }

        builder.AppendLine();
    }
}
