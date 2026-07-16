using DigifyCXIntranet.Options;
using DigifyCXIntranet.Services;
using FluentAssertions;

namespace DigifyCXIntranet.Tests.Services;

public class ConfiguredTestAccountCatalogTests
{
    [Fact]
    public void ValidateAndNormalize_NormalizesConfiguredAccount()
    {
        var options = new AuthModeOptions
        {
            DevelopmentUsers =
            [
                new DevelopmentUserOption
                {
                    Username = "  test.user  ",
                    Password = "Example123",
                    DisplayName = "  Test User  ",
                    Role = "superadmin"
                }
            ]
        };

        var account = ConfiguredTestAccountCatalog.ValidateAndNormalize(options).Single();

        account.Username.Should().Be("test.user");
        account.DisplayName.Should().Be("Test User");
        account.Role.Should().Be(AppRoles.SuperAdmin);
    }

    [Fact]
    public void ValidateAndNormalize_RejectsDuplicateUsernamesIgnoringCase()
    {
        var options = new AuthModeOptions
        {
            DevelopmentUsers =
            [
                new DevelopmentUserOption { Username = "test.user", Password = "Example123", Role = AppRoles.Employee },
                new DevelopmentUserOption { Username = "TEST.USER", Password = "Example456", Role = AppRoles.HrAdmin }
            ]
        };

        var action = () => ConfiguredTestAccountCatalog.ValidateAndNormalize(options);

        action.Should().Throw<InvalidOperationException>()
            .WithMessage("*duplicates another username*");
    }

    [Fact]
    public void ValidateAndNormalize_RejectsUnsupportedRole()
    {
        var options = new AuthModeOptions
        {
            DevelopmentUsers =
            [
                new DevelopmentUserOption { Username = "test.user", Password = "Example123", Role = "UnknownRole" }
            ]
        };

        var action = () => ConfiguredTestAccountCatalog.ValidateAndNormalize(options);

        action.Should().Throw<InvalidOperationException>()
            .WithMessage("*unsupported role*");
    }

    [Fact]
    public void InternalTestSwitches_DefaultToDisabled()
    {
        var options = new AuthModeOptions();

        options.AllowInsecureHttpForInternalTest.Should().BeFalse();
        options.SeedConfiguredTestUsers.Should().BeFalse();
    }
}
