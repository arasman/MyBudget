using Mediator;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;
using MyBudget.Features.SharedKernel.Auth;

namespace MyBudget.Features.Features.Auth.RefreshToken;

public static class RefreshTokenEndpoint
{
    public static IEndpointRouteBuilder Map(IEndpointRouteBuilder app)
    {
        app.MapPost("/api/auth/refresh", Handle)
            .WithTags("Auth")
            .WithName("RefreshToken")
            .Produces<LoginResponse>(StatusCodes.Status200OK)
            .ProducesValidationProblem()
            .AllowAnonymous();

        return app;
    }

    private static async Task<IResult> Handle(
        RefreshTokenCommand command,
        IMediator mediator,
        CancellationToken ct)
    {
        var result = await mediator.Send(command, ct);

        if (!result.IsSuccess)
            return Results.Unauthorized();

        return Results.Ok(result.Value);
    }
}
