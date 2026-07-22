using Mediator;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;

namespace MyBudget.Features.Features.BudgetStructure.ReorderBudgetLines;

public static class ReorderBudgetLinesEndpoint
{
    public static IEndpointRouteBuilder Map(IEndpointRouteBuilder app)
    {
        app.MapPut("/api/budgets/{id}/lines/order", Handle)
            .WithTags("BudgetStructure")
            .WithName("ReorderBudgetLines")
            .Produces(StatusCodes.Status204NoContent)
            .ProducesValidationProblem()
            .RequireAuthorization("budget:admin");

        return app;
    }

    private static async Task<IResult> Handle(
        Guid id,
        ReorderBudgetLinesRequest body,
        IMediator mediator,
        CancellationToken ct)
    {
        var cmd    = new ReorderBudgetLinesCommand(id, body.OrderedIds);
        var result = await mediator.Send(cmd, ct);

        if (!result.IsSuccess)
        {
            return result.Error is "REORDER_ID_NOT_IN_SCOPE"
                ? Results.UnprocessableEntity(new { error = result.Error })
                : result.Error == "BUDGET_NOT_FOUND"
                    ? Results.NotFound(new { error = result.Error })
                    : Results.Problem(result.Error, statusCode: StatusCodes.Status422UnprocessableEntity);
        }

        return Results.NoContent();
    }
}

public sealed record ReorderBudgetLinesRequest(Guid[] OrderedIds);
