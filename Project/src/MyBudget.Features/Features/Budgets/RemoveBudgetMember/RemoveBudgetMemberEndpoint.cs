using System.Security.Claims;
using Mediator;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;

namespace MyBudget.Features.Features.Budgets.RemoveBudgetMember;

public static class RemoveBudgetMemberEndpoint
{
    public static IEndpointRouteBuilder Map(IEndpointRouteBuilder app)
    {
        app.MapDelete("/api/budgets/{id}/members/{userId}", Handle)
            .WithTags("Budgets")
            .WithName("RemoveBudgetMember")
            .Produces(StatusCodes.Status204NoContent)
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

        var command = new RemoveBudgetMemberCommand(id, userId, actorId);
        var result  = await mediator.Send(command, ct);

        if (!result.IsSuccess)
        {
            return result.Error switch
            {
                "MEMBERS_NOT_FOUND" => Results.NotFound(new { error = "MEMBERS_NOT_FOUND" }),
                "MEMBERS_CANNOT_ACT_ON_SELF" => Results.Problem(
                    detail: "MEMBERS_CANNOT_ACT_ON_SELF", statusCode: StatusCodes.Status403Forbidden),
                "MEMBERS_CANNOT_ACT_ON_OWNER" => Results.Problem(
                    detail: "MEMBERS_CANNOT_ACT_ON_OWNER", statusCode: StatusCodes.Status403Forbidden),
                "MEMBERS_CANNOT_ACT_ON_ADMIN" => Results.Problem(
                    detail: "MEMBERS_CANNOT_ACT_ON_ADMIN", statusCode: StatusCodes.Status403Forbidden),
                _ => Results.Problem(result.Error, statusCode: StatusCodes.Status500InternalServerError),
            };
        }

        return Results.NoContent();
    }
}
