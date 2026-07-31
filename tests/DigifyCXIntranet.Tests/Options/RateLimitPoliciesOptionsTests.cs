using DigifyCXIntranet.Options;
using FluentAssertions;

namespace DigifyCXIntranet.Tests.Options;

public sealed class RateLimitPoliciesOptionsTests
{
    [Fact]
    public void Defaults_UseStricterLimitsForLoginAndPublicFormsThanGlobalLimit()
    {
        var options = new RateLimitPoliciesOptions();

        options.GlobalPermitLimit.Should().BeGreaterThan(options.Login.PermitLimit);
        options.GlobalPermitLimit.Should().BeGreaterThan(options.ForgotPassword.PermitLimit);
        options.GlobalPermitLimit.Should().BeGreaterThan(options.ExternalApplication.PermitLimit);
    }
}
