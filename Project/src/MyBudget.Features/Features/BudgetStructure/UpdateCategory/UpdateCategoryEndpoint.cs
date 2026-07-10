using Mediator;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;

namespace MyBudget.Features.Features.BudgetStructure.UpdateCategory;

public static class UpdateCategoryEndpoint
{
    public static IEndpointRouteBuilder Map(IEndpointRouteBuilder app)
    {
        app.MapPut("/api/budgets/{id}/category-groups/{groupId}/categories/{categoryId}", Handle)
            .WithTags("BudgetStructure")
            .WithName("UpdateCategory")
            .Produces<Guid>(StatusCodes.Status200OK)
            .ProducesValidationProblem()
            .RequireAuthorization("budget:admin");

        return app;
    }

    private static async Task<IResult> Handle(
        Guid id,
        Guid groupId,
        Guid categoryId,
        UpdateCategoryRequest body,
        IMediator mediator,
        CancellationToken ct)
    {
        var cmd    = new UpdateCategoryCommand(id, groupId, categoryId, body.Name, body.DisplayOrder);
        var result = await mediator.Send(cmd, ct);

        if (!result.IsSuccess)
        {
            return result.Error == "CATEGORY_NAME_DUPLICATE"
                ? Results.UnprocessableEntity(new { error = result.Error })
                : result.Error == "CATEGORY_NOT_FOUND"
                    ? Results.NotFound(new { error = result.Error })
                    : Results.Problem(result.Error, statusCode: StatusCodes.Status422UnprocessableEntity);
        }

        return Results.Ok(new { id = result.Value });
    }
}

public sealed record UpdateCategoryRequest(string Name, int DisplayOrder);
