using Mediator;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;

namespace MyBudget.Features.Features.BudgetStructure.RestoreCategoryGroup;

public static class RestoreCategoryGroupEndpoint
{
    public static IEndpointRouteBuilder Map(IEndpointRouteBuilder app)
    {
        app.MapPost("/api/budgets/{id}/category-groups/{groupId}/restore", Handle)
            .WithTags("BudgetStructure")
            .WithName("RestoreCategoryGroup")
            .Produces(StatusCodes.Status204NoContent)
            .RequireAuthorization("budget:admin");

        return app;
    }

    private static async Task<IResult> Handle(
        Guid id,
        Guid groupId,
        bool includeExecutionRecords,
        IMediator mediator,
        CancellationToken ct)
    {
        var cmd    = new RestoreCategoryGroupCommand(id, groupId, includeExecutionRecords);
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
