using Mediator;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;

namespace MyBudget.Features.Features.BudgetStructure.RestoreCategory;

public static class RestoreCategoryEndpoint
{
    public static IEndpointRouteBuilder Map(IEndpointRouteBuilder app)
    {
        app.MapPost("/api/budgets/{id}/category-groups/{groupId}/categories/{categoryId}/restore", Handle)
            .WithTags("BudgetStructure")
            .WithName("RestoreCategory")
            .Produces(StatusCodes.Status204NoContent)
            .RequireAuthorization("budget:admin");

        return app;
    }

    private static async Task<IResult> Handle(
        Guid id,
        Guid groupId,
        Guid categoryId,
        bool includeExecutionRecords,
        IMediator mediator,
        CancellationToken ct)
    {
        var cmd    = new RestoreCategoryCommand(id, groupId, categoryId, includeExecutionRecords);
        var result = await mediator.Send(cmd, ct);

        if (!result.IsSuccess)
        {
            if (result.Error == "PARENT_IS_DELETED")
                return Results.Conflict(new { error = result.Error });
            if (result.Error == "CATEGORY_NOT_FOUND" || result.Error == "CATEGORY_GROUP_NOT_FOUND")
                return Results.NotFound(new { error = result.Error });
            return Results.Problem(result.Error, statusCode: StatusCodes.Status422UnprocessableEntity);
        }

        return Results.NoContent();
    }
}
