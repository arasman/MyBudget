using Mediator;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;

namespace MyBudget.Features.Features.Dashboard.GetBudgetLineSeries;

public static class GetBudgetLineSeriesEndpoint
{
    public static IEndpointRouteBuilder Map(IEndpointRouteBuilder app)
    {
        app.MapGet("/api/budgets/{id}/dashboard/line-series", Handle)
            .WithTags("Dashboard")
            .WithName("GetBudgetLineSeries")
            .Produces<BudgetLineSeriesResponse>(StatusCodes.Status200OK)
            .RequireAuthorization("budget:read");

        return app;
    }

    private static async Task<IResult> Handle(
        Guid            id,
        IMediator       mediator,
        CancellationToken ct,
        Guid[]?         lineIds   = null,
        Guid[]?         periodIds = null)
    {
        var result = await mediator.Send(
            new GetBudgetLineSeriesQuery(id, lineIds ?? [], periodIds ?? []), ct);

        return result.IsSuccess
            ? Results.Ok(result.Value)
            : Results.NotFound(new { error = result.Error });
    }
}
