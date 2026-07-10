using Mediator;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;

namespace MyBudget.Features.Features.BudgetStructure.DeleteCategory;

public static class DeleteCategoryEndpoint
{
    public static IEndpointRouteBuilder Map(IEndpointRouteBuilder app)
    {
        app.MapDelete("/api/budgets/{id}/category-groups/{groupId}/categories/{categoryId}", Handle)
            .WithTags("BudgetStructure")
            .WithName("DeleteCategory")
            .Produces(StatusCodes.Status204NoContent)
            .RequireAuthorization("budget:admin");

        return app;
    }

    private static async Task<IResult> Handle(
        Guid id,
        Guid groupId,
        Guid categoryId,
        IMediator mediator,
        CancellationToken ct)
    {
        var cmd    = new DeleteCategoryCommand(id, groupId, categoryId);
        var result = await mediator.Send(cmd, ct);

        if (!result.IsSuccess)
        {
            return result.Error == "CATEGORY_NOT_FOUND"
                ? Results.NotFound(new { error = result.Error })
                : Results.Problem(result.Error, statusCode: StatusCodes.Status422UnprocessableEntity);
        }

        return Results.NoContent();
    }
}
