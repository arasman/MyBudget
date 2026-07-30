using Mediator;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;

namespace MyBudget.Features.Features.BankAccounts.CreateBankAccount;

public static class CreateBankAccountEndpoint
{
    public static IEndpointRouteBuilder Map(IEndpointRouteBuilder app)
    {
        app.MapPost("/api/budgets/{id}/bank-accounts", Handle)
            .WithTags("BankAccounts")
            .WithName("CreateBankAccount")
            .Produces<Guid>(StatusCodes.Status201Created)
            .ProducesValidationProblem()
            .RequireAuthorization("budget:admin");

        return app;
    }

    private static async Task<IResult> Handle(
        Guid id,
        CreateBankAccountRequest body,
        IMediator mediator,
        CancellationToken ct)
    {
        var cmd = new CreateBankAccountCommand(
            id,
            body.CurrencyId,
            body.Alias,
            body.IsPositive,
            body.DisplayOrder);

        var result = await mediator.Send(cmd, ct);

        if (!result.IsSuccess)
        {
            return result.Error switch
            {
                "BUDGET_NOT_FOUND"   => Results.NotFound(new { error = result.Error }),
                "CURRENCY_NOT_FOUND" => Results.UnprocessableEntity(new { error = result.Error }),
                _                    => Results.Problem(result.Error, statusCode: StatusCodes.Status422UnprocessableEntity),
            };
        }

        return Results.Created(
            $"/api/budgets/{id}/bank-accounts/{result.Value}",
            new { id = result.Value });
    }
}

public sealed record CreateBankAccountRequest(
    Guid   CurrencyId,
    string Alias,
    bool   IsPositive,
    int    DisplayOrder);
