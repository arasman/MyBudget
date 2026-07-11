using Mediator;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;

namespace MyBudget.Features.Features.BudgetStructure.CreateCycle;

public static class CreateCycleEndpoint
{
    public static IEndpointRouteBuilder Map(IEndpointRouteBuilder app)
    {
        app.MapPost("/api/budgets/{id}/cycles", Handle)
            .WithTags("BudgetStructure")
            .WithName("CreateCycle")
            .Produces<Guid>(StatusCodes.Status201Created)
            .ProducesValidationProblem()
            .RequireAuthorization("budget:admin");

        return app;
    }

    private static async Task<IResult> Handle(
        Guid id,
        CreateCycleRequest body,
        IMediator mediator,
        CancellationToken ct)
    {
        var cmd    = new CreateCycleCommand(
            id,
            body.Name,
            body.StartDate,
            body.EndDate,
            body.DefaultCurrencyId,
            body.AlternateCurrencyId,
            body.ExchangeRate);
        var result = await mediator.Send(cmd, ct);

        if (!result.IsSuccess)
        {
            return result.Error == "CYCLE_DATE_OVERLAP"
                ? Results.UnprocessableEntity(new { error = result.Error })
                : Results.Problem(result.Error, statusCode: StatusCodes.Status422UnprocessableEntity);
        }

        return Results.Created($"/api/budgets/{id}/cycles/{result.Value}", new { id = result.Value });
    }
}

public sealed record CreateCycleRequest(
    string   Name,
    DateOnly StartDate,
    DateOnly EndDate,
    Guid     DefaultCurrencyId,
    Guid?    AlternateCurrencyId,
    decimal? ExchangeRate);
