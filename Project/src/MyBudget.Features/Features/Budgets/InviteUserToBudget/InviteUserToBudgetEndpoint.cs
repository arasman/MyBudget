using System.Security.Claims;
using Mediator;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;
using MyBudget.Features.SharedKernel.Entities;

namespace MyBudget.Features.Features.Budgets.InviteUserToBudget;

public static class InviteUserToBudgetEndpoint
{
    public static IEndpointRouteBuilder Map(IEndpointRouteBuilder app)
    {
        app.MapPost("/api/budgets/{id}/invitations", Handle)
            .WithTags("Budgets")
            .WithName("InviteUserToBudget")
            .Produces<InviteUserToBudgetResponse>(StatusCodes.Status201Created)
            .ProducesValidationProblem()
            .RequireAuthorization("budget:admin");

        return app;
    }

    private static async Task<IResult> Handle(
        Guid id,
        InviteRequest request,
        ClaimsPrincipal principal,
        IMediator mediator,
        CancellationToken ct)
    {
        var userIdRaw = principal.FindFirstValue(ClaimTypes.NameIdentifier);
        if (!Guid.TryParse(userIdRaw, out var userId))
            return Results.Unauthorized();

        // Map string role to enum
        if (!TryParseRole(request.Role, out var role))
            return Results.Problem("Invalid role value.", statusCode: StatusCodes.Status422UnprocessableEntity);

        var command = new InviteUserToBudgetCommand(
            BudgetId:       id,
            InviteeEmail:   request.Email,
            Role:           role,
            InvitedByUserId: userId);

        var result = await mediator.Send(command, ct);

        if (!result.IsSuccess)
        {
            return result.Error switch
            {
                "BUDGET_NOT_FOUND"          => Results.NotFound(new { error = "BUDGET_NOT_FOUND" }),
                "AUTH_ALREADY_MEMBER"       => Results.Conflict(new { error = "AUTH_ALREADY_MEMBER" }),
                "AUTH_CANNOT_INVITE_AS_OWNER" => Results.Problem(
                    "Cannot invite as Owner.", statusCode: StatusCodes.Status422UnprocessableEntity),
                _ => Results.Problem(result.Error, statusCode: StatusCodes.Status500InternalServerError),
            };
        }

        return Results.Created($"/api/budgets/{id}/invitations", result.Value);
    }

    private static bool TryParseRole(string role, out BudgetRole parsed)
    {
        (parsed, var ok) = role?.ToLowerInvariant() switch
        {
            "admin"     => (BudgetRole.Admin,    true),
            "operator"  => (BudgetRole.Operator, true),
            "read-only" => (BudgetRole.ReadOnly, true),
            "owner"     => (BudgetRole.Owner,    true),   // validator will reject this
            _           => (default,             false),
        };
        return ok;
    }

    private sealed record InviteRequest(string Email, string Role);
}
