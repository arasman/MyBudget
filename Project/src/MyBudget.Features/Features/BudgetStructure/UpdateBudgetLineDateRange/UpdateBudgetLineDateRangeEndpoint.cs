using Mediator;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;

namespace MyBudget.Features.Features.BudgetStructure.UpdateBudgetLineDateRange;

public static class UpdateBudgetLineDateRangeEndpoint
{
    public static IEndpointRouteBuilder Map(IEndpointRouteBuilder app)
    {
        app.MapPatch("/api/budgets/{id}/lines/{lineId}/date-range", Handle)
            .WithTags("BudgetStructure")
            .WithName("UpdateBudgetLineDateRange")
            .Produces<Guid>(StatusCodes.Status200OK)
            .ProducesValidationProblem()
            .RequireAuthorization("budget:admin");

        return app;
    }

    private static async Task<IResult> Handle(
        Guid id,
        Guid lineId,
        UpdateBudgetLineDateRangeRequest body,
        IMediator mediator,
        CancellationToken ct)
    {
        var cmd    = new UpdateBudgetLineDateRangeCommand(id, lineId, body.StartDate, body.EndDate);
        var result = await mediator.Send(cmd, ct);

        if (!result.IsSuccess)
        {
            return result.Error switch
            {
                "BUDGET_LINE_NOT_FOUND"          => Results.NotFound(new { error = result.Error }),
                "RANGE_WOULD_ORPHAN_REVISION"    => Results.UnprocessableEntity(new { error = result.Error }),
                "RANGE_WOULD_ORPHAN_EXECUTION"   => Results.Conflict(new { error = result.Error }),
                "DATE_RANGE_CONCURRENCY_CONFLICT" => Results.Conflict(new { error = result.Error }),
                _                                => Results.Problem(result.Error, statusCode: StatusCodes.Status422UnprocessableEntity),
            };
        }

        return Results.Ok(new { id = result.Value });
    }
}

public sealed record UpdateBudgetLineDateRangeRequest(
    DateOnly  StartDate,
    DateOnly? EndDate);
