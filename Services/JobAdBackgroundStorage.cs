using Microsoft.AspNetCore.Http;

namespace DigifyCXIntranet.Services;

public static class JobAdBackgroundStorage
{
    private static readonly HashSet<string> AllowedExtensions = new(StringComparer.OrdinalIgnoreCase)
    {
        ".jpg",
        ".jpeg",
        ".png",
        ".webp"
    };

    public static async Task<string> SaveAsync(IFormFile file, string webRootPath)
    {
        var extension = Path.GetExtension(file.FileName);
        if (string.IsNullOrWhiteSpace(extension) || !AllowedExtensions.Contains(extension))
        {
            throw new InvalidOperationException("Only .jpg, .jpeg, .png and .webp files are allowed.");
        }

        var folder = Path.Combine(webRootPath, "uploads", "job-ads");
        Directory.CreateDirectory(folder);

        var fileName = $"jobad_{DateTime.UtcNow:yyyyMMddHHmmssfff}_{Guid.NewGuid():N}{extension.ToLowerInvariant()}";
        var fullPath = Path.Combine(folder, fileName);

        await using var stream = File.Create(fullPath);
        await file.CopyToAsync(stream);

        return $"/uploads/job-ads/{fileName}";
    }
}
