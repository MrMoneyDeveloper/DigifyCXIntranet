namespace DigifyCXIntranet.Services;

public interface IIdentifierRateLimiter
{
    bool TryAcquire(string policyName, string? identifier);
}
