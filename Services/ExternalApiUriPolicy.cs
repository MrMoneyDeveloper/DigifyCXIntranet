namespace DigifyCXIntranet.Services;

internal static class ExternalApiUriPolicy
{
    internal static Uri ResolveSameOrigin(string configuredBaseUrl, string pathOrUrl)
    {
        if (!Uri.TryCreate(configuredBaseUrl, UriKind.Absolute, out var baseUri) ||
            baseUri.Scheme != Uri.UriSchemeHttps ||
            !string.IsNullOrEmpty(baseUri.UserInfo))
        {
            throw new InvalidOperationException("The configured external API base URL is not a safe HTTPS origin.");
        }

        Uri candidate;
        if (Uri.TryCreate(pathOrUrl, UriKind.Absolute, out var absolute))
        {
            candidate = absolute;
        }
        else
        {
            var origin = new Uri($"{baseUri.Scheme}://{baseUri.Authority}/", UriKind.Absolute);
            candidate = new Uri(origin, pathOrUrl.TrimStart('/'));
        }

        if (candidate.Scheme != Uri.UriSchemeHttps ||
            !string.IsNullOrEmpty(candidate.UserInfo) ||
            !string.Equals(candidate.IdnHost, baseUri.IdnHost, StringComparison.OrdinalIgnoreCase) ||
            candidate.Port != baseUri.Port)
        {
            throw new InvalidOperationException("The external API attempted to redirect pagination outside its configured origin.");
        }

        return candidate;
    }

    internal static string? NormalizeAbsoluteHttpsUrl(string? value)
    {
        return Uri.TryCreate(value, UriKind.Absolute, out var uri) &&
               uri.Scheme == Uri.UriSchemeHttps &&
               string.IsNullOrEmpty(uri.UserInfo)
            ? uri.AbsoluteUri
            : null;
    }
}
