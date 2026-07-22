using Mediator;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;

namespace MyBudget.Features.Features.BudgetStructure.DeleteBudgetLineRevision;

public static class DeleteBudgetLineRevisionEndpoint
{
    public static IEndpointRouteBuilder Map(IEndpointRouteBuilder app)
    {
        app.MapDelete("/api/budgets/{id}/lines/{lineId}/revisions/{revisionId}", Handle)
            .WithTags("BudgetStructure")
            .WithName("DeleteBudgetLineRevision")
            .Produces(StatusCodes.Status204NoContent)
            .RequireAuthorization("budget:admin");

        return app;
    }

    private static async Task<IResult> Handle(
        Guid id,
        Guid lineId,
        Guid revisionId,
        IMediator mediator,
        CancellationToken ct)
    {
        var cmd    = new DeleteBudgetLineRevisionCommand(id, lineId, revisionId);
        var result = await mediator.Send(cmd, ct);

        if (!result.IsSuccess)
        {
            return result.Error switch
            {
                "BUDGET_LINE_NOT_FOUND"             => Results.NotFound(new { error = result.Error }),
                "REVISION_NOT_FOUND"                => Results.NotFound(new { error = result.Error }),
                "CANNOT_DELETE_ORIGINAL_REVISION"   => Results.UnprocessableEntity(new { error = result.Error }),
                "REVISION_HAS_ACTIVE_EXECUTIONS"    => Results.Conflict(new { error = result.Error }),
                "REVISION_CONCURRENCY_CONFLICT"     => Results.Conflict(new { error = result.Error }),
                _                                   => Results.Problem(result.Error, statusCode: StatusCodes.Status422UnprocessableEntity),
            };
        }

        return Results.NoContent();
    }
}
