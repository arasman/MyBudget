using Mediator;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;

namespace MyBudget.Features.Features.BudgetStructure.CreateBudgetLineRevision;

public static class CreateBudgetLineRevisionEndpoint
{
    public static IEndpointRouteBuilder Map(IEndpointRouteBuilder app)
    {
        app.MapPost("/api/budgets/{id}/lines/{lineId}/revisions", Handle)
            .WithTags("BudgetStructure")
            .WithName("CreateBudgetLineRevision")
            .Produces<Guid>(StatusCodes.Status201Created)
            .ProducesValidationProblem()
            .RequireAuthorization("budget:admin");

        return app;
    }

    private static async Task<IResult> Handle(
        Guid id,
        Guid lineId,
        CreateBudgetLineRevisionRequest body,
        IMediator mediator,
        CancellationToken ct)
    {
        var cmd = new CreateBudgetLineRevisionCommand(
            id, lineId, body.ValidFrom, body.ValidTo, body.Amount, body.CurrencyId, body.Note);

        var result = await mediator.Send(cmd, ct);

        if (!result.IsSuccess)
        {
            return result.Error switch
            {
                "BUDGET_LINE_NOT_FOUND"              => Results.NotFound(new { error = result.Error }),
                "REVISION_CONCURRENCY_CONFLICT"      => Results.Conflict(new { error = result.Error }),
                "REVISION_OUTSIDE_LINE_DATE_RANGE"   => Results.UnprocessableEntity(new { error = result.Error }),
                _                                    => Results.Problem(result.Error, statusCode: StatusCodes.Status422UnprocessableEntity),
            };
        }

        return Results.Created(
            $"/api/budgets/{id}/lines/{lineId}/revisions/{result.Value}",
            new { id = result.Value });
    }
}

public sealed record CreateBudgetLineRevisionRequest(
    DateOnly  ValidFrom,
    DateOnly? ValidTo,
    decimal   Amount,
    Guid?     CurrencyId,
    string?   Note = null);
