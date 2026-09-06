using System.Text.Json;

namespace DigifyCXIntranet.Services;

public sealed record CspViolationReport(
    string DocumentUri,
    string ViolatedDirective,
    string EffectiveDirective,
    string BlockedUri,
    string Disposition)
{
    private const int MaxFieldLength = 300;

    public static CspViolationReport? Parse(JsonElement root)
    {
        if (root.ValueKind == JsonValueKind.Array && root.GetArrayLength() > 0)
        {
            root = root[0];
        }

        if (root.ValueKind != JsonValueKind.Object)
        {
            return null;
        }

        if (root.TryGetProperty("csp-report", out var legacyReport))
        {
            root = legacyReport;
        }
        else if (root.TryGetProperty("body", out var reportBody))
        {
            root = reportBody;
        }

        if (root.ValueKind != JsonValueKind.Object)
        {
            return null;
        }

        return new CspViolationReport(
            NormalizeUri(Read(root, "document-uri", "documentURL")),
            NormalizeText(Read(root, "violated-directive", "violatedDirective")),
            NormalizeText(Read(root, "effective-directive", "effectiveDirective")),
            NormalizeUri(Read(root, "blocked-uri", "blockedURL")),
            NormalizeText(Read(root, "disposition")));
    }

    private static string Read(JsonElement element, params string[] names)
    {
        foreach (var name in names)
        {
            if (element.TryGetProperty(name, out var value) && value.ValueKind == JsonValueKind.String)
            {
                return value.GetString() ?? string.Empty;
            }
        }

        return string.Empty;
    }

    private static string NormalizeUri(string value)
    {
        if (Uri.TryCreate(value, UriKind.Absolute, out var uri))
        {
            if (string.Equals(uri.Scheme, Uri.UriSchemeHttp, StringComparison.OrdinalIgnoreCase) ||
                string.Equals(uri.Scheme, Uri.UriSchemeHttps, StringComparison.OrdinalIgnoreCase))
            {
                var withoutCredentialsOrQuery = uri.GetComponents(
                    UriComponents.SchemeAndServer | UriComponents.Path,
                    UriFormat.UriEscaped);
                return NormalizeText(withoutCredentialsOrQuery);
            }

            return NormalizeText($"{uri.Scheme}:");
        }

        return NormalizeText(value);
    }

    private static string NormalizeText(string value)
    {
        var normalized = value.Replace('\r', ' ').Replace('\n', ' ').Trim();
        return normalized.Length <= MaxFieldLength ? normalized : normalized[..MaxFieldLength];
    }
}
