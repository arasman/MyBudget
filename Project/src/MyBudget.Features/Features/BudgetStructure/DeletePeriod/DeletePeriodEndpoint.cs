using Mediator;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;

namespace MyBudget.Features.Features.BudgetStructure.DeletePeriod;

public static class DeletePeriodEndpoint
{
    public static IEndpointRouteBuilder Map(IEndpointRouteBuilder app)
    {
        app.MapDelete("/api/budgets/{id}/cycles/{cycleId}/periods/{periodId}", Handle)
            .WithTags("BudgetStructure")
            .WithName("DeletePeriod")
            .Produces(StatusCodes.Status204NoContent)
            .RequireAuthorization("budget:admin");

        return app;
    }

    private static async Task<IResult> Handle(
        Guid id,
        Guid cycleId,
        Guid periodId,
        IMediator mediator,
        CancellationToken ct)
    {
        var cmd    = new DeletePeriodCommand(id, cycleId, periodId);
        var result = await mediator.Send(cmd, ct);

        if (!result.IsSuccess)
        {
            return result.Error == "PERIOD_NOT_FOUND"
                ? Results.NotFound(new { error = result.Error })
                : Results.Problem(result.Error, statusCode: StatusCodes.Status422UnprocessableEntity);
        }

        return Results.NoContent();
    }
}
