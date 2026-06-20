using System.Text;
using DigifyCXIntranet.Services;
using FluentAssertions;
using Microsoft.AspNetCore.Http;

namespace DigifyCXIntranet.Tests.Services;

public sealed class FileUploadValidatorTests
{
    [Fact]
    public async Task ValidateAsync_AcceptsPdfResumeWithMatchingSignature()
    {
        var file = CreateFile("%PDF-1.7\n", "resume.pdf", "application/pdf");

        var action = () => FileUploadValidator.ValidateAsync(file, FileUploadPolicies.RequiredResume);

        await action.Should().NotThrowAsync();
    }

    [Fact]
    public async Task ValidateAsync_RejectsMismatchedExtensionAndSignature()
    {
        var file = CreateFile("not a pdf", "resume.pdf", "application/pdf");

        var action = () => FileUploadValidator.ValidateAsync(file, FileUploadPolicies.RequiredResume);

        await action.Should().ThrowAsync<InvalidOperationException>()
            .WithMessage("Uploaded file content does not match its extension.");
    }

    [Fact]
    public void SafeFileNames_Normalize_RemovesPathTraversalSegments()
    {
        var safeName = SafeFileNames.Normalize(@"..\..\secret.txt", "fallback.bin");

        safeName.Should().Be("secret.txt");
    }

    private static IFormFile CreateFile(string content, string fileName, string contentType)
    {
        var bytes = Encoding.UTF8.GetBytes(content);
        var stream = new MemoryStream(bytes);
        return new FormFile(stream, 0, bytes.Length, "file", fileName)
        {
            Headers = new HeaderDictionary(),
            ContentType = contentType
        };
    }
}
