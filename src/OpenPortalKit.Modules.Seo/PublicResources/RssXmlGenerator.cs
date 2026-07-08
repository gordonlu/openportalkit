using System.Globalization;
using System.Text;
using System.Xml;

namespace OpenPortalKit.Modules.Seo.PublicResources;

public static class RssXmlGenerator
{
    private static readonly UTF8Encoding Utf8NoBom = new(encoderShouldEmitUTF8Identifier: false);

    public static string Generate(RssFeed feed)
    {
        ArgumentNullException.ThrowIfNull(feed);

        using var stream = new MemoryStream();
        using var writer = XmlWriter.Create(stream, new XmlWriterSettings
        {
            Async = false,
            Encoding = Utf8NoBom,
            Indent = false,
            OmitXmlDeclaration = false
        });

        writer.WriteStartDocument();
        writer.WriteStartElement("rss");
        writer.WriteAttributeString("version", "2.0");
        writer.WriteStartElement("channel");
        writer.WriteElementString("title", feed.Title);
        writer.WriteElementString("description", feed.Description);
        writer.WriteElementString("link", feed.Link.ToString());
        writer.WriteElementString("lastBuildDate", FormatRfc1123(feed.UpdatedAt));

        foreach (var item in feed.Items.OrderByDescending(item => item.PublishedAt))
        {
            writer.WriteStartElement("item");
            writer.WriteElementString("title", item.Title);
            writer.WriteElementString("description", item.Description);
            writer.WriteElementString("link", item.Link.ToString());
            writer.WriteElementString("guid", item.Guid ?? item.Link.ToString());
            writer.WriteElementString("pubDate", FormatRfc1123(item.PublishedAt));
            writer.WriteEndElement();
        }

        writer.WriteEndElement();
        writer.WriteEndElement();
        writer.WriteEndDocument();
        writer.Flush();

        return Utf8NoBom.GetString(stream.ToArray());
    }

    private static string FormatRfc1123(DateTimeOffset value)
    {
        return value.ToUniversalTime().ToString("R", CultureInfo.InvariantCulture);
    }
}
