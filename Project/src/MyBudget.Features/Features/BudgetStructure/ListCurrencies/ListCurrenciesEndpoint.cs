using Mediator;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;

namespace MyBudget.Features.Features.BudgetStructure.ListCurrencies;

public static class ListCurrenciesEndpoint
{
    public static IEndpointRouteBuilder Map(IEndpointRouteBuilder app)
    {
        app.MapGet("/api/budgets/{id}/currencies", Handle)
            .WithTags("BudgetStructure")
            .WithName("ListCurrencies")
            .Produces<IReadOnlyList<CurrencyResponse>>(StatusCodes.Status200OK)
            .RequireAuthorization("budget:read");

        return app;
    }

    private static async Task<IResult> Handle(
        Guid id,
        IMediator mediator,
        CancellationToken ct)
    {
        var result = await mediator.Send(new ListCurrenciesQuery(id), ct);
        return result.IsSuccess
            ? Results.Ok(result.Value)
            : Results.Problem(result.Error, statusCode: StatusCodes.Status500InternalServerError);
    }
}
