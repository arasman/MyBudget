using Mediator;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;

namespace MyBudget.Features.Features.Auth.RequestPasswordReset;

public static class RequestPasswordResetEndpoint
{
    public static IEndpointRouteBuilder Map(IEndpointRouteBuilder app)
    {
        app.MapPost("/api/auth/forgot-password", Handle)
            .WithTags("Auth")
            .WithName("RequestPasswordReset")
            .Produces(StatusCodes.Status200OK)
            .ProducesValidationProblem()
            .AllowAnonymous();

        return app;
    }

    private static async Task<IResult> Handle(
        RequestPasswordResetRequest request,
        IMediator mediator,
        CancellationToken ct)
    {
        var command = new RequestPasswordResetCommand(request.Email);
        var result  = await mediator.Send(command, ct);

        // Validation failures (FIELD_REQUIRED, FIELD_INVALID) return 422
        if (!result.IsSuccess)
        {
            return Results.Problem(
                detail: result.Error,
                statusCode: StatusCodes.Status422UnprocessableEntity);
        }

        // Always return 200 for non-validation outcomes — anti-enumeration
        // (registered and unregistered emails look identical to the caller)
        return Results.Ok(new { message = "If the email exists, a reset link has been sent." });
    }

    private sealed record RequestPasswordResetRequest(string Email);
}
