using Mediator;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;

namespace MyBudget.Features.Features.BudgetStructure.DeleteCategoryGroup;

public static class DeleteCategoryGroupEndpoint
{
    public static IEndpointRouteBuilder Map(IEndpointRouteBuilder app)
    {
        app.MapDelete("/api/budgets/{id}/category-groups/{groupId}", Handle)
            .WithTags("BudgetStructure")
            .WithName("DeleteCategoryGroup")
            .Produces(StatusCodes.Status204NoContent)
            .RequireAuthorization("budget:admin");

        return app;
    }

    private static async Task<IResult> Handle(
        Guid id,
        Guid groupId,
        IMediator mediator,
        CancellationToken ct)
    {
        var cmd    = new DeleteCategoryGroupCommand(id, groupId);
        var result = await mediator.Send(cmd, ct);

        if (!result.IsSuccess)
        {
            return result.Error == "CATEGORY_GROUP_NOT_FOUND"
                ? Results.NotFound(new { error = result.Error })
                : Results.Problem(result.Error, statusCode: StatusCodes.Status422UnprocessableEntity);
        }

        return Results.NoContent();
    }
}
