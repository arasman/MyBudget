using Mediator;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;
using MyBudget.Features.SharedKernel.Auth;

namespace MyBudget.Features.Features.Auth.RegisterUser;

public static class RegisterUserEndpoint
{
    public static IEndpointRouteBuilder Map(IEndpointRouteBuilder app)
    {
        app.MapPost("/api/auth/register", Handle)
            .WithTags("Auth")
            .WithName("RegisterUser")
            .Produces<LoginResponse>(StatusCodes.Status201Created)
            .ProducesValidationProblem()
            .AllowAnonymous();

        return app;
    }

    private static async Task<IResult> Handle(
        RegisterUserCommand command,
        IMediator mediator,
        CancellationToken ct)
    {
        var result = await mediator.Send(command, ct);

        if (!result.IsSuccess)
        {
            return result.Error == "AUTH_EMAIL_TAKEN"
                ? Results.Conflict(new { error = "AUTH_EMAIL_TAKEN" })
                : Results.Problem(result.Error, statusCode: StatusCodes.Status422UnprocessableEntity);
        }

        return Results.Created("/api/auth/me", result.Value);
    }
}
