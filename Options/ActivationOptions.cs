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
    public string DefaultPassword { get; set; } = "Digify@2026";

    /// <summary>
    /// Apps Script web-app URL that returns the active employee name list as JSON.
    /// </summary>
    public string SheetApiUrl { get; set; } =
        "https://script.google.com/macros/s/AKfycbxrrhzqV_pFVYpUH-vv2i7EUA5x7i184HokCBVdUxNJVe49r6RBwxI24S2ZVauUo9A5Zg/exec";

    /// <summary>
    /// Timeout in seconds for the Google Sheet Apps Script call.
    /// </summary>
    public int SheetTimeoutSeconds { get; set; } = 15;
}
