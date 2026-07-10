using Mediator;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;

namespace MyBudget.Features.Features.BudgetStructure.UpdatePeriod;

public static class UpdatePeriodEndpoint
{
    public static IEndpointRouteBuilder Map(IEndpointRouteBuilder app)
    {
        app.MapPut("/api/budgets/{id}/cycles/{cycleId}/periods/{periodId}", Handle)
            .WithTags("BudgetStructure")
            .WithName("UpdatePeriod")
            .Produces<Guid>(StatusCodes.Status200OK)
            .ProducesValidationProblem()
            .RequireAuthorization("budget:admin");

        return app;
    }

    private static async Task<IResult> Handle(
        Guid id,
        Guid cycleId,
        Guid periodId,
        UpdatePeriodRequest body,
        IMediator mediator,
        CancellationToken ct)
    {
        var cmd    = new UpdatePeriodCommand(id, cycleId, periodId, body.Name, body.PeriodNumber, body.StartDate, body.EndDate);
        var result = await mediator.Send(cmd, ct);

        if (!result.IsSuccess)
        {
            return result.Error switch
            {
                "PERIOD_NOT_FOUND"         => Results.NotFound(new { error = result.Error }),
                "PERIOD_OUT_OF_CYCLE_RANGE"=> Results.UnprocessableEntity(new { error = result.Error }),
                "PERIOD_DATE_OVERLAP"      => Results.UnprocessableEntity(new { error = result.Error }),
                _                          => Results.Problem(result.Error, statusCode: StatusCodes.Status422UnprocessableEntity)
            };
        }

        return Results.Ok(new { id = result.Value });
    }
}

public sealed record UpdatePeriodRequest(string Name, int PeriodNumber, DateOnly StartDate, DateOnly EndDate);
