using Mediator;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;

namespace MyBudget.Features.Features.BudgetStructure.ListCycles;

public static class ListCyclesEndpoint
{
    public static IEndpointRouteBuilder Map(IEndpointRouteBuilder app)
    {
        app.MapGet("/api/budgets/{id}/cycles", Handle)
            .WithTags("BudgetStructure")
            .WithName("ListCycles")
            .Produces<IReadOnlyList<CycleListItem>>(StatusCodes.Status200OK)
            .RequireAuthorization("budget:read");

        return app;
    }

    private static async Task<IResult> Handle(
        Guid id,
        IMediator mediator,
        IAuthorizationService authz,
        HttpContext httpContext,
        CancellationToken ct,
        bool includeDeleted = false)
    {
        // includeDeleted requires elevated budget:admin policy; pass budget id as resource
        // so BudgetAuthorizationHandler can resolve membership correctly.
        if (includeDeleted)
        {
            var authResult = await authz.AuthorizeAsync(httpContext.User, httpContext, "budget:admin");
            if (!authResult.Succeeded)
                return Results.Forbid();
        }

        var result = await mediator.Send(new ListCyclesQuery(id, includeDeleted), ct);
        return result.IsSuccess
            ? Results.Ok(result.Value)
            : Results.Problem(result.Error, statusCode: StatusCodes.Status500InternalServerError);
    }
}
