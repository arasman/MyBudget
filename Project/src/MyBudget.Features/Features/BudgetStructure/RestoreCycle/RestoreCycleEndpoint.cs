using Mediator;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;

namespace MyBudget.Features.Features.BudgetStructure.RestoreCycle;

public static class RestoreCycleEndpoint
{
    public static IEndpointRouteBuilder Map(IEndpointRouteBuilder app)
    {
        app.MapPost("/api/budgets/{id}/cycles/{cycleId}/restore", Handle)
            .WithTags("BudgetStructure")
            .WithName("RestoreCycle")
            .Produces(StatusCodes.Status204NoContent)
            .RequireAuthorization("budget:admin");

        return app;
    }

    private static async Task<IResult> Handle(
        Guid id,
        Guid cycleId,
        bool includeExecutionRecords,
        IMediator mediator,
        CancellationToken ct)
    {
        var cmd    = new RestoreCycleCommand(id, cycleId, includeExecutionRecords);
        var result = await mediator.Send(cmd, ct);

        if (!result.IsSuccess)
        {
            return result.Error == "CYCLE_NOT_FOUND"
                ? Results.NotFound(new { error = result.Error })
                : Results.Problem(result.Error, statusCode: StatusCodes.Status422UnprocessableEntity);
        }

        return Results.NoContent();
    }
}
