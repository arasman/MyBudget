using Mediator;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;

namespace MyBudget.Features.Features.BudgetStructure.ListBudgetLines;

public static class ListBudgetLinesEndpoint
{
    public static IEndpointRouteBuilder Map(IEndpointRouteBuilder app)
    {
        app.MapGet("/api/budgets/{id}/lines", Handle)
            .WithTags("BudgetStructure")
            .WithName("ListBudgetLines")
            .Produces<IReadOnlyList<BudgetLineResponse>>(StatusCodes.Status200OK)
            .RequireAuthorization("budget:read");

        return app;
    }

    private static async Task<IResult> Handle(
        Guid id,
        bool? includeDeleted,
        IMediator mediator,
        CancellationToken ct)
    {
        var result = await mediator.Send(new ListBudgetLinesQuery(id, includeDeleted ?? false), ct);
        return result.IsSuccess
            ? Results.Ok(result.Value)
            : Results.Problem(result.Error, statusCode: StatusCodes.Status500InternalServerError);
    }
}
