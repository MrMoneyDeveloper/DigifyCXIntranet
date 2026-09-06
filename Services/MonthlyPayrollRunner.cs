using DigifyCXIntranet.Data;
using DigifyCXIntranet.Models;
using DigifyCXIntranet.Options;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;

namespace DigifyCXIntranet.Services;

public class MonthlyPayrollRunner : IMonthlyPayrollRunner
{
    private readonly IServiceScopeFactory _scopeFactory;
    private readonly PayrollOptions _payrollOptions;
    private readonly RoutingInboxesOptions _routingOptions;
    private readonly IClock _clock;
    private readonly ILogger<MonthlyPayrollRunner> _logger;

    public MonthlyPayrollRunner(
        IServiceScopeFactory scopeFactory,
        IOptions<PayrollOptions> payrollOptions,
        IOptions<RoutingInboxesOptions> routingOptions,
        IClock clock,
        ILogger<MonthlyPayrollRunner> logger)
    {
        _scopeFactory = scopeFactory;
        _payrollOptions = payrollOptions.Value;
        _routingOptions = routingOptions.Value;
        _clock = clock;
        _logger = logger;
    }

    public async Task<int> RunAsync(CancellationToken cancellationToken = default)
    {
        PayrollRun? run = null;
        using var scope = _scopeFactory.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<CanteenDbContext>();
        var exportService = scope.ServiceProvider.GetRequiredService<IFileExportService>();
        var emailSender = scope.ServiceProvider.GetRequiredService<IEmailSender>();
        var auditService = scope.ServiceProvider.GetRequiredService<IFinanceAuditService>();

        try
        {
            var localNow = _clock.NowInZone(_payrollOptions.TimeZoneId);
            var runDay = Math.Clamp(_payrollOptions.RunDayOfMonth, 1, 28);
            var runKey = $"{localNow:yyyyMM}:{runDay:D2}";
            var previousRun = await db.PayrollRuns
                .AsNoTracking()
                .FirstOrDefaultAsync(x => x.RunKey == runKey, cancellationToken);
            if (previousRun is not null)
            {
                if (previousRun.SentSuccessfully)
                {
                    return 0;
                }

                throw new InvalidOperationException(
                    $"Payroll export '{runKey}' has an incomplete previous run. Review the payroll run and email outbox before resending; automatic resend is disabled.");
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
            return rows.Count;
        }
        catch (Exception ex)
        {
            if (run is not null)
            {
                run.SentSuccessfully = false;
                run.ErrorMessage = ex.Message;
                await db.SaveChangesAsync(CancellationToken.None);
            }

            _logger.LogError(ex, "Monthly payroll export failed.");
            throw;
        }
    }
}
