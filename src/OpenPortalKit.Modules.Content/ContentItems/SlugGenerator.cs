using System.Text;

namespace OpenPortalKit.Modules.Content.ContentItems;

public static class SlugGenerator
{
    public static string Generate(string text)
    {
        if (string.IsNullOrWhiteSpace(text))
        {
            return "item";
        }

        var builder = new StringBuilder();
        var previousWasSeparator = false;

        foreach (var character in text.Trim().ToLowerInvariant())
        {
            if (char.IsLetterOrDigit(character))
            {
                builder.Append(character);
                previousWasSeparator = false;
                continue;
            }

            if (previousWasSeparator || builder.Length == 0)
            {
                continue;
            }

            builder.Append('-');
            previousWasSeparator = true;
        }

        return builder.ToString().TrimEnd('-') switch
        {
            "" => "item",
            var slug => slug
        };
    }
}
