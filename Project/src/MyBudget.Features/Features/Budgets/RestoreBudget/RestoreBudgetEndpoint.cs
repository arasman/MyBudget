using System.Security.Claims;
using Mediator;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;

namespace MyBudget.Features.Features.Budgets.RestoreBudget;

public static class RestoreBudgetEndpoint
{
    public static IEndpointRouteBuilder Map(IEndpointRouteBuilder app)
    {
        app.MapPost("/api/budgets/{id}/restore", Handle)
            .WithTags("Budgets")
            .WithName("RestoreBudget")
            .Produces<RestoreBudgetResponse>(StatusCodes.Status200OK)
            .RequireAuthorization();

        return app;
    }

    private static async Task<IResult> Handle(
        Guid id,
        ClaimsPrincipal principal,
        IMediator mediator,
        CancellationToken ct)
    {
        var userIdRaw = principal.FindFirstValue(ClaimTypes.NameIdentifier);
        if (!Guid.TryParse(userIdRaw, out var userId))
            return Results.Unauthorized();

        var command = new RestoreBudgetCommand(id, userId);
        var result  = await mediator.Send(command, ct);

        if (!result.IsSuccess)
        {
            return result.Error switch
            {
                "AUTH_INSUFFICIENT_ROLE" => Results.Problem(
                    "Insufficient role to restore budget.",
                    statusCode: StatusCodes.Status403Forbidden,
                    extensions: new Dictionary<string, object?> { ["error"] = "AUTH_INSUFFICIENT_ROLE" }),
                "BUDGET_NOT_FOUND"    => Results.NotFound(new { error = "BUDGET_NOT_FOUND" }),
                "BUDGET_NOT_DELETED"  => Results.NotFound(new { error = "BUDGET_NOT_DELETED" }),
                _ => Results.Problem(result.Error, statusCode: StatusCodes.Status500InternalServerError),
            };
        }

        return Results.Ok(result.Value);
    }
}
