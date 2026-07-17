using Mediator;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;

namespace MyBudget.Features.Features.BudgetStructure.ListPeriods;

public static class ListPeriodsEndpoint
{
    public static IEndpointRouteBuilder Map(IEndpointRouteBuilder app)
    {
        app.MapGet("/api/budgets/{id}/cycles/{cycleId}/periods", Handle)
            .WithTags("BudgetStructure")
            .WithName("ListPeriods")
            .Produces<IReadOnlyList<PeriodListItem>>(StatusCodes.Status200OK)
            .Produces(StatusCodes.Status404NotFound)
            .RequireAuthorization("budget:read");

        return app;
    }

    private static async Task<IResult> Handle(
        Guid id,
        Guid cycleId,
        bool? includeDeleted,
        IMediator mediator,
        CancellationToken ct)
    {
        var result = await mediator.Send(new ListPeriodsQuery(id, cycleId, includeDeleted ?? false), ct);
        return result.IsSuccess
            ? Results.Ok(result.Value)
            : Results.NotFound(new { error = result.Error });
    }
}
