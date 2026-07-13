using Mediator;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;
using MyBudget.Features.SharedKernel.Auth;

namespace MyBudget.Features.Features.Auth.LoginUser;

public static class LoginUserEndpoint
{
    public static IEndpointRouteBuilder Map(IEndpointRouteBuilder app)
    {
        app.MapPost("/api/auth/login", Handle)
            .WithTags("Auth")
            .WithName("LoginUser")
            .Produces<LoginResponse>(StatusCodes.Status200OK)
            .ProducesValidationProblem()
            .AllowAnonymous();

        return app;
    }

    private static async Task<IResult> Handle(
        LoginUserCommand command,
        IMediator mediator,
        CancellationToken ct)
    {
        var result = await mediator.Send(command, ct);

        if (!result.IsSuccess)
        {
            return result.Error switch
            {
                "AUTH_ACCOUNT_LOCKED" => Results.Problem(
                    detail: "AUTH_ACCOUNT_LOCKED",
                    statusCode: StatusCodes.Status423Locked),
                "AUTH_FORCE_PASSWORD_CHANGE" => Results.Problem(
                    detail: "AUTH_FORCE_PASSWORD_CHANGE",
                    statusCode: StatusCodes.Status403Forbidden),
                _ => Results.Unauthorized(),
            };
        }

        return Results.Ok(result.Value);
    }
}
