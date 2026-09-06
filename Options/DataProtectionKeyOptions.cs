using System.ComponentModel.DataAnnotations;

namespace DigifyCXIntranet.Options;

public sealed class DataProtectionKeyOptions
{
    public const string SectionName = "DataProtection";

    [Required, MaxLength(200)]
    public string ApplicationName { get; set; } = "DigifyCXIntranet";

    [Required, MaxLength(500)]
    public string KeyRingPath { get; set; } = @"App_Data\DataProtection-Keys";

    public bool ProtectKeysWithDpapi { get; set; } = true;
}
