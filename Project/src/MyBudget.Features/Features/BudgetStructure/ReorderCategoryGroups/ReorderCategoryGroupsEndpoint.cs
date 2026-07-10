using Mediator;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;

namespace MyBudget.Features.Features.BudgetStructure.ReorderCategoryGroups;

public static class ReorderCategoryGroupsEndpoint
{
    public static IEndpointRouteBuilder Map(IEndpointRouteBuilder app)
    {
        app.MapPut("/api/budgets/{id}/category-groups/order", Handle)
            .WithTags("BudgetStructure")
            .WithName("ReorderCategoryGroups")
            .Produces(StatusCodes.Status204NoContent)
            .ProducesValidationProblem()
            .RequireAuthorization("budget:admin");

        return app;
    }

    private static async Task<IResult> Handle(
        Guid id,
        ReorderCategoryGroupsRequest body,
        IMediator mediator,
        CancellationToken ct)
    {
        var cmd    = new ReorderCategoryGroupsCommand(id, body.OrderedIds);
        var result = await mediator.Send(cmd, ct);

        if (!result.IsSuccess)
        {
            return result.Error is "REORDER_LIST_INCOMPLETE" or "REORDER_LIST_INVALID"
                ? Results.UnprocessableEntity(new { error = result.Error })
                : Results.Problem(result.Error, statusCode: StatusCodes.Status422UnprocessableEntity);
        }

        return Results.NoContent();
    }
}

public sealed record ReorderCategoryGroupsRequest(List<Guid> OrderedIds);
