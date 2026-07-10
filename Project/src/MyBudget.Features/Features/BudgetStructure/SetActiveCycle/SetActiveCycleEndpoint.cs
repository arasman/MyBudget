using Mediator;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;

namespace MyBudget.Features.Features.BudgetStructure.SetActiveCycle;

public static class SetActiveCycleEndpoint
{
    public static IEndpointRouteBuilder Map(IEndpointRouteBuilder app)
    {
        app.MapPut("/api/budgets/{id}/active-cycle", Handle)
            .WithTags("BudgetStructure")
            .WithName("SetActiveCycle")
            .Produces<Guid>(StatusCodes.Status200OK)
            .RequireAuthorization("budget:admin");

        return app;
    }

    private static async Task<IResult> Handle(
        Guid id,
        SetActiveCycleRequest body,
        IMediator mediator,
        CancellationToken ct)
    {
        var cmd    = new SetActiveCycleCommand(id, body.CycleId);
        var result = await mediator.Send(cmd, ct);

        if (!result.IsSuccess)
        {
            return result.Error == "CYCLE_NOT_FOUND"
                ? Results.NotFound(new { error = result.Error })
                : Results.Problem(result.Error, statusCode: StatusCodes.Status422UnprocessableEntity);
        }

        return Results.Ok(new { id = result.Value });
    }
}

public sealed record SetActiveCycleRequest(Guid CycleId);
