using Mediator;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;

namespace MyBudget.Features.Features.BudgetStructure.UpdateBudgetLineRevision;

public static class UpdateBudgetLineRevisionEndpoint
{
    public static IEndpointRouteBuilder Map(IEndpointRouteBuilder app)
    {
        app.MapPatch("/api/budgets/{id}/lines/{lineId}/revisions/{revisionId}", Handle)
            .WithTags("BudgetStructure")
            .WithName("UpdateBudgetLineRevision")
            .Produces(StatusCodes.Status200OK)
            .RequireAuthorization("budget:admin");

        return app;
    }

    private static async Task<IResult> Handle(
        Guid id,
        Guid lineId,
        Guid revisionId,
        UpdateBudgetLineRevisionRequest body,
        IMediator mediator,
        CancellationToken ct)
    {
        var cmd    = new UpdateBudgetLineRevisionCommand(id, lineId, revisionId, body.Amount, body.Note);
        var result = await mediator.Send(cmd, ct);

        if (!result.IsSuccess)
        {
            return result.Error switch
            {
                "REVISION_NOT_FOUND"      => Results.NotFound(new { error = result.Error }),
                "REVISION_AMOUNT_INVALID" => Results.UnprocessableEntity(new { error = result.Error }),
                _                         => Results.Problem(result.Error, statusCode: StatusCodes.Status422UnprocessableEntity),
            };
        }

        return Results.Ok();
    }
}

public sealed record UpdateBudgetLineRevisionRequest(decimal Amount, string? Note);
