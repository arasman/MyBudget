using Mediator;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;

namespace MyBudget.Features.Features.BudgetExecution.ListPeriodExecutionTotals;

public static class ListPeriodExecutionTotalsEndpoint
{
    public static IEndpointRouteBuilder Map(IEndpointRouteBuilder app)
    {
        app.MapGet("/api/budgets/{id}/periods/{periodId}/execution-totals", Handle)
            .WithTags("BudgetExecution")
            .WithName("ListPeriodExecutionTotals")
            .Produces<PeriodExecutionTotalsResponse>(StatusCodes.Status200OK)
            .RequireAuthorization("budget:read");

        return app;
    }

    private static async Task<IResult> Handle(
        Guid id,
        Guid periodId,
        IMediator mediator,
        CancellationToken ct)
    {
        var result = await mediator.Send(
            new ListPeriodExecutionTotalsQuery(id, periodId), ct);

        return result.IsSuccess
            ? Results.Ok(result.Value)
            : Results.NotFound(new { error = result.Error });
    }
}
