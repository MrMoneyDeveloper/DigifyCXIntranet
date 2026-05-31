using DigifyCXIntranet.Models;

namespace DigifyCXIntranet.Services;

public interface ICanteenBatchService
{
    Task RunBatchAsync(MealSlot mealSlot, CancellationToken cancellationToken = default);
}
