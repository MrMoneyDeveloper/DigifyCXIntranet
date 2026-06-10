using DigifyCXIntranet.Data;
using DigifyCXIntranet.Models;
using DigifyCXIntranet.Options;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;

namespace DigifyCXIntranet.Services;

public class MonthlyPayrollHostedService : BackgroundService
{
    private readonly IServiceScopeFactory _scopeFactory;
    private readonly PayrollOptions _payrollOptions;
    private readonly RoutingInboxesOptions _routingOptions;
    private readonly IClock _clock;
    private readonly ILogger<MonthlyPayrollHostedService> _logger;

    public MonthlyPayrollHostedService(
        IServiceScopeFactory scopeFactory,
        IOptions<PayrollOptions> payrollOptions,
        IOptions<RoutingInboxesOptions> routingOptions,
        IClock clock,
        ILogger<MonthlyPayrollHostedService> logger)
    {
        _scopeFactory = scopeFactory;
        _payrollOptions = payrollOptions.Value;
        _routingOptions = routingOptions.Value;
        _clock = clock;
        _logger = logger;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        while (!stoppingToken.IsCancellationRequested)
        {
            if (ShouldRunNow())
            {
                await RunAsync(stoppingToken);
            }

            var delay = GetDelayUntilNextRun();
            await Task.Delay(delay, stoppingToken);
            await RunAsync(stoppingToken);
        }
    }

    private bool ShouldRunNow()
    {
        var runDay = Math.Clamp(_payrollOptions.RunDayOfMonth, 1, 28);
        var localNow = _clock.NowInZone(_payrollOptions.TimeZoneId);
        if (localNow.Day != runDay)
        {
            return false;
        }

        var dueAt = new DateTimeOffset(
            localNow.Year,
            localNow.Month,
            runDay,
            Math.Clamp(_payrollOptions.RunHour24, 0, 23),
            0,
            0,
            localNow.Offset);

        return localNow >= dueAt;
    }

    private TimeSpan GetDelayUntilNextRun()
    {
        var tzNow = _clock.NowInZone(_payrollOptions.TimeZoneId);
        var currentDate = tzNow.Date;
        var runDay = Math.Clamp(_payrollOptions.RunDayOfMonth, 1, 28);
        var runThisMonth = new DateTimeOffset(
            currentDate.Year,
            currentDate.Month,
            runDay,
            Math.Clamp(_payrollOptions.RunHour24, 0, 23),
            0,
            0,
            tzNow.Offset);

        var nextRun = runThisMonth > tzNow ? runThisMonth : runThisMonth.AddMonths(1);
        return nextRun - tzNow;
    }

    private async Task RunAsync(CancellationToken cancellationToken)
    {
        PayrollRun? run = null;
        try
        {
            using var scope = _scopeFactory.CreateScope();
            var db = scope.ServiceProvider.GetRequiredService<CanteenDbContext>();
            var exportService = scope.ServiceProvider.GetRequiredService<IFileExportService>();
            var emailSender = scope.ServiceProvider.GetRequiredService<IEmailSender>();
            var auditService = scope.ServiceProvider.GetRequiredService<IFinanceAuditService>();

            var localNow = _clock.NowInZone(_payrollOptions.TimeZoneId);
            var runDay = Math.Clamp(_payrollOptions.RunDayOfMonth, 1, 28);
            var runKey = $"{localNow:yyyyMM}:{runDay:D2}";
            var alreadyRan = await db.PayrollRuns.AnyAsync(x => x.RunKey == runKey, cancellationToken);
            if (alreadyRan)
            {
                return;
            }

            var nowUtc = _clock.UtcNow.UtcDateTime;
            var timeZone = _clock.ResolveTimeZone(_payrollOptions.TimeZoneId);
            var periodStartLocal = new DateTime(localNow.Year, localNow.Month, 1, 0, 0, 0, DateTimeKind.Unspecified);
            var periodStart = TimeZoneInfo.ConvertTimeToUtc(periodStartLocal, timeZone);
            var periodEnd = nowUtc;

            var rows = await db.CanteenOrders
                .Where(x => x.OrderTimeUtc >= periodStart && x.OrderTimeUtc <= periodEnd)
                .GroupBy(x => x.EmployeeUsername)
                .Select(g => new PayrollExportRow
                {
                    EmployeeUsername = g.Key,
                    TotalAmount = g.Sum(x => x.TotalAmount)
                })
                .OrderByDescending(x => x.TotalAmount)
                .ToListAsync(cancellationToken);

            var fileName = $"payroll_canteen_{nowUtc:yyyyMM}.xlsx";
            var attachmentBytes = exportService.BuildPayrollWorkbook("Payroll", rows, _clock.UtcNow);

            run = new PayrollRun
            {
                RunKey = runKey,
                PeriodStartUtc = periodStart,
                PeriodEndUtc = periodEnd,
                EmployeesCount = rows.Count,
                EmailTo = _routingOptions.HrPayrollInbox,
                AttachmentFileName = fileName
            };
            db.PayrollRuns.Add(run);
            await db.SaveChangesAsync(cancellationToken);

            var sendResult = await emailSender.SendAsync(new EmailMessage
            {
                To = _routingOptions.HrPayrollInbox,
                Subject = $"Monthly Canteen Payroll Export - {nowUtc:yyyy-MM}",
                BodyText = "Attached is the monthly canteen payroll deduction workbook.",
                Attachments = new List<EmailAttachment>
                {
                    new()
                    {
                        FileName = fileName,
                        ContentType = "application/vnd.openxmlformats-officedocument.spreadsheetml.sheet",
                        Bytes = attachmentBytes
                    }
                }
            }, cancellationToken);

            run.SentSuccessfully = true;
            run.ArtifactPath = sendResult.ArtifactPath;
            await db.SaveChangesAsync(cancellationToken);
            await auditService.WriteAsync("system", "Export", "PayrollRun", $"runKey={runKey};periodStart={periodStart:O};periodEnd={periodEnd:O};employees={rows.Count};file={fileName}", cancellationToken);
        }
        catch (Exception ex)
        {
            if (run is not null)
            {
                run.SentSuccessfully = false;
                run.ErrorMessage = ex.Message;
                try
                {
                    using var scope = _scopeFactory.CreateScope();
                    var db = scope.ServiceProvider.GetRequiredService<CanteenDbContext>();
                    db.PayrollRuns.Update(run);
                    await db.SaveChangesAsync(cancellationToken);
                }
                catch
                {
                    // Ignore secondary logging errors.
                }
            }
            _logger.LogError(ex, "Monthly payroll export failed.");
        }
    }
}
