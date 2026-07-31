using System.ComponentModel.DataAnnotations;

namespace DigifyCXIntranet.Options;

public class RateLimitPoliciesOptions
{
    public const string SectionName = "RateLimitPolicies";

    [Range(1, 1000)]
    public int GlobalPermitLimit { get; set; } = 240;

    [Range(1, 60)]
    public int GlobalWindowSeconds { get; set; } = 60;

    public EndpointRateLimitOptions Login { get; set; } = new() { PermitLimit = 8, WindowMinutes = 5 };
    public EndpointRateLimitOptions ForgotPassword { get; set; } = new() { PermitLimit = 4, WindowMinutes = 10 };
    public EndpointRateLimitOptions AccountActivation { get; set; } = new() { PermitLimit = 6, WindowMinutes = 10 };
    public EndpointRateLimitOptions PasswordReset { get; set; } = new() { PermitLimit = 6, WindowMinutes = 10 };
    public EndpointRateLimitOptions ExternalApplication { get; set; } = new() { PermitLimit = 12, WindowMinutes = 10 };
}

public class EndpointRateLimitOptions
{
    [Range(1, 200)]
    public int PermitLimit { get; set; } = 10;

    [Range(1, 120)]
    public int WindowMinutes { get; set; } = 10;
}
