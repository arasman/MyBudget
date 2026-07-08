namespace MyBudget.Features.SharedKernel.Caching;

/// <summary>
/// No-op cache service. Always returns cache miss. Used at foundation until Redis is wired (ADR-005).
/// </summary>
public sealed class NullCacheService : ICacheService
{
    public Task<T?> GetAsync<T>(string key, CancellationToken ct = default)
        => Task.FromResult<T?>(default);

    public Task SetAsync<T>(string key, T value, TimeSpan? expiry = null, CancellationToken ct = default)
        => Task.CompletedTask;

    public Task RemoveAsync(string key, CancellationToken ct = default)
        => Task.CompletedTask;
}
