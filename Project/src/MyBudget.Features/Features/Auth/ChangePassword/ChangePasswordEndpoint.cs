using Mediator;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;

namespace MyBudget.Features.Features.Auth.ChangePassword;

public static class ChangePasswordEndpoint
{
    public static IEndpointRouteBuilder Map(IEndpointRouteBuilder app)
    {
        app.MapPost("/api/auth/change-password", Handle)
            .WithTags("Auth")
            .WithName("ChangePassword")
            .Produces(StatusCodes.Status200OK)
            .ProducesValidationProblem()
            .RequireAuthorization();

        return app;
    }

    private static async Task<IResult> Handle(
        ChangePasswordRequest request,
        IMediator mediator,
        CancellationToken ct)
    {
        var command = new ChangePasswordCommand(
            request.CurrentPassword,
            request.NewPassword,
            request.CurrentRefreshToken);

        var result = await mediator.Send(command, ct);

        if (!result.IsSuccess)
        {
            var error = result.Error ?? string.Empty;

            if (error == "PWD_CURRENT_INCORRECT")
                return Results.Problem(
                    detail: "PWD_CURRENT_INCORRECT",
                    statusCode: StatusCodes.Status400BadRequest);

            // Validation errors (FIELD_REQUIRED, PWD_PASSWORD_TOO_WEAK, etc.)
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

        return Results.Ok(new { message = "Password changed successfully." });
    }

    private sealed record ChangePasswordRequest(
        string  CurrentPassword,
        string  NewPassword,
        string? CurrentRefreshToken = null);
}
