using Mediator;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;

namespace MyBudget.Features.Features.BudgetExecution.ListExecutionRecords;

public static class ListExecutionRecordsEndpoint
{
    public static IEndpointRouteBuilder Map(IEndpointRouteBuilder app)
    {
        app.MapGet(
                "/api/budgets/{id}/periods/{periodId}/budget-lines/{lineId}/executions",
                Handle)
            .WithTags("BudgetExecution")
            .WithName("ListExecutionRecords")
            .Produces<IReadOnlyList<ExecutionRecordDto>>(StatusCodes.Status200OK)
            .Produces(StatusCodes.Status404NotFound)
            .RequireAuthorization("budget:read");

        return app;
    }

    private static async Task<IResult> Handle(
        Guid id,
        Guid periodId,
        Guid lineId,
        IMediator mediator,
        CancellationToken ct)
    {
        var result = await mediator.Send(
            new ListExecutionRecordsQuery(id, periodId, lineId), ct);

        return result.IsSuccess
            ? Results.Ok(result.Value)
            : Results.NotFound(new { error = result.Error });
    }
}
