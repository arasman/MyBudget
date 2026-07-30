using Mediator;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;

namespace MyBudget.Features.Features.BankAccounts.DeleteBankAccount;

public static class DeleteBankAccountEndpoint
{
    public static IEndpointRouteBuilder Map(IEndpointRouteBuilder app)
    {
        app.MapDelete("/api/budgets/{id}/bank-accounts/{accountId}", Handle)
            .WithTags("BankAccounts")
            .WithName("DeleteBankAccount")
            .Produces(StatusCodes.Status204NoContent)
            .RequireAuthorization("budget:admin");

        return app;
    }

    private static async Task<IResult> Handle(
        Guid id,
        Guid accountId,
        IMediator mediator,
        CancellationToken ct)
    {
        var cmd    = new DeleteBankAccountCommand(id, accountId);
        var result = await mediator.Send(cmd, ct);

        if (!result.IsSuccess)
        {
            return result.Error switch
            {
                "ACCOUNT_NOT_FOUND" => Results.NotFound(new { error = result.Error }),
                _                   => Results.Problem(result.Error, statusCode: StatusCodes.Status422UnprocessableEntity),
            };
        }

        return Results.NoContent();
    }
}
