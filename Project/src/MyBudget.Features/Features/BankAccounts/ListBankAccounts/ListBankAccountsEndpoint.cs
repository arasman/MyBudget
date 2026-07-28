using Mediator;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;

namespace MyBudget.Features.Features.BankAccounts.ListBankAccounts;

public static class ListBankAccountsEndpoint
{
    public static IEndpointRouteBuilder Map(IEndpointRouteBuilder app)
    {
        app.MapGet("/api/budgets/{id}/bank-accounts", Handle)
            .WithTags("BankAccounts")
            .WithName("ListBankAccounts")
            .Produces<IReadOnlyList<BankAccountDto>>(StatusCodes.Status200OK)
            .RequireAuthorization("budget:read");

        return app;
    }

    private static async Task<IResult> Handle(
        Guid id,
        IMediator mediator,
        CancellationToken ct)
    {
        var query  = new ListBankAccountsQuery(id);
        var result = await mediator.Send(query, ct);

        return result.IsSuccess
            ? Results.Ok(result.Value)
            : Results.Problem(result.Error, statusCode: StatusCodes.Status500InternalServerError);
    }
}
