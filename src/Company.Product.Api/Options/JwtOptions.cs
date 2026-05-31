using System.ComponentModel.DataAnnotations;

namespace Company.Product.Api.Options;

public sealed class JwtOptions
{
    public const string SectionName = "Authentication:Jwt";

    [Required]
    public string Authority { get; init; } = string.Empty;

    [Required]
    public string Audience { get; init; } = string.Empty;

    public bool RequireHttpsMetadata { get; init; } = true;

    public string ScopeClaimType { get; init; } = "scope";
}
