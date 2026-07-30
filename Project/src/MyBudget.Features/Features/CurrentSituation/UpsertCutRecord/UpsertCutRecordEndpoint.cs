using Mediator;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;

namespace MyBudget.Features.Features.CurrentSituation.UpsertCutRecord;

public static class UpsertCutRecordEndpoint
{
    public static IEndpointRouteBuilder Map(IEndpointRouteBuilder app)
    {
        app.MapPut("/api/budgets/{id}/cut-records/{date}", Handle)
            .WithTags("CurrentSituation")
            .WithName("UpsertCutRecord")
            .Produces(StatusCodes.Status200OK)
            .ProducesValidationProblem()
            .RequireAuthorization("budget:operator");

        return app;
    }

    private static async Task<IResult> Handle(
        Guid id,
        string date,
        UpsertCutRecordRequest body,
        IMediator mediator,
        CancellationToken ct)
    {
        if (!DateOnly.TryParseExact(date, "yyyy-MM-dd", out var cutDate))
            return Results.BadRequest(new { error = "INVALID_DATE_FORMAT" });

        var cmd = new UpsertCutRecordCommand(
            id,
            cutDate,
            body.ExchangeRate,
            body.ProjectionsJson,
            body.Accounts.Select(a => new UpsertCutBankAccountItem(a.BankAccountId, a.Balance)).ToList());

        var result = await mediator.Send(cmd, ct);

        if (!result.IsSuccess)
        {
            return result.Error switch
            {
                "NO_ACTIVE_PERIOD_FOR_CUT_DATE" => Results.UnprocessableEntity(new { error = result.Error }),
                "ACCOUNT_NOT_FOUND"             => Results.UnprocessableEntity(new { error = result.Error }),
                _                               => Results.Problem(result.Error, statusCode: StatusCodes.Status422UnprocessableEntity),
            };
        }

        return Results.Ok();
    }
}

public sealed record UpsertCutRecordRequest(
    decimal                            ExchangeRate,
    string?                            ProjectionsJson,
    IReadOnlyList<AccountBalanceItem>  Accounts);

public sealed record AccountBalanceItem(
    Guid    BankAccountId,
    decimal Balance);
