using DigifyCXIntranet.Options;

namespace DigifyCXIntranet.Services;

public static class PayrollSchedule
{
    public static bool ShouldRunNow(PayrollOptions options, IClock clock)
    {
        var runDay = Math.Clamp(options.RunDayOfMonth, 1, 28);
        var localNow = clock.NowInZone(options.TimeZoneId);
        if (localNow.Day != runDay)
        {
            return false;
        }

        var dueAt = new DateTimeOffset(
            localNow.Year,
            localNow.Month,
            runDay,
            Math.Clamp(options.RunHour24, 0, 23),
            0,
            0,
            localNow.Offset);

        return localNow >= dueAt;
    }

    public static TimeSpan GetDelayUntilNextRun(PayrollOptions options, IClock clock)
    {
        var tzNow = clock.NowInZone(options.TimeZoneId);
        var currentDate = tzNow.Date;
        var runDay = Math.Clamp(options.RunDayOfMonth, 1, 28);
        var runThisMonth = new DateTimeOffset(
            currentDate.Year,
            currentDate.Month,
            runDay,
            Math.Clamp(options.RunHour24, 0, 23),
            0,
            0,
            tzNow.Offset);

        var nextRun = runThisMonth > tzNow ? runThisMonth : runThisMonth.AddMonths(1);
        return nextRun - tzNow;
    }
}
