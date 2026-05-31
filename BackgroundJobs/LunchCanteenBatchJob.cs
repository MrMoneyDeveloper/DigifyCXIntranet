using DigifyCXIntranet.Models;
using DigifyCXIntranet.Services;
using Quartz;

namespace DigifyCXIntranet.BackgroundJobs;

public class LunchCanteenBatchJob : IJob
{
    private readonly ICanteenBatchService _canteenBatchService;

    public LunchCanteenBatchJob(ICanteenBatchService canteenBatchService)
    {
        _canteenBatchService = canteenBatchService;
    }

    public async Task Execute(IJobExecutionContext context)
    {
        await _canteenBatchService.RunBatchAsync(MealSlot.Lunch, context.CancellationToken);
    }
}
