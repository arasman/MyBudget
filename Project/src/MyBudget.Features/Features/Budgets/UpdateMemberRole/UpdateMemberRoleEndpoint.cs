using System.Security.Claims;
using Mediator;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;
using MyBudget.Features.SharedKernel.Entities;

namespace MyBudget.Features.Features.Budgets.UpdateMemberRole;

public static class UpdateMemberRoleEndpoint
{
    public static IEndpointRouteBuilder Map(IEndpointRouteBuilder app)
    {
        app.MapPatch("/api/budgets/{id}/members/{userId}/role", Handle)
            .WithTags("Budgets")
            .WithName("UpdateMemberRole")
            .Produces<UpdateMemberRoleResponse>(StatusCodes.Status200OK)
            .ProducesValidationProblem()
            .RequireAuthorization("budget:admin");

        return app;
    }

    private static async Task<IResult> Handle(
        Guid id,
        Guid userId,
        UpdateMemberRoleRequest request,
        ClaimsPrincipal principal,
        IMediator mediator,
        CancellationToken ct)
    {
        var actorIdRaw = principal.FindFirstValue(ClaimTypes.NameIdentifier);
        if (!Guid.TryParse(actorIdRaw, out var actorId))
            return Results.Unauthorized();

        // "owner" is a valid parse here — MemberActionPolicy rejects it semantically (rule 4)
        // with the exact MEMBERS_CANNOT_PROMOTE_TO_OWNER contract, not a generic shape error.
        if (!BudgetRoleStrings.TryParse(request.Role, out var newRole))
            return Results.Problem("Invalid role value.", statusCode: StatusCodes.Status422UnprocessableEntity);

        var command = new UpdateMemberRoleCommand(id, userId, newRole, actorId);
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
                "MEMBERS_CANNOT_PROMOTE_TO_OWNER" => Results.Problem(
                    detail: "MEMBERS_CANNOT_PROMOTE_TO_OWNER", statusCode: StatusCodes.Status422UnprocessableEntity),
                _ => Results.Problem(result.Error, statusCode: StatusCodes.Status422UnprocessableEntity),
            };
        }

        return Results.Ok(result.Value);
    }

    private sealed record UpdateMemberRoleRequest(string Role);
}
