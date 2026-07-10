using Mediator;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;

namespace MyBudget.Features.Features.BudgetStructure.SetPeriodStatus;

public static class SetPeriodStatusEndpoint
{
    public static IEndpointRouteBuilder Map(IEndpointRouteBuilder app)
    {
        app.MapPatch("/api/budgets/{id}/cycles/{cycleId}/periods/{periodId}/status", Handle)
            .WithTags("BudgetStructure")
            .WithName("SetPeriodStatus")
            .Produces<Guid>(StatusCodes.Status200OK)
            .RequireAuthorization("budget:admin");

        return app;
    }

    private static async Task<IResult> Handle(
        Guid id,
        Guid cycleId,
        Guid periodId,
        SetPeriodStatusRequest body,
        IMediator mediator,
        CancellationToken ct)
    {
        var cmd    = new SetPeriodStatusCommand(id, cycleId, periodId, body.IsClosed);
        var result = await mediator.Send(cmd, ct);

        if (!result.IsSuccess)
        {
            return result.Error == "PERIOD_NOT_FOUND"
                ? Results.NotFound(new { error = result.Error })
                : Results.Problem(result.Error, statusCode: StatusCodes.Status422UnprocessableEntity);
        }

        return Results.Ok(new { id = result.Value });
    }
}

public sealed record SetPeriodStatusRequest(bool IsClosed);
