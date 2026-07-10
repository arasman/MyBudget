using Mediator;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;

namespace MyBudget.Features.Features.BudgetStructure.UpdateCycle;

public static class UpdateCycleEndpoint
{
    public static IEndpointRouteBuilder Map(IEndpointRouteBuilder app)
    {
        app.MapPut("/api/budgets/{id}/cycles/{cycleId}", Handle)
            .WithTags("BudgetStructure")
            .WithName("UpdateCycle")
            .Produces<Guid>(StatusCodes.Status200OK)
            .ProducesValidationProblem()
            .RequireAuthorization("budget:admin");

        return app;
    }

    private static async Task<IResult> Handle(
        Guid id,
        Guid cycleId,
        UpdateCycleRequest body,
        IMediator mediator,
        CancellationToken ct)
    {
        var cmd    = new UpdateCycleCommand(id, cycleId, body.Name, body.StartDate, body.EndDate);
        var result = await mediator.Send(cmd, ct);

        if (!result.IsSuccess)
        {
            return result.Error switch
            {
                "CYCLE_NOT_FOUND"          => Results.NotFound(new { error = result.Error }),
                "CYCLE_DATE_OVERLAP"       => Results.UnprocessableEntity(new { error = result.Error }),
                "CYCLE_PERIOD_OUT_OF_RANGE"=> Results.UnprocessableEntity(new { error = result.Error }),
                _                          => Results.Problem(result.Error, statusCode: StatusCodes.Status422UnprocessableEntity)
            };
        }

        return Results.Ok(new { id = result.Value });
    }
}

public sealed record UpdateCycleRequest(string Name, DateOnly StartDate, DateOnly EndDate);
