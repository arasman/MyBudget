using Mediator;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;

namespace MyBudget.Features.Features.Auth.ResetPassword;

public static class ResetPasswordEndpoint
{
    public static IEndpointRouteBuilder Map(IEndpointRouteBuilder app)
    {
        app.MapPost("/api/auth/reset-password", Handle)
            .WithTags("Auth")
            .WithName("ResetPassword")
            .Produces(StatusCodes.Status200OK)
            .ProducesValidationProblem()
            .AllowAnonymous();

        return app;
    }

    private static async Task<IResult> Handle(
        ResetPasswordRequest request,
        IMediator mediator,
        CancellationToken ct)
    {
        var command = new ResetPasswordCommand(request.Token, request.NewPassword);
        var result  = await mediator.Send(command, ct);

        if (!result.IsSuccess)
        {
            var error = result.Error ?? string.Empty;

            if (error == "PWD_TOKEN_INVALID")
                return Results.Problem(
                    detail: "PWD_TOKEN_INVALID",
                    statusCode: StatusCodes.Status404NotFound);

            if (error == "PWD_TOKEN_EXPIRED")
                return Results.Problem(
                    detail: "PWD_TOKEN_EXPIRED",
                    statusCode: StatusCodes.Status410Gone);

            // Validation errors (FIELD_REQUIRED, PWD_PASSWORD_TOO_WEAK, PWD_SAME_AS_CURRENT, etc.)
            if (error.Contains("FIELD_REQUIRED") || error.Contains("FIELD_INVALID")
                || error.Contains("PWD_PASSWORD_TOO_WEAK") || error.Contains("PWD_SAME_AS_CURRENT")
                || error.Contains("PWD_PREVIOUSLY_USED"))
            {
                return Results.Problem(
                    detail: error,
                    statusCode: StatusCodes.Status422UnprocessableEntity);
            }

            return Results.Problem(error, statusCode: StatusCodes.Status500InternalServerError);
        }

        return Results.Ok(new { message = "Password reset successfully." });
    }

    private sealed record ResetPasswordRequest(string Token, string NewPassword);
}
