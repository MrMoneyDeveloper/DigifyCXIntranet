using System.ComponentModel.DataAnnotations;

namespace DigifyCXIntranet.Options;

/// <summary>
/// Configuration for the Account Activation flow.
/// Bind from appsettings.json section "Activation".
/// </summary>
public class ActivationOptions
{
    public const string SectionName = "Activation";

    /// <summary>
    /// The shared default password every new employee must enter on first activation.
    /// Change this value in appsettings.json — no redeployment required.
    /// </summary>
    [Required]
    public string DefaultPassword { get; set; } = string.Empty;

}
