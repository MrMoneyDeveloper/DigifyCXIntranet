using System.Text.RegularExpressions;

namespace DigifyCXIntranet.Services;

internal static partial class AuditRedactor
{
    public static string Redact(string? value)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            return string.Empty;
        }

        var redacted = SensitiveAssignmentPattern().Replace(value, "$1=[redacted]");
        return BearerTokenPattern().Replace(redacted, "Bearer [redacted]");
    }

    [GeneratedRegex(@"(?i)\b(password|token|api[-_]?key|apitoken|secret|authorization)\b[\""']?\s*[:=]\s*(?:\""[^\""\r\n]*\""|'[^'\r\n]*'|[^;,&\r\n]*)")]
    private static partial Regex SensitiveAssignmentPattern();

    [GeneratedRegex(@"(?i)\bBearer\s+[A-Za-z0-9._~+/=-]+")]
    private static partial Regex BearerTokenPattern();
}
