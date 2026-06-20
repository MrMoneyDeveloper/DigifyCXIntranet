using Microsoft.AspNetCore.Http;

namespace DigifyCXIntranet.Services;

public static class MenuItemImageStorage
{
    public static async Task<string> SaveAsync(IFormFile file, string webRootPath)
    {
        await FileUploadValidator.ValidateAsync(file, FileUploadPolicies.RequiredImage);

        var safeName = SafeFileNames.Normalize(file.FileName, "menu.jpg");
        var extension = Path.GetExtension(safeName);
        var folder = Path.Combine(webRootPath, "uploads", "menu-items");
        Directory.CreateDirectory(folder);

        var fileName = $"menu_{DateTime.UtcNow:yyyyMMddHHmmssfff}_{Guid.NewGuid():N}{extension.ToLowerInvariant()}";
        var fullPath = Path.Combine(folder, fileName);

        await using var stream = File.Create(fullPath);
        await file.CopyToAsync(stream);

        return $"/uploads/menu-items/{fileName}";
    }
}
