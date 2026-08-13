using Mediator;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;

namespace MyBudget.Features.Features.Budgets.ListBudgetMembers;

public static class ListBudgetMembersEndpoint
{
    public static IEndpointRouteBuilder Map(IEndpointRouteBuilder app)
    {
        app.MapGet("/api/budgets/{id}/members", Handle)
            .WithTags("Budgets")
            .WithName("ListBudgetMembers")
            .Produces<ListBudgetMembersResponse>(StatusCodes.Status200OK)
            .RequireAuthorization("budget:admin");

        return app;
    }

    private static async Task<IResult> Handle(
        Guid id,
        IMediator mediator,
        CancellationToken ct)
    {
        var result = await mediator.Send(new ListBudgetMembersQuery(id), ct);
        return result.IsSuccess
            ? Results.Ok(result.Value)
            : Results.Problem(result.Error, statusCode: StatusCodes.Status500InternalServerError);
    }
}
