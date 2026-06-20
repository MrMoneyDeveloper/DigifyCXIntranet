using System.ComponentModel.DataAnnotations;

namespace DigifyCXIntranet.Options;

public class UserRegistrySyncOptions
{
    public const string SectionName = "UserRegistrySync";

    public bool Enabled { get; set; } = true;

    [Url]
    public string SheetApiUrl { get; set; } =
        "https://script.google.com/macros/s/AKfycbxrrhzqV_pFVYpUH-vv2i7EUA5x7i184HokCBVdUxNJVe49r6RBwxI24S2ZVauUo9A5Zg/exec";

    [Range(0, 300)]
    public int InitialDelaySeconds { get; set; } = 5;

    [Range(1, 168)]
    public int SyncIntervalHours { get; set; } = 24;

    [Range(5, 120)]
    public int TimeoutSeconds { get; set; } = 30;
}
