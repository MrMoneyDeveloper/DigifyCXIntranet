using System.Security.Claims;
using DigifyCXIntranet.Pages.Jobs;
using FluentAssertions;

namespace DigifyCXIntranet.Tests;

public class RazorPageBindingTests
{
    [Fact]
    public void InternalApplicationIdentity_ComesFromAuthenticatedClaims()
    {
        var principal = CreatePrincipal(
            new Claim(ClaimTypes.Name, @"DOMAIN\employee.one"),
            new Claim(ClaimTypes.Email, "employee.one@example.com"));

        var identity = ApplyModel.ResolveEmployeeIdentity(principal);

        identity.Name.Should().Be("employee.one");
        identity.Email.Should().Be("employee.one@example.com");
    }

    [Fact]
    public void InternalApplicationIdentity_UsesLocalFallbackWhenEmailClaimIsMissing()
    {
        var principal = CreatePrincipal(new Claim(ClaimTypes.Name, "employee.one"));

        ApplyModel.ResolveEmployeeIdentity(principal).Email
            .Should().Be("employee.one@company.local");
    }

    [Fact]
    public void InternalApplicationInput_DoesNotExposeEmployeeIdentityForBinding()
    {
        var inputType = typeof(ApplyModel.InputModel);

        inputType.GetProperty("EmployeeName").Should().BeNull();
        inputType.GetProperty("EmployeeEmail").Should().BeNull();
    }

    private static ClaimsPrincipal CreatePrincipal(params Claim[] claims)
    {
        return new ClaimsPrincipal(new ClaimsIdentity(claims, "Test"));
    }
}
