using Ganss.Xss;

namespace DigifyCXIntranet.Services;

public sealed class ZendeskHtmlSanitizer : IZendeskHtmlSanitizer
{
    private static readonly string[] AllowedTags =
    {
        "a", "blockquote", "br", "code", "div", "em", "h1", "h2", "h3", "h4",
        "h5", "h6", "hr", "i", "img", "li", "ol", "p", "pre", "span", "strong",
        "sub", "sup", "table", "tbody", "td", "th", "thead", "tr", "u", "ul"
    };

    private static readonly string[] AllowedAttributes =
    {
        "alt", "class", "colspan", "height", "href", "rel", "rowspan", "src", "title", "width"
    };

    public string Sanitize(string html)
    {
        if (string.IsNullOrWhiteSpace(html))
        {
            return string.Empty;
        }

        var sanitizer = new HtmlSanitizer();
        sanitizer.AllowedTags.Clear();
        sanitizer.AllowedTags.UnionWith(AllowedTags);
        sanitizer.AllowedAttributes.Clear();
        sanitizer.AllowedAttributes.UnionWith(AllowedAttributes);
        sanitizer.AllowedCssProperties.Clear();
        sanitizer.AllowedSchemes.Clear();
        sanitizer.AllowedSchemes.Add("http");
        sanitizer.AllowedSchemes.Add("https");
        sanitizer.AllowedSchemes.Add("mailto");

        return sanitizer.Sanitize(html);
    }

    public string? SanitizeHttpsUrl(string? value)
    {
        if (!Uri.TryCreate(value, UriKind.Absolute, out var uri) || uri.Scheme != Uri.UriSchemeHttps)
        {
            return null;
        }

        return uri.ToString();
    }
}
