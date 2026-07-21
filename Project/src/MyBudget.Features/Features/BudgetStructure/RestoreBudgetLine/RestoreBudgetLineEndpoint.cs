using Mediator;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;

namespace MyBudget.Features.Features.BudgetStructure.RestoreBudgetLine;

public static class RestoreBudgetLineEndpoint
{
    public static IEndpointRouteBuilder Map(IEndpointRouteBuilder app)
    {
        app.MapPost("/api/budgets/{id}/lines/{lineId}/restore", Handle)
            .WithTags("BudgetStructure")
            .WithName("RestoreBudgetLine")
            .Produces(StatusCodes.Status204NoContent)
            .RequireAuthorization("budget:admin");

        return app;
    }

    private static async Task<IResult> Handle(
        Guid id,
        Guid lineId,
        bool includeExecutionRecords,
        IMediator mediator,
        CancellationToken ct)
    {
        var cmd    = new RestoreBudgetLineCommand(id, lineId, includeExecutionRecords);
        var result = await mediator.Send(cmd, ct);

        if (!result.IsSuccess)
        {
            if (result.Error == "BUDGET_LINE_NOT_FOUND")
                return Results.NotFound(new { error = result.Error });
            if (result.Error is "PARENT_IS_DELETED" or "BUDGET_LINE_NAME_DUPLICATE")
                return Results.Conflict(new { error = result.Error });
            return Results.Problem(result.Error, statusCode: StatusCodes.Status422UnprocessableEntity);
        }

        return Results.NoContent();
    }
}
