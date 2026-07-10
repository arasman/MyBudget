using Mediator;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;

namespace MyBudget.Features.Features.BudgetStructure.CreateCategory;

public static class CreateCategoryEndpoint
{
    public static IEndpointRouteBuilder Map(IEndpointRouteBuilder app)
    {
        app.MapPost("/api/budgets/{id}/category-groups/{groupId}/categories", Handle)
            .WithTags("BudgetStructure")
            .WithName("CreateCategory")
            .Produces<Guid>(StatusCodes.Status201Created)
            .ProducesValidationProblem()
            .RequireAuthorization("budget:admin");

        return app;
    }

    private static async Task<IResult> Handle(
        Guid id,
        Guid groupId,
        CreateCategoryRequest body,
        IMediator mediator,
        CancellationToken ct)
    {
        var cmd    = new CreateCategoryCommand(id, groupId, body.Name, body.DisplayOrder);
        var result = await mediator.Send(cmd, ct);

        if (!result.IsSuccess)
        {
            return result.Error == "CATEGORY_NAME_DUPLICATE"
                ? Results.UnprocessableEntity(new { error = result.Error })
                : result.Error == "CATEGORY_GROUP_NOT_FOUND"
                    ? Results.NotFound(new { error = result.Error })
                    : Results.Problem(result.Error, statusCode: StatusCodes.Status422UnprocessableEntity);
        }

        return Results.Created(
            $"/api/budgets/{id}/category-groups/{groupId}/categories/{result.Value}",
            new { id = result.Value });
    }
}

public sealed record CreateCategoryRequest(string Name, int DisplayOrder);
