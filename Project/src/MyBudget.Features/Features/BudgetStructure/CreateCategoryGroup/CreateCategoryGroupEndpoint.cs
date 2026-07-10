using Mediator;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;

namespace MyBudget.Features.Features.BudgetStructure.CreateCategoryGroup;

public static class CreateCategoryGroupEndpoint
{
    public static IEndpointRouteBuilder Map(IEndpointRouteBuilder app)
    {
        app.MapPost("/api/budgets/{id}/category-groups", Handle)
            .WithTags("BudgetStructure")
            .WithName("CreateCategoryGroup")
            .Produces<Guid>(StatusCodes.Status201Created)
            .ProducesValidationProblem()
            .RequireAuthorization("budget:admin");

        return app;
    }

    private static async Task<IResult> Handle(
        Guid id,
        CreateCategoryGroupRequest body,
        IMediator mediator,
        CancellationToken ct)
    {
        var cmd    = new CreateCategoryGroupCommand(id, body.Name, body.DisplayOrder);
        var result = await mediator.Send(cmd, ct);

        if (!result.IsSuccess)
        {
            return result.Error == "CATEGORY_GROUP_NAME_DUPLICATE"
                ? Results.UnprocessableEntity(new { error = result.Error })
                : Results.Problem(result.Error, statusCode: StatusCodes.Status422UnprocessableEntity);
        }

        return Results.Created($"/api/budgets/{id}/category-groups/{result.Value}", new { id = result.Value });
    }
}

public sealed record CreateCategoryGroupRequest(string Name, int DisplayOrder);
