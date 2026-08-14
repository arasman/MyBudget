using System.Security.Claims;
using Mediator;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;

namespace MyBudget.Features.Features.Budgets.RestoreBudgetMember;

public static class RestoreBudgetMemberEndpoint
{
    public static IEndpointRouteBuilder Map(IEndpointRouteBuilder app)
    {
        app.MapPost("/api/budgets/{id}/members/{userId}/restore", Handle)
            .WithTags("Budgets")
            .WithName("RestoreBudgetMember")
            .Produces<RestoreBudgetMemberResponse>(StatusCodes.Status200OK)
            .RequireAuthorization("budget:admin");

        return app;
    }

    private static async Task<IResult> Handle(
        Guid id,
        Guid userId,
        ClaimsPrincipal principal,
        IMediator mediator,
        CancellationToken ct)
    {
        var actorIdRaw = principal.FindFirstValue(ClaimTypes.NameIdentifier);
        if (!Guid.TryParse(actorIdRaw, out var actorId))
            return Results.Unauthorized();

        var command = new RestoreBudgetMemberCommand(id, userId, actorId);
        var result  = await mediator.Send(command, ct);

        if (!result.IsSuccess)
        {
            return result.Error switch
            {
                "MEMBERS_NOT_FOUND" => Results.NotFound(new { error = "MEMBERS_NOT_FOUND" }),
                "MEMBERS_NOT_DELETED" => Results.Problem(
                    detail: "MEMBERS_NOT_DELETED", statusCode: StatusCodes.Status409Conflict),
                "MEMBERS_CANNOT_ACT_ON_SELF" => Results.Problem(
                    detail: "MEMBERS_CANNOT_ACT_ON_SELF", statusCode: StatusCodes.Status403Forbidden),
                "MEMBERS_CANNOT_ACT_ON_OWNER" => Results.Problem(
                    detail: "MEMBERS_CANNOT_ACT_ON_OWNER", statusCode: StatusCodes.Status403Forbidden),
                "MEMBERS_CANNOT_ACT_ON_ADMIN" => Results.Problem(
                    detail: "MEMBERS_CANNOT_ACT_ON_ADMIN", statusCode: StatusCodes.Status403Forbidden),
                _ => Results.Problem(result.Error, statusCode: StatusCodes.Status500InternalServerError),
            };
        }

        return Results.Ok(result.Value);
    }
}
