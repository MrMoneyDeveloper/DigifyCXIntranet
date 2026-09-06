using DigifyCXIntranet.Data;
using DigifyCXIntranet.Models;
using DigifyCXIntranet.Options;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;

namespace DigifyCXIntranet.Services;

public class CanteenBatchService : ICanteenBatchService
{
    private readonly CanteenDbContext _db;
    private readonly IFileExportService _fileExportService;
    private readonly IEmailSender _emailSender;
    private readonly IFinanceAuditService _auditService;
    private readonly RoutingInboxesOptions _routing;
    private readonly IClock _clock;
    private readonly CanteenBatchingOptions _batchOptions;

    public CanteenBatchService(
        CanteenDbContext db,
        IFileExportService fileExportService,
        IEmailSender emailSender,
        IFinanceAuditService auditService,
        IOptions<RoutingInboxesOptions> routingOptions,
        IOptions<CanteenBatchingOptions> batchOptions,
        IClock clock)
    {
        _db = db;
        _fileExportService = fileExportService;
        _emailSender = emailSender;
        _auditService = auditService;
        _clock = clock;
        _routing = routingOptions.Value;
        _batchOptions = batchOptions.Value;
    }

    public async Task RunBatchAsync(MealSlot mealSlot, CancellationToken cancellationToken = default)
    {
        var nowUtc = _clock.UtcNow.UtcDateTime;
        var localNow = _clock.NowInZone(_batchOptions.TimeZoneId);
        var runKey = $"{mealSlot}:{localNow:yyyyMMdd}";

        var previousRun = await _db.CanteenBatchRuns
            .AsNoTracking()
            .FirstOrDefaultAsync(x => x.RunKey == runKey, cancellationToken);
        if (previousRun is not null)
        {
            if (previousRun.SentSuccessfully)
            {
                return;
            }

            throw new InvalidOperationException(
                $"Canteen batch '{runKey}' has an incomplete previous run. Review the batch and email outbox before resending; automatic resend is disabled.");
        }

        var orders = await _db.CanteenOrders
            .Where(x => x.MealSlot == mealSlot && x.OrderTimeUtc <= nowUtc && x.CanteenBatchRunId == null)
            .OrderBy(x => x.OrderTimeUtc)
            .ToListAsync(cancellationToken);

        var run = new CanteenBatchRun
        {
            RunKey = runKey,
            MealSlot = mealSlot,
            TriggeredUtc = nowUtc,
            CutoffLocalTime = localNow.DateTime,
            OrdersCount = orders.Count,
            EmailTo = _routing.CanteenInbox
        };
        _db.CanteenBatchRuns.Add(run);
        await _db.SaveChangesAsync(cancellationToken);

        if (orders.Count == 0)
        {
            run.SentSuccessfully = true;
            run.AttachmentFileName = "none";
            await _db.SaveChangesAsync(cancellationToken);
            return;
        }

        foreach (var order in orders)
        {
            order.CanteenBatchRunId = run.Id;
            order.Status = "Batched";
        }

        var fileName = $"canteen_{mealSlot.ToString().ToLowerInvariant()}_{DateTime.UtcNow:yyyyMMdd_HHmm}.xlsx";
        try
        {
            var bytes = _fileExportService.BuildCanteenBatchWorkbook(
                $"{mealSlot} Orders",
                orders,
                _clock.UtcNow);
            var sendResult = await _emailSender.SendAsync(new EmailMessage
            {
                To = _routing.CanteenInbox,
                Subject = $"Canteen {mealSlot} Batch - {orders.Count} order(s)",
                BodyText = $"Automated batch for {mealSlot}. Orders attached.",
                Attachments = new List<EmailAttachment>
                {
                    new()
                    {
                        FileName = fileName,
                        ContentType = "application/vnd.openxmlformats-officedocument.spreadsheetml.sheet",
                        Bytes = bytes
                    }
                }
            }, cancellationToken);

            run.SentSuccessfully = true;
            run.AttachmentFileName = fileName;
            run.ArtifactPath = sendResult.ArtifactPath;
        }
        catch (Exception ex)
        {
            run.SentSuccessfully = false;
            run.ErrorMessage = ex.Message;
            await _db.SaveChangesAsync(CancellationToken.None);
            throw;
        }

        await _db.SaveChangesAsync(cancellationToken);
        await _auditService.WriteAsync("system", "Export", "CanteenBatch", $"runKey={runKey};meal={mealSlot};orders={orders.Count};sent={run.SentSuccessfully};file={run.AttachmentFileName}", cancellationToken);
    }
}
