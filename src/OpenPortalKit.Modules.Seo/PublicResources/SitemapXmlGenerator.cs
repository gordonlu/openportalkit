using System.Globalization;
using System.Text;
using System.Xml;

namespace OpenPortalKit.Modules.Seo.PublicResources;

public static class SitemapXmlGenerator
{
    private const string SitemapNamespace = "http://www.sitemaps.org/schemas/sitemap/0.9";
    private static readonly UTF8Encoding Utf8NoBom = new(encoderShouldEmitUTF8Identifier: false);

    public static string Generate(IEnumerable<SitemapEntry> entries)
    {
        ArgumentNullException.ThrowIfNull(entries);

        using var stream = new MemoryStream();
        using var writer = XmlWriter.Create(stream, new XmlWriterSettings
        {
            Async = false,
            Encoding = Utf8NoBom,
            Indent = false,
            OmitXmlDeclaration = false
        });

        writer.WriteStartDocument();
        writer.WriteStartElement("urlset", SitemapNamespace);

        foreach (var entry in entries.OrderBy(entry => entry.Location.ToString(), StringComparer.Ordinal))
        {
            writer.WriteStartElement("url", SitemapNamespace);
            writer.WriteElementString("loc", SitemapNamespace, entry.Location.ToString());
            writer.WriteElementString("lastmod", SitemapNamespace, entry.LastModified.ToUniversalTime().ToString("yyyy-MM-ddTHH:mm:ssZ", CultureInfo.InvariantCulture));
            writer.WriteElementString("changefreq", SitemapNamespace, entry.ChangeFrequency.ToString().ToLowerInvariant());
            writer.WriteElementString("priority", SitemapNamespace, entry.Priority.ToString("0.0", CultureInfo.InvariantCulture));
            writer.WriteEndElement();
        }

        writer.WriteEndElement();
        writer.WriteEndDocument();
        writer.Flush();

        return Utf8NoBom.GetString(stream.ToArray());
    }
}
