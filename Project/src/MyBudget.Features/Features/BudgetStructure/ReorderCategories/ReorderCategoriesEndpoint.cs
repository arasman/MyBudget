using Mediator;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;

namespace MyBudget.Features.Features.BudgetStructure.ReorderCategories;

public static class ReorderCategoriesEndpoint
{
    public static IEndpointRouteBuilder Map(IEndpointRouteBuilder app)
    {
        app.MapPut("/api/budgets/{id}/category-groups/{groupId}/categories/order", Handle)
            .WithTags("BudgetStructure")
            .WithName("ReorderCategories")
            .Produces(StatusCodes.Status204NoContent)
            .ProducesValidationProblem()
            .RequireAuthorization("budget:admin");

        return app;
    }

    private static async Task<IResult> Handle(
        Guid id,
        Guid groupId,
        ReorderCategoriesRequest body,
        IMediator mediator,
        CancellationToken ct)
    {
        var cmd    = new ReorderCategoriesCommand(id, groupId, body.OrderedIds);
        var result = await mediator.Send(cmd, ct);

        if (!result.IsSuccess)
        {
            return result.Error is "REORDER_LIST_INCOMPLETE" or "REORDER_LIST_INVALID"
                ? Results.UnprocessableEntity(new { error = result.Error })
                : result.Error == "CATEGORY_GROUP_NOT_FOUND"
                    ? Results.NotFound(new { error = result.Error })
                    : Results.Problem(result.Error, statusCode: StatusCodes.Status422UnprocessableEntity);
        }

        return Results.NoContent();
    }
}

public sealed record ReorderCategoriesRequest(List<Guid> OrderedIds);
