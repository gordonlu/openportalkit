using System.Text;

namespace OpenPortalKit.Modules.Seo.PublicResources;

public static class RobotsTxtGenerator
{
    public static string Generate(RobotsPolicy policy)
    {
        ArgumentNullException.ThrowIfNull(policy);

        var builder = new StringBuilder();

        foreach (var directive in policy.Directives)
        {
            builder.Append("User-agent: ").AppendLine(directive.UserAgent);

            foreach (var path in directive.Allow)
            {
                builder.Append("Allow: ").AppendLine(path);
            }

            foreach (var path in directive.Disallow)
            {
                builder.Append("Disallow: ").AppendLine(path);
            }

            builder.AppendLine();
        }

        if (policy.SitemapUrl is not null)
        {
            builder.Append("Sitemap: ").AppendLine(policy.SitemapUrl.ToString());
        }

        return builder.ToString().TrimEnd() + Environment.NewLine;
    }
}
