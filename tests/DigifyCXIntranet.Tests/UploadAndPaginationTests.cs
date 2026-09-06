using DigifyCXIntranet.Models;
using DigifyCXIntranet.Services;
using FluentAssertions;
using Microsoft.AspNetCore.Http;

namespace DigifyCXIntranet.Tests;

public class UploadAndPaginationTests
{
    [Fact]
    public async Task UploadValidator_AcceptsPdfWithMatchingSignature()
    {
        var bytes = "%PDF-1.7 test"u8.ToArray();
        var file = CreateFile(bytes, "resume.pdf", "application/pdf");

        var action = () => FileUploadValidator.ValidateAsync(file, FileUploadPolicies.RequiredResume);

        await action.Should().NotThrowAsync();
    }

    [Fact]
    public async Task UploadValidator_RejectsExtensionSignatureMismatch()
    {
        var file = CreateFile("not a pdf"u8.ToArray(), "resume.pdf", "application/pdf");

        var action = () => FileUploadValidator.ValidateAsync(file, FileUploadPolicies.RequiredResume);

        await action.Should().ThrowAsync<InvalidOperationException>()
            .WithMessage("*does not match*");
    }

    [Fact]
    public void SafeFileName_RemovesDirectoryTraversal()
    {
        SafeFileNames.Normalize("../../private/resume.pdf", "fallback.pdf")
            .Should().Be("resume.pdf");
    }

    [Fact]
    public void Pagination_ComputesStableBounds()
    {
        var page = new PaginationViewModel(PageNumber: 3, PageSize: 25, TotalCount: 63);

        page.TotalPages.Should().Be(3);
        page.FirstItemNumber.Should().Be(51);
        page.LastItemNumber.Should().Be(63);
        page.HasPreviousPage.Should().BeTrue();
        page.HasNextPage.Should().BeFalse();
    }

    private static FormFile CreateFile(byte[] bytes, string fileName, string contentType)
    {
        var stream = new MemoryStream(bytes);
        return new FormFile(stream, 0, bytes.Length, "upload", fileName)
        {
            Headers = new HeaderDictionary(),
            ContentType = contentType
        };
    }
}
