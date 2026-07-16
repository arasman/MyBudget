using Mediator;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;
using MyBudget.Features.SharedKernel.Entities;

namespace MyBudget.Features.Features.BudgetExecution.UpdateExecutionRecord;

public static class UpdateExecutionRecordEndpoint
{
    public static IEndpointRouteBuilder Map(IEndpointRouteBuilder app)
    {
        app.MapPut("/api/budgets/{id}/periods/{periodId}/budget-lines/{lineId}/executions/{executionId}", Handle)
            .WithTags("BudgetExecution")
            .WithName("UpdateExecutionRecord")
            .Produces<Guid>(StatusCodes.Status200OK)
            .ProducesValidationProblem()
            .RequireAuthorization("budget:operator");

        return app;
    }

    private static async Task<IResult> Handle(
        Guid id,
        Guid periodId,
        Guid lineId,
        Guid executionId,
        UpdateExecutionRecordRequest body,
        IMediator mediator,
        CancellationToken ct)
    {
        var cmd = new UpdateExecutionRecordCommand(
            id, periodId, lineId, executionId,
            body.EntryType, body.Amount, body.Note,
            body.CurrencyId, body.ExchangeRate, body.ExchangeRateTo,
            body.AccountId, body.PaymentMethodId,
            body.OperationDate);

        var result = await mediator.Send(cmd, ct);

        if (!result.IsSuccess)
        {
            return result.Error switch
            {
                "PERIOD_CLOSED"             => Results.Conflict(new { error = result.Error }),
                "EXECUTION_RECORD_NOT_FOUND" => Results.NotFound(new { error = result.Error }),
                _ => Results.Problem(result.Error, statusCode: StatusCodes.Status422UnprocessableEntity),
            };
        }

        return Results.Ok(new { id = result.Value });
    }
}

public sealed record UpdateExecutionRecordRequest(
    EntryType EntryType,
    decimal   Amount,
    string?   Note,
    Guid      CurrencyId,
    decimal?  ExchangeRate,
    decimal?  ExchangeRateTo,
    Guid?     AccountId,
    Guid?     PaymentMethodId,
    DateOnly? OperationDate = null);
