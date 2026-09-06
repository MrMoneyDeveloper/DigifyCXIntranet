namespace DigifyCXIntranet.Services;

public interface IZendeskHtmlSanitizer
{
    string Sanitize(string html);
    string? SanitizeHttpsUrl(string? value);
}
