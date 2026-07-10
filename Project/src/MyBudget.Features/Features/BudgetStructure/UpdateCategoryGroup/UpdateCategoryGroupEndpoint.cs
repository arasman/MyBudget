using Mediator;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;

namespace MyBudget.Features.Features.BudgetStructure.UpdateCategoryGroup;

public static class UpdateCategoryGroupEndpoint
{
    public static IEndpointRouteBuilder Map(IEndpointRouteBuilder app)
    {
        app.MapPut("/api/budgets/{id}/category-groups/{groupId}", Handle)
            .WithTags("BudgetStructure")
            .WithName("UpdateCategoryGroup")
            .Produces<Guid>(StatusCodes.Status200OK)
            .ProducesValidationProblem()
            .RequireAuthorization("budget:admin");

        return app;
    }

    private static async Task<IResult> Handle(
        Guid id,
        Guid groupId,
        UpdateCategoryGroupRequest body,
        IMediator mediator,
        CancellationToken ct)
    {
        var cmd    = new UpdateCategoryGroupCommand(id, groupId, body.Name, body.DisplayOrder);
        var result = await mediator.Send(cmd, ct);

        if (!result.IsSuccess)
        {
            return result.Error switch
            {
                "CATEGORY_GROUP_NOT_FOUND"      => Results.NotFound(new { error = result.Error }),
                "CATEGORY_GROUP_NAME_DUPLICATE" => Results.UnprocessableEntity(new { error = result.Error }),
                _                               => Results.Problem(result.Error, statusCode: StatusCodes.Status422UnprocessableEntity)
            };
        }

        return Results.Ok(new { id = result.Value });
    }
}

public sealed record UpdateCategoryGroupRequest(string Name, int DisplayOrder);
