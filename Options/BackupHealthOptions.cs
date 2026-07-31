using System.ComponentModel.DataAnnotations;

namespace DigifyCXIntranet.Options;

public class BackupHealthOptions
{
    public const string SectionName = "BackupHealth";

    public bool Enabled { get; set; } = false;

    [Range(1, 168)]
    public int MaxFullBackupAgeHours { get; set; } = 26;

    [Range(1, 72)]
    public int MaxDifferentialBackupAgeHours { get; set; } = 6;

    [Range(1, 24)]
    public int MaxLogBackupAgeHours { get; set; } = 2;

    [MaxLength(128)]
    public string DatabaseName { get; set; } = string.Empty;
}
