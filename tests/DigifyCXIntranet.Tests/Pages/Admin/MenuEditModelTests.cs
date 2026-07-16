using DigifyCXIntranet.Pages.Admin;
using FluentAssertions;

namespace DigifyCXIntranet.Tests.Pages.Admin;

public sealed class MenuEditModelTests
{
    [Fact]
    public void ResolveImagePath_KeepsExistingImage_WhenNoReplacementWasUploaded()
    {
        var result = MenuEditModel.ResolveImagePath(
            "/uploads/menu-items/current.png",
            replacementImagePath: null);

        result.Should().Be("/uploads/menu-items/current.png");
    }

    [Fact]
    public void ResolveImagePath_UsesReplacementImage_WhenUploaded()
    {
        var result = MenuEditModel.ResolveImagePath(
            "/uploads/menu-items/current.png",
            " /uploads/menu-items/new.png ");

        result.Should().Be("/uploads/menu-items/new.png");
    }
}
