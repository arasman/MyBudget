using Mediator;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;

namespace MyBudget.Features.Features.BudgetExecution.RestoreExecutionRecord;

public static class RestoreExecutionRecordEndpoint
{
    public static IEndpointRouteBuilder Map(IEndpointRouteBuilder app)
    {
        app.MapPost(
                "/api/budgets/{id}/periods/{periodId}/budget-lines/{lineId}/executions/{executionId}/restore",
                Handle)
            .WithTags("BudgetExecution")
            .WithName("RestoreExecutionRecord")
            .Produces<object>(StatusCodes.Status200OK)
            .Produces(StatusCodes.Status404NotFound)
            .RequireAuthorization("budget:operator");

        return app;
    }

    private static async Task<IResult> Handle(
        Guid id,
        Guid periodId,
        Guid lineId,
        Guid executionId,
        IMediator mediator,
        CancellationToken ct)
    {
        var cmd    = new RestoreExecutionRecordCommand(id, periodId, lineId, executionId);
        var result = await mediator.Send(cmd, ct);

        if (!result.IsSuccess)
        {
            return result.Error switch
            {
                "PERIOD_CLOSED"              => Results.Conflict(new { error = result.Error }),
                "EXECUTION_RECORD_NOT_FOUND" => Results.NotFound(new { error = result.Error }),
                "EXECUTION_OUT_OF_DATE_RANGE" => Results.UnprocessableEntity(new { error = result.Error }),
                _ => Results.Problem(result.Error, statusCode: StatusCodes.Status422UnprocessableEntity),
            };
        }

        return Results.Ok(new { id = result.Value });
    }
}
