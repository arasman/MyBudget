using System.Security.Claims;
using Mediator;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;

namespace MyBudget.Features.Features.Auth.LogoutUser;

public static class LogoutUserEndpoint
{
    public static IEndpointRouteBuilder Map(IEndpointRouteBuilder app)
    {
        app.MapPost("/api/auth/logout", Handle)
            .WithTags("Auth")
            .WithName("LogoutUser")
            .Produces(StatusCodes.Status200OK)
            .RequireAuthorization();

        return app;
    }

    private static async Task<IResult> Handle(
        LogoutUserRequest request,
        ClaimsPrincipal principal,
        IMediator mediator,
        CancellationToken ct)
    {
        var userIdRaw = principal.FindFirstValue(ClaimTypes.NameIdentifier);
        if (!Guid.TryParse(userIdRaw, out var userId))
            return Results.Unauthorized();

        var command = new LogoutUserCommand(request.RefreshToken, userId);
        await mediator.Send(command, ct);
        return Results.Ok(new { message = "Logged out" });
    }

    private sealed record LogoutUserRequest(string RefreshToken);
}
