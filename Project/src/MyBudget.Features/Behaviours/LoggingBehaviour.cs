using System.Diagnostics;
using Mediator;
using Microsoft.Extensions.Logging;
using MyBudget.Features.SharedKernel.Telemetry;

namespace MyBudget.Features.Behaviours;

/// <summary>
/// Pipeline behaviour that logs request entry/exit with elapsed time and creates an OTel span.
/// Pipeline order: ValidationBehaviour → LoggingBehaviour → CachingBehaviour → Handler
/// </summary>
public sealed class LoggingBehaviour<TRequest, TResponse> : IPipelineBehavior<TRequest, TResponse>
    where TRequest : notnull, IMessage
{
    private readonly ILogger<LoggingBehaviour<TRequest, TResponse>> _logger;

    public LoggingBehaviour(ILogger<LoggingBehaviour<TRequest, TResponse>> logger)
    {
        _logger = logger;
    }

    public async ValueTask<TResponse> Handle(
        TRequest message,
        MessageHandlerDelegate<TRequest, TResponse> next,
        CancellationToken cancellationToken)
    {
        var requestName = typeof(TRequest).Name;
        var stopwatch = Stopwatch.StartNew();

        _logger.LogInformation("Handling {RequestType}", requestName);

        using var activity = SliceActivitySource.Source.StartActivity(requestName);

        TResponse response;
        try
        {
            response = await next(message, cancellationToken);
            stopwatch.Stop();
            _logger.LogInformation(
                "Handled {RequestType} in {ElapsedMs}ms — Success",
                requestName,
                stopwatch.ElapsedMilliseconds);
        }
        catch (Exception ex)
        {
            stopwatch.Stop();
            _logger.LogError(
                ex,
                "Handled {RequestType} in {ElapsedMs}ms — Failure",
                requestName,
                stopwatch.ElapsedMilliseconds);
            activity?.SetStatus(ActivityStatusCode.Error, ex.Message);
            throw;
        }

        return response;
    }
}
