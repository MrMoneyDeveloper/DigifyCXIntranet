using System.ComponentModel.DataAnnotations;
using Microsoft.Extensions.Options;

namespace DigifyCXIntranet.Options;

public class RateLimitPoliciesOptions
{
    public const string SectionName = "RateLimitPolicies";

    [Range(1, 1000)]
    public int GlobalPermitLimit { get; set; } = 240;

    [Range(1, 60)]
    public int GlobalWindowSeconds { get; set; } = 60;

    [ValidateObjectMembers]
    public EndpointRateLimitOptions Login { get; set; } = new() { PermitLimit = 8, WindowMinutes = 5 };
    [ValidateObjectMembers]
    public EndpointRateLimitOptions ForgotPassword { get; set; } = new() { PermitLimit = 4, WindowMinutes = 10 };
    [ValidateObjectMembers]
    public EndpointRateLimitOptions AccountActivation { get; set; } = new() { PermitLimit = 6, WindowMinutes = 10 };
    [ValidateObjectMembers]
    public EndpointRateLimitOptions PasswordReset { get; set; } = new() { PermitLimit = 6, WindowMinutes = 10 };
    [ValidateObjectMembers]
    public EndpointRateLimitOptions ExternalApplication { get; set; } = new() { PermitLimit = 12, WindowMinutes = 10 };
    [ValidateObjectMembers]
    public EndpointRateLimitOptions ZendeskWebhook { get; set; } = new() { PermitLimit = 60, WindowMinutes = 1 };
    [ValidateObjectMembers]
    public EndpointRateLimitOptions CspReport { get; set; } = new() { PermitLimit = 30, WindowMinutes = 1 };
}

public class EndpointRateLimitOptions
{
    [Range(1, 200)]
    public int PermitLimit { get; set; } = 10;

    [Range(1, 120)]
    public int WindowMinutes { get; set; } = 10;
}
