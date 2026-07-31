using DigifyCXIntranet.Options;
using DigifyCXIntranet.Services;
using FluentAssertions;

namespace DigifyCXIntranet.Tests.Services;

public sealed class PayrollScheduleTests
{
    [Fact]
    public void ShouldRunNow_ReturnsTrueOnConfiguredDayAfterHour()
    {
        var clock = new FixedClock(new DateTimeOffset(2026, 6, 25, 1, 0, 0, TimeSpan.Zero));
        var options = new PayrollOptions { RunDayOfMonth = 25, RunHour24 = 0, TimeZoneId = "UTC" };

        PayrollSchedule.ShouldRunNow(options, clock).Should().BeTrue();
    }

    [Fact]
    public void GetDelayUntilNextRun_DoesNotReturnImmediateSecondRunAfterDueTime()
    {
        var clock = new FixedClock(new DateTimeOffset(2026, 6, 25, 1, 0, 0, TimeSpan.Zero));
        var options = new PayrollOptions { RunDayOfMonth = 25, RunHour24 = 0, TimeZoneId = "UTC" };

        var delay = PayrollSchedule.GetDelayUntilNextRun(options, clock);

        delay.Should().BeCloseTo(TimeSpan.FromDays(30).Subtract(TimeSpan.FromHours(1)), TimeSpan.FromMinutes(1));
    }

    private sealed class FixedClock : IClock
    {
        public FixedClock(DateTimeOffset utcNow)
        {
            UtcNow = utcNow;
        }

        public DateTimeOffset UtcNow { get; }
        public DateTimeOffset NowInZone(string timeZoneId) => TimeZoneInfo.ConvertTime(UtcNow, ResolveTimeZone(timeZoneId));
        public TimeZoneInfo ResolveTimeZone(string timeZoneId) => TimeZoneInfo.FindSystemTimeZoneById(timeZoneId);
    }
}
