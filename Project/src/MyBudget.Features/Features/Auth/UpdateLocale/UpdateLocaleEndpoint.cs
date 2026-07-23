using Mediator;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;

namespace MyBudget.Features.Features.Auth.UpdateLocale;

public static class UpdateLocaleEndpoint
{
    public static IEndpointRouteBuilder Map(IEndpointRouteBuilder app)
    {
        app.MapPatch("/api/auth/me/locale", Handle)
            .WithTags("Auth")
            .WithName("UpdateLocale")
            .Produces(StatusCodes.Status204NoContent)
            .ProducesValidationProblem()
            .RequireAuthorization();

        return app;
    }

    private static async Task<IResult> Handle(
        UpdateLocaleRequest request,
        IMediator mediator,
        CancellationToken ct)
    {
        var command = new UpdateLocaleCommand(request.Locale);
        var result  = await mediator.Send(command, ct);

        if (!result.IsSuccess)
        {
            var error = result.Error ?? string.Empty;

            if (error == "AUTH_LOCALE_UNSUPPORTED")
                return Results.Problem(
                    detail: "AUTH_LOCALE_UNSUPPORTED",
                    statusCode: StatusCodes.Status422UnprocessableEntity);

            return Results.Problem(error, statusCode: StatusCodes.Status500InternalServerError);
        }

        return Results.NoContent();
    }

    private sealed record UpdateLocaleRequest(string Locale);
}
