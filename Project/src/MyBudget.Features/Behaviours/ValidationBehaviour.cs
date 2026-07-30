using FluentValidation;
using Mediator;
using MyBudget.Features.SharedKernel.Results;

namespace MyBudget.Features.Behaviours;

/// <summary>
/// Pipeline behaviour that runs FluentValidation validators before the handler.
/// Short-circuits and returns Result.Failure if any validator fails (ADR-001).
/// Pipeline order: ValidationBehaviour → LoggingBehaviour → CachingBehaviour → Handler
/// </summary>
public sealed class ValidationBehaviour<TRequest, TResponse> : IPipelineBehavior<TRequest, TResponse>
    where TRequest : notnull, IMessage
{
    private readonly IEnumerable<IValidator<TRequest>> _validators;

    public ValidationBehaviour(IEnumerable<IValidator<TRequest>> validators)
    {
        _validators = validators;
    }

    public async ValueTask<TResponse> Handle(
        TRequest message,
        MessageHandlerDelegate<TRequest, TResponse> next,
        CancellationToken cancellationToken)
    {
        if (!_validators.Any())
            return await next(message, cancellationToken);

        var context = new ValidationContext<TRequest>(message);
        var results = await Task.WhenAll(
            _validators.Select(v => v.ValidateAsync(context, cancellationToken)));
        var failures = results
            .SelectMany(r => r.Errors)
            .Where(f => f is not null)
            .ToList();

        if (failures.Count == 0)
            return await next(message, cancellationToken);

        // Use ErrorCode when set (matches endpoint switch cases); fall back to ErrorMessage
        var errorMessage = string.Join("; ", failures.Select(f =>
            string.IsNullOrEmpty(f.ErrorCode) ? f.ErrorMessage : f.ErrorCode));

        // Return a failure Result — requires TResponse to be Result<T>
        // The cast is intentional: all handlers in this codebase return Result<T>
        var resultType = typeof(TResponse);
        if (resultType.IsGenericType && resultType.GetGenericTypeDefinition() == typeof(Result<>))
        {
            var failureMethod = resultType.GetMethod(nameof(Result<object>.Failure));
            var result = failureMethod!.Invoke(null, [errorMessage]);
            return (TResponse)result!;
        }

        throw new ValidationException(failures);
    }
}
