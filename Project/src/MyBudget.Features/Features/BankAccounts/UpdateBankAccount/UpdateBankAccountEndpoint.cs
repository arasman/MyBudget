using Mediator;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;

namespace MyBudget.Features.Features.BankAccounts.UpdateBankAccount;

public static class UpdateBankAccountEndpoint
{
    public static IEndpointRouteBuilder Map(IEndpointRouteBuilder app)
    {
        app.MapPut("/api/budgets/{id}/bank-accounts/{accountId}", Handle)
            .WithTags("BankAccounts")
            .WithName("UpdateBankAccount")
            .Produces(StatusCodes.Status200OK)
            .ProducesValidationProblem()
            .RequireAuthorization("budget:admin");

        return app;
    }

    private static async Task<IResult> Handle(
        Guid id,
        Guid accountId,
        UpdateBankAccountRequest body,
        IMediator mediator,
        CancellationToken ct)
    {
        var cmd = new UpdateBankAccountCommand(
            id,
            accountId,
            body.Alias,
            body.IsPositive,
            body.DisplayOrder);

        var result = await mediator.Send(cmd, ct);

        if (!result.IsSuccess)
        {
            return result.Error switch
            {
                "ACCOUNT_NOT_FOUND" => Results.NotFound(new { error = result.Error }),
                _                   => Results.Problem(result.Error, statusCode: StatusCodes.Status422UnprocessableEntity),
            };
        }

        return Results.Ok();
    }
}

public sealed record UpdateBankAccountRequest(
    string Alias,
    bool   IsPositive,
    int    DisplayOrder);
