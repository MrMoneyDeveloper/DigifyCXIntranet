using DigifyCXIntranet.Models;
using DigifyCXIntranet.Services;
using Quartz;

namespace DigifyCXIntranet.BackgroundJobs;

public class BreakfastCanteenBatchJob : IJob
{
    private readonly ICanteenBatchService _canteenBatchService;

    public BreakfastCanteenBatchJob(ICanteenBatchService canteenBatchService)
    {
        _canteenBatchService = canteenBatchService;
    }

    public async Task Execute(IJobExecutionContext context)
    {
        await _canteenBatchService.RunBatchAsync(MealSlot.Breakfast, context.CancellationToken);
    }
}
