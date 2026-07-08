using Mediator;
using Microsoft.Extensions.Logging;
using MyBudget.Features.SharedKernel.Caching;

namespace MyBudget.Features.Behaviours;

/// <summary>
/// Pipeline behaviour that caches responses for requests implementing ICacheable.
/// Non-ICacheable requests pass through directly (ADR-005).
/// Pipeline order: ValidationBehaviour → LoggingBehaviour → CachingBehaviour → Handler
/// </summary>
public sealed class CachingBehaviour<TRequest, TResponse> : IPipelineBehavior<TRequest, TResponse>
    where TRequest : notnull, IMessage
{
    private readonly ICacheService _cacheService;
    private readonly ILogger<CachingBehaviour<TRequest, TResponse>> _logger;

    public CachingBehaviour(
        ICacheService cacheService,
        ILogger<CachingBehaviour<TRequest, TResponse>> logger)
    {
        _cacheService = cacheService;
        _logger = logger;
    }

    public async ValueTask<TResponse> Handle(
        TRequest message,
        MessageHandlerDelegate<TRequest, TResponse> next,
        CancellationToken cancellationToken)
    {
        // Non-cacheable requests pass through with no cache interaction
        if (message is not ICacheable cacheable)
            return await next(message, cancellationToken);

        var cached = await _cacheService.GetAsync<TResponse>(cacheable.CacheKey, cancellationToken);
        if (cached is not null)
        {
            _logger.LogDebug("Cache hit for {CacheKey}", cacheable.CacheKey);
            return cached;
        }

        var response = await next(message, cancellationToken);

        await _cacheService.SetAsync(cacheable.CacheKey, response, cacheable.CacheDuration, cancellationToken);
        _logger.LogDebug("Cache miss — stored {CacheKey}", cacheable.CacheKey);

        return response;
    }
}
