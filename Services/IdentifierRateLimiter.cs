using System.Collections.Concurrent;
using System.Security.Cryptography;
using System.Text;
using DigifyCXIntranet.Options;
using Microsoft.Extensions.Options;

namespace DigifyCXIntranet.Services;

public sealed class IdentifierRateLimiter : IIdentifierRateLimiter
{
    private const int MaximumTrackedIdentifiers = 10_000;
    private readonly ConcurrentDictionary<string, WindowCounter> _counters = new(StringComparer.Ordinal);
    private readonly RateLimitPoliciesOptions _options;
    private long _attempts;

    public IdentifierRateLimiter(IOptions<RateLimitPoliciesOptions> options)
    {
        _options = options.Value;
    }

    public bool TryAcquire(string policyName, string? identifier)
    {
        if (string.IsNullOrWhiteSpace(identifier))
        {
            return true;
        }

        var policy = GetPolicy(policyName);
        var now = DateTimeOffset.UtcNow;
        if (Interlocked.Increment(ref _attempts) % 128 == 0)
        {
            RemoveExpired(now);
        }

        var key = $"{policyName}:{Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(identifier.Trim().ToUpperInvariant())))}";
        if (_counters.Count >= MaximumTrackedIdentifiers && !_counters.ContainsKey(key))
        {
            RemoveExpired(now);
            if (_counters.Count >= MaximumTrackedIdentifiers)
            {
                return false;
            }
        }

        var counter = _counters.GetOrAdd(key, _ => new WindowCounter(now));
        lock (counter)
        {
            var window = TimeSpan.FromMinutes(policy.WindowMinutes);
            if (now - counter.StartedAt >= window)
            {
                counter.StartedAt = now;
                counter.Count = 0;
            }

            if (counter.Count >= policy.PermitLimit)
            {
                return false;
            }

            counter.Count++;
            return true;
        }
    }

    private EndpointRateLimitOptions GetPolicy(string policyName) => policyName switch
    {
        "login" => _options.Login,
        "forgot-password" => _options.ForgotPassword,
        "account-activation" => _options.AccountActivation,
        "password-reset" => _options.PasswordReset,
        "external-application" => _options.ExternalApplication,
        _ => throw new ArgumentOutOfRangeException(nameof(policyName), policyName, "Unknown identifier rate-limit policy.")
    };

    private void RemoveExpired(DateTimeOffset now)
    {
        foreach (var pair in _counters)
        {
            var policyName = pair.Key[..pair.Key.IndexOf(':')];
            var maximumAge = TimeSpan.FromMinutes(GetPolicy(policyName).WindowMinutes * 2);
            if (now - pair.Value.StartedAt >= maximumAge)
            {
                _counters.TryRemove(pair.Key, out _);
            }
        }
    }

    private sealed class WindowCounter
    {
        public WindowCounter(DateTimeOffset startedAt)
        {
            StartedAt = startedAt;
        }

        public DateTimeOffset StartedAt { get; set; }
        public int Count { get; set; }
    }
}
