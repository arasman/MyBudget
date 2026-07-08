namespace MyBudget.Features.SharedKernel.Caching;

public interface ICacheable
{
    string CacheKey { get; }
    TimeSpan CacheDuration { get; }
}
