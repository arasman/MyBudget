using System.Security.Claims;
using Mediator;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;
using MyBudget.Features.SharedKernel.Auth;

namespace MyBudget.Features.Features.Auth.GetCurrentUser;

public static class GetCurrentUserEndpoint
{
    public static IEndpointRouteBuilder Map(IEndpointRouteBuilder app)
    {
        app.MapGet("/api/auth/me", Handle)
            .WithTags("Auth")
            .WithName("GetCurrentUser")
            .Produces<CurrentUserResponse>(StatusCodes.Status200OK)
            .RequireAuthorization();

        return app;
    }

    private static async Task<IResult> Handle(
        ClaimsPrincipal principal,
        IMediator mediator,
        CancellationToken ct)
    {
        var userIdRaw = principal.FindFirstValue(ClaimTypes.NameIdentifier);
        if (!Guid.TryParse(userIdRaw, out var userId))
            return Results.Unauthorized();

        var result = await mediator.Send(new GetCurrentUserQuery(userId), ct);
        return result.IsSuccess
            ? Results.Ok(result.Value)
            : Results.NotFound();
    }
}
