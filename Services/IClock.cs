namespace DigifyCXIntranet.Services;

public interface IClock
{
    DateTimeOffset UtcNow { get; }
    DateTimeOffset NowInZone(string timeZoneId);
    TimeZoneInfo ResolveTimeZone(string timeZoneId);
}
