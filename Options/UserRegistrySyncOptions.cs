using System.ComponentModel.DataAnnotations;

namespace DigifyCXIntranet.Options;

public class UserRegistrySyncOptions
{
    public const string SectionName = "UserRegistrySync";

    public bool Enabled { get; set; } = true;

    [Url]
    public string SheetApiUrl { get; set; } = string.Empty;

    /// <summary>
    /// Initial startup delay before the first sync, in seconds.
    /// </summary>
    [Range(0, 300)]
    public int InitialDelaySeconds { get; set; } = 5;

    /// <summary>
    /// How many hours between syncs. Default 24 = once per day.
    /// </summary>
    [Range(1, 168)]
    public int SyncIntervalHours { get; set; } = 24;

    [Range(5, 120)]
    public int TimeoutSeconds { get; set; } = 30;

    /// <summary>
    /// Target time-of-day (UTC) at which the daily sync should fire.
    /// Format: "HH:mm" e.g. "03:00" = 3 AM UTC = 5 AM SAST.
    /// Leave empty to run immediately at startup then every SyncIntervalHours.
    /// </summary>
    public string SyncTimeUtc { get; set; } = "03:00";

    /// <summary>
    /// When true the worker will delete DB accounts whose names are
    /// no longer present in the Google Sheet (terminated employees).
    /// Set to false to disable automatic deletion during testing.
    /// </summary>
    public bool DeleteRemovedUsers { get; set; } = true;
}
