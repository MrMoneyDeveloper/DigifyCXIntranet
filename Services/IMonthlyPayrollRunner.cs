namespace DigifyCXIntranet.Services;

public interface IMonthlyPayrollRunner
{
    Task<int> RunAsync(CancellationToken cancellationToken = default);
}
