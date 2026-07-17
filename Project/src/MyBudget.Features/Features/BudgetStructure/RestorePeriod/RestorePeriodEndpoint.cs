using Mediator;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;

namespace MyBudget.Features.Features.BudgetStructure.RestorePeriod;

public static class RestorePeriodEndpoint
{
    public static IEndpointRouteBuilder Map(IEndpointRouteBuilder app)
    {
        app.MapPost("/api/budgets/{id}/cycles/{cycleId}/periods/{periodId}/restore", Handle)
            .WithTags("BudgetStructure")
            .WithName("RestorePeriod")
            .Produces(StatusCodes.Status204NoContent)
            .RequireAuthorization("budget:admin");

        return app;
    }

    private static async Task<IResult> Handle(
        Guid id,
        Guid cycleId,
        Guid periodId,
        bool includeExecutionRecords,
        IMediator mediator,
        CancellationToken ct)
    {
        var cmd    = new RestorePeriodCommand(id, cycleId, periodId, includeExecutionRecords);
        var result = await mediator.Send(cmd, ct);

        if (!result.IsSuccess)
        {
            if (result.Error == "PARENT_IS_DELETED")
                return Results.Conflict(new { error = result.Error });
            if (result.Error is "PERIOD_NOT_FOUND" or "CYCLE_NOT_FOUND")
                return Results.NotFound(new { error = result.Error });
            return Results.Problem(result.Error, statusCode: StatusCodes.Status422UnprocessableEntity);
        }

        return Results.NoContent();
    }
}
