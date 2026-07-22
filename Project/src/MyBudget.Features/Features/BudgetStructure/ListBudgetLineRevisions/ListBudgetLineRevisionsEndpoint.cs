using Mediator;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;

namespace MyBudget.Features.Features.BudgetStructure.ListBudgetLineRevisions;

public static class ListBudgetLineRevisionsEndpoint
{
    public static IEndpointRouteBuilder Map(IEndpointRouteBuilder app)
    {
        app.MapGet("/api/budgets/{id}/lines/{lineId}/revisions", Handle)
            .WithTags("BudgetStructure")
            .WithName("ListBudgetLineRevisions")
            .Produces<IReadOnlyList<RevisionDto>>(StatusCodes.Status200OK)
            .RequireAuthorization("budget:admin");

        return app;
    }

    private static async Task<IResult> Handle(
        Guid id,
        Guid lineId,
        IMediator mediator,
        CancellationToken ct)
    {
        var query  = new ListBudgetLineRevisionsQuery(id, lineId);
        var result = await mediator.Send(query, ct);

        if (!result.IsSuccess)
        {
            if (result.Error == "BUDGET_LINE_NOT_FOUND")
                return Results.NotFound(new { error = result.Error });

            return Results.Problem(result.Error, statusCode: StatusCodes.Status422UnprocessableEntity);
        }

        return Results.Ok(result.Value);
    }
}
