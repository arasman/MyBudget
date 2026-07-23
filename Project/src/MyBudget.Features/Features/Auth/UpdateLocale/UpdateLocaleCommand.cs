using Mediator;
using MyBudget.Features.SharedKernel.Results;

namespace MyBudget.Features.Features.Auth.UpdateLocale;

public sealed record UpdateLocaleCommand(string Locale) : IRequest<Result<Unit>>;
