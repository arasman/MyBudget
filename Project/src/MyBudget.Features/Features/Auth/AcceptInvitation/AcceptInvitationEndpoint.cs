using System.Security.Claims;
using Mediator;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;

namespace MyBudget.Features.Features.Auth.AcceptInvitation;

public static class AcceptInvitationEndpoint
{
    public static IEndpointRouteBuilder Map(IEndpointRouteBuilder app)
    {
        app.MapPost("/api/auth/invitations/accept", Handle)
            .WithTags("Auth")
            .WithName("AcceptInvitation")
            .Produces<AcceptInvitationResponse>(StatusCodes.Status200OK)
            .ProducesValidationProblem()
            .RequireAuthorization();

        return app;
    }

    private static async Task<IResult> Handle(
        AcceptInvitationRequest request,
        ClaimsPrincipal principal,
        IMediator mediator,
        CancellationToken ct)
    {
        var userIdRaw = principal.FindFirstValue(ClaimTypes.NameIdentifier);
        if (!Guid.TryParse(userIdRaw, out var userId))
            return Results.Unauthorized();

        var command = new AcceptInvitationCommand(request.Token, userId);
        var result  = await mediator.Send(command, ct);

        if (!result.IsSuccess)
        {
            return result.Error switch
            {
                "AUTH_INVITATION_NOT_FOUND"   => Results.NotFound(new { error = "AUTH_INVITATION_NOT_FOUND" }),
                "AUTH_INVITATION_EXPIRED"     => Results.Problem(
                    detail: "AUTH_INVITATION_EXPIRED", statusCode: StatusCodes.Status410Gone),
                "AUTH_INVITATION_ALREADY_USED" => Results.Problem(
                    detail: "AUTH_INVITATION_ALREADY_USED", statusCode: StatusCodes.Status410Gone),
                "AUTH_INVITATION_EMAIL_MISMATCH" => Results.Problem(
                    detail: "AUTH_INVITATION_EMAIL_MISMATCH", statusCode: StatusCodes.Status403Forbidden),
                _ => Results.Problem(result.Error, statusCode: StatusCodes.Status500InternalServerError),
            };
        }

        return Results.Ok(result.Value);
    }

    private sealed record AcceptInvitationRequest(string Token);
}
