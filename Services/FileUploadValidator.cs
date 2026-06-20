using Microsoft.AspNetCore.Http;

namespace DigifyCXIntranet.Services;

public enum UploadFileKind
{
    Resume,
    Image
}

public sealed record FileUploadValidationPolicy(
    UploadFileKind Kind,
    long MaxBytes,
    bool Required);

public static class FileUploadPolicies
{
    public static readonly FileUploadValidationPolicy RequiredResume =
        new(UploadFileKind.Resume, 5 * 1024 * 1024, Required: true);

    public static readonly FileUploadValidationPolicy OptionalResume =
        new(UploadFileKind.Resume, 5 * 1024 * 1024, Required: false);

    public static readonly FileUploadValidationPolicy RequiredImage =
        new(UploadFileKind.Image, 3 * 1024 * 1024, Required: true);

    public static readonly FileUploadValidationPolicy OptionalImage =
        new(UploadFileKind.Image, 3 * 1024 * 1024, Required: false);
}

public static class FileUploadValidator
{
    private static readonly byte[] PdfSignature = new byte[] { 0x25, 0x50, 0x44, 0x46, 0x2D };
    private static readonly byte[] ZipLocalFileHeaderSignature = new byte[] { 0x50, 0x4B, 0x03, 0x04 };
    private static readonly byte[] ZipEmptyArchiveSignature = new byte[] { 0x50, 0x4B, 0x05, 0x06 };
    private static readonly byte[] ZipSpannedArchiveSignature = new byte[] { 0x50, 0x4B, 0x07, 0x08 };
    private static readonly byte[] JpegSignature = new byte[] { 0xFF, 0xD8, 0xFF };
    private static readonly byte[] PngSignature = new byte[] { 0x89, 0x50, 0x4E, 0x47, 0x0D, 0x0A, 0x1A, 0x0A };
    private static readonly byte[] RiffSignature = new byte[] { 0x52, 0x49, 0x46, 0x46 };

    private static readonly HashSet<string> ResumeExtensions = new(StringComparer.OrdinalIgnoreCase)
    {
        ".pdf",
        ".docx"
    };

    private static readonly HashSet<string> ImageExtensions = new(StringComparer.OrdinalIgnoreCase)
    {
        ".jpg",
        ".jpeg",
        ".png",
        ".webp"
    };

    private static readonly HashSet<string> ResumeContentTypes = new(StringComparer.OrdinalIgnoreCase)
    {
        "application/pdf",
        "application/vnd.openxmlformats-officedocument.wordprocessingml.document",
        "application/octet-stream"
    };

    private static readonly HashSet<string> ImageContentTypes = new(StringComparer.OrdinalIgnoreCase)
    {
        "image/jpeg",
        "image/png",
        "image/webp",
        "application/octet-stream"
    };

    public static async Task ValidateAsync(
        IFormFile? file,
        FileUploadValidationPolicy policy,
        CancellationToken cancellationToken = default)
    {
        if (file is null || file.Length <= 0)
        {
            if (policy.Required)
            {
                throw new InvalidOperationException(policy.Kind == UploadFileKind.Resume
                    ? "Resume file is required."
                    : "Image file is required.");
            }

            return;
        }

        if (file.Length > policy.MaxBytes)
        {
            throw new InvalidOperationException(policy.Kind == UploadFileKind.Resume
                ? "Resume file must be 5 MB or less."
                : "Image file must be 3 MB or less.");
        }

        var fileName = SafeFileNames.Normalize(file.FileName, "upload.bin");
        var extension = Path.GetExtension(fileName);
        var allowedExtensions = policy.Kind == UploadFileKind.Resume ? ResumeExtensions : ImageExtensions;
        if (string.IsNullOrWhiteSpace(extension) || !allowedExtensions.Contains(extension))
        {
            throw new InvalidOperationException(policy.Kind == UploadFileKind.Resume
                ? "Only PDF and DOCX files are allowed."
                : "Only .jpg, .jpeg, .png and .webp files are allowed.");
        }

        var contentType = string.IsNullOrWhiteSpace(file.ContentType)
            ? "application/octet-stream"
            : file.ContentType;
        var allowedContentTypes = policy.Kind == UploadFileKind.Resume ? ResumeContentTypes : ImageContentTypes;
        if (!allowedContentTypes.Contains(contentType))
        {
            throw new InvalidOperationException("Uploaded file content type is not allowed.");
        }

        if (!await HasExpectedSignatureAsync(file, extension, cancellationToken))
        {
            throw new InvalidOperationException("Uploaded file content does not match its extension.");
        }
    }

    private static async Task<bool> HasExpectedSignatureAsync(
        IFormFile file,
        string extension,
        CancellationToken cancellationToken)
    {
        var header = new byte[16];
        await using var stream = file.OpenReadStream();
        var bytesRead = await stream.ReadAsync(header.AsMemory(0, header.Length), cancellationToken);

        return extension.ToLowerInvariant() switch
        {
            ".pdf" => StartsWith(header, bytesRead, PdfSignature),
            ".docx" => StartsWith(header, bytesRead, ZipLocalFileHeaderSignature)
                || StartsWith(header, bytesRead, ZipEmptyArchiveSignature)
                || StartsWith(header, bytesRead, ZipSpannedArchiveSignature),
            ".jpg" or ".jpeg" => StartsWith(header, bytesRead, JpegSignature),
            ".png" => StartsWith(header, bytesRead, PngSignature),
            ".webp" => IsWebp(header, bytesRead),
            _ => false
        };
    }

    private static bool IsWebp(byte[] header, int bytesRead)
    {
        return bytesRead >= 12
            && StartsWith(header, bytesRead, RiffSignature)
            && header[8] == (byte)'W'
            && header[9] == (byte)'E'
            && header[10] == (byte)'B'
            && header[11] == (byte)'P';
    }

    private static bool StartsWith(byte[] value, int bytesRead, ReadOnlySpan<byte> expected)
    {
        return bytesRead >= expected.Length && value.AsSpan(0, expected.Length).SequenceEqual(expected);
    }
}
