using Mediator;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;
using MyBudget.Features.SharedKernel.Entities;

namespace MyBudget.Features.Features.BudgetExecution.CreateExecutionRecord;

public static class CreateExecutionRecordEndpoint
{
    public static IEndpointRouteBuilder Map(IEndpointRouteBuilder app)
    {
        app.MapPost("/api/budgets/{id}/periods/{periodId}/budget-lines/{lineId}/executions", Handle)
            .WithTags("BudgetExecution")
            .WithName("CreateExecutionRecord")
            .Produces<Guid>(StatusCodes.Status201Created)
            .ProducesValidationProblem()
            .RequireAuthorization("budget:operator");

        return app;
    }

    private static async Task<IResult> Handle(
        Guid id,
        Guid periodId,
        Guid lineId,
        CreateExecutionRecordRequest body,
        IMediator mediator,
        CancellationToken ct)
    {
        var cmd = new CreateExecutionRecordCommand(
            id, periodId, lineId,
            body.EntryType, body.Amount, body.Note,
            body.CurrencyId, body.ExchangeRate, body.ExchangeRateTo,
            body.AccountId, body.PaymentMethodId,
            body.OperationDate);

        var result = await mediator.Send(cmd, ct);

        if (!result.IsSuccess)
        {
            return result.Error switch
            {
                "PERIOD_CLOSED"              => Results.Conflict(new { error = result.Error }),
                "PARENT_IS_DELETED"          => Results.Conflict(new { error = result.Error }),
                "BUDGET_LINE_NOT_FOUND"      => Results.NotFound(new { error = result.Error }),
                "BUDGET_LINE_NOT_IN_PERIOD"  => Results.UnprocessableEntity(new { error = result.Error }),
                _ => Results.Problem(result.Error, statusCode: StatusCodes.Status422UnprocessableEntity),
            };
        }

        return Results.Created(
            $"/api/budgets/{id}/periods/{periodId}/budget-lines/{lineId}/executions/{result.Value}",
            new { id = result.Value });
    }
}

public sealed record CreateExecutionRecordRequest(
    EntryType EntryType,
    decimal   Amount,
    string?   Note,
    Guid      CurrencyId,
    decimal?  ExchangeRate,
    decimal?  ExchangeRateTo,
    Guid?     AccountId,
    Guid?     PaymentMethodId,
    DateOnly? OperationDate = null);
