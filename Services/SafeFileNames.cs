namespace DigifyCXIntranet.Services;

public static class SafeFileNames
{
    public static string Normalize(string? fileName, string fallback)
    {
        var safeName = Path.GetFileName(fileName);
        if (string.IsNullOrWhiteSpace(safeName))
        {
            safeName = fallback;
        }

        foreach (var invalid in Path.GetInvalidFileNameChars())
        {
            safeName = safeName.Replace(invalid, '_');
        }

        return safeName;
    }
}
